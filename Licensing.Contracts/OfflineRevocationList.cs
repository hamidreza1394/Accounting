using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Faranou.Licensing.Contracts;

/// <summary>
/// Versioned offline revocation snapshot. It contains only license identifiers and freshness metadata.
/// A snapshot is advisory until the customer-side verifier persists its highest accepted sequence.
/// </summary>
public sealed record OfflineRevocationList(
    int SchemaVersion,
    string ListId,
    long Sequence,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    IReadOnlyList<string> RevokedLicenseIds);

/// <summary>
/// Signed envelope. KeyId is only a lookup hint; callers must obtain the public key from an
/// independently configured trust store. This contract never contains signing key material.
/// </summary>
public sealed record OfflineRevocationListEnvelope(
    int FormatVersion,
    string KeyId,
    string Algorithm,
    string PayloadJson,
    string SignatureBase64);

/// <summary>
/// Canonical serialization and fail-closed validation for signed offline revocation snapshots.
/// Signature verification is performed over the exact bounded UTF-8 payload bytes before JSON parsing.
/// This contract does not implement signing, persistence, or customer-side enforcement.
/// </summary>
public static class OfflineRevocationListValidator
{
    public const int CurrentSchemaVersion = 1;
    public const int MaxPayloadJsonChars = 256 * 1024;
    public const int MaxRevokedLicenseIds = 10_000;
    public const int MaxLicenseIdLength = 128;
    public const int MaxListIdLength = 128;
    public const int MaxKeyIdLength = 128;
    public const int MaxValidityDays = 365;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        MaxDepth = 8
    };

    public static string Serialize(OfflineRevocationList payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", payload.SchemaVersion);
            writer.WriteString("listId", payload.ListId);
            writer.WriteNumber("sequence", payload.Sequence);
            writer.WriteString("issuedAtUtc", payload.IssuedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            writer.WriteString("expiresAtUtc", payload.ExpiresAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            writer.WriteStartArray("revokedLicenseIds");
            foreach (var licenseId in payload.RevokedLicenseIds ?? Array.Empty<string>())
            {
                writer.WriteStringValue(licenseId);
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static bool TryValidateAndVerify(
        OfflineRevocationListEnvelope? envelope,
        ReadOnlySpan<byte> trustedPublicKeySubjectPublicKeyInfo,
        DateTimeOffset nowUtc,
        long minimumSequenceExclusive,
        out OfflineRevocationList? revocationList,
        out string errorCode)
    {
        revocationList = null;
        errorCode = "offline_revocation_invalid";

        if (envelope is null ||
            envelope.FormatVersion != CurrentSchemaVersion ||
            !HasBoundedValue(envelope.KeyId, MaxKeyIdLength) ||
            !string.Equals(envelope.Algorithm, LicenseAlgorithms.EcdsaP256Sha256, StringComparison.Ordinal) ||
            string.IsNullOrEmpty(envelope.PayloadJson) ||
            envelope.PayloadJson.Length > MaxPayloadJsonChars ||
            string.IsNullOrEmpty(envelope.SignatureBase64) ||
            trustedPublicKeySubjectPublicKeyInfo.IsEmpty ||
            trustedPublicKeySubjectPublicKeyInfo.Length > 1024)
        {
            errorCode = "offline_revocation_envelope_invalid";
            return false;
        }

        byte[] signature;
        try
        {
            signature = Convert.FromBase64String(envelope.SignatureBase64);
        }
        catch (FormatException)
        {
            errorCode = "offline_revocation_signature_invalid";
            return false;
        }

        if (signature.Length != 64)
        {
            errorCode = "offline_revocation_signature_invalid";
            return false;
        }

        try
        {
            using var publicKey = ECDsa.Create();
            publicKey.ImportSubjectPublicKeyInfo(trustedPublicKeySubjectPublicKeyInfo, out var bytesRead);
            if (bytesRead != trustedPublicKeySubjectPublicKeyInfo.Length || publicKey.KeySize != 256)
            {
                errorCode = "offline_revocation_trusted_key_invalid";
                return false;
            }

            if (!publicKey.VerifyData(
                    Encoding.UTF8.GetBytes(envelope.PayloadJson),
                    signature,
                    HashAlgorithmName.SHA256,
                    DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
            {
                errorCode = "offline_revocation_signature_invalid";
                return false;
            }
        }
        catch (CryptographicException)
        {
            errorCode = "offline_revocation_trusted_key_invalid";
            return false;
        }

        try
        {
            revocationList = System.Text.Json.JsonSerializer.Deserialize<OfflineRevocationList>(
                envelope.PayloadJson, JsonOptions);
        }
        catch (System.Text.Json.JsonException)
        {
            errorCode = "offline_revocation_payload_invalid";
            return false;
        }

        var now = nowUtc.ToUniversalTime();
        if (revocationList is null ||
            !string.Equals(Serialize(revocationList), envelope.PayloadJson, StringComparison.Ordinal) ||
            revocationList.SchemaVersion != CurrentSchemaVersion ||
            !HasBoundedValue(revocationList.ListId, MaxListIdLength) ||
            revocationList.Sequence <= 0 ||
            revocationList.Sequence <= minimumSequenceExclusive ||
            revocationList.IssuedAtUtc == default ||
            revocationList.ExpiresAtUtc == default ||
            revocationList.IssuedAtUtc > now.AddMinutes(2) ||
            revocationList.ExpiresAtUtc <= now ||
            revocationList.ExpiresAtUtc <= revocationList.IssuedAtUtc ||
            revocationList.ExpiresAtUtc - revocationList.IssuedAtUtc > TimeSpan.FromDays(MaxValidityDays) ||
            revocationList.RevokedLicenseIds is null ||
            revocationList.RevokedLicenseIds.Count > MaxRevokedLicenseIds ||
            !AreLicenseIdsValidAndCanonical(revocationList.RevokedLicenseIds))
        {
            revocationList = null;
            errorCode = revocationList is not null && revocationList.Sequence <= minimumSequenceExclusive
                ? "offline_revocation_sequence_invalid"
                : "offline_revocation_payload_invalid";
            return false;
        }

        errorCode = string.Empty;
        return true;
    }

    private static bool AreLicenseIdsValidAndCanonical(IReadOnlyList<string> ids)
    {
        string? previous = null;
        foreach (var id in ids)
        {
            if (!HasBoundedValue(id, MaxLicenseIdLength) ||
                id.Any(char.IsControl) ||
                (previous is not null && string.Compare(previous, id, StringComparison.Ordinal) >= 0))
            {
                return false;
            }

            previous = id;
        }

        return true;
    }

    private static bool HasBoundedValue(string? value, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maxLength;
}
