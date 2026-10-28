using System.Security.Cryptography;
using Faranou.Licensing.Contracts;
using Faranou.Vendor.Host.Signing;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class LicenseSigningProviderContractTests
{
    [Fact]
    public async Task TestProviderSignsExactCanonicalBytesAndReturnsVerifierCompatibleEnvelopeMetadata()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var payload = System.Text.Encoding.UTF8.GetBytes("{\"formatVersion\":1,\"licenseId\":\"license-1\"}");
        var provider = new TestOnlySigningProvider(key, "test-kid");

        var result = await provider.SignAsync(payload);

        Assert.True(result.Succeeded);
        Assert.Equal("test-kid", result.KeyId);
        Assert.Equal(LicenseAlgorithms.EcdsaP256Sha256, result.Algorithm);
        Assert.Null(result.ErrorCode);
        var envelope = new LicenseEnvelope(
            LicenseAlgorithms.CurrentFormatVersion,
            result.KeyId!,
            result.Algorithm!,
            System.Text.Encoding.UTF8.GetString(payload),
            Convert.ToBase64String(result.Signature.Span));
        var verifier = new EcdsaP256Sha256Verifier();
        Assert.True(verifier.TryVerify(envelope, key.ExportSubjectPublicKeyInfo(), out _));

        var altered = envelope with { PayloadJson = envelope.PayloadJson + " " };
        Assert.False(verifier.TryVerify(altered, key.ExportSubjectPublicKeyInfo(), out _));
    }

    [Fact]
    public void SuccessRejectsUnsupportedAlgorithmsAndMalformedSignatures()
    {
        Assert.Throws<ArgumentException>(() =>
            LicenseSigningResult.Success("kid", "none", new byte[64]));
        Assert.Throws<ArgumentException>(() =>
            LicenseSigningResult.Success("kid", LicenseAlgorithms.EcdsaP256Sha256, new byte[63]));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LicenseSigningResult.Success(new string('k', 129), LicenseAlgorithms.EcdsaP256Sha256, new byte[64]));
    }

    [Fact]
    public void UnavailableAndFailedResultsDoNotContainSigningMaterial()
    {
        var unavailable = LicenseSigningResult.Unavailable();
        var failed = LicenseSigningResult.Failed();

        Assert.False(unavailable.Succeeded);
        Assert.Equal("signing_provider_unavailable", unavailable.ErrorCode);
        Assert.Empty(unavailable.Signature.ToArray());
        Assert.False(failed.Succeeded);
        Assert.Equal("signing_failed", failed.ErrorCode);
        Assert.Empty(failed.Signature.ToArray());
    }

    private sealed class TestOnlySigningProvider(ECDsa key, string keyId) : ILicenseSigningProvider
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
                keyId, LicenseAlgorithms.EcdsaP256Sha256, signature));
        }
    }
}
