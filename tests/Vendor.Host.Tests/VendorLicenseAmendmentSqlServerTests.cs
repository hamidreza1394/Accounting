using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Faranou.Licensing.Contracts;
using Faranou.Vendor.Host.Authorization;
using Faranou.Vendor.Host.Signing;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Auditing;
using Faranou.Vendor.Persistence.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

/// <summary>
/// D-108/D-109 SQL Server cycle for explicit license amendment requests: an issued license is
/// amended through the official builder from its latest signature-verified envelope, signed by
/// the existing fail-closed signer as the next sequence of the same LicenseId, with full rollback
/// on audit failure. Payment state is never consulted during issuance.
/// </summary>
public sealed class VendorLicenseAmendmentSqlServerTests
{
    private const string TestKeyId = "test-kid";

    [Fact]
    public async Task ApprovedAmendmentRebuildsCandidateFromVerifiedEnvelopeAndIssuesNextSequence()
    {
        await using var fixture = await AmendmentFixture.OpenAsync();
        if (fixture is null) return;
        await fixture.IssueBaseLicenseAsync();
        await fixture.SeedPurchasedLineAsync(productCode: "addon.company.seat", verifiedPayment: true);
        await fixture.SeedAmendmentRequestAsync(VendorLicenseAmendmentRequest.KindMaxCompanies, requestedMaxCompanies: 8);

        var result = await fixture.IssueAmendmentAsync();

        Assert.True(result.Succeeded, result.ErrorCode);
        fixture.Db.ChangeTracker.Clear();
        var license = await fixture.Db.Licenses.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, license.LastIssuedSequence);
        var envelopes = await fixture.Db.SignedLicenseEnvelopes.AsNoTracking()
            .OrderBy(item => item.Sequence).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, envelopes.Count);
        Assert.Equal(2, envelopes[1].Sequence);
        var envelope = JsonSerializer.Deserialize<LicenseEnvelope>(envelopes[1].EnvelopeJson)!;
        Assert.True(CanonicalJson.TryParsePayload(envelope.PayloadJson, out var payload, out _));
        Assert.Equal(8, payload!.Limits.MaxCompanies);
        Assert.Equal(1, payload.CompanyEntitlements!.Count); // v2 payload keeps every other entitlement untouched
        var request = await fixture.Db.LicenseAmendmentRequests.AsNoTracking()
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(VendorLicenseAmendmentRequest.StatusIssued, request.Status);
        Assert.NotNull(request.ApprovalRequestId);
        var audits = await fixture.Db.AuditEvents.AsNoTracking()
            .Where(item => item.ActionCode == "vendor.amendment.issued").ToListAsync(TestContext.Current.CancellationToken);
        Assert.Single(audits);
    }

    [Fact]
    public async Task AuditFailureRollsBackEnvelopeSequenceAndRequestStatusTogether()
    {
        await using var fixture = await AmendmentFixture.OpenAsync();
        if (fixture is null) return;
        await fixture.IssueBaseLicenseAsync();
        await fixture.SeedPurchasedLineAsync(productCode: "addon.company.seat", verifiedPayment: true);
        await fixture.SeedAmendmentRequestAsync(VendorLicenseAmendmentRequest.KindMaxCompanies, requestedMaxCompanies: 8);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.IssueAmendmentAsync(failAudit: true));

        fixture.Db.ChangeTracker.Clear();
        var license = await fixture.Db.Licenses.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, license.LastIssuedSequence);
        Assert.Equal(1, await fixture.Db.SignedLicenseEnvelopes.CountAsync(TestContext.Current.CancellationToken));
        var request = await fixture.Db.LicenseAmendmentRequests.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(VendorLicenseAmendmentRequest.StatusPendingApproval, request.Status);
    }

    [Fact]
    public async Task StaleApprovalVersionOrUndecidedRequestIsRejectedFailClosed()
    {
        await using var fixture = await AmendmentFixture.OpenAsync();
        if (fixture is null) return;
        await fixture.IssueBaseLicenseAsync();
        await fixture.SeedPurchasedLineAsync(productCode: "addon.company.seat", verifiedPayment: true);
        await fixture.SeedAmendmentRequestAsync(VendorLicenseAmendmentRequest.KindMaxCompanies, requestedMaxCompanies: 8);

        // A decision built on a stale approval version never reaches the signing pipeline.
        var staleResult = await fixture.IssueAmendmentAsync(approvalVersionOverride: new byte[8]);
        Assert.False(staleResult.Succeeded);
        Assert.Equal("stale_approval_version", staleResult.ErrorCode);

        // A request that is not in PendingApproval cannot be decided: seed an independent
        // purchased line (the filtered unique index forbids two active requests on one line).
        await fixture.SeedPurchasedLineAsync(productCode: "addon.company.seat", verifiedPayment: true);
        await fixture.SeedAmendmentRequestAsync(
            VendorLicenseAmendmentRequest.KindMaxCompanies, requestedMaxCompanies: 9,
            status: VendorLicenseAmendmentRequest.StatusRequested);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
        var undecidedRequest = await db.LicenseAmendmentRequests.AsNoTracking()
            .SingleAsync(item => item.OrderLineId == fixture.OrderLineId, TestContext.Current.CancellationToken);
        var undecidedApprovalId = await SeedApprovalAsync(db, undecidedRequest, status: "Pending");
        db.ChangeTracker.Clear();
        var undecidedApproval = await db.ApprovalRequests.AsNoTracking()
            .SingleAsync(item => item.Id == undecidedApprovalId, TestContext.Current.CancellationToken);
        var service = fixture.CreateService(failAudit: false);
        var undecidedResult = await service.IssueApprovedAsync(
            undecidedApproval, Guid.NewGuid(), undecidedApproval.RowVersion.ToArray(), null,
            Guid.NewGuid().ToString("N"), TestContext.Current.CancellationToken);
        // The request row is still Requested, not PendingApproval: fail closed.
        Assert.False(undecidedResult.Succeeded);
        Assert.Equal("approval_resource_stale", undecidedResult.ErrorCode);
    }

    [Fact]
    public async Task TamperedStoredEnvelopeBlocksAmendmentBeforeAnySigning()
    {
        await using var fixture = await AmendmentFixture.OpenAsync();
        if (fixture is null) return;
        await fixture.IssueBaseLicenseAsync();
        await fixture.SeedPurchasedLineAsync(productCode: "addon.company.seat", verifiedPayment: true);
        await fixture.SeedAmendmentRequestAsync(VendorLicenseAmendmentRequest.KindMaxCompanies, requestedMaxCompanies: 8);

        // Corrupt the stored payload hash: the candidate must never be built from an unverifiable envelope.
        await fixture.Db.Database.ExecuteSqlRawAsync(
            "UPDATE VendorSignedLicenseEnvelopes SET PayloadSha256 = '0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef'",
            TestContext.Current.CancellationToken);
        fixture.Db.ChangeTracker.Clear();

        var result = await fixture.IssueAmendmentAsync();
        Assert.False(result.Succeeded);
        Assert.Equal("amendment_envelope_invalid", result.ErrorCode);
        Assert.Equal(1, await fixture.Db.SignedLicenseEnvelopes.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RejectedDecisionMarksTheRequestRejectedAndAllowsACorrectedRetry()
    {
        await using var fixture = await AmendmentFixture.OpenAsync();
        if (fixture is null) return;
        await fixture.IssueBaseLicenseAsync();
        await fixture.SeedPurchasedLineAsync(productCode: "renewal.support-updates", verifiedPayment: true);
        await fixture.SeedAmendmentRequestAsync(VendorLicenseAmendmentRequest.KindSupportUpdates,
            supportUntilUtc: DateTimeOffset.UtcNow.AddYears(1));

        await fixture.RejectAmendmentAsync();
        fixture.Db.ChangeTracker.Clear();
        var request = await fixture.Db.LicenseAmendmentRequests.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(VendorLicenseAmendmentRequest.StatusRejected, request.Status);
        Assert.Equal("amendment_rejected", request.FailureCode);
        Assert.Equal(1, await fixture.Db.SignedLicenseEnvelopes.CountAsync(TestContext.Current.CancellationToken));
    }

    private static async Task<Guid> SeedApprovalAsync(VendorDbContext db, VendorLicenseAmendmentRequest request, string status)
    {
        var approval = new VendorApprovalRequest
        {
            Id = Guid.NewGuid(), ResourceType = "licenseAmendment", ResourceId = request.Id,
            TargetStatus = "Issued", ExpectedResourceRowVersion = request.RowVersion.ToArray(),
            RequestedByUserId = Guid.NewGuid(), RequestedAtUtc = DateTimeOffset.UtcNow, Status = status
        };
        db.ApprovalRequests.Add(approval);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return approval.Id;
    }

    private sealed class AmendmentFixture : IAsyncDisposable
    {
        public required Microsoft.Extensions.DependencyInjection.ServiceProvider Provider { get; init; }
        public required IServiceScope Scope { get; init; }
        public required VendorDbContext Db { get; init; }
        public required VendorLicenseAmendmentIssuanceService Issuance { get; init; }
        public required VendorLicenseIssuanceService LicenseIssuance { get; init; }
        public Guid CustomerId { get; private set; }
        public Guid LicenseId { get; private set; }
        public Guid? OrderId { get; private set; }
        public Guid? OrderLineId { get; private set; }
        // ONE key signs every envelope and is the only trusted key: signing and verification
        // must agree or the signature check would fail closed for the wrong reason.
        public required ECDsa SigningKey { get; init; }
        private Guid _pendingAmendmentRequestId;

        public VendorLicenseAmendmentIssuanceService CreateService(bool failAudit) => new(
            Db, new VendorLicenseEnvelopeFactory(new TestSigner(SigningKey)), BuildTrustStore(),
            failAudit ? new FailingAuditWriter() : new VendorAuditWriter(Db, TimeProvider.System), TimeProvider.System);

        private LicenseSigningTrustStore BuildTrustStore() => new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"Licensing:TrustedSigningKeys:{TestKeyId}:PublicKeySubjectPublicKeyInfoBase64"] =
                    Convert.ToBase64String(SigningKey.ExportSubjectPublicKeyInfo())
            }).Build());

        public async Task IssueBaseLicenseAsync()
        {
            var license = await Db.Licenses.SingleAsync(TestContext.Current.CancellationToken);
            LicenseId = license.Id;
            CustomerId = license.CustomerId;
            var approval = await Db.ApprovalRequests.SingleAsync(TestContext.Current.CancellationToken);
            await using var tx = await Db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var result = await LicenseIssuance.IssueApprovedAsync(
                approval, Guid.NewGuid(), approval.RowVersion.ToArray(), null,
                Guid.NewGuid().ToString("N"), TestContext.Current.CancellationToken);
            if (!result.Succeeded)
            {
                await tx.RollbackAsync(TestContext.Current.CancellationToken);
                Assert.Fail($"Base license issuance failed: {result.ErrorCode}");
            }
            await tx.CommitAsync(TestContext.Current.CancellationToken);
            Db.ChangeTracker.Clear();
        }

        public async Task SeedPurchasedLineAsync(string productCode, bool verifiedPayment)
        {
            var now = DateTimeOffset.UtcNow;
            var order = new VendorSalesOrder
            {
                Id = Guid.NewGuid(), CustomerId = CustomerId,
                OrderNumber = "SO-" + Guid.NewGuid().ToString("N")[..12],
                TotalAmount = 400m, CurrencyCode = "EUR", CreatedAtUtc = now
            };
            var line = new VendorSalesOrderLine
            {
                Id = Guid.NewGuid(), OrderId = order.Id, Order = order,
                ProductCode = productCode, Description = "Add-on purchased line",
                Quantity = 2, UnitAmount = 200m, LineAmount = 400m,
                LicenseId = LicenseId, CreatedAtUtc = now
            };
            var payment = new VendorPaymentRecord
            {
                Id = Guid.NewGuid(), OrderId = order.Id, Order = order,
                Amount = 400m, CurrencyCode = "EUR", Method = "bank_transfer",
                Status = verifiedPayment ? VendorPaymentRecord.StatusVerified : VendorPaymentRecord.StatusRecorded,
                VerifiedByUserId = verifiedPayment ? Guid.NewGuid() : null,
                VerifiedAtUtc = verifiedPayment ? now : null,
                CreatedAtUtc = now
            };
            Db.AddRange(order, line, payment);
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
            OrderId = order.Id;
            OrderLineId = line.Id;
            Db.ChangeTracker.Clear();
        }

        public async Task SeedAmendmentRequestAsync(
            string kind, int? requestedMaxCompanies = null, DateTimeOffset? supportUntilUtc = null,
            string? status = null)
        {
            var request = new VendorLicenseAmendmentRequest
            {
                Id = Guid.NewGuid(), OrderId = OrderId!.Value, OrderLineId = OrderLineId!.Value,
                CustomerId = CustomerId, LicenseId = LicenseId, Kind = kind,
                RequestedMaxCompanies = requestedMaxCompanies, RequestedSupportUntilUtc = supportUntilUtc,
                Status = status ?? VendorLicenseAmendmentRequest.StatusPendingApproval,
                RequestedByUserId = Guid.NewGuid(),
                CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
            };
            Db.LicenseAmendmentRequests.Add(request);
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
            if (request.Status == VendorLicenseAmendmentRequest.StatusPendingApproval)
            {
                Db.ApprovalRequests.Add(new VendorApprovalRequest
                {
                    Id = Guid.NewGuid(), ResourceType = "licenseAmendment", ResourceId = request.Id,
                    TargetStatus = "Issued", ExpectedResourceRowVersion = request.RowVersion.ToArray(),
                    RequestedByUserId = Guid.NewGuid(), RequestedAtUtc = DateTimeOffset.UtcNow, Status = "Pending"
                });
                await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
                _pendingAmendmentRequestId = request.Id;
            }
            Db.ChangeTracker.Clear();
        }

        public async Task<VendorLicenseAmendmentIssuanceResult> IssueAmendmentAsync(
            byte[]? approvalVersionOverride = null, bool failAudit = false)
        {
            var approval = await Db.ApprovalRequests.AsNoTracking()
                .SingleAsync(item => item.ResourceId == _pendingAmendmentRequestId
                    && item.ResourceType == "licenseAmendment", TestContext.Current.CancellationToken);
            await using var tx = await Db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
            try
            {
                var result = await CreateService(failAudit).IssueApprovedAsync(
                    approval, Guid.NewGuid(), approvalVersionOverride ?? approval.RowVersion.ToArray(), null,
                    Guid.NewGuid().ToString("N"), TestContext.Current.CancellationToken);
                if (result.Succeeded) await tx.CommitAsync(TestContext.Current.CancellationToken);
                else await tx.RollbackAsync(TestContext.Current.CancellationToken);
                return result;
            }
            catch
            {
                await tx.RollbackAsync(TestContext.Current.CancellationToken);
                throw;
            }
        }

        public async Task RejectAmendmentAsync()
        {
            var approval = await Db.ApprovalRequests.AsNoTracking()
                .SingleAsync(item => item.ResourceId == _pendingAmendmentRequestId
                    && item.ResourceType == "licenseAmendment", TestContext.Current.CancellationToken);
            var request = await Db.LicenseAmendmentRequests.SingleAsync(
                item => item.Id == _pendingAmendmentRequestId, TestContext.Current.CancellationToken);
            var now = DateTimeOffset.UtcNow;
            Db.Entry(request).Property(item => item.RowVersion).OriginalValue =
                approval.ExpectedResourceRowVersion;
            request.Status = VendorLicenseAmendmentRequest.StatusRejected;
            request.FailureCode = "amendment_rejected";
            request.UpdatedAtUtc = now;
            approval.Status = "Rejected";
            approval.ReviewedByUserId = Guid.NewGuid();
            approval.ReviewedAtUtc = now;
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        public static async Task<AmendmentFixture?> OpenAsync()
        {
            var baseConnection = Environment.GetEnvironmentVariable("FARANO_VENDOR_TEST_SQLSERVER");
            if (string.IsNullOrWhiteSpace(baseConnection))
            {
                const string message = "Set FARANO_VENDOR_TEST_SQLSERVER to a disposable local SQL Server.";
                if (Environment.GetEnvironmentVariable("FARANOU_REQUIRE_VENDOR_SQLSERVER") == "1") Assert.Fail(message);
                Assert.Skip(message);
                return null;
            }

            var connection = new SqlConnectionStringBuilder(baseConnection)
            {
                InitialCatalog = $"FaranouVendorAmendment_{Guid.NewGuid():N}"
            }.ConnectionString;
            var provider = VendorTestServiceProviderFactory.Create(connection);
            try
            {
                var scope = provider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
                await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
                var now = DateTimeOffset.UtcNow;
                var customer = new VendorCustomer
                {
                    Id = Guid.NewGuid(), CustomerNumber = $"C-{Guid.NewGuid():N}"[..14],
                    LegalName = "Amendment test customer", DisplayName = "Amendment test",
                    Status = "Active", CreatedAtUtc = now, UpdatedAtUtc = now
                };
                var installation = new VendorInstallation
                {
                    Id = Guid.NewGuid(), CustomerId = customer.Id, Customer = customer,
                    InstallationCode = $"I-{Guid.NewGuid():N}"[..14], Name = "Test installation",
                    Environment = "Production", Status = "Active", CreatedAtUtc = now
                };
                var draft = AssertVendorCompanyDraft();
                var license = new VendorLicense
                {
                    Id = Guid.NewGuid(), CustomerId = customer.Id, Customer = customer,
                    InstallationId = installation.Id, Installation = installation,
                    LicenseNumber = $"L-{Guid.NewGuid():N}"[..14], ProductCode = "FARANOU",
                    Status = "Draft", LicenseKind = LicensePayload.KindPerpetual, MaxCompanies = 5,
                    SubsystemsJson = "[\"finance\",\"sales\"]", GraceDays = 30, LastIssuedSequence = 0,
                    ValidFromUtc = now.AddMinutes(-1), CreatedAtUtc = now,
                    CompanyEntitlementsDraftJson = draft
                };
                db.AddRange(customer, installation, license);
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
                db.ApprovalRequests.Add(new VendorApprovalRequest
                {
                    Id = Guid.NewGuid(), ResourceType = "license", ResourceId = license.Id,
                    TargetStatus = "Issued", ExpectedResourceRowVersion = license.RowVersion.ToArray(),
                    RequestedByUserId = Guid.NewGuid(), RequestedAtUtc = now, Status = "Pending"
                });
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);

                var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                var trustStore = new LicenseSigningTrustStore(new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        [$"Licensing:TrustedSigningKeys:{TestKeyId}:PublicKeySubjectPublicKeyInfoBase64"] =
                            Convert.ToBase64String(key.ExportSubjectPublicKeyInfo())
                    }).Build());
                var envelopeFactory = new VendorLicenseEnvelopeFactory(new TestSigner(key));
                var fixture = new AmendmentFixture
                {
                    Provider = provider, Scope = scope, Db = db, SigningKey = key,
                    LicenseIssuance = new VendorLicenseIssuanceService(
                        db, envelopeFactory, new VendorAuditWriter(db, TimeProvider.System), TimeProvider.System),
                    Issuance = new VendorLicenseAmendmentIssuanceService(
                        db, envelopeFactory, trustStore, new VendorAuditWriter(db, TimeProvider.System), TimeProvider.System)
                };
                return fixture;
            }
            catch
            {
                await provider.DisposeAsync();
                throw;
            }
        }

        private static string AssertVendorCompanyDraft()
        {
            Assert.True(VendorLicenseCompanyDraft.TryNormalize(
                [new VendorCompanyDraftInput(null, "Main", ["finance"], 7, null)], null, 5,
                out var json, out var error), error);
            return json!;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.Database.EnsureDeletedAsync();
            Scope.Dispose();
            await Provider.DisposeAsync();
            SigningKey.Dispose();
        }
    }

    private sealed class TestSigner(ECDsa key) : ILicenseSigningProvider
    {
        public ValueTask<LicenseSigningResult> SignAsync(ReadOnlyMemory<byte> payload, CancellationToken ct = default) =>
            ValueTask.FromResult(LicenseSigningResult.Success(
                TestKeyId, LicenseAlgorithms.EcdsaP256Sha256,
                key.SignData(payload.Span, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)));
    }

    private sealed class FailingAuditWriter : IVendorAuditWriter
    {
        public Task<VendorAuditWriteResult> AppendAsync(VendorAuditWriteRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated audit write failure.");
    }
}
