namespace Faranou.Licensing.Contracts;

/// <summary>
/// Rights for one opaque company reference inside a single Installation license.
/// CompanyReference is not a company name, tax number, or accounting datum.
/// </summary>
public sealed record CompanyLicenseEntitlement(
    string? CompanyReference,
    IReadOnlyList<string>? Modules,
    int MaxUsers,
    IReadOnlyList<LicenseFeatureEntitlement?>? Features);

/// <summary>
/// Exactly one value field must match Type: enabled → Enabled, max_count → MaxCount,
/// enum → EnumValue.
/// </summary>
public sealed record LicenseFeatureEntitlement(
    string? Code,
    string? Type,
    bool? Enabled = null,
    int? MaxCount = null,
    string? EnumValue = null);
