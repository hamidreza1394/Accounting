namespace Faranou.Licensing.Contracts;

/// <summary>
/// Canonical signed payload. Payload v1 remains byte-compatible; v2 adds company-scoped
/// module/feature/seat entitlements and independent support/update dates.
/// </summary>
public sealed record LicensePayload(
    int FormatVersion,
    string LicenseId,
    string InstallationId,
    string ProductCode,
    string LicenseKind,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset? NotBeforeUtc,
    DateTimeOffset? ExpiresAtUtc,
    int GraceDays,
    int Sequence,
    LicenseLimits Limits)
{
    public const int LegacyFormatVersion = 1;
    public const int CurrentFormatVersion = 2;

    public static bool IsSupportedFormatVersion(int version)
        => version is LegacyFormatVersion or CurrentFormatVersion;

    /// <summary>Null is invalid in v2; an empty list explicitly grants no company entitlements.</summary>
    public IReadOnlyList<CompanyLicenseEntitlement?>? CompanyEntitlements { get; init; }

    /// <summary>Independent support end date; it does not expire a perpetual license.</summary>
    public DateTimeOffset? SupportUntilUtc { get; init; }

    /// <summary>Independent update entitlement end date; it does not expire a perpetual license.</summary>
    public DateTimeOffset? UpdatesUntilUtc { get; init; }

    public const string KindPerpetual = "perpetual";
    public const string KindTerm = "term";
    public const int MaxGraceDaysCeiling = 90;
    public const int DefaultGraceDays = 30;
}
