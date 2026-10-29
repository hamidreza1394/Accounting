using System.Security.Cryptography;
using System.Text;
using Faranou.Licensing.Contracts;
using Xunit;

namespace Licensing.Contracts.Tests;

public sealed class OfflineRevocationListValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ValidSignedList_VerifiesExactPayloadAndSequence()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var envelope = Sign(key, Sample());

        var valid = OfflineRevocationListValidator.TryValidateAndVerify(
            envelope, key.ExportSubjectPublicKeyInfo(), Now, minimumSequenceExclusive: 7,
            out var list, out var error);

        Assert.True(valid, error);
        Assert.NotNull(list);
        Assert.Equal(8L, list!.Sequence);
        Assert.Equal(["license-a", "license-z"], list.RevokedLicenseIds);
    }

    [Fact]
    public void TamperedPayload_IsRejectedBeforeParsing()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var envelope = Sign(key, Sample()) with { PayloadJson = "{not-json" };

        Assert.False(OfflineRevocationListValidator.TryValidateAndVerify(
            envelope, key.ExportSubjectPublicKeyInfo(), Now, 7, out var list, out var error));
        Assert.Null(list);
        Assert.Equal("offline_revocation_signature_invalid", error);
    }

    [Fact]
    public void ExpiredList_IsRejected()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var envelope = Sign(key, Sample() with { ExpiresAtUtc = Now.AddMinutes(-1) });

        Assert.False(OfflineRevocationListValidator.TryValidateAndVerify(
            envelope, key.ExportSubjectPublicKeyInfo(), Now, 7, out var list, out var error));
        Assert.Null(list);
        Assert.Equal("offline_revocation_payload_invalid", error);
    }

    [Fact]
    public void ReplayOrRollbackSequence_IsRejected()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var envelope = Sign(key, Sample() with { Sequence = 7 });

        Assert.False(OfflineRevocationListValidator.TryValidateAndVerify(
            envelope, key.ExportSubjectPublicKeyInfo(), Now, 7, out var list, out var error));
        Assert.Null(list);
        Assert.Equal("offline_revocation_sequence_invalid", error);
    }

    [Fact]
    public void DuplicateOrUnsortedLicenseIds_AreRejected()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var duplicate = Sign(key, Sample() with { RevokedLicenseIds = ["license-a", "license-a"] });
        Assert.False(OfflineRevocationListValidator.TryValidateAndVerify(
            duplicate, key.ExportSubjectPublicKeyInfo(), Now, 7, out _, out var duplicateError));
        Assert.Equal("offline_revocation_payload_invalid", duplicateError);

        var unsorted = Sign(key, Sample() with { RevokedLicenseIds = ["license-z", "license-a"] });
        Assert.False(OfflineRevocationListValidator.TryValidateAndVerify(
            unsorted, key.ExportSubjectPublicKeyInfo(), Now, 7, out _, out var unsortedError));
        Assert.Equal("offline_revocation_payload_invalid", unsortedError);
    }

    [Fact]
    public void MissingTrustOrWrongCurve_FailsClosed()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var envelope = Sign(key, Sample());
        Assert.False(OfflineRevocationListValidator.TryValidateAndVerify(
            envelope, ReadOnlySpan<byte>.Empty, Now, 7, out _, out var error));
        Assert.Equal("offline_revocation_envelope_invalid", error);

        using var wrongCurve = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        Assert.False(OfflineRevocationListValidator.TryValidateAndVerify(
            envelope, wrongCurve.ExportSubjectPublicKeyInfo(), Now, 7, out _, out var invalidKeyError));
        Assert.Equal("offline_revocation_trusted_key_invalid", invalidKeyError);
    }

    private static OfflineRevocationListEnvelope Sign(ECDsa key, OfflineRevocationList payload)
    {
        var json = OfflineRevocationListValidator.Serialize(payload);
        var signature = key.SignData(Encoding.UTF8.GetBytes(json), HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return new OfflineRevocationListEnvelope(
            1, "offline-revocation-key-1", LicenseAlgorithms.EcdsaP256Sha256,
            json, Convert.ToBase64String(signature));
    }

    private static OfflineRevocationList Sample() => new(
        1, "revocation-list-2026-10", 8, Now.AddMinutes(-10), Now.AddDays(7),
        ["license-a", "license-z"]);
}
