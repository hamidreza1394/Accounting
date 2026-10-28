using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Faranou.Licensing.Contracts;

/// <summary>
/// Versioned, signed customer-side provisioning payload. It intentionally contains no password,
/// password hash, reusable bootstrap secret, connection string, or customer accounting data.
/// </summary>
public sealed record CustomerProvisioningPayload(
    int SchemaVersion,
    string RequestId,
    string CustomerReference,
    string InstallationId,
    string CompanyName,
    string CompanyType,
    string InitialAdminUserName,
    IReadOnlyList<string> Subsystems,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string Nonce);

/// <summary>
/// Signed provisioning package transported to a trusted customer-side provisioning consumer.
/// The consumer must resolve KeyId only against an independently configured trusted-key set.
/// </summary>
public sealed record CustomerProvisioningPackage(
    int FormatVersion,
    string KeyId,
    string Algorithm,
    string PayloadJson,
    string SignatureBase64);

/// <summary>Stable UTF-8 JSON representation used as the exact signature input.</summary>
public static class CustomerProvisioningCanonicalJson
{
    public static string Serialize(CustomerProvisioningPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", payload.SchemaVersion);
            writer.WriteString("requestId", payload.RequestId);
            writer.WriteString("customerReference", payload.CustomerReference);
            writer.WriteString("installationId", payload.InstallationId);
            writer.WriteString("companyName", payload.CompanyName);
            writer.WriteString("companyType", payload.CompanyType);
            writer.WriteString("initialAdminUserName", payload.InitialAdminUserName);
            writer.WritePropertyName("subsystems");
            writer.WriteStartArray();
            foreach (var subsystem in (payload.Subsystems ?? Array.Empty<string>()).Order(StringComparer.Ordinal))
            {
                writer.WriteStringValue(subsystem);
            }
            writer.WriteEndArray();
            writer.WriteString("issuedAtUtc", payload.IssuedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            writer.WriteString("expiresAtUtc", payload.ExpiresAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            writer.WriteString("nonce", payload.Nonce);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
