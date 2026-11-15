using System.Security.Cryptography;
using System.Text.Json;
using Faranou.Licensing.Contracts;
using Faranou.Vendor.Host.Authorization;
using Faranou.Vendor.Host.Signing;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Auditing;
using Faranou.Vendor.Persistence.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

/// <summary>Real SQL Server tests for the company-scoped (payload v2) draft: migration, issuance, RowVersion.</summary>
public sealed class VendorLicenseCompanyDraftSqlServerTests
{
    private static string BuildDraft(params string[] descriptors)
    {
        var inputs = descriptors.Select(d => new VendorCompanyDraftInput(null, d, ["finance", "sales"], 7, null)).ToArray();
        Assert.True(VendorLicenseCompanyDraft.TryNormalize(inputs, null, 5, out var json, out var error), error);
        return json!;
    }

    [Fact]
    public async Task MigrationAddsNullableDraftColumnAndLegacyRowsStayNull()
    {
        await using var fixture = await Fixture.OpenAsync(draftJson: null);
        if (fixture is null) return;

        var column = await fixture.Db.Database.SqlQueryRaw<string>(
            "SELECT CONCAT(DATA_TYPE, '/', CHARACTER_MAXIMUM_LENGTH, '/', IS_NULLABLE) AS [Value] " +
            "FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'VendorLicenses' AND COLUMN_NAME = 'CompanyEntitlementsDraftJson'")
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("nvarchar/-1/YES", column);
        Assert.Null((await fixture.Db.Licenses.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken)).CompanyEntitlementsDraftJson);
    }

    [Fact]
    public async Task IssuanceSignsApprovedDraftAndReducesItInTheSameTransaction()
    {
        await using var fixture = await Fixture.OpenAsync(BuildDraft("Main", "Branch"));
        if (fixture is null) return;
        var original = (await fixture.Db.Licenses.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken)).CompanyEntitlementsDraftJson!;

        var result = await fixture.IssueAsync(commit: true);

        Assert.True(result.Succeeded, result.ErrorCode);
        fixture.Db.ChangeTracker.Clear();
        var license = await fixture.Db.Licenses.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        var stored = await fixture.Db.SignedLicenseEnvelopes.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        var envelope = JsonSerializer.Deserialize<LicenseEnvelope>(stored.EnvelopeJson)!;
        Assert.True(CanonicalJson.TryParsePayload(envelope.PayloadJson, out var payload, out _));
        Assert.Equal(2, payload!.FormatVersion);
        VendorLicenseCompanyDraft.TryParse(original, out var before);
        Assert.Equal(before!.Companies.Select(c => c.CompanyReference).Order(),
            payload.CompanyEntitlements!.Select(c => c!.CompanyReference!).Order());
        Assert.Equal("Issued", license.Status);

        using var doc = JsonDocument.Parse(license.CompanyEntitlementsDraftJson!);
        foreach (var company in doc.RootElement.GetProperty("companies").EnumerateArray())
            Assert.Equal(new[] { "companyReference", "descriptor" }, company.EnumerateObject().Select(p => p.Name).Order().ToArray());
        Assert.DoesNotContain("maxUsers", license.CompanyEntitlementsDraftJson);
        Assert.DoesNotContain("modules", license.CompanyEntitlementsDraftJson);
    }

    [Fact]
    public async Task FailedAuditRollsBackDraftReductionAndEnvelopeTogether()
    {
        await using var fixture = await Fixture.OpenAsync(BuildDraft("Main"));
        if (fixture is null) return;
        var original = (await fixture.Db.Licenses.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken)).CompanyEntitlementsDraftJson;

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.IssueAsync(commit: false, failAudit: true));

        fixture.Db.ChangeTracker.Clear();
        var license = await fixture.Db.Licenses.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Draft", license.Status);
        Assert.Equal(original, license.CompanyEntitlementsDraftJson);
        Assert.Empty(await fixture.Db.SignedLicenseEnvelopes.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DraftChangeAfterApprovalRequestInvalidatesTheApproval()
    {
        await using var fixture = await Fixture.OpenAsync(BuildDraft("Main"));
        if (fixture is null) return;
        var license = await fixture.Db.Licenses.SingleAsync(TestContext.Current.CancellationToken);
        Assert.True(VendorLicenseCompanyDraft.TryNormalize(
            [new VendorCompanyDraftInput(null, "Changed", ["finance"], 99, null)], license.CompanyEntitlementsDraftJson, 5,
            out var changed, out _));
        var versionBefore = license.RowVersion.ToArray();
        license.CompanyEntitlementsDraftJson = changed;
        await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.False(license.RowVersion.AsSpan().SequenceEqual(versionBefore));

        var result = await fixture.IssueAsync(commit: false);

        Assert.False(result.Succeeded);
        Assert.Equal("approval_resource_stale", result.ErrorCode);
        Assert.Empty(await fixture.Db.SignedLicenseEnvelopes.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DraftEditCommittedDuringIssuanceMakesIssuanceFailWithoutSigningOldContent()
    {
        await using var fixture = await Fixture.OpenAsync(BuildDraft("Main"));
        if (fixture is null) return;
        await using var scopeB = fixture.Provider.CreateAsyncScope();
        var dbB = scopeB.ServiceProvider.GetRequiredService<VendorDbContext>();
        await using var txA = await fixture.Db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var approval = await fixture.Db.ApprovalRequests.SingleAsync(TestContext.Current.CancellationToken);
        var service = fixture.Service(fixture.Db, failAudit: false);
        // A loads the license at the approved version, then B commits a draft edit before A saves.
        await fixture.Db.Licenses.SingleAsync(TestContext.Current.CancellationToken);
        var licenseB = await dbB.Licenses.SingleAsync(TestContext.Current.CancellationToken);
        licenseB.CompanyEntitlementsDraftJson = BuildDraft("Edited by B");
        await dbB.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await service.IssueApprovedAsync(
            approval, Guid.NewGuid(), approval.RowVersion.ToArray(), null,
            Guid.NewGuid().ToString("N"), TestContext.Current.CancellationToken);
        await txA.RollbackAsync(TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        dbB.ChangeTracker.Clear();
        Assert.Equal("Draft", (await dbB.Licenses.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken)).Status);
        Assert.Empty(await dbB.SignedLicenseEnvelopes.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LegacyV1LicenseIssuesV1AndKeepsDraftNull()
    {
        await using var fixture = await Fixture.OpenAsync(draftJson: null);
        if (fixture is null) return;

        var result = await fixture.IssueAsync(commit: true);

        Assert.True(result.Succeeded, result.ErrorCode);
        fixture.Db.ChangeTracker.Clear();
        var license = await fixture.Db.Licenses.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        var stored = await fixture.Db.SignedLicenseEnvelopes.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        var envelope = JsonSerializer.Deserialize<LicenseEnvelope>(stored.EnvelopeJson)!;
        Assert.True(CanonicalJson.TryParsePayload(envelope.PayloadJson, out var payload, out _));
        Assert.Equal(1, payload!.FormatVersion);
        Assert.Null(payload.CompanyEntitlements);
        Assert.Null(license.CompanyEntitlementsDraftJson);
        Assert.Equal("[\"finance\",\"sales\"]", license.SubsystemsJson);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public required Microsoft.Extensions.DependencyInjection.ServiceProvider Provider { get; init; }
        public required IServiceScope Scope { get; init; }
        public required VendorDbContext Db { get; init; }
        private readonly ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        public VendorLicenseIssuanceService Service(VendorDbContext db, bool failAudit) => new(
            db, new VendorLicenseEnvelopeFactory(new TestSigner(key)),
            failAudit ? new FailingAuditWriter() : new VendorAuditWriter(db, TimeProvider.System), TimeProvider.System);

        public async Task<VendorLicenseIssuanceResult> IssueAsync(bool commit, bool failAudit = false)
        {
            var approval = await Db.ApprovalRequests.SingleAsync(TestContext.Current.CancellationToken);
            await using var tx = await Db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
            VendorLicenseIssuanceResult result;
            try
            {
                result = await Service(Db, failAudit).IssueApprovedAsync(
                    approval, Guid.NewGuid(), approval.RowVersion.ToArray(), null,
                    Guid.NewGuid().ToString("N"), TestContext.Current.CancellationToken);
            }
            catch
            {
                await tx.RollbackAsync(TestContext.Current.CancellationToken);
                throw;
            }

            if (commit && result.Succeeded) await tx.CommitAsync(TestContext.Current.CancellationToken);
            else await tx.RollbackAsync(TestContext.Current.CancellationToken);
            return result;
        }

        public static async Task<Fixture?> OpenAsync(string? draftJson)
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
                    LegalName = "Draft test customer", DisplayName = "Draft test",
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
                    Status = "Draft", LicenseKind = LicensePayload.KindPerpetual, MaxCompanies = draftJson is null ? null : 5,
                    SubsystemsJson = "[\"finance\",\"sales\"]", GraceDays = 30, LastIssuedSequence = 0,
                    ValidFromUtc = now.AddMinutes(-1), CreatedAtUtc = now,
                    CompanyEntitlementsDraftJson = draftJson
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
                return new Fixture { Provider = provider, Scope = scope, Db = db };
            }
            catch
            {
                await provider.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await Db.Database.EnsureDeletedAsync();
            Scope.Dispose();
            await Provider.DisposeAsync();
            key.Dispose();
        }
    }

    private sealed class TestSigner(ECDsa key) : ILicenseSigningProvider
    {
        public ValueTask<LicenseSigningResult> SignAsync(ReadOnlyMemory<byte> payload, CancellationToken ct = default) =>
            ValueTask.FromResult(LicenseSigningResult.Success(
                "test-kid", LicenseAlgorithms.EcdsaP256Sha256,
                key.SignData(payload.Span, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)));
    }

    private sealed class FailingAuditWriter : IVendorAuditWriter
    {
        public Task<VendorAuditWriteResult> AppendAsync(VendorAuditWriteRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated audit write failure.");
    }
}
