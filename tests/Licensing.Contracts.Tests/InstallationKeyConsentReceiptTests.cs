using System.Security.Cryptography;
using Faranou.Licensing.Contracts;
using Xunit;

namespace Licensing.Contracts.Tests;

public sealed class InstallationKeyConsentReceiptTests
{
    [Fact]
    public void ValidSoftwareConsentReceipt_VerifiesAgainstInstallationPublicKey()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = key.ExportSubjectPublicKeyInfo();
        var fingerprint = Convert.ToHexString(SHA256.HashData(publicKey)).ToLowerInvariant();
        var receipt = CreateReceipt(key);

        Assert.True(InstallationKeyConsentReceiptVerifier.TryVerify(
            receipt, publicKey, "inst-001", "key-001", fingerprint,
            receipt.AcceptedAtUtc, out var errorCode), errorCode);
    }

    [Fact]
    public void TamperedConsentOrWrongInstallationKey_FailsVerification()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = key.ExportSubjectPublicKeyInfo();
        var fingerprint = Convert.ToHexString(SHA256.HashData(publicKey)).ToLowerInvariant();
        var receipt = CreateReceipt(key, fingerprint);

        var tampered = receipt with { AcceptedBy = "OTHER\\admin" };
        Assert.False(InstallationKeyConsentReceiptVerifier.TryVerify(
            tampered, publicKey, "inst-001", "key-001", fingerprint,
            receipt.AcceptedAtUtc, out var tamperedError));
        Assert.Equal("software_consent_signature_invalid", tamperedError);

        Assert.False(InstallationKeyConsentReceiptVerifier.TryVerify(
            receipt, publicKey, "other-install", "key-001", fingerprint,
            receipt.AcceptedAtUtc, out var installationError));
        Assert.Equal("software_consent_receipt_invalid", installationError);
    }

    [Fact]
    public void FutureConsentReceipt_FailsClosed()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = key.ExportSubjectPublicKeyInfo();
        var fingerprint = Convert.ToHexString(SHA256.HashData(publicKey)).ToLowerInvariant();
        var receipt = CreateReceipt(key, fingerprint);
        var now = receipt.AcceptedAtUtc.AddMinutes(-10);

        Assert.False(InstallationKeyConsentReceiptVerifier.TryVerify(
            receipt, publicKey, "inst-001", "key-001", fingerprint, now, out var errorCode));
        Assert.Equal("software_consent_receipt_invalid", errorCode);
    }

    private static InstallationKeyConsentReceipt CreateReceipt(ECDsa key)
    {
        var unsigned = new InstallationKeyConsentReceipt(
            1,
            "inst-001",
            "key-001",
            InstallationKeyConsentReceiptVerifier.SoftwareProviderName,
            InstallationKeyConsentReceiptVerifier.SoftwareAssuranceLevel,
            DateTimeOffset.UtcNow,
            "CUSTOMER\\admin",
            "");
        var signature = key.SignData(
            InstallationKeyConsentReceiptVerifier.GetSigningBytes(unsigned),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return unsigned with { SignatureBase64 = Convert.ToBase64String(signature) };
    }
}
