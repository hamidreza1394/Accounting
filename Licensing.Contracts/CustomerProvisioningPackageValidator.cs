using System.Globalization;
using System.Net.Mail;
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
    public const int CurrentPayloadSchemaVersion = 3;
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

        // Authenticate the exact bounded UTF-8 bytes before deserializing attacker-controlled JSON.
        // Schema/canonicalization checks run only after the signature has been verified.
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
                errorCode = "provisioning_signature_invalid";
                return false;
            }
        }
        catch (CryptographicException)
        {
            errorCode = "provisioning_trusted_key_invalid";
            return false;
        }

        if (!TryValidatePayload(package.PayloadJson, nowUtc, out payload, out errorCode))
            return false;

        errorCode = string.Empty;
        return true;
    }

    /// <summary>
    /// Validates the canonical payload independently of a signature. Issuers must apply the same
    /// schema, allow-list, length, nonce, and expiry checks before requesting a signature.
    /// </summary>
    public static bool TryValidatePayload(
        string? payloadJson,
        DateTimeOffset nowUtc,
        out CustomerProvisioningPayload? payload,
        out string errorCode)
    {
        payload = null;
        errorCode = "provisioning_payload_invalid";

        if (string.IsNullOrEmpty(payloadJson) || payloadJson.Length > MaxPayloadJsonChars)
            return false;

        try
        {
            payload = JsonSerializer.Deserialize<CustomerProvisioningPayload>(payloadJson, JsonOptions);
        }
        catch (JsonException)
        {
            return false;
        }

        if (payload is null || !HasValidPayload(payload, nowUtc))
        {
            payload = null;
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
            return false;
        }

        if (!string.Equals(canonicalJson, payloadJson, StringComparison.Ordinal))
        {
            payload = null;
            errorCode = "provisioning_payload_noncanonical";
            return false;
        }

        errorCode = string.Empty;
        return true;
    }

    private static bool HasValidPayload(CustomerProvisioningPayload payload, DateTimeOffset nowUtc)
    {
        var isV1 = payload.SchemaVersion == 1;
        var isV2 = payload.SchemaVersion == 2;
        var isV3 = payload.SchemaVersion == CurrentPayloadSchemaVersion;

        if ((!isV1 && !isV2 && !isV3) ||
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

        if (isV1)
        {
            // v1 did not carry period or parent-company metadata.
            return payload.FiscalPeriodTitle is null &&
                payload.FiscalPeriodStartDate is null &&
                payload.FiscalPeriodEndDate is null &&
                payload.ParentCompanyReference is null &&
                payload.InitialAdminEmail is null;
        }

        if (!IsBoundedText(payload.FiscalPeriodTitle, 1, 100) ||
            payload.FiscalPeriodStartDate is null ||
            payload.FiscalPeriodEndDate is null ||
            payload.FiscalPeriodStartDate.Value >= payload.FiscalPeriodEndDate.Value)
        {
            return false;
        }

        if (isV2 && payload.InitialAdminEmail is not null)
            return false;

        if (isV3 && !IsValidEmail(payload.InitialAdminEmail))
            return false;

        var hasParentReference = IsVendorIssuedRequestReference(payload.ParentCompanyReference);
        return payload.CompanyType switch
        {
            "subsidiary" => hasParentReference,
            "independent" or "parent" => payload.ParentCompanyReference is null,
            _ => false
        };
    }

    private static bool IsVendorIssuedRequestReference(string? value) =>
        Guid.TryParseExact(value, "D", out var requestId) &&
        requestId != Guid.Empty &&
        string.Equals(value, requestId.ToString("D"), StringComparison.OrdinalIgnoreCase);

    private static bool IsValidEmail(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 254 &&
        !value.Any(char.IsControl) &&
        MailAddress.TryCreate(value, out var parsed) &&
        string.Equals(parsed.Address, value, StringComparison.OrdinalIgnoreCase);

    private static bool IsBoundedText(string? value, int minLength, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length >= minLength &&
        value.Length <= maxLength &&
        !value.Any(char.IsControl);
}
