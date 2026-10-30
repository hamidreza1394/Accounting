using System.Security.Cryptography;
using System.Text;
using Faranou.Licensing.Contracts;
using Xunit;

namespace Licensing.Contracts.Tests;

public sealed class RevocationAwareLicenseEnvelopeValidatorTests
{
    [Fact]
    public async Task ValidateAsync_FailsClosedWhenNoCurrentRevocationSnapshotExists()
    {
        using var licenseKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var revocationKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var validator = CreateValidator(licenseKey);
        var consumer = CreateConsumer(revocationKey, state: null);
        var pipeline = new RevocationAwareLicenseEnvelopeValidator(validator, consumer);

        var result = await pipeline.ValidateAsync(CreateLicenseEnvelope(licenseKey), "install-1");

        Assert.False(result.Success);
        Assert.Equal("offline_revocation_unavailable", result.ErrorCode);
    }

    [Fact]
    public async Task ValidateAsync_AllowsValidLicenseWhenCurrentSnapshotDoesNotRevokeIt()
    {
        using var licenseKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var revocationKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var now = DateTimeOffset.UtcNow;
        var state = new OfflineRevocationState(
            4,
            new string('a', 64),
            now.AddDays(1),
            new HashSet<string>(["some-other-license"], StringComparer.Ordinal));
        var pipeline = new RevocationAwareLicenseEnvelopeValidator(
            CreateValidator(licenseKey),
            CreateConsumer(revocationKey, state));

        var result = await pipeline.ValidateAsync(CreateLicenseEnvelope(licenseKey), "install-1");

        Assert.True(result.Success, result.ErrorCode);
        Assert.Equal("license-0001", result.Payload?.LicenseId);
    }

    [Fact]
    public async Task ValidateAsync_RejectsLicensePresentInCurrentRevocationSnapshot()
    {
        using var licenseKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var revocationKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var now = DateTimeOffset.UtcNow;
        var state = new OfflineRevocationState(
            5,
            new string('b', 64),
            now.AddDays(1),
            new HashSet<string>(["license-0001"], StringComparer.Ordinal));
        var pipeline = new RevocationAwareLicenseEnvelopeValidator(
            CreateValidator(licenseKey),
            CreateConsumer(revocationKey, state));

        var result = await pipeline.ValidateAsync(CreateLicenseEnvelope(licenseKey), "install-1");

        Assert.False(result.Success);
        Assert.Equal("license_revoked", result.ErrorCode);
    }

    private static LicenseEnvelopeValidator CreateValidator(ECDsa key)
    {
        var resolver = new SingleLicenseKeyResolver(key.ExportSubjectPublicKeyInfo());
        return new LicenseEnvelopeValidator(
            resolver,
            new EcdsaP256Sha256Verifier(),
            new LicenseEvaluator(TimeProvider.System));
    }

    private static OfflineRevocationConsumer CreateConsumer(ECDsa key, OfflineRevocationState? state)
    {
        return new OfflineRevocationConsumer(
            new SingleRevocationKeyResolver(key.ExportSubjectPublicKeyInfo()),
            new FixedStateStore(state),
            TimeProvider.System);
    }

    private static LicenseEnvelope CreateLicenseEnvelope(ECDsa key)
    {
        var now = DateTimeOffset.UtcNow;
        var payload = new LicensePayload(
            1,
            "license-0001",
            "install-1",
            "faranou-accounting",
            LicensePayload.KindPerpetual,
            now,
            now.AddMinutes(-1),
            null,
            30,
            1,
            new LicenseLimits(5, ["finance"]));
        var json = CanonicalJson.SerializePayload(payload);
        var signature = key.SignData(
            Encoding.UTF8.GetBytes(json),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return new LicenseEnvelope(
            1,
            "license-key-1",
            LicenseAlgorithms.EcdsaP256Sha256,
            json,
            Convert.ToBase64String(signature));
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
            string snapshotDigest,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }
}
