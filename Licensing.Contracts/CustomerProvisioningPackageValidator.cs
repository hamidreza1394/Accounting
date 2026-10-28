using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Faranou.Licensing.Contracts;

/// <summary>
/// Validates and verifies a provisioning package using a caller-resolved trusted public key.
/// Replay protection still requires the consumer to persist and atomically claim each nonce.
/// </summary>
public static class CustomerProvisioningPackageValidator
{
    public const int CurrentFormatVersion = 1;
    public const int MaxPayloadJsonChars = 64 * 1024;
    public const int MaxPackageLifetimeHours = 24;
    public const int ClockSkewMinutes = 2;

    private static readonly HashSet<string> CompanyTypes =
        new(StringComparer.Ordinal) { "independent", "parent", "subsidiary" };

    private static readonly HashSet<string> SubsystemCodes =
        new(StringComparer.Ordinal) { "finance", "sales", "inventory", "treasury" };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false
    };

    public static bool TryValidateAndVerify(
        CustomerProvisioningPackage? package,
        ReadOnlySpan<byte> trustedPublicKeySubjectPublicKeyInfo,
        DateTimeOffset nowUtc,
        out CustomerProvisioningPayload? payload,
        out string errorCode)
    {
        payload = null;
        errorCode = "provisioning_package_invalid";

        if (package is null ||
            package.FormatVersion != CurrentFormatVersion ||
            string.IsNullOrWhiteSpace(package.KeyId) || package.KeyId.Length > 128 ||
            !string.Equals(package.Algorithm, LicenseAlgorithms.EcdsaP256Sha256, StringComparison.Ordinal) ||
            string.IsNullOrEmpty(package.PayloadJson) || package.PayloadJson.Length > MaxPayloadJsonChars ||
            string.IsNullOrEmpty(package.SignatureBase64))
        {
            errorCode = "provisioning_envelope_invalid";
            return false;
        }

        byte[] signature;
        try
        {
            signature = Convert.FromBase64String(package.SignatureBase64);
        }
        catch (FormatException)
        {
            errorCode = "provisioning_signature_invalid";
            return false;
        }

        if (signature.Length != 64 || trustedPublicKeySubjectPublicKeyInfo.IsEmpty ||
            trustedPublicKeySubjectPublicKeyInfo.Length > 1024)
        {
            errorCode = "provisioning_signature_invalid";
            return false;
        }

        try
        {
            payload = JsonSerializer.Deserialize<CustomerProvisioningPayload>(package.PayloadJson, JsonOptions);
        }
        catch (JsonException)
        {
            errorCode = "provisioning_payload_invalid";
            return false;
        }

        if (payload is null || !HasValidPayload(payload, nowUtc))
        {
            payload = null;
            errorCode = "provisioning_payload_invalid";
            return false;
        }

        string canonicalJson;
        try
        {
            canonicalJson = CustomerProvisioningCanonicalJson.Serialize(payload);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            payload = null;
            errorCode = "provisioning_payload_invalid";
            return false;
        }

        if (!string.Equals(canonicalJson, package.PayloadJson, StringComparison.Ordinal))
        {
            payload = null;
            errorCode = "provisioning_payload_noncanonical";
            return false;
        }

        try
        {
            using var publicKey = ECDsa.Create();
            publicKey.ImportSubjectPublicKeyInfo(trustedPublicKeySubjectPublicKeyInfo, out var bytesRead);
            if (bytesRead != trustedPublicKeySubjectPublicKeyInfo.Length || publicKey.KeySize != 256 ||
                !publicKey.VerifyData(
                    Encoding.UTF8.GetBytes(package.PayloadJson),
                    signature,
                    HashAlgorithmName.SHA256,
                    DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
            {
                payload = null;
                errorCode = "provisioning_signature_invalid";
                return false;
            }
        }
        catch (CryptographicException)
        {
            payload = null;
            errorCode = "provisioning_trusted_key_invalid";
            return false;
        }

        errorCode = string.Empty;
        return true;
    }

    private static bool HasValidPayload(CustomerProvisioningPayload payload, DateTimeOffset nowUtc)
    {
        if (payload.SchemaVersion != CurrentFormatVersion ||
            !Guid.TryParseExact(payload.RequestId, "D", out _) ||
            !IsBoundedText(payload.CustomerReference, 1, 128) ||
            !IsBoundedText(payload.InstallationId, 1, 128) ||
            !IsBoundedText(payload.CompanyName, 1, 200) ||
            !CompanyTypes.Contains(payload.CompanyType) ||
            !IsBoundedText(payload.InitialAdminUserName, 3, 64) ||
            payload.InitialAdminUserName.Any(char.IsControl) ||
            payload.Nonce is null || payload.Nonce.Length is < 32 or > 128 ||
            payload.Nonce.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_')) ||
            payload.Subsystems is null || payload.Subsystems.Count is < 1 or > 4 ||
            payload.Subsystems.Any(code => code is null || !SubsystemCodes.Contains(code)) ||
            payload.Subsystems.Distinct(StringComparer.Ordinal).Count() != payload.Subsystems.Count ||
            payload.IssuedAtUtc.Offset != TimeSpan.Zero ||
            payload.ExpiresAtUtc.Offset != TimeSpan.Zero ||
            payload.IssuedAtUtc > nowUtc.ToUniversalTime().AddMinutes(ClockSkewMinutes) ||
            payload.ExpiresAtUtc <= nowUtc.ToUniversalTime() ||
            payload.ExpiresAtUtc <= payload.IssuedAtUtc ||
            payload.ExpiresAtUtc > payload.IssuedAtUtc.AddHours(MaxPackageLifetimeHours))
        {
            return false;
        }

        return true;
    }

    private static bool IsBoundedText(string? value, int minLength, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length >= minLength &&
        value.Length <= maxLength &&
        !value.Any(char.IsControl);
}
