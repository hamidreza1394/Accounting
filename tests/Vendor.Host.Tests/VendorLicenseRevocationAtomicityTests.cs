using System.Security.Cryptography;
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
using Microsoft.Extensions.Options;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorLicenseRevocationAtomicityTests
{
    [Fact]
    public async Task SuccessfulRevocationPersistsSnapshotLicenseApprovalAndAuditTogether()
    {
        var fixture = await OpenAsync();
        if (fixture is null) return;
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            var db = fixture.Db;
            var approval = await db.ApprovalRequests.SingleAsync();
            var service = CreateService(db, new TestRevocationSigner(key), new VendorAuditWriter(db, TimeProvider.System));

            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                var result = await service.RevokeApprovedAsync(
                    approval, Guid.NewGuid(), approval.RowVersion.ToArray(), "approved revoke",
                    Guid.NewGuid().ToString("N"), TestContext.Current.CancellationToken);
                Assert.True(result.Succeeded, result.ErrorCode);
                Assert.Equal(1, result.Sequence);
                await tx.CommitAsync(TestContext.Current.CancellationToken);
            }

            db.ChangeTracker.Clear();
            Assert.Equal("Revoked", (await db.Licenses.SingleAsync()).Status);
            Assert.Equal("Approved", (await db.ApprovalRequests.SingleAsync()).Status);
            var snapshot = await db.OfflineRevocationSnapshots.SingleAsync();
            Assert.Equal(1, snapshot.Sequence);
            Assert.Equal(1L, await db.OfflineRevocationSequences.Select(item => item.LastSequence).SingleAsync());
            var envelope = JsonSerializer.Deserialize<OfflineRevocationListEnvelope>(snapshot.EnvelopeJson);
            Assert.NotNull(envelope);
            Assert.True(OfflineRevocationListValidator.TryValidateAndVerify(
                envelope, key.ExportSubjectPublicKeyInfo(), DateTimeOffset.UtcNow, 0,
                out var payload, out var errorCode), errorCode);
            Assert.NotNull(payload);
            Assert.Equal(new[] { (await db.Licenses.Select(item => item.Id).SingleAsync()).ToString("D") },
                payload.RevokedLicenseIds);
            Assert.Equal(snapshot.ListId, payload.ListId);
            Assert.Equal(snapshot.IssuedAtUtc, payload.IssuedAtUtc);
            Assert.Equal(snapshot.ExpiresAtUtc, payload.ExpiresAtUtc);
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
    public async Task UnavailableSignerRollsBackSequenceLicenseApprovalAndSnapshot()
    {
        var fixture = await OpenAsync();
        if (fixture is null) return;
        try
        {
            var db = fixture.Db;
            var approval = await db.ApprovalRequests.SingleAsync();
            var service = CreateService(db, new UnavailableOfflineRevocationSigningProvider(),
                new VendorAuditWriter(db, TimeProvider.System));

            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                var result = await service.RevokeApprovedAsync(
                    approval, Guid.NewGuid(), approval.RowVersion.ToArray(), null,
                    Guid.NewGuid().ToString("N"), TestContext.Current.CancellationToken);
                Assert.False(result.Succeeded);
                Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
                await tx.RollbackAsync(TestContext.Current.CancellationToken);
            }

            db.ChangeTracker.Clear();
            Assert.Equal("Issued", (await db.Licenses.SingleAsync()).Status);
            Assert.Equal("Pending", (await db.ApprovalRequests.SingleAsync()).Status);
            Assert.Equal(0L, await db.OfflineRevocationSequences.Select(item => item.LastSequence).SingleAsync());
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
    public async Task AuditFailureRollsBackSequenceLicenseApprovalAndSnapshot()
    {
        var fixture = await OpenAsync();
        if (fixture is null) return;
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            var db = fixture.Db;
            var approval = await db.ApprovalRequests.SingleAsync();
            var service = CreateService(db, new TestRevocationSigner(key), new FailingAuditWriter());

            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => service.RevokeApprovedAsync(
                    approval, Guid.NewGuid(), approval.RowVersion.ToArray(), null,
                    Guid.NewGuid().ToString("N"), TestContext.Current.CancellationToken));
                await tx.RollbackAsync(TestContext.Current.CancellationToken);
            }

            db.ChangeTracker.Clear();
            Assert.Equal("Issued", (await db.Licenses.SingleAsync()).Status);
            Assert.Equal("Pending", (await db.ApprovalRequests.SingleAsync()).Status);
            Assert.Equal(0L, await db.OfflineRevocationSequences.Select(item => item.LastSequence).SingleAsync());
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
    public async Task ConcurrentTransactionsReceiveDistinctMonotonicSequences()
    {
        var fixture = await OpenAsync();
        if (fixture is null) return;
        try
        {
            var dbA = fixture.Db;
            await using var scopeB = fixture.Provider.CreateAsyncScope();
            var dbB = scopeB.ServiceProvider.GetRequiredService<VendorDbContext>();
            await using var txA = await dbA.Database.BeginTransactionAsync();
            await using var txB = await dbB.Database.BeginTransactionAsync();
            var allocatorA = new VendorOfflineRevocationSequenceAllocator(dbA);
            var allocatorB = new VendorOfflineRevocationSequenceAllocator(dbB);

            var taskA = allocatorA.AllocateNextAsync(TestContext.Current.CancellationToken);
            var taskB = allocatorB.AllocateNextAsync(TestContext.Current.CancellationToken);
            var firstTask = await Task.WhenAny(taskA, taskB);
            var firstSequence = await firstTask;
            if (firstTask == taskA)
                await txA.CommitAsync(TestContext.Current.CancellationToken);
            else
                await txB.CommitAsync(TestContext.Current.CancellationToken);

            var secondSequence = await (firstTask == taskA ? taskB : taskA);
            if (firstTask == taskA)
                await txB.CommitAsync(TestContext.Current.CancellationToken);
            else
                await txA.CommitAsync(TestContext.Current.CancellationToken);

            Assert.Equal(new long[] { 1, 2 }, new[] { firstSequence, secondSequence }.Order().ToArray());
            dbA.ChangeTracker.Clear();
            Assert.Equal(2L, await dbA.OfflineRevocationSequences.Select(item => item.LastSequence).SingleAsync());
        }
        finally
        {
            await fixture.Db.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
            fixture.Scope.Dispose();
            await fixture.Provider.DisposeAsync();
        }
    }

    private static VendorLicenseRevocationService CreateService(
        VendorDbContext db,
        IOfflineRevocationSigningProvider signer,
        IVendorAuditWriter audit) =>
        new(
            db,
            new VendorOfflineRevocationSequenceAllocator(db),
            new VendorOfflineRevocationSnapshotFactory(signer, TimeProvider.System),
            Options.Create(new VendorOfflineRevocationOptions { SnapshotValidityDays = 7 }),
            audit,
            TimeProvider.System);

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
            var license = new VendorLicense
            {
                Id = Guid.NewGuid(), CustomerId = customer.Id, Customer = customer,
                InstallationId = installation.Id, Installation = installation,
                LicenseNumber = $"L-{Guid.NewGuid():N}"[..14], ProductCode = "FARANOU",
                Status = "Issued", LicenseKind = LicensePayload.KindPerpetual,
                SubsystemsJson = "[\"finance\",\"sales\"]", GraceDays = 30,
                LastIssuedSequence = 1, ValidFromUtc = now.AddMinutes(-1), CreatedAtUtc = now
            };
            db.AddRange(customer, installation, license);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            db.SignedLicenseEnvelopes.Add(new VendorSignedLicenseEnvelope
            {
                Id = Guid.NewGuid(), LicenseId = license.Id, Sequence = 1,
                KeyId = "license-test-key", Algorithm = LicenseAlgorithms.EcdsaP256Sha256,
                EnvelopeJson = "{}", PayloadSha256 = new string('a', 64), CreatedAtUtc = now
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            db.ApprovalRequests.Add(new VendorApprovalRequest
            {
                Id = Guid.NewGuid(), ResourceType = "license", ResourceId = license.Id,
                TargetStatus = "Revoked", ExpectedResourceRowVersion = license.RowVersion.ToArray(),
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

    private sealed class TestRevocationSigner(ECDsa key) : IOfflineRevocationSigningProvider
    {
        public ValueTask<LicenseSigningResult> SignAsync(
            ReadOnlyMemory<byte> canonicalPayload, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var signature = key.SignData(canonicalPayload.Span, HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            return ValueTask.FromResult(LicenseSigningResult.Success(
                "revocation-test-v1", LicenseAlgorithms.EcdsaP256Sha256, signature));
        }
    }

    private sealed class FailingAuditWriter : IVendorAuditWriter
    {
        public Task<VendorAuditWriteResult> AppendAsync(
            VendorAuditWriteRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated audit write failure.");
    }
}
