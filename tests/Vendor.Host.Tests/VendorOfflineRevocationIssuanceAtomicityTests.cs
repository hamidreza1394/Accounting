using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Faranou.Licensing.Contracts;
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

public sealed class VendorOfflineRevocationIssuanceAtomicityTests
{
    [Fact]
    public async Task ApprovedRevocationPersistsLicenseSnapshotApprovalAndAuditAtomically()
    {
        var fixture = await OpenAsync();
        if (fixture is null) return;
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            var db = fixture.Db;
            var approval = await db.ApprovalRequests.SingleAsync();
            var reviewer = Guid.NewGuid();
            var service = CreateService(db, key, new VendorAuditWriter(db, TimeProvider.System));

            await using (var transaction = await db.Database.BeginTransactionAsync())
            {
                var result = await service.RevokeApprovedAsync(
                    approval,
                    reviewer,
                    approval.RowVersion.ToArray(),
                    "approved revocation",
                    Guid.NewGuid().ToString("N"),
                    TestContext.Current.CancellationToken);
                Assert.True(result.Succeeded, result.ErrorCode);
                Assert.Equal(1, result.Sequence);
                await transaction.CommitAsync(TestContext.Current.CancellationToken);
            }

            db.ChangeTracker.Clear();
            var license = await db.Licenses.SingleAsync();
            var persistedApproval = await db.ApprovalRequests.SingleAsync();
            var stored = await db.OfflineRevocationSnapshots.SingleAsync();
            var envelope = JsonSerializer.Deserialize<OfflineRevocationListEnvelope>(
                stored.EnvelopeJson, JsonSerializerOptions.Web);
            Assert.Equal("Revoked", license.Status);
            Assert.Equal("Approved", persistedApproval.Status);
            Assert.Equal(reviewer, persistedApproval.ReviewedByUserId);
            Assert.Equal(1, stored.Sequence);
            Assert.NotNull(envelope);
            Assert.Equal(stored.KeyId, envelope!.KeyId);
            Assert.Equal(stored.Algorithm, envelope.Algorithm);
            Assert.Equal(
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(envelope.PayloadJson))).ToLowerInvariant(),
                stored.PayloadSha256);
            Assert.True(OfflineRevocationListValidator.TryValidateAndVerify(
                envelope,
                key.ExportSubjectPublicKeyInfo(),
                DateTimeOffset.UtcNow,
                0,
                out var payload,
                out var errorCode), errorCode);
            Assert.Contains(license.Id.ToString("D"), payload!.RevokedLicenseIds);
            var audit = await VendorAuditChainVerifier.VerifyAsync(db, TestContext.Current.CancellationToken);
            Assert.True(audit.IsValid, audit.FailureCode);
            Assert.Equal(3, audit.EventCount);
        }
        finally
        {
            await fixture.Db.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
            fixture.Scope.Dispose();
            await fixture.Provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task FailedAuditAppendRollsBackLicenseApprovalAndSnapshot()
    {
        var fixture = await OpenAsync();
        if (fixture is null) return;
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            var db = fixture.Db;
            var approval = await db.ApprovalRequests.SingleAsync();
            var service = CreateService(db, key, new FailingAuditWriter());

            await using (var transaction = await db.Database.BeginTransactionAsync())
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => service.RevokeApprovedAsync(
                    approval,
                    Guid.NewGuid(),
                    approval.RowVersion.ToArray(),
                    null,
                    Guid.NewGuid().ToString("N"),
                    TestContext.Current.CancellationToken));
                await transaction.RollbackAsync(TestContext.Current.CancellationToken);
            }

            db.ChangeTracker.Clear();
            Assert.Equal("Issued", (await db.Licenses.SingleAsync()).Status);
            Assert.Equal("Pending", (await db.ApprovalRequests.SingleAsync()).Status);
            Assert.Empty(await db.OfflineRevocationSnapshots.ToListAsync());
            Assert.Empty(await db.AuditEvents.ToListAsync());
        }
        finally
        {
            await fixture.Db.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
            fixture.Scope.Dispose();
            await fixture.Provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task InitialSnapshotIsIdempotentAndRefreshAdvancesSequence()
    {
        var fixture = await OpenAsync();
        if (fixture is null) return;
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            var db = fixture.Db;
            var service = CreateService(db, key, new VendorAuditWriter(db, TimeProvider.System));
            var actor = Guid.NewGuid();

            await using (var transaction = await db.Database.BeginTransactionAsync())
            {
                var initial = await service.EnsureInitialSnapshotAsync(
                    actor, Guid.NewGuid().ToString("N"), TestContext.Current.CancellationToken);
                Assert.True(initial.Succeeded, initial.ErrorCode);
                Assert.Equal(1, initial.Sequence);
                await transaction.CommitAsync(TestContext.Current.CancellationToken);
            }

            await using (var transaction = await db.Database.BeginTransactionAsync())
            {
                var repeated = await service.EnsureInitialSnapshotAsync(
                    actor, Guid.NewGuid().ToString("N"), TestContext.Current.CancellationToken);
                Assert.True(repeated.Succeeded, repeated.ErrorCode);
                Assert.Equal(1, repeated.Sequence);
                await transaction.CommitAsync(TestContext.Current.CancellationToken);
            }

            await using (var transaction = await db.Database.BeginTransactionAsync())
            {
                var refreshed = await service.RefreshCurrentSnapshotAsync(
                    actor, Guid.NewGuid().ToString("N"), TestContext.Current.CancellationToken);
                Assert.True(refreshed.Succeeded, refreshed.ErrorCode);
                Assert.Equal(2, refreshed.Sequence);
                await transaction.CommitAsync(TestContext.Current.CancellationToken);
            }

            db.ChangeTracker.Clear();
            Assert.Equal(new long[] { 1, 2 },
                await db.OfflineRevocationSnapshots.OrderBy(item => item.Sequence)
                    .Select(item => item.Sequence).ToArrayAsync());
            Assert.Equal(2, await db.AuditEvents.CountAsync());
        }
        finally
        {
            await fixture.Db.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
            fixture.Scope.Dispose();
            await fixture.Provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task ConcurrentRevocationsProduceMonotonicFullStateSnapshots()
    {
        var fixture = await OpenAsync(licenseCount: 2);
        if (fixture is null) return;
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            await using var scopeB = fixture.Provider.CreateAsyncScope();
            var dbA = fixture.Db;
            var dbB = scopeB.ServiceProvider.GetRequiredService<VendorDbContext>();
            var spki = key.ExportSubjectPublicKeyInfo();

            var approvalIds = await dbA.ApprovalRequests
                .OrderBy(item => item.ResourceId)
                .Select(item => item.Id)
                .ToArrayAsync();

            async Task<VendorOfflineRevocationIssuanceResult> RevokeAsync(
                VendorDbContext db,
                Guid approvalId)
            {
                await using var transaction = await db.Database.BeginTransactionAsync();
                var approval = await db.ApprovalRequests.SingleOrDefaultAsync(item => item.Id == approvalId);
                Assert.NotNull(approval);
                var service = CreateService(db, key, new VendorAuditWriter(db, TimeProvider.System), spki);
                var result = await service.RevokeApprovedAsync(
                    approval!,
                    Guid.NewGuid(),
                    approval!.RowVersion.ToArray(),
                    "concurrent revocation",
                    Guid.NewGuid().ToString("N"),
                    TestContext.Current.CancellationToken);
                if (result.Succeeded)
                    await transaction.CommitAsync(TestContext.Current.CancellationToken);
                else
                    await transaction.RollbackAsync(TestContext.Current.CancellationToken);
                return result;
            }

            var results = await Task.WhenAll(
                RevokeAsync(dbA, approvalIds[0]),
                RevokeAsync(dbB, approvalIds[1]));
            Assert.All(results, result => Assert.True(result.Succeeded, result.ErrorCode));
            Assert.Equal(new long[] { 1, 2 }, results.Select(item => item.Sequence!.Value).Order().ToArray());

            dbA.ChangeTracker.Clear();
            var snapshots = await dbA.OfflineRevocationSnapshots.OrderBy(item => item.Sequence).ToListAsync();
            Assert.Equal(2, snapshots.Count);
            var latestEnvelope = JsonSerializer.Deserialize<OfflineRevocationListEnvelope>(
                snapshots[^1].EnvelopeJson, JsonSerializerOptions.Web);
            Assert.NotNull(latestEnvelope);
            Assert.True(OfflineRevocationListValidator.TryValidateAndVerify(
                latestEnvelope!,
                spki,
                DateTimeOffset.UtcNow,
                1,
                out var latestPayload,
                out var errorCode), errorCode);
            Assert.Equal(2, latestPayload!.RevokedLicenseIds.Count);
            Assert.Equal(2, await dbA.Licenses.CountAsync(item => item.Status == "Revoked"));
            var audit = await VendorAuditChainVerifier.VerifyAsync(dbA, TestContext.Current.CancellationToken);
            Assert.True(audit.IsValid, audit.FailureCode);
            Assert.Equal(6, audit.EventCount);
        }
        finally
        {
            await fixture.Db.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
            fixture.Scope.Dispose();
            await fixture.Provider.DisposeAsync();
        }
    }

    private static VendorOfflineRevocationIssuanceService CreateService(
        VendorDbContext db,
        ECDsa key,
        IVendorAuditWriter audit,
        byte[]? publicKey = null)
    {
        const string keyId = "vendor-revocation-test-2026-v1";
        publicKey ??= key.ExportSubjectPublicKeyInfo();
        var factory = new VendorOfflineRevocationEnvelopeFactory(
            new TestSigner(key, keyId),
            new TestKeyResolver(keyId, publicKey),
            TimeProvider.System);
        return new VendorOfflineRevocationIssuanceService(db, factory, audit, TimeProvider.System);
    }

    private static async Task<Fixture?> OpenAsync(int licenseCount = 1)
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
            InitialCatalog = $"FaranouVendorRevocationTests_{Guid.NewGuid():N}"
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
                LegalName = "Revocation test customer", DisplayName = "Revocation test",
                Status = "Active", CreatedAtUtc = now, UpdatedAtUtc = now
            };
            var installation = new VendorInstallation
            {
                Id = Guid.NewGuid(), CustomerId = customer.Id, Customer = customer,
                InstallationCode = $"I-{Guid.NewGuid():N}"[..14], Name = "Revocation test installation",
                Environment = "Production", Status = "Active", CreatedAtUtc = now
            };
            var licenses = Enumerable.Range(0, licenseCount).Select(index => new VendorLicense
            {
                Id = Guid.NewGuid(), CustomerId = customer.Id, Customer = customer,
                InstallationId = installation.Id, Installation = installation,
                LicenseNumber = $"R{index}-{Guid.NewGuid():N}"[..14], ProductCode = "FARANOU",
                Status = "Issued", LicenseKind = LicensePayload.KindPerpetual,
                SubsystemsJson = "[\\"finance\\",\\"sales\\"]", GraceDays = 30,
                LastIssuedSequence = 1, ValidFromUtc = now.AddMinutes(-1), CreatedAtUtc = now
            }).ToArray();
            db.AddRange(customer, installation);
            db.Licenses.AddRange(licenses);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            db.ApprovalRequests.AddRange(licenses.Select(license => new VendorApprovalRequest
            {
                Id = Guid.NewGuid(), ResourceType = "license", ResourceId = license.Id,
                TargetStatus = "Revoked", ExpectedResourceRowVersion = license.RowVersion.ToArray(),
                RequestedByUserId = Guid.NewGuid(), RequestedAtUtc = now, Status = "Pending"
            }));
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

    private sealed class TestSigner(ECDsa key, string keyId) : IOfflineRevocationSigningProvider
    {
        private static readonly object SharedGate = new();

        public ValueTask<LicenseSigningResult> SignAsync(
            ReadOnlyMemory<byte> canonicalPayload,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[] signature;
            lock (SharedGate)
            {
                signature = key.SignData(
                    canonicalPayload.Span,
                    HashAlgorithmName.SHA256,
                    DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            }

            return ValueTask.FromResult(LicenseSigningResult.Success(
                keyId, LicenseAlgorithms.EcdsaP256Sha256, signature));
        }
    }

    private sealed class TestKeyResolver(string keyId, byte[] publicKey) : IOfflineRevocationKeyResolver
    {
        public bool TryResolve(string requestedKeyId, out ReadOnlyMemory<byte> trustedPublicKeySpki)
        {
            if (string.Equals(requestedKeyId, keyId, StringComparison.Ordinal))
            {
                trustedPublicKeySpki = publicKey;
                return true;
            }

            trustedPublicKeySpki = ReadOnlyMemory<byte>.Empty;
            return false;
        }
    }

    private sealed class FailingAuditWriter : IVendorAuditWriter
    {
        public Task<VendorAuditWriteResult> AppendAsync(
            VendorAuditWriteRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated audit write failure.");
    }
}
