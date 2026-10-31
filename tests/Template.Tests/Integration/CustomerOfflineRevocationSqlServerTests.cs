using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
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
            var envelope = Sign(signingKey, Sample(DateTimeOffset.UtcNow, 1));
            var anchor = new FakeRollbackAnchor();
            var consumer = CreateConsumer(database, anchor, envelope.KeyId, signingKey.ExportSubjectPublicKeyInfo());

            var applied = await consumer.ApplyAsync(envelope);
            Assert.True(applied.Success, applied.ErrorCode);
            Assert.NotEqual(Data.TpmAnchoredOfflineRevocationStateStore.InitialAnchorDigest, anchor.Digest);

            var accepted = await consumer.CheckAsync("license-not-revoked");
            Assert.Equal(OfflineRevocationCheckStatus.NotRevoked, accepted.Status);

            var row = await database.CustomerOfflineRevocationStates.SingleAsync();
            Assert.Equal(1, row.Sequence);
            Assert.Equal(1, row.AnchorGeneration);
            Assert.Equal(anchor.Digest, row.AnchorDigest);

            // Simulate restoring an older customer DB backup while the TPM NV Extend value remains newer.
            row.Sequence = 0;
            row.SnapshotDigest = null;
            row.ExpiresAtUtc = null;
            row.RevokedLicenseIdsJson = "[]";
            row.PreviousAnchorDigest = Data.TpmAnchoredOfflineRevocationStateStore.InitialAnchorDigest;
            row.AnchorDigest = Data.TpmAnchoredOfflineRevocationStateStore.InitialAnchorDigest;
            row.AnchorGeneration = 0;
            row.AcceptedEnvelopeJson = null;
            row.PendingEnvelopeJson = null;
            row.PendingExpectedGeneration = null;
            row.PendingExpectedAnchorDigest = null;
            await database.SaveChangesAsync();

            var afterRestore = await consumer.CheckAsync("license-not-revoked");
            Assert.Equal(OfflineRevocationCheckStatus.Unavailable, afterRestore.Status);
            Assert.False(afterRestore.IsAvailable);
        });
    }

    [Fact]
    public async Task ApplyAsync_ConcurrentSameSequenceAcrossContextsOnlyAcceptsOneSnapshot()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        await WithDatabaseAsync(async name =>
        {
            await using var firstDatabase = CreateContext(name);
            await using var secondDatabase = CreateContext(name);
            await firstDatabase.Database.MigrateAsync();

            using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var envelope = Sign(signingKey, Sample(DateTimeOffset.UtcNow, 7));
            var publicKey = signingKey.ExportSubjectPublicKeyInfo();
            var anchor = new FakeRollbackAnchor();
            var first = CreateConsumer(firstDatabase, anchor, envelope.KeyId, publicKey);
            var second = CreateConsumer(secondDatabase, anchor, envelope.KeyId, publicKey);

            var results = await Task.WhenAll(first.ApplyAsync(envelope), second.ApplyAsync(envelope));

            Assert.Single(results, result => result.Success);
            Assert.Single(results, result => !result.Success);
            Assert.Equal(ExpectedAnchorDigest(envelope, 1), anchor.Digest);

            await using var verify = CreateContext(name);
            var state = await verify.CustomerOfflineRevocationStates.SingleAsync();
            Assert.Equal(7, state.Sequence);
            Assert.Equal(1, state.AnchorGeneration);
        });
    }

    [Fact]
    public async Task GetCurrentAsync_RecoversPendingSnapshotWhenCrashHappenedBeforeTpmExtend()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        await WithDatabaseAsync(async name =>
        {
            await using var database = CreateContext(name);
            await database.Database.MigrateAsync();

            using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var envelope = Sign(signingKey, Sample(DateTimeOffset.UtcNow, 4));
            AddPendingRow(database, envelope);
            await database.SaveChangesAsync();

            var anchor = new FakeRollbackAnchor();
            var store = new Data.TpmAnchoredOfflineRevocationStateStore(
                database, anchor, new TestKeyResolver(envelope.KeyId, signingKey.ExportSubjectPublicKeyInfo()), TimeProvider.System);

            var state = await store.GetCurrentAsync();

            Assert.NotNull(state);
            Assert.Equal(4, state!.Sequence);
            Assert.Equal(1, state.AnchorGeneration);
            Assert.Equal(ExpectedAnchorDigest(envelope, 1), anchor.Digest);
            Assert.Contains("license-a", state.RevokedLicenseIds);
        });
    }

    [Fact]
    public async Task GetCurrentAsync_FinalizesPendingSnapshotWhenCrashHappenedAfterTpmExtend()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        await WithDatabaseAsync(async name =>
        {
            await using var database = CreateContext(name);
            await database.Database.MigrateAsync();

            using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var envelope = Sign(signingKey, Sample(DateTimeOffset.UtcNow, 5));
            AddPendingRow(database, envelope);
            await database.SaveChangesAsync();

            var expectedDigest = ExpectedAnchorDigest(envelope, 1);
            var anchor = new FakeRollbackAnchor(expectedDigest);
            var store = new Data.TpmAnchoredOfflineRevocationStateStore(
                database, anchor, new TestKeyResolver(envelope.KeyId, signingKey.ExportSubjectPublicKeyInfo()), TimeProvider.System);

            var state = await store.GetCurrentAsync();

            Assert.NotNull(state);
            Assert.Equal(5, state!.Sequence);
            Assert.Equal(1, state.AnchorGeneration);
            Assert.Equal(expectedDigest, anchor.Digest);
        });
    }

    private static void AddPendingRow(Data.DatabaseContext database, OfflineRevocationListEnvelope envelope)
    {
        database.CustomerOfflineRevocationStates.Add(new Domain.CustomerOfflineRevocationState
        {
            Sequence = 0,
            RevokedLicenseIdsJson = "[]",
            PreviousAnchorDigest = Data.TpmAnchoredOfflineRevocationStateStore.InitialAnchorDigest,
            AnchorDigest = Data.TpmAnchoredOfflineRevocationStateStore.InitialAnchorDigest,
            AnchorGeneration = 0,
            PendingEnvelopeJson = JsonSerializer.Serialize(envelope, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            PendingExpectedGeneration = 1,
            PendingExpectedAnchorDigest = ExpectedAnchorDigest(envelope, 1)
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

    private static string SnapshotDigest(OfflineRevocationListEnvelope envelope) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(envelope.PayloadJson))).ToLowerInvariant();

    private static string ExpectedAnchorDigest(OfflineRevocationListEnvelope envelope, long generation) =>
        Data.TpmAnchoredOfflineRevocationStateStore.ComputeExpectedAnchorDigest(
            Data.TpmAnchoredOfflineRevocationStateStore.InitialAnchorDigest,
            generation,
            SnapshotDigest(envelope));

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

    private sealed class FakeRollbackAnchor(string initialDigest = Data.TpmAnchoredOfflineRevocationStateStore.InitialAnchorDigest)
        : IOfflineRevocationRollbackAnchor
    {
        public string Digest { get; private set; } = initialDigest;

        public Task<string> ReadDigestAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Digest);

        public Task<string> ExtendAsync(
            string expectedCurrentDigest,
            ReadOnlyMemory<byte> extensionData,
            string expectedResultDigest,
            CancellationToken cancellationToken = default)
        {
            if (!string.Equals(Digest, expectedCurrentDigest, StringComparison.Ordinal))
            {
                throw new OfflineRevocationRollbackAnchorUnavailableException();
            }

            var current = Convert.FromHexString(Digest);
            var input = new byte[current.Length + extensionData.Length];
            Buffer.BlockCopy(current, 0, input, 0, current.Length);
            extensionData.CopyTo(input.AsMemory(current.Length));
            var actual = Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
            if (!string.Equals(actual, expectedResultDigest, StringComparison.Ordinal))
            {
                throw new OfflineRevocationRollbackAnchorUnavailableException();
            }

            Digest = actual;
            return Task.FromResult(Digest);
        }
    }
}
