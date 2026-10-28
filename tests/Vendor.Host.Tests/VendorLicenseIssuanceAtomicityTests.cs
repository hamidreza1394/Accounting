using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Faranou.Licensing.Contracts;
using Faranou.Vendor.Host.Signing;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Auditing;
using Faranou.Vendor.Persistence.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorLicenseIssuanceAtomicityTests
{
    [Fact]
    public async Task SuccessfulIssuancePersistsEnvelopeAndRelatedStateTogether()
    {
        var fixture = await OpenAsync();
        if (fixture is null) return;
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            var db = fixture.Db;
            var approval = await db.ApprovalRequests.SingleAsync();
            var expectedVersion = approval.RowVersion.ToArray();
            var reviewer = Guid.NewGuid();
            var service = new VendorLicenseIssuanceService(
                db, new VendorLicenseEnvelopeFactory(new TestSigner(key)),
                new VendorAuditWriter(db, TimeProvider.System), TimeProvider.System);

            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                var result = await service.IssueApprovedAsync(
                    approval, reviewer, expectedVersion, "integration approval",
                    Guid.NewGuid().ToString("N"), TestContext.Current.CancellationToken);
                Assert.True(result.Succeeded, result.ErrorCode);
                await tx.CommitAsync(TestContext.Current.CancellationToken);
            }

            db.ChangeTracker.Clear();
            var license = await db.Licenses.SingleAsync();
            var persistedApproval = await db.ApprovalRequests.SingleAsync();
            var stored = await db.SignedLicenseEnvelopes.SingleAsync();
            var envelope = JsonSerializer.Deserialize<LicenseEnvelope>(stored.EnvelopeJson);
            Assert.NotNull(envelope);
            Assert.Equal("Issued", license.Status);
            Assert.Equal(1, license.LastIssuedSequence);
            Assert.Equal("Approved", persistedApproval.Status);
            Assert.Equal(reviewer, persistedApproval.ReviewedByUserId);
            Assert.Equal(1, stored.Sequence);
            Assert.Equal(envelope.Kid, stored.KeyId);
            Assert.Equal(envelope.Algorithm, stored.Algorithm);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(envelope.PayloadJson))).ToLowerInvariant(), stored.PayloadSha256);
            Assert.True(new EcdsaP256Sha256Verifier().TryVerify(envelope, key.ExportSubjectPublicKeyInfo(), out _));
            var audit = await VendorAuditChainVerifier.VerifyAsync(db, TestContext.Current.CancellationToken);
            Assert.True(audit.IsValid, audit.FailureCode);
            Assert.Equal(2, audit.EventCount);
        }
        finally
        {
            await fixture.Db.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
            fixture.Scope.Dispose();
            await fixture.Provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task FailedAuditAppendRollsBackEnvelopeLicenseSequenceAndApproval()
    {
        var fixture = await OpenAsync();
        if (fixture is null) return;
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            var db = fixture.Db;
            var approval = await db.ApprovalRequests.SingleAsync();
            var service = new VendorLicenseIssuanceService(
                db, new VendorLicenseEnvelopeFactory(new TestSigner(key)),
                new FailingAuditWriter(), TimeProvider.System);

            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => service.IssueApprovedAsync(
                    approval, Guid.NewGuid(), approval.RowVersion.ToArray(), null,
                    Guid.NewGuid().ToString("N"), TestContext.Current.CancellationToken));
                await tx.RollbackAsync(TestContext.Current.CancellationToken);
            }

            db.ChangeTracker.Clear();
            Assert.Equal("Draft", (await db.Licenses.SingleAsync()).Status);
            Assert.Equal(0, (await db.Licenses.SingleAsync()).LastIssuedSequence);
            Assert.Equal("Pending", (await db.ApprovalRequests.SingleAsync()).Status);
            Assert.Empty(await db.SignedLicenseEnvelopes.ToListAsync());
            Assert.Empty(await db.AuditEvents.ToListAsync());
        }
        finally
        {
            await fixture.Db.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
            fixture.Scope.Dispose();
            await fixture.Provider.DisposeAsync();
        }
    }

    private static async Task<Fixture?> OpenAsync()
    {
        var baseConnection = Environment.GetEnvironmentVariable("FARANO_VENDOR_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(baseConnection))
        {
            const string message = "Set FARANO_VENDOR_TEST_SQLSERVER to a disposable local SQL Server. " +
                "Set FARANOU_REQUIRE_VENDOR_SQLSERVER=1 to require these tests in integration CI.";
            if (Environment.GetEnvironmentVariable("FARANOU_REQUIRE_VENDOR_SQLSERVER") == "1") Assert.Fail(message);
            Assert.Skip(message);
            return null;
        }

        var connection = new SqlConnectionStringBuilder(baseConnection)
        {
            InitialCatalog = $"FaranouVendorTests_{Guid.NewGuid():N}"
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
                LegalName = "Issuance test customer", DisplayName = "Issuance test",
                Status = "Active", CreatedAtUtc = now, UpdatedAtUtc = now
            };
            var installation = new VendorInstallation
            {
                Id = Guid.NewGuid(), CustomerId = customer.Id, Customer = customer,
                InstallationCode = $"I-{Guid.NewGuid():N}"[..14], Name = "Test installation",
                Environment = "Production", Status = "Active", CreatedAtUtc = now
            };
            var license = new VendorLicense
            {
                Id = Guid.NewGuid(), CustomerId = customer.Id, Customer = customer,
                InstallationId = installation.Id, Installation = installation,
                LicenseNumber = $"L-{Guid.NewGuid():N}"[..14], ProductCode = "FARANOU",
                Status = "Draft", LicenseKind = LicensePayload.KindPerpetual,
                SubsystemsJson = "[\"finance\",\"sales\"]", GraceDays = 30,
                LastIssuedSequence = 0, ValidFromUtc = now.AddMinutes(-1), CreatedAtUtc = now
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
            return new Fixture(provider, scope, db);
        }
        catch
        {
            await provider.DisposeAsync();
            throw;
        }
    }

    private sealed record Fixture(
        Microsoft.Extensions.DependencyInjection.ServiceProvider Provider,
        Microsoft.Extensions.DependencyInjection.IServiceScope Scope,
        VendorDbContext Db);

    private sealed class TestSigner(ECDsa key) : ILicenseSigningProvider
    {
        public ValueTask<LicenseSigningResult> SignAsync(ReadOnlyMemory<byte> payload, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return ValueTask.FromResult(LicenseSigningResult.Success(
                "test-kid", LicenseAlgorithms.EcdsaP256Sha256,
                key.SignData(payload.Span, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)));
        }
    }

    private sealed class FailingAuditWriter : IVendorAuditWriter
    {
        public Task<VendorAuditWriteResult> AppendAsync(VendorAuditWriteRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated audit write failure.");
    }
}
