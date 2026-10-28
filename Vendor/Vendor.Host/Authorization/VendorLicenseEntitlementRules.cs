using System.Text.Json;
using Faranou.Licensing.Contracts;

namespace Faranou.Vendor.Host.Authorization;

public sealed record NormalizedVendorLicenseEntitlements(
    string LicenseKind,
    int? MaxCompanies,
    IReadOnlyList<string> Subsystems,
    string SubsystemsJson,
    int GraceDays);

/// <summary>Canonical validation for Vendor license entitlement metadata before persistence or signing.</summary>
public static class VendorLicenseEntitlementRules
{
    public static bool TryNormalize(
        string? licenseKind,
        int? maxCompanies,
        IReadOnlyList<string>? subsystems,
        int? requestedGraceDays,
        DateTimeOffset validFromUtc,
        DateTimeOffset? validUntilUtc,
        out NormalizedVendorLicenseEntitlements? normalized)
    {
        normalized = null;
        var kind = string.IsNullOrWhiteSpace(licenseKind)
            ? (validUntilUtc.HasValue ? LicensePayload.KindTerm : LicensePayload.KindPerpetual)
            : licenseKind.Trim();
        var graceDays = requestedGraceDays ?? LicensePayload.DefaultGraceDays;

        if (kind is not (LicensePayload.KindTerm or LicensePayload.KindPerpetual) ||
            maxCompanies.HasValue && maxCompanies.Value < 1 ||
            graceDays is < 0 or > LicensePayload.MaxGraceDaysCeiling ||
            validUntilUtc.HasValue && validUntilUtc.Value < validFromUtc ||
            kind == LicensePayload.KindTerm && !validUntilUtc.HasValue ||
            kind == LicensePayload.KindPerpetual && validUntilUtc.HasValue)
        {
            return false;
        }

        var codes = (subsystems ?? Array.Empty<string>())
            .Select(code => code?.Trim() ?? string.Empty)
            .ToArray();
        if (codes.Length == 0 ||
            codes.Any(code => !LicenseSubsystemCodes.IsAllowed(code)) ||
            codes.Distinct(StringComparer.Ordinal).Count() != codes.Length)
        {
            return false;
        }

        var sortedCodes = codes.Order(StringComparer.Ordinal).ToArray();
        normalized = new NormalizedVendorLicenseEntitlements(
            kind,
            maxCompanies,
            sortedCodes,
            JsonSerializer.Serialize(sortedCodes),
            graceDays);
        return true;
    }

    public static IReadOnlyList<string> ParseSubsystems(string? json)
    {
        string[] values;
        try
        {
            values = JsonSerializer.Deserialize<string[]>(string.IsNullOrWhiteSpace(json) ? "[]" : json)
                ?? throw new InvalidOperationException("Stored license subsystem metadata is null.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Stored license subsystem metadata is invalid.", exception);
        }

        if (values.Any(code => !LicenseSubsystemCodes.IsAllowed(code)) ||
            values.Distinct(StringComparer.Ordinal).Count() != values.Length)
        {
            throw new InvalidOperationException("Stored license subsystem metadata contains unsupported or duplicate codes.");
        }

        return values.Order(StringComparer.Ordinal).ToArray();
    }
}
