using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Faranou.Licensing.Contracts;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests.Integration;

/// <summary>
/// Exercises customer-only revocation persistence on isolated SQL Server databases.
/// The fake anchor tests protocol recovery; it is not evidence of TPM hardware assurance.
/// </summary>
public sealed class CustomerOfflineRevocationSqlServerTests
{
    private static bool SqlServerAvailable()
    {
        if (IntegrationDatabase.UsesSqlServer)
            return true;

        if (Environment.GetEnvironmentVariable("FARANOU_REQUIRE_SQLSERVER") == "1")
            throw new InvalidOperationException("FARANOU_REQUIRE_SQLSERVER=1 but FARANOU_TEST_SQLSERVER is not set.");

        return false;
    }

    private static Data.DatabaseContext CreateContext(string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(IntegrationDatabase.SqlServerConnectionString)
        {
            InitialCatalog = databaseName,
        };

        return new Data.DatabaseContext(new DbContextOptionsBuilder<Data.DatabaseContext>()
            .UseSqlServer(builder.ConnectionString, sql => sql.MigrationsAssembly(typeof(Data.DatabaseContext).Assembly.FullName))
            .Options);
    }

    private static async Task WithDatabaseAsync(Func<string, Task> test)
    {
        var name = "FaranouTests_Revocation_" + Guid.NewGuid().ToString("N")[..12];
        try
        {
            await test(name);
        }
        finally
        {
            await using var cleanup = CreateContext(name);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task ApplyAsync_PersistsSnapshotAndDatabaseRollbackIsDetectedByAnchorMismatch()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        await WithDatabaseAsync(async name =>
        {
            await using var database = CreateContext(name);
            await database.Database.MigrateAsync();

            using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var now = DateTimeOffset.UtcNow;
            var envelope = Sign(signingKey, Sample(now, 1));
            var anchor = new FakeRollbackAnchor();
            var consumer = CreateConsumer(database, anchor, envelope.KeyId, signingKey.ExportSubjectPublicKeyInfo());

            var applied = await consumer.ApplyAsync(envelope);
            Assert.True(applied.Success, applied.ErrorCode);
            Assert.Equal(1, anchor.Generation);

            var accepted = await consumer.CheckAsync("license-not-revoked");
            Assert.Equal(OfflineRevocationCheckStatus.NotRevoked, accepted.Status);

            var row = await database.CustomerOfflineRevocationStates.SingleAsync();
            Assert.Equal(1, row.Sequence);
            Assert.Equal(1, row.AnchorGeneration);

            // Simulate restoring an older customer DB backup while the TPM NV counter remains newer.
            row.Sequence = 0;
            row.SnapshotDigest = null;
            row.ExpiresAtUtc = null;
            row.RevokedLicenseIdsJson = "[]";
            row.AnchorGeneration = 0;
            row.PendingEnvelopeJson = null;
            row.PendingExpectedGeneration = null;
            await database.SaveChangesAsync();

            var afterRestore = await consumer.CheckAsync("license-not-revoked");
            Assert.Equal(OfflineRevocationCheckStatus.Unavailable, afterRestore.Status);
            Assert.False(afterRestore.IsAvailable);
        });
    }

    [Fact]
    public async Task GetCurrentAsync_RecoversPendingSnapshotWhenCrashHappenedBeforeTpmIncrement()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        await WithDatabaseAsync(async name =>
        {
            await using var database = CreateContext(name);
            await database.Database.MigrateAsync();

            using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var envelope = Sign(signingKey, Sample(DateTimeOffset.UtcNow, 4));
            database.CustomerOfflineRevocationStates.Add(new Domain.CustomerOfflineRevocationState
            {
                Sequence = 0,
                RevokedLicenseIdsJson = "[]",
                AnchorGeneration = 0,
                PendingEnvelopeJson = JsonSerializer.Serialize(envelope, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                PendingExpectedGeneration = 1
            });
            await database.SaveChangesAsync();

            var anchor = new FakeRollbackAnchor();
            var store = new Data.TpmAnchoredOfflineRevocationStateStore(
                database, anchor, new TestKeyResolver(envelope.KeyId, signingKey.ExportSubjectPublicKeyInfo()), TimeProvider.System);

            var state = await store.GetCurrentAsync();

            Assert.NotNull(state);
            Assert.Equal(4, state!.Sequence);
            Assert.Equal(1, state.AnchorGeneration);
            Assert.Equal(1, anchor.Generation);
            Assert.Contains("license-a", state.RevokedLicenseIds);
        });
    }

    [Fact]
    public async Task GetCurrentAsync_FinalizesPendingSnapshotWhenCrashHappenedAfterTpmIncrement()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        await WithDatabaseAsync(async name =>
        {
            await using var database = CreateContext(name);
            await database.Database.MigrateAsync();

            using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var envelope = Sign(signingKey, Sample(DateTimeOffset.UtcNow, 5));
            database.CustomerOfflineRevocationStates.Add(new Domain.CustomerOfflineRevocationState
            {
                Sequence = 0,
                RevokedLicenseIdsJson = "[]",
                AnchorGeneration = 0,
                PendingEnvelopeJson = JsonSerializer.Serialize(envelope, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                PendingExpectedGeneration = 1
            });
            await database.SaveChangesAsync();

            var anchor = new FakeRollbackAnchor(initialGeneration: 1);
            var store = new Data.TpmAnchoredOfflineRevocationStateStore(
                database, anchor, new TestKeyResolver(envelope.KeyId, signingKey.ExportSubjectPublicKeyInfo()), TimeProvider.System);

            var state = await store.GetCurrentAsync();

            Assert.NotNull(state);
            Assert.Equal(5, state!.Sequence);
            Assert.Equal(1, state.AnchorGeneration);
            Assert.Equal(1, anchor.Generation);
        });
    }

    private static OfflineRevocationConsumer CreateConsumer(
        Data.DatabaseContext database,
        IOfflineRevocationRollbackAnchor anchor,
        string keyId,
        byte[] publicKey)
    {
        var store = new Data.TpmAnchoredOfflineRevocationStateStore(
            database, anchor, new TestKeyResolver(keyId, publicKey), TimeProvider.System);
        return new OfflineRevocationConsumer(new TestKeyResolver(keyId, publicKey), store, TimeProvider.System);
    }

    private static OfflineRevocationList Sample(DateTimeOffset now, long sequence) =>
        new(1, "revocation-list-sql-test", sequence, now.AddMinutes(-1), now.AddDays(7), ["license-a"]);

    private static OfflineRevocationListEnvelope Sign(ECDsa key, OfflineRevocationList payload)
    {
        var json = OfflineRevocationListValidator.Serialize(payload);
        var signature = key.SignData(Encoding.UTF8.GetBytes(json), HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return new OfflineRevocationListEnvelope(
            1, "revocation-key-sql-test", LicenseAlgorithms.EcdsaP256Sha256, json, Convert.ToBase64String(signature));
    }

    private sealed class TestKeyResolver(string keyId, byte[] publicKey) : IOfflineRevocationKeyResolver
    {
        public bool TryResolve(string requestedKeyId, out ReadOnlyMemory<byte> trustedPublicKeySpki)
        {
            trustedPublicKeySpki = string.Equals(requestedKeyId, keyId, StringComparison.Ordinal)
                ? publicKey
                : ReadOnlyMemory<byte>.Empty;
            return !trustedPublicKeySpki.IsEmpty;
        }
    }

    private sealed class FakeRollbackAnchor(long initialGeneration = 0) : IOfflineRevocationRollbackAnchor
    {
        public long Generation { get; private set; } = initialGeneration;

        public Task<long> ReadGenerationAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Generation);

        public Task<long> AdvanceAsync(long expectedCurrentGeneration, CancellationToken cancellationToken = default)
        {
            if (Generation != expectedCurrentGeneration)
            {
                throw new OfflineRevocationRollbackAnchorUnavailableException();
            }

            Generation = checked(Generation + 1);
            return Task.FromResult(Generation);
        }
    }
}
