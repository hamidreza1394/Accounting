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
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorOfflineRevocationIssuanceTests
{
    [Fact]
    public void EmptyOrRevokedTrustEntriesNeverTrustAnOfflineRevocationSigner()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var keyId = "test-kid";
        var empty = new OfflineRevocationSigningTrustStore(Array.Empty<OfflineRevocationSigningTrustedKeyOptions>());
        Assert.False(empty.TryGetTrustedPublicKey(keyId, out var missing));
        Assert.True(missing.IsEmpty);

        var revoked = new OfflineRevocationSigningTrustStore(new[]
        {
            new OfflineRevocationSigningTrustedKeyOptions
            {
                KeyId = keyId,
                PublicKeySubjectPublicKeyInfoBase64 = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
                Revoked = true
            }
        });
        Assert.False(revoked.TryGetTrustedPublicKey(keyId, out var retired));
        Assert.True(retired.IsEmpty);
    }

    [Fact]
    public async Task SuccessfulRevocationPersistsSignedSnapshotLicenseAndApprovalTogether()
    {
        var fixture = await OpenAsync();
        if (fixture is null) return;
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            var db = fixture.Db;
            var licenseId = fixture.LicenseIds.Single();
            var approval = await db.ApprovalRequests.SingleAsync(
                item => item.Id == fixture.ApprovalIds[licenseId], TestContext.Current.CancellationToken);
            var reviewer = Guid.NewGuid();
            var service = CreateService(db, key, new VendorAuditWriter(db, TimeProvider.System));

            await using (var tx = await db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken))
            {
                var result = await service.RevokeApprovedAsync(
                    approval, reviewer, approval.RowVersion.ToArray(), " approved offline revocation ",
                    Guid.NewGuid().ToString("N"), TestContext.Current.CancellationToken);
                Assert.True(result.Succeeded, result.ErrorCode);
                await tx.CommitAsync(TestContext.Current.CancellationToken);
            }

            db.ChangeTracker.Clear();
            var license = await db.Licenses.SingleAsync(
                item => item.Id == licenseId, TestContext.Current.CancellationToken);
            var persistedApproval = await db.ApprovalRequests.SingleAsync(
                item => item.Id == approval.Id, TestContext.Current.CancellationToken);
            var snapshot = await db.SignedRevocationSnapshots.SingleAsync(TestContext.Current.CancellationToken);
            var envelope = JsonSerializer.Deserialize<OfflineRevocationListEnvelope>(
                snapshot.EnvelopeJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Assert.Equal("Revoked", license.Status);
            Assert.Equal("Approved", persistedApproval.Status);
            Assert.Equal(reviewer, persistedApproval.ReviewedByUserId);
            Assert.Equal("approved offline revocation", persistedApproval.ReviewComment);
            Assert.Equal(1, snapshot.Sequence);
            Assert.Equal(approval.Id, snapshot.ApprovalRequestId);
            Assert.Equal(licenseId, snapshot.TriggeringLicenseId);
            Assert.Equal("test-kid", snapshot.KeyId);
            Assert.NotNull(envelope);
            Assert.Equal(snapshot.KeyId, envelope!.KeyId);
            Assert.Equal(snapshot.Algorithm, envelope.Algorithm);
            Assert.Equal(
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(envelope.PayloadJson))).ToLowerInvariant(),
                snapshot.PayloadSha256);
            Assert.True(OfflineRevocationListValidator.TryValidateAndVerify(
                envelope, key.ExportSubjectPublicKeyInfo(), DateTimeOffset.UtcNow, 0,
                out var parsed, out var error), error);
            Assert.NotNull(parsed);
            Assert.Equal(new[] { licenseId.ToString("D") }, parsed!.RevokedLicenseIds);

            var audit = await VendorAuditChainVerifier.VerifyAsync(db, TestContext.Current.CancellationToken);
            Assert.True(audit.IsValid, audit.FailureCode);
            Assert.Equal(3, audit.EventCount);

            await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [dbo].[VendorSignedRevocationSnapshots] SET [EnvelopeJson] = [EnvelopeJson] WHERE [Id] = {snapshot.Id}",
                TestContext.Current.CancellationToken));
        }
        finally
        {
            await CloseAsync(fixture);
        }
    }

    [Fact]
    public async Task UnavailableSignerLeavesLicenseApprovalSnapshotAndAuditUnchanged()
    {
        var fixture = await OpenAsync();
        if (fixture is null) return;
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            var db = fixture.Db;
            var licenseId = fixture.LicenseIds.Single();
            var approval = await db.ApprovalRequests.SingleAsync(
                item => item.Id == fixture.ApprovalIds[licenseId], TestContext.Current.CancellationToken);
            var service = CreateService(
                db, key, new VendorAuditWriter(db, TimeProvider.System),
                new UnavailableOfflineRevocationSigningProvider());

            await using (var tx = await db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken))
            {
                var result = await service.RevokeApprovedAsync(
                    approval, Guid.NewGuid(), approval.RowVersion.ToArray(), null,
                    Guid.NewGuid().ToString("N"), TestContext.Current.CancellationToken);
                Assert.False(result.Succeeded);
                Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
                Assert.Equal("offline_revocation_signing_unavailable", result.ErrorCode);
                await tx.RollbackAsync(TestContext.Current.CancellationToken);
            }

            db.ChangeTracker.Clear();
            Assert.Equal("Issued", (await db.Licenses.SingleAsync(
                item => item.Id == licenseId, TestContext.Current.CancellationToken)).Status);
            Assert.Equal("Pending", (await db.ApprovalRequests.SingleAsync(
                item => item.Id == approval.Id, TestContext.Current.CancellationToken)).Status);
            Assert.Empty(await db.SignedRevocationSnapshots.ToListAsync(TestContext.Current.CancellationToken));
            Assert.Empty(await db.AuditEvents.ToListAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            await CloseAsync(fixture);
        }
    }

    [Fact]
    public async Task InvalidSignatureFailsClosedWithoutChangingLicenseOrApproval()
    {
        var fixture = await OpenAsync();
        if (fixture is null) return;
        using var trustedKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var wrongKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            var db = fixture.Db;
            var licenseId = fixture.LicenseIds.Single();
            var approval = await db.ApprovalRequests.SingleAsync(
                item => item.Id == fixture.ApprovalIds[licenseId], TestContext.Current.CancellationToken);
            var service = CreateService(
                db, trustedKey, new VendorAuditWriter(db, TimeProvider.System),
                new TestSigner(wrongKey, "test-kid"));

            await using (var tx = await db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken))
            {
                var result = await service.RevokeApprovedAsync(
                    approval, Guid.NewGuid(), approval.RowVersion.ToArray(), null,
                    Guid.NewGuid().ToString("N"), TestContext.Current.CancellationToken);
                Assert.False(result.Succeeded);
                Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
                Assert.Equal("offline_revocation_signing_unavailable", result.ErrorCode);
                await tx.RollbackAsync(TestContext.Current.CancellationToken);
            }

            db.ChangeTracker.Clear();
            Assert.Equal("Issued", (await db.Licenses.SingleAsync(
                item => item.Id == licenseId, TestContext.Current.CancellationToken)).Status);
            Assert.Equal("Pending", (await db.ApprovalRequests.SingleAsync(
                item => item.Id == approval.Id, TestContext.Current.CancellationToken)).Status);
            Assert.Empty(await db.SignedRevocationSnapshots.ToListAsync(TestContext.Current.CancellationToken));
            Assert.Empty(await db.AuditEvents.ToListAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            await CloseAsync(fixture);
        }
    }

    [Fact]
    public async Task AuditFailureRollsBackRevocationSnapshotLicenseAndApproval()
    {
        var fixture = await OpenAsync();
        if (fixture is null) return;
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            var db = fixture.Db;
            var licenseId = fixture.LicenseIds.Single();
            var approval = await db.ApprovalRequests.SingleAsync(
                item => item.Id == fixture.ApprovalIds[licenseId], TestContext.Current.CancellationToken);
            var service = CreateService(db, key, new FailingAuditWriter());

            await using (var tx = await db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken))
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => service.RevokeApprovedAsync(
                    approval, Guid.NewGuid(), approval.RowVersion.ToArray(), null,
                    Guid.NewGuid().ToString("N"), TestContext.Current.CancellationToken));
                await tx.RollbackAsync(TestContext.Current.CancellationToken);
            }

            db.ChangeTracker.Clear();
            Assert.Equal("Issued", (await db.Licenses.SingleAsync(
                item => item.Id == licenseId, TestContext.Current.CancellationToken)).Status);
            Assert.Equal("Pending", (await db.ApprovalRequests.SingleAsync(
                item => item.Id == approval.Id, TestContext.Current.CancellationToken)).Status);
            Assert.Empty(await db.SignedRevocationSnapshots.ToListAsync(TestContext.Current.CancellationToken));
            Assert.Empty(await db.AuditEvents.ToListAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            await CloseAsync(fixture);
        }
    }

    [Fact]
    public async Task ConcurrentRevocationsUseMonotonicSequencesAndIncludeEarlierRevocations()
    {
        var fixture = await OpenAsync(licenseCount: 2);
        if (fixture is null) return;
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            var dbA = fixture.Db;
            await using var scopeB = fixture.Provider.CreateAsyncScope();
            var dbB = scopeB.ServiceProvider.GetRequiredService<VendorDbContext>();
            await using var txA = await dbA.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
            await using var txB = await dbB.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);

            var licenseA = fixture.LicenseIds[0];
            var licenseB = fixture.LicenseIds[1];
            var approvalA = await dbA.ApprovalRequests.SingleAsync(
                item => item.Id == fixture.ApprovalIds[licenseA], TestContext.Current.CancellationToken);
            var approvalB = await dbB.ApprovalRequests.SingleAsync(
                item => item.Id == fixture.ApprovalIds[licenseB], TestContext.Current.CancellationToken);
            var serviceA = CreateService(dbA, key, new VendorAuditWriter(dbA, TimeProvider.System));
            var serviceB = CreateService(dbB, key, new VendorAuditWriter(dbB, TimeProvider.System));

            var taskA = serviceA.RevokeApprovedAsync(
                approvalA, Guid.NewGuid(), approvalA.RowVersion.ToArray(), null,
                Guid.NewGuid().ToString("N"), TestContext.Current.CancellationToken);
            var taskB = serviceB.RevokeApprovedAsync(
                approvalB, Guid.NewGuid(), approvalB.RowVersion.ToArray(), null,
                Guid.NewGuid().ToString("N"), TestContext.Current.CancellationToken);

            var firstTask = await Task.WhenAny(taskA, taskB);
            var firstResult = await firstTask;
            Assert.True(firstResult.Succeeded, firstResult.ErrorCode);
            if (firstTask == taskA)
                await txA.CommitAsync(TestContext.Current.CancellationToken);
            else
                await txB.CommitAsync(TestContext.Current.CancellationToken);

            var secondResult = await (firstTask == taskA ? taskB : taskA);
            Assert.True(secondResult.Succeeded, secondResult.ErrorCode);
            if (firstTask == taskA)
                await txB.CommitAsync(TestContext.Current.CancellationToken);
            else
                await txA.CommitAsync(TestContext.Current.CancellationToken);

            dbA.ChangeTracker.Clear();
            var snapshots = await dbA.SignedRevocationSnapshots.AsNoTracking()
                .OrderBy(item => item.Sequence)
                .ToListAsync(TestContext.Current.CancellationToken);
            Assert.Equal(new long[] { 1, 2 }, snapshots.Select(item => item.Sequence));
            Assert.Equal(2, snapshots.Count);

            var latest = JsonSerializer.Deserialize<OfflineRevocationListEnvelope>(
                snapshots[1].EnvelopeJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.NotNull(latest);
            Assert.True(OfflineRevocationListValidator.TryValidateAndVerify(
                latest, key.ExportSubjectPublicKeyInfo(), DateTimeOffset.UtcNow, 1,
                out var latestPayload, out var error), error);
            Assert.NotNull(latestPayload);
            Assert.Equal(
                new[] { licenseA.ToString("D"), licenseB.ToString("D") }.Order(StringComparer.Ordinal),
                latestPayload!.RevokedLicenseIds);
            Assert.All(await dbA.Licenses.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken),
                item => Assert.Equal("Revoked", item.Status));
        }
        finally
        {
            await CloseAsync(fixture);
        }
    }

    private static VendorOfflineRevocationIssuanceService CreateService(
        VendorDbContext db,
        ECDsa key,
        IVendorAuditWriter audit,
        IOfflineRevocationSigningProvider? signingProvider = null)
    {
        var keyId = "test-kid";
        var trustStore = new OfflineRevocationSigningTrustStore(new[]
        {
            new OfflineRevocationSigningTrustedKeyOptions
            {
                KeyId = keyId,
                PublicKeySubjectPublicKeyInfoBase64 = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo())
            }
        });

        return new VendorOfflineRevocationIssuanceService(
            db,
            new VendorOfflineRevocationSnapshotFactory(
                signingProvider ?? new TestSigner(key, keyId), TimeProvider.System),
            trustStore,
            Options.Create(new VendorOfflineRevocationOptions { SnapshotValidityDays = 30 }),
            audit,
            TimeProvider.System,
            NullLogger<VendorOfflineRevocationIssuanceService>.Instance);
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
                Id = Guid.NewGuid(),
                CustomerNumber = $"C-{Guid.NewGuid():N}"[..14],
                LegalName = "Revocation test customer",
                DisplayName = "Revocation test",
                Status = "Active",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            var installation = new VendorInstallation
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                Customer = customer,
                InstallationCode = $"I-{Guid.NewGuid():N}"[..14],
                Name = "Revocation test installation",
                Environment = "Production",
                Status = "Active",
                CreatedAtUtc = now
            };
            db.AddRange(customer, installation);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var licenses = Enumerable.Range(0, licenseCount).Select(_ => new VendorLicense
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                Customer = customer,
                InstallationId = installation.Id,
                Installation = installation,
                LicenseNumber = $"L-{Guid.NewGuid():N}"[..18],
                ProductCode = "FARANOU",
                Status = "Issued",
                LicenseKind = LicensePayload.KindPerpetual,
                SubsystemsJson = "[\"finance\",\"sales\"]",
                GraceDays = 30,
                LastIssuedSequence = 1,
                ValidFromUtc = now.AddMinutes(-1),
                CreatedAtUtc = now
            }).ToArray();
            db.Licenses.AddRange(licenses);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            foreach (var license in licenses)
            {
                db.SignedLicenseEnvelopes.Add(new VendorSignedLicenseEnvelope
                {
                    Id = Guid.NewGuid(),
                    LicenseId = license.Id,
                    Sequence = 1,
                    KeyId = "license-test-v1",
                    Algorithm = LicenseAlgorithms.EcdsaP256Sha256,
                    EnvelopeJson = "{}",
                    PayloadSha256 = new string('a', 64),
                    CreatedAtUtc = now
                });
                db.ApprovalRequests.Add(new VendorApprovalRequest
                {
                    Id = Guid.NewGuid(),
                    ResourceType = "license",
                    ResourceId = license.Id,
                    TargetStatus = "Revoked",
                    ExpectedResourceRowVersion = license.RowVersion.ToArray(),
                    RequestedByUserId = Guid.NewGuid(),
                    RequestedAtUtc = now,
                    Status = "Pending"
                });
            }
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            return new Fixture(
                provider,
                scope,
                db,
                licenses.Select(item => item.Id).ToArray(),
                await db.ApprovalRequests.ToDictionaryAsync(
                    item => item.ResourceId, item => item.Id, TestContext.Current.CancellationToken));
        }
        catch
        {
            await provider.DisposeAsync();
            throw;
        }
    }

    private static async Task CloseAsync(Fixture fixture)
    {
        await fixture.Db.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
        fixture.Scope.Dispose();
        await fixture.Provider.DisposeAsync();
    }

    private sealed record Fixture(
        ServiceProvider Provider,
        IServiceScope Scope,
        VendorDbContext Db,
        Guid[] LicenseIds,
        Dictionary<Guid, Guid> ApprovalIds);

    private sealed class TestSigner(ECDsa key, string keyId) : IOfflineRevocationSigningProvider
    {
        public ValueTask<LicenseSigningResult> SignAsync(
            ReadOnlyMemory<byte> canonicalPayload,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(LicenseSigningResult.Success(
                keyId,
                LicenseAlgorithms.EcdsaP256Sha256,
                key.SignData(canonicalPayload.Span, HashAlgorithmName.SHA256,
                    DSASignatureFormat.IeeeP1363FixedFieldConcatenation)));
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
