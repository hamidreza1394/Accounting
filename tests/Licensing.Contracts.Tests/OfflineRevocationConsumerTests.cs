using System.Security.Cryptography;
using System.Text;
using Faranou.Licensing.Contracts;
using Xunit;

namespace Licensing.Contracts.Tests;

public sealed class OfflineRevocationConsumerTests
{
    [Fact]
    public async Task ApplyAsync_VerifiesTrustedSignatureAndPersistsSnapshotBeforeReturningSuccess()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var now = DateTimeOffset.UtcNow;
        var payload = Sample(now);
        var envelope = Sign(key, payload);
        var store = new InMemoryRevocationStateStore();
        var consumer = new OfflineRevocationConsumer(
            new TestKeyResolver(envelope.KeyId, key.ExportSubjectPublicKeyInfo()),
            store,
            TimeProvider.System);

        var result = await consumer.ApplyAsync(envelope);

        Assert.True(result.Success, result.ErrorCode);
        Assert.Equal(payload.Sequence, store.State?.Sequence);
        Assert.Contains("license-a", store.State!.RevokedLicenseIds);
        var revoked = await consumer.CheckAsync("license-a");
        var notRevoked = await consumer.CheckAsync("license-not-revoked");
        Assert.Equal(OfflineRevocationCheckStatus.Revoked, revoked.Status);
        Assert.Equal(OfflineRevocationCheckStatus.NotRevoked, notRevoked.Status);
    }

    [Fact]
    public async Task CheckAsync_ReturnsUnavailableWhenNoSnapshotHasBeenAccepted()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var store = new InMemoryRevocationStateStore();
        var consumer = new OfflineRevocationConsumer(
            new TestKeyResolver("revocation-key-1", key.ExportSubjectPublicKeyInfo()),
            store,
            TimeProvider.System);

        var result = await consumer.CheckAsync("license-a");

        Assert.Equal(OfflineRevocationCheckStatus.Unavailable, result.Status);
        Assert.False(result.IsAvailable);
    }

    [Fact]
    public async Task CheckAsync_ReturnsUnavailableWhenSnapshotHasExpired()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var now = DateTimeOffset.UtcNow;
        var envelope = Sign(key, Sample(now) with { ExpiresAtUtc = now.AddMinutes(1) });
        var store = new InMemoryRevocationStateStore();
        var clock = new MutableTimeProvider(now);
        var consumer = new OfflineRevocationConsumer(
            new TestKeyResolver(envelope.KeyId, key.ExportSubjectPublicKeyInfo()),
            store,
            clock);

        Assert.True((await consumer.ApplyAsync(envelope)).Success);
        clock.SetUtcNow(now.AddMinutes(2));

        var result = await consumer.CheckAsync("license-not-revoked");

        Assert.Equal(OfflineRevocationCheckStatus.Unavailable, result.Status);
        Assert.False(result.IsAvailable);
    }

    [Fact]
    public async Task ApplyAsync_RejectsReplayAfterPersistedSequence()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var now = DateTimeOffset.UtcNow;
        var envelope = Sign(key, Sample(now));
        var store = new InMemoryRevocationStateStore();
        var consumer = new OfflineRevocationConsumer(
            new TestKeyResolver(envelope.KeyId, key.ExportSubjectPublicKeyInfo()),
            store,
            TimeProvider.System);

        Assert.True((await consumer.ApplyAsync(envelope)).Success);
        var replay = await consumer.ApplyAsync(envelope);

        Assert.False(replay.Success);
        Assert.Equal("offline_revocation_sequence_invalid", replay.ErrorCode);
        Assert.Equal(8L, store.State!.Sequence);
    }

    [Fact]
    public async Task ApplyAsync_RejectsUnknownKeyWithoutChangingPersistedState()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var envelope = Sign(key, Sample(DateTimeOffset.UtcNow));
        var store = new InMemoryRevocationStateStore();
        var consumer = new OfflineRevocationConsumer(
            new TestKeyResolver("different-key-id", key.ExportSubjectPublicKeyInfo()),
            store,
            TimeProvider.System);

        var result = await consumer.ApplyAsync(envelope);

        Assert.False(result.Success);
        Assert.Equal("offline_revocation_key_unknown", result.ErrorCode);
        Assert.Null(store.State);
    }

    [Fact]
    public async Task ApplyAsync_RejectsConcurrentOrStaleSequenceAtAtomicStoreBoundary()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var now = DateTimeOffset.UtcNow;
        var envelope = Sign(key, Sample(now));
        var store = new InMemoryRevocationStateStore();
        var consumer = new OfflineRevocationConsumer(
            new TestKeyResolver(envelope.KeyId, key.ExportSubjectPublicKeyInfo()),
            store,
            TimeProvider.System);

        var results = await Task.WhenAll(consumer.ApplyAsync(envelope), consumer.ApplyAsync(envelope));

        Assert.Single(results.Where(result => result.Success));
        Assert.Single(results.Where(result => !result.Success));
        Assert.Equal(8L, store.State!.Sequence);
    }

    private static OfflineRevocationList Sample(DateTimeOffset now) => new(
        1, "revocation-list-test", 8, now.AddMinutes(-1), now.AddDays(7), ["license-a", "license-z"]);

    private static OfflineRevocationListEnvelope Sign(ECDsa key, OfflineRevocationList payload)
    {
        var json = OfflineRevocationListValidator.Serialize(payload);
        var signature = key.SignData(Encoding.UTF8.GetBytes(json), HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return new OfflineRevocationListEnvelope(
            1, "revocation-key-1", LicenseAlgorithms.EcdsaP256Sha256, json, Convert.ToBase64String(signature));
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void SetUtcNow(DateTimeOffset value) => _now = value;
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

    private sealed class InMemoryRevocationStateStore : IOfflineRevocationStateStore
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        public OfflineRevocationState? State { get; private set; }

        public Task<OfflineRevocationState?> GetCurrentAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(State);

        public async Task<bool> TryApplyAsync(
            OfflineRevocationList list,
            OfflineRevocationListEnvelope envelope,
            string snapshotDigest,
            CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                if (State is not null && list.Sequence <= State.Sequence)
                {
                    return false;
                }

                State = new OfflineRevocationState(
                    list.Sequence,
                    snapshotDigest,
                    list.ExpiresAtUtc,
                    new HashSet<string>(list.RevokedLicenseIds, StringComparer.Ordinal));
                return true;
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}


public sealed class RevocationAwareLicenseEnvelopeValidatorTests
{
    [Fact]
    public async Task ValidateAsync_FailsClosedWhenNoCurrentRevocationSnapshotExists()
    {
        using var licenseKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var revocationKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pipeline = new RevocationAwareLicenseEnvelopeValidator(
            CreateValidator(licenseKey),
            CreateConsumer(revocationKey, null));

        var result = await pipeline.ValidateAsync(CreateLicenseEnvelope(licenseKey), "install-1");

        Assert.False(result.Success);
        Assert.Equal("offline_revocation_unavailable", result.ErrorCode);
    }

    [Fact]
    public async Task ValidateAsync_AllowsValidLicenseWhenCurrentSnapshotDoesNotRevokeIt()
    {
        using var licenseKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var revocationKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var state = new OfflineRevocationState(
            4, new string('a', 64), DateTimeOffset.UtcNow.AddDays(1),
            new HashSet<string>(["another-license"], StringComparer.Ordinal));
        var pipeline = new RevocationAwareLicenseEnvelopeValidator(
            CreateValidator(licenseKey), CreateConsumer(revocationKey, state));

        var result = await pipeline.ValidateAsync(CreateLicenseEnvelope(licenseKey), "install-1");

        Assert.True(result.Success, result.ErrorCode);
        Assert.Equal("license-0001", result.Payload?.LicenseId);
    }

    [Fact]
    public async Task ValidateAsync_RejectsLicensePresentInCurrentRevocationSnapshot()
    {
        using var licenseKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var revocationKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var state = new OfflineRevocationState(
            5, new string('b', 64), DateTimeOffset.UtcNow.AddDays(1),
            new HashSet<string>(["license-0001"], StringComparer.Ordinal));
        var pipeline = new RevocationAwareLicenseEnvelopeValidator(
            CreateValidator(licenseKey), CreateConsumer(revocationKey, state));

        var result = await pipeline.ValidateAsync(CreateLicenseEnvelope(licenseKey), "install-1");

        Assert.False(result.Success);
        Assert.Equal("license_revoked", result.ErrorCode);
    }

    private static LicenseEnvelopeValidator CreateValidator(ECDsa key) =>
        new(
            new SingleLicenseKeyResolver(key.ExportSubjectPublicKeyInfo()),
            new EcdsaP256Sha256Verifier(),
            new LicenseEvaluator(TimeProvider.System));

    private static OfflineRevocationConsumer CreateConsumer(ECDsa key, OfflineRevocationState? state) =>
        new(
            new SingleRevocationKeyResolver(key.ExportSubjectPublicKeyInfo()),
            new FixedStateStore(state),
            TimeProvider.System);

    private static LicenseEnvelope CreateLicenseEnvelope(ECDsa key)
    {
        var now = DateTimeOffset.UtcNow;
        var payload = new LicensePayload(
            1, "license-0001", "install-1", "faranou-accounting",
            LicensePayload.KindPerpetual, now, now.AddMinutes(-1), null, 30, 1,
            new LicenseLimits(5, ["finance"]));
        var json = CanonicalJson.SerializePayload(payload);
        var signature = key.SignData(Encoding.UTF8.GetBytes(json), HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return new LicenseEnvelope(1, "license-key-1", LicenseAlgorithms.EcdsaP256Sha256,
            json, Convert.ToBase64String(signature));
    }

    private sealed class SingleLicenseKeyResolver(byte[] publicKey) : ITrustedKeyResolver
    {
        public bool TryResolve(string kid, out TrustedPublicKey? key)
        {
            key = kid == "license-key-1"
                ? new TrustedPublicKey(kid, LicenseAlgorithms.EcdsaP256Sha256, publicKey)
                : null;
            return key is not null;
        }
    }

    private sealed class SingleRevocationKeyResolver(byte[] publicKey) : IOfflineRevocationKeyResolver
    {
        public bool TryResolve(string keyId, out ReadOnlyMemory<byte> trustedPublicKeySpki)
        {
            trustedPublicKeySpki = keyId == "revocation-key-1"
                ? publicKey
                : ReadOnlyMemory<byte>.Empty;
            return !trustedPublicKeySpki.IsEmpty;
        }
    }

    private sealed class FixedStateStore(OfflineRevocationState? state) : IOfflineRevocationStateStore
    {
        public Task<OfflineRevocationState?> GetCurrentAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(state);

        public Task<bool> TryApplyAsync(
            OfflineRevocationList list,
            OfflineRevocationListEnvelope envelope,
            string snapshotDigest,
            CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}
