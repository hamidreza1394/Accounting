using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Faranou.Licensing.Contracts;

/// <summary>
/// Signed outcome returned by a trusted customer-side provisioning consumer.
/// It contains identifiers and an outcome only, never credentials or customer business data.
/// </summary>
public sealed record CustomerProvisioningAcknowledgement(
    int SchemaVersion,
    string RequestId,
    string Nonce,
    string PayloadSha256,
    string CustomerReference,
    string InstallationId,
    string Result,
    DateTimeOffset ProcessedAtUtc,
    string? ErrorCode);

/// <summary>
/// Signed acknowledgement envelope. KeyId is a lookup hint only; the caller must resolve it
/// against an independently configured trusted acknowledgement-key set.
/// </summary>
public sealed record CustomerProvisioningAcknowledgementEnvelope(
    int FormatVersion,
    string KeyId,
    string Algorithm,
    string AcknowledgementJson,
    string SignatureBase64);

/// <summary>Canonical v1 representation and strict verifier for signed provisioning acknowledgements.</summary>
public static class CustomerProvisioningAcknowledgementValidator
{
    public const int CurrentSchemaVersion = 1;
    public const int MaxAcknowledgementJsonChars = 16 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false
    };

    public static string Serialize(CustomerProvisioningAcknowledgement acknowledgement)
    {
        ArgumentNullException.ThrowIfNull(acknowledgement);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", acknowledgement.SchemaVersion);
            writer.WriteString("requestId", acknowledgement.RequestId);
            writer.WriteString("nonce", acknowledgement.Nonce);
            writer.WriteString("payloadSha256", acknowledgement.PayloadSha256);
            writer.WriteString("customerReference", acknowledgement.CustomerReference);
            writer.WriteString("installationId", acknowledgement.InstallationId);
            writer.WriteString("result", acknowledgement.Result);
            writer.WriteString("processedAtUtc", acknowledgement.ProcessedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            if (acknowledgement.ErrorCode is null)
                writer.WriteNull("errorCode");
            else
                writer.WriteString("errorCode", acknowledgement.ErrorCode);
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// Verifies the exact bounded UTF-8 bytes before JSON parsing, then validates canonical form
    /// and every binding against the original Vendor request. The public key must come from a
    /// trust store independent of this envelope.
    /// </summary>
    public static bool TryValidateAndVerify(
        CustomerProvisioningAcknowledgementEnvelope? envelope,
        ReadOnlySpan<byte> trustedPublicKeySubjectPublicKeyInfo,
        string expectedRequestId,
        string expectedNonce,
        string expectedPayloadSha256,
        string expectedCustomerReference,
        string expectedInstallationId,
        DateTimeOffset nowUtc,
        out CustomerProvisioningAcknowledgement? acknowledgement,
        out string errorCode)
    {
        acknowledgement = null;
        errorCode = "provisioning_acknowledgement_invalid";

        if (envelope is null ||
            envelope.FormatVersion != CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(envelope.KeyId) || envelope.KeyId.Length > 128 ||
            !LicenseAlgorithms.IsAllowed(envelope.Algorithm) ||
            string.IsNullOrEmpty(envelope.AcknowledgementJson) ||
            envelope.AcknowledgementJson.Length > MaxAcknowledgementJsonChars ||
            string.IsNullOrEmpty(envelope.SignatureBase64) ||
            trustedPublicKeySubjectPublicKeyInfo.IsEmpty ||
            trustedPublicKeySubjectPublicKeyInfo.Length > 1024)
        {
            errorCode = "provisioning_acknowledgement_envelope_invalid";
            return false;
        }

        byte[] signature;
        try
        {
            signature = Convert.FromBase64String(envelope.SignatureBase64);
        }
        catch (FormatException)
        {
            errorCode = "provisioning_acknowledgement_signature_invalid";
            return false;
        }

        if (signature.Length != 64)
        {
            errorCode = "provisioning_acknowledgement_signature_invalid";
            return false;
        }

        try
        {
            using var publicKey = ECDsa.Create();
            publicKey.ImportSubjectPublicKeyInfo(trustedPublicKeySubjectPublicKeyInfo, out var bytesRead);
            if (bytesRead != trustedPublicKeySubjectPublicKeyInfo.Length ||
                !publicKey.VerifyData(
                    Encoding.UTF8.GetBytes(envelope.AcknowledgementJson),
                    signature,
                    HashAlgorithmName.SHA256,
                    DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
            {
                errorCode = "provisioning_acknowledgement_signature_invalid";
                return false;
            }
        }
        catch (CryptographicException)
        {
            errorCode = "provisioning_acknowledgement_trusted_key_invalid";
            return false;
        }

        try
        {
            acknowledgement = JsonSerializer.Deserialize<CustomerProvisioningAcknowledgement>(
                envelope.AcknowledgementJson, JsonOptions);
        }
        catch (JsonException)
        {
            errorCode = "provisioning_acknowledgement_payload_invalid";
            return false;
        }

        if (acknowledgement is null ||
            !string.Equals(Serialize(acknowledgement), envelope.AcknowledgementJson, StringComparison.Ordinal) ||
            acknowledgement.SchemaVersion != CurrentSchemaVersion ||
            !HasBoundedValue(acknowledgement.RequestId, 128) ||
            !HasBoundedValue(acknowledgement.Nonce, 256) ||
            !IsSha256Hex(acknowledgement.PayloadSha256) ||
            !HasBoundedValue(acknowledgement.CustomerReference, 128) ||
            !HasBoundedValue(acknowledgement.InstallationId, 128) ||
            acknowledgement.ProcessedAtUtc == default ||
            acknowledgement.ProcessedAtUtc > nowUtc.AddMinutes(2) ||
            acknowledgement.ErrorCode is { Length: > 128 } ||
            acknowledgement.Result is not ("Applied" or "AlreadyApplied" or "Rejected") ||
            (acknowledgement.Result == "Rejected" && string.IsNullOrWhiteSpace(acknowledgement.ErrorCode)) ||
            (acknowledgement.Result != "Rejected" && acknowledgement.ErrorCode is not null))
        {
            acknowledgement = null;
            errorCode = "provisioning_acknowledgement_payload_invalid";
            return false;
        }

        if (!string.Equals(acknowledgement.RequestId, expectedRequestId, StringComparison.Ordinal) ||
            !string.Equals(acknowledgement.Nonce, expectedNonce, StringComparison.Ordinal) ||
            !string.Equals(acknowledgement.PayloadSha256, expectedPayloadSha256, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(acknowledgement.CustomerReference, expectedCustomerReference, StringComparison.Ordinal) ||
            !string.Equals(acknowledgement.InstallationId, expectedInstallationId, StringComparison.Ordinal))
        {
            acknowledgement = null;
            errorCode = "provisioning_acknowledgement_binding_mismatch";
            return false;
        }

        errorCode = string.Empty;
        return true;
    }

    private static bool HasBoundedValue(string? value, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maxLength;

    private static bool IsSha256Hex(string? value) =>
        value is { Length: 64 } && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
