using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Faranou.Licensing.Contracts;

/// <summary>
/// Versioned, signed customer-side provisioning payload. It intentionally contains no password,
/// password hash, reusable bootstrap secret, connection string, or customer accounting data.
/// Schema v1/v2 remain readable; v2 adds the initial fiscal period and v3 adds the initial administrator email.
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
    string Nonce,
    string? FiscalPeriodTitle = null,
    DateOnly? FiscalPeriodStartDate = null,
    DateOnly? FiscalPeriodEndDate = null,
    string? ParentCompanyReference = null,
    string? InitialAdminEmail = null);

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

            // Do not add fields to v1 canonical JSON: already issued v1 signatures must remain valid.
            if (payload.SchemaVersion >= 2)
            {
                writer.WriteString("fiscalPeriodTitle", payload.FiscalPeriodTitle);
                writer.WriteString("fiscalPeriodStartDate", payload.FiscalPeriodStartDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                writer.WriteString("fiscalPeriodEndDate", payload.FiscalPeriodEndDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                writer.WriteString("parentCompanyReference", payload.ParentCompanyReference);
            }

            // v1/v2 canonical JSON is immutable for already-issued packages.
            if (payload.SchemaVersion >= 3)
            {
                writer.WriteString("initialAdminEmail", payload.InitialAdminEmail);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
