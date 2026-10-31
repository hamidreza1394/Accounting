using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Faranou.Licensing.Contracts;

/// <summary>
/// Signed operator consent emitted only by the explicit Software KSP key-provisioning ceremony.
/// The signature proves the consent statement is tied to the installation proof key.
/// </summary>
public sealed record InstallationKeyConsentReceipt(
    int FormatVersion,
    string InstallationId,
    string KeyId,
    string ProviderName,
    string AssuranceLevel,
    DateTimeOffset AcceptedAtUtc,
    string AcceptedBy,
    string SignatureBase64);

public static class InstallationKeyConsentReceiptVerifier
{
    public const string SoftwareProviderName = "Microsoft Software Key Storage Provider";
    public const string SoftwareAssuranceLevel = "SoftwareProtected";
    private const string DomainSeparator = "FARANOU-INSTALLATION-KEY-SOFTWARE-CONSENT";
    private const int MaximumClockSkewMinutes = 5;

    public static byte[] GetSigningBytes(InstallationKeyConsentReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var canonical = string.Join("\n",
            DomainSeparator,
            receipt.FormatVersion.ToString(CultureInfo.InvariantCulture),
            receipt.InstallationId,
            receipt.KeyId,
            receipt.ProviderName,
            receipt.AssuranceLevel,
            receipt.AcceptedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            receipt.AcceptedBy);
        return Encoding.UTF8.GetBytes(canonical);
    }

    public static bool TryVerify(
        InstallationKeyConsentReceipt receipt,
        ReadOnlySpan<byte> publicKeySubjectPublicKeyInfo,
        string expectedInstallationId,
        string expectedKeyId,
        string expectedPublicKeyFingerprint,
        DateTimeOffset nowUtc,
        out string? errorCode)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        errorCode = null;

        if (receipt.FormatVersion != 1 ||
            !string.Equals(receipt.InstallationId, expectedInstallationId, StringComparison.Ordinal) ||
            !string.Equals(receipt.KeyId, expectedKeyId, StringComparison.Ordinal) ||
            !string.Equals(receipt.ProviderName, SoftwareProviderName, StringComparison.Ordinal) ||
            !string.Equals(receipt.AssuranceLevel, SoftwareAssuranceLevel, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(receipt.AcceptedBy) ||
            receipt.AcceptedBy.Length > 256 ||
            receipt.AcceptedAtUtc == default ||
            receipt.AcceptedAtUtc > nowUtc.ToUniversalTime().AddMinutes(MaximumClockSkewMinutes))
        {
            errorCode = "software_consent_receipt_invalid";
            return false;
        }

        if (publicKeySubjectPublicKeyInfo.IsEmpty || publicKeySubjectPublicKeyInfo.Length > 1024)
        {
            errorCode = "installation_public_key_invalid";
            return false;
        }

        var fingerprint = Convert.ToHexString(SHA256.HashData(publicKeySubjectPublicKeyInfo)).ToLowerInvariant();
        if (!string.Equals(fingerprint, expectedPublicKeyFingerprint, StringComparison.Ordinal))
        {
            errorCode = "installation_public_key_fingerprint_mismatch";
            return false;
        }

        byte[] signature;
        try
        {
            signature = Convert.FromBase64String(receipt.SignatureBase64);
        }
        catch (FormatException)
        {
            errorCode = "software_consent_signature_invalid";
            return false;
        }

        if (signature.Length != 64)
        {
            errorCode = "software_consent_signature_invalid";
            return false;
        }

        try
        {
            using var publicKey = ECDsa.Create();
            publicKey.ImportSubjectPublicKeyInfo(publicKeySubjectPublicKeyInfo, out var bytesRead);
            if (bytesRead != publicKeySubjectPublicKeyInfo.Length || publicKey.KeySize != 256)
            {
                errorCode = "installation_public_key_invalid";
                return false;
            }

            if (!publicKey.VerifyData(
                GetSigningBytes(receipt),
                signature,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
            {
                errorCode = "software_consent_signature_invalid";
                return false;
            }
        }
        catch (CryptographicException)
        {
            errorCode = "installation_public_key_invalid";
            return false;
        }

        return true;
    }
}
