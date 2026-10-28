using System.Security.Cryptography;
using Faranou.Licensing.Contracts;
using Faranou.Vendor.Host.Signing;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorLicenseEnvelopeFactoryTests
{
    [Fact]
    public async Task CreatesVerifierCompatibleEnvelopeFromCanonicalPayload()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var provider = new TestSigningProvider(key);
        var factory = new VendorLicenseEnvelopeFactory(provider);
        var payload = ValidPayload();

        var result = await factory.CreateAsync(payload);

        Assert.True(result.Succeeded);
        Assert.Null(result.ErrorCode);
        Assert.NotNull(result.Envelope);
        Assert.Equal(CanonicalJson.SerializePayload(payload), result.Envelope.PayloadJson);
        Assert.Empty(StructuralValidation.ValidateEnvelopeStructure(result.Envelope));
        Assert.True(new EcdsaP256Sha256Verifier().TryVerify(
            result.Envelope, key.ExportSubjectPublicKeyInfo(), out _));
    }

    [Fact]
    public async Task DoesNotCreateEnvelopeWhenProviderIsUnavailable()
    {
        var factory = new VendorLicenseEnvelopeFactory(new UnavailableSigningProvider());

        var result = await factory.CreateAsync(ValidPayload());

        Assert.False(result.Succeeded);
        Assert.Null(result.Envelope);
        Assert.Equal("signing_provider_unavailable", result.ErrorCode);
    }

    [Fact]
    public async Task RejectsInvalidPayloadBeforeCallingProvider()
    {
        var provider = new UnavailableSigningProvider();
        var factory = new VendorLicenseEnvelopeFactory(provider);
        var payload = ValidPayload() with { Sequence = 0 };

        var result = await factory.CreateAsync(payload);

        Assert.False(result.Succeeded);
        Assert.Null(result.Envelope);
        Assert.Equal("license_payload_invalid", result.ErrorCode);
        Assert.False(provider.Called);
    }

    private static LicensePayload ValidPayload() => new(
        FormatVersion: LicenseAlgorithms.CurrentFormatVersion,
        LicenseId: "license-123",
        InstallationId: "install-123",
        ProductCode: "FARANOU",
        LicenseKind: LicensePayload.KindPerpetual,
        IssuedAtUtc: new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.Zero),
        NotBeforeUtc: new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.Zero),
        ExpiresAtUtc: null,
        GraceDays: 30,
        Sequence: 1,
        Limits: new LicenseLimits(null, new[] { "finance", "sales" }));

    private sealed class TestSigningProvider(ECDsa key) : ILicenseSigningProvider
    {
        public ValueTask<LicenseSigningResult> SignAsync(
            ReadOnlyMemory<byte> canonicalPayload,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var signature = key.SignData(canonicalPayload.Span, HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            return ValueTask.FromResult(LicenseSigningResult.Success(
                "test-kid", LicenseAlgorithms.EcdsaP256Sha256, signature));
        }
    }

    private sealed class UnavailableSigningProvider : ILicenseSigningProvider
    {
        public bool Called { get; private set; }

        public ValueTask<LicenseSigningResult> SignAsync(
            ReadOnlyMemory<byte> canonicalPayload,
            CancellationToken cancellationToken = default)
        {
            Called = true;
            return ValueTask.FromResult(LicenseSigningResult.Unavailable());
        }
    }
}
