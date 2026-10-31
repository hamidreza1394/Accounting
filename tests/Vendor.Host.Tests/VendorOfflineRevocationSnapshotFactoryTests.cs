using System.Security.Cryptography;
using Faranou.Licensing.Contracts;
using Faranou.Vendor.Host.Signing;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorOfflineRevocationSnapshotFactoryTests
{
    [Fact]
    public async Task CreateAsync_SignsCanonicalSortedPayload_AndProducesVerifierCompatibleEnvelope()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var now = new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
        var factory = new VendorOfflineRevocationSnapshotFactory(
            new EcdsaTestSigner(key),
            new FixedTimeProvider(now));

        var result = await factory.CreateAsync(
            7,
            ["license-z", "license-a", "license-z"],
            TimeSpan.FromDays(7));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Envelope);
        Assert.Equal("revocation-test-v1", result.Envelope.KeyId);
        Assert.Equal(LicenseAlgorithms.EcdsaP256Sha256, result.Envelope.Algorithm);
        Assert.True(OfflineRevocationListValidator.TryValidateAndVerify(
            result.Envelope,
            key.ExportSubjectPublicKeyInfo(),
            now,
            6,
            out var payload,
            out var errorCode), errorCode);
        Assert.NotNull(payload);
        Assert.Equal(7, payload.Sequence);
        Assert.Equal(new[] { "license-a", "license-z" }, payload.RevokedLicenseIds);
        Assert.Equal(now, payload.IssuedAtUtc);
        Assert.Equal(now.AddDays(7), payload.ExpiresAtUtc);
    }

    [Fact]
    public async Task CreateAsync_WhenSignerUnavailable_FailsClosedWithoutEnvelope()
    {
        var factory = new VendorOfflineRevocationSnapshotFactory(
            new UnavailableOfflineRevocationSigningProvider(),
            new FixedTimeProvider(DateTimeOffset.UtcNow));

        var result = await factory.CreateAsync(1, ["license-1"], TimeSpan.FromDays(1));

        Assert.False(result.Succeeded);
        Assert.Null(result.Envelope);
        Assert.Equal("offline_revocation_signing_unavailable", result.ErrorCode);
    }

    [Theory]
    [InlineData(0, "offline_revocation_sequence_invalid")]
    [InlineData(-1, "offline_revocation_sequence_invalid")]
    public async Task CreateAsync_RejectsNonPositiveSequence(long sequence, string expectedError)
    {
        var factory = CreateFactory(new UnavailableOfflineRevocationSigningProvider());

        var result = await factory.CreateAsync(sequence, [], TimeSpan.FromDays(1));

        Assert.False(result.Succeeded);
        Assert.Equal(expectedError, result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_RejectsInvalidLicenseIdentifiersBeforeSigning()
    {
        var signer = new RecordingUnavailableSigner();
        var factory = CreateFactory(signer);

        var result = await factory.CreateAsync(1, ["valid", "bad\nvalue"], TimeSpan.FromDays(1));

        Assert.False(result.Succeeded);
        Assert.Equal("offline_revocation_license_id_invalid", result.ErrorCode);
        Assert.Equal(0, signer.CallCount);
    }

    [Fact]
    public async Task CreateAsync_RejectsValidityOutsideContractBounds()
    {
        var signer = new RecordingUnavailableSigner();
        var factory = CreateFactory(signer);

        var result = await factory.CreateAsync(
            1,
            ["license-1"],
            TimeSpan.FromDays(OfflineRevocationListValidator.MaxValidityDays + 1));

        Assert.False(result.Succeeded);
        Assert.Equal("offline_revocation_validity_invalid", result.ErrorCode);
        Assert.Equal(0, signer.CallCount);
    }

    [Fact]
    public async Task CreateAsync_PropagatesCallerCancellation()
    {
        var factory = CreateFactory(new UnavailableOfflineRevocationSigningProvider());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await factory.CreateAsync(1, ["license-1"], TimeSpan.FromDays(1), cancellation.Token));
    }

    private static VendorOfflineRevocationSnapshotFactory CreateFactory(
        IOfflineRevocationSigningProvider signer) =>
        new(signer, new FixedTimeProvider(DateTimeOffset.UtcNow));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class EcdsaTestSigner(ECDsa key) : IOfflineRevocationSigningProvider
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
                "revocation-test-v1",
                LicenseAlgorithms.EcdsaP256Sha256,
                signature));
        }
    }

    private sealed class RecordingUnavailableSigner : IOfflineRevocationSigningProvider
    {
        public int CallCount { get; private set; }

        public ValueTask<LicenseSigningResult> SignAsync(
            ReadOnlyMemory<byte> canonicalPayload,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(LicenseSigningResult.Unavailable());
        }
    }
}
