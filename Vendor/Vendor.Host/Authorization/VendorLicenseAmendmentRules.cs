namespace Faranou.Vendor.Host.Authorization;

/// <summary>
/// Pure business rules for explicit license amendment requests (D-108/D-109). An amendment request
/// is started by an authorized operator for a purchased line; payment state is NEVER a condition
/// for starting or issuing one (D-108 §1: payment is not a condition of license/amendment
/// issuance), and only installation-scoped kinds are accepted while CompanyReference mapping
/// (#392) is not operational: company-scoped module/feature/user-seat amendments stay Blocked.
/// The rules never sign, persist or change payment state.
/// </summary>
public static class VendorLicenseAmendmentRules
{
    public const string KindMaxCompanies = Faranou.Vendor.Persistence.Domain.VendorLicenseAmendmentRequest.KindMaxCompanies;
    public const string KindSupportUpdates = Faranou.Vendor.Persistence.Domain.VendorLicenseAmendmentRequest.KindSupportUpdates;

    public const string StatusRequested = Faranou.Vendor.Persistence.Domain.VendorLicenseAmendmentRequest.StatusRequested;
    public const string StatusPendingApproval = Faranou.Vendor.Persistence.Domain.VendorLicenseAmendmentRequest.StatusPendingApproval;
    public const string StatusIssued = Faranou.Vendor.Persistence.Domain.VendorLicenseAmendmentRequest.StatusIssued;
    public const string StatusRejected = Faranou.Vendor.Persistence.Domain.VendorLicenseAmendmentRequest.StatusRejected;
    public const string StatusFailed = Faranou.Vendor.Persistence.Domain.VendorLicenseAmendmentRequest.StatusFailed;

    /// <summary>Active states that block a second amendment request for the same purchased line.</summary>
    public static readonly string[] BlockingStatuses =
    [
        StatusRequested, StatusPendingApproval, StatusIssued
    ];

    /// <summary>
    /// Commercial product-code prefixes that may be fulfilled by each supported amendment kind.
    /// Unknown kinds and arbitrary strings are rejected fail-closed: a line can never purchase an
    /// unsupported entitlement just by naming it.
    /// </summary>
    public static bool IsKindCompatible(string kind, string productCode) => kind switch
    {
        KindMaxCompanies => productCode.StartsWith("addon.company", StringComparison.Ordinal),
        KindSupportUpdates => productCode.StartsWith("renewal.support", StringComparison.Ordinal),
        _ => false
    };

    /// <summary>
    /// Fail-closed validation for starting an explicit amendment request. Payment state is
    /// deliberately absent: a missing, recorded, verified or rejected payment never gates the
    /// amendment cycle (D-108 §1), and no payment state may trigger issuance by itself either.
    /// Returns null when the request may be created; otherwise a stable error code.
    /// </summary>
    public static string? ValidateCreate(
        string? kind,
        string productCode,
        Guid? lineLicenseId,
        Guid licenseId,
        string licenseStatus,
        int? requestedMaxCompanies,
        int? licenseMaxCompanies,
        DateTimeOffset? requestedSupportUntilUtc,
        DateTimeOffset? requestedUpdatesUntilUtc,
        DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(kind) ||
            kind is not (KindMaxCompanies or KindSupportUpdates))
            return "invalid_amendment_kind";
        if (lineLicenseId is null || lineLicenseId.Value != licenseId)
            return "order_line_license_mismatch";
        if (!IsKindCompatible(kind, productCode))
            return "amendment_kind_product_mismatch";
        if (!string.Equals(licenseStatus, "Issued", StringComparison.Ordinal))
            return "license_not_amendable";
        if (kind == KindMaxCompanies)
        {
            if (requestedMaxCompanies is not int newMax || newMax <= 0)
                return "invalid_amendment_request";
            if (licenseMaxCompanies is not int currentMax)
                return "current_company_limit_missing";
            if (newMax <= currentMax)
                return "company_limit_not_increased";
        }
        else
        {
            if (requestedSupportUntilUtc is null && requestedUpdatesUntilUtc is null)
                return "amendment_dates_missing";
            if (requestedSupportUntilUtc is { } support && support.ToUniversalTime() <= nowUtc ||
                requestedUpdatesUntilUtc is { } updates && updates.ToUniversalTime() <= nowUtc)
                return "amendment_dates_in_past";
        }
        return null;
    }

    /// <summary>Whether a decision may move the request out of its current state (fail-closed).</summary>
    public static bool CanDecide(string status) => status == StatusPendingApproval;
}
