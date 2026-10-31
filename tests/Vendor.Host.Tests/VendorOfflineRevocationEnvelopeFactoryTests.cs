using System.Security.Cryptography;
using System.Text;
using Faranou.Licensing.Contracts;
using Faranou.Vendor.Host.Signing;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorOfflineRevocationEnvelopeFactoryTests
{
    [Fact]
    public async Task CreatesCanonicalEnvelopeAndVerifiesWithDedicatedRevocationTrustKey()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var now = DateTimeOffset.UtcNow;
        var payload = CreatePayload(now);
        const string keyId = "vendor-revocation-2026-v1";
        var resolver = new TestKeyResolver(keyId, key.ExportSubjectPublicKeyInfo());
        var factory = new VendorOfflineRevocationEnvelopeFactory(
            new TestSigner(key, keyId),
            resolver,
            new FixedTimeProvider(now));

        var result = await factory.CreateAsync(payload, minimumSequenceExclusive: 0);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Envelope);
        Assert.Equal(keyId, result.Envelope!.KeyId);
        Assert.Equal(LicenseAlgorithms.EcdsaP256Sha256, result.Envelope.Algorithm);
        Assert.True(OfflineRevocationListValidator.TryValidateAndVerify(
            result.Envelope,
            key.ExportSubjectPublicKeyInfo(),
            now,
            0,
            out var verified,
            out var errorCode), errorCode);
        Assert.Equal(payload.Sequence, verified!.Sequence);
        Assert.Equal(payload.RevokedLicenseIds, verified.RevokedLicenseIds);
        Assert.Equal(OfflineRevocationListValidator.Serialize(payload), result.Envelope.PayloadJson);
    }

    [Fact]
    public async Task RejectsSignerKeyThatIsNotInDedicatedRevocationTrustStore()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var now = DateTimeOffset.UtcNow;
        var factory = new VendorOfflineRevocationEnvelopeFactory(
            new TestSigner(key, "vendor-untrusted-2026-v1"),
            new TestKeyResolver("vendor-revocation-2026-v1", key.ExportSubjectPublicKeyInfo()),
            new FixedTimeProvider(now));

        var result = await factory.CreateAsync(CreatePayload(now), minimumSequenceExclusive: 0);

        Assert.False(result.Succeeded);
        Assert.Equal("offline_revocation_signing_key_untrusted", result.ErrorCode);
        Assert.Null(result.Envelope);
    }

    [Fact]
    public async Task RejectsSignerOutputWithInvalidSignature()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var now = DateTimeOffset.UtcNow;
        const string keyId = "vendor-revocation-2026-v1";
        var factory = new VendorOfflineRevocationEnvelopeFactory(
            new InvalidSignatureSigner(keyId),
            new TestKeyResolver(keyId, key.ExportSubjectPublicKeyInfo()),
            new FixedTimeProvider(now));

        var result = await factory.CreateAsync(CreatePayload(now), minimumSequenceExclusive: 0);

        Assert.False(result.Succeeded);
        Assert.Equal("offline_revocation_signature_invalid", result.ErrorCode);
        Assert.Null(result.Envelope);
    }

    [Fact]
    public async Task RejectsSequenceNotGreaterThanCurrentHighWaterMark()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var now = DateTimeOffset.UtcNow;
        const string keyId = "vendor-revocation-2026-v1";
        var factory = new VendorOfflineRevocationEnvelopeFactory(
            new TestSigner(key, keyId),
            new TestKeyResolver(keyId, key.ExportSubjectPublicKeyInfo()),
            new FixedTimeProvider(now));

        var result = await factory.CreateAsync(CreatePayload(now), minimumSequenceExclusive: 1);

        Assert.False(result.Succeeded);
        Assert.Equal("offline_revocation_payload_invalid", result.ErrorCode);
    }

    private static OfflineRevocationList CreatePayload(DateTimeOffset now) =>
        new(
            OfflineRevocationListValidator.CurrentSchemaVersion,
            Guid.NewGuid().ToString("D"),
            1,
            now,
            now.AddDays(30),
            new[]
            {
                "00000000-0000-0000-0000-000000000001",
                "00000000-0000-0000-0000-000000000002"
            });

    private sealed class TestSigner(ECDsa key, string keyId) : IOfflineRevocationSigningProvider
    {
        public ValueTask<LicenseSigningResult> SignAsync(
            ReadOnlyMemory<byte> canonicalPayload,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var signature = key.SignData(
                canonicalPayload.Span,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            return ValueTask.FromResult(LicenseSigningResult.Success(
                keyId,
                LicenseAlgorithms.EcdsaP256Sha256,
                signature));
        }
    }

    private sealed class InvalidSignatureSigner(string keyId) : IOfflineRevocationSigningProvider
    {
        public ValueTask<LicenseSigningResult> SignAsync(
            ReadOnlyMemory<byte> canonicalPayload,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(LicenseSigningResult.Success(
                keyId,
                LicenseAlgorithms.EcdsaP256Sha256,
                new byte[64]));
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

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
