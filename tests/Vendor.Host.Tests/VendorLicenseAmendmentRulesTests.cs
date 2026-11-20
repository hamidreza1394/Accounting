using Faranou.Vendor.Host.Authorization;
using Faranou.Vendor.Persistence.Domain;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

/// <summary>
/// Pure rules for explicit license amendment requests (D-108/D-109): payment state is never part
/// of eligibility (missing, recorded, verified, rejected or partially paid all allow the
/// amendment cycle), only installation-scoped kinds are accepted while CompanyReference
/// mapping (#392) is unresolved, and product codes must commercially match the requested kind.
/// </summary>
public sealed class VendorLicenseAmendmentRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    private static string? Validate(
        string kind,
        string productCode = "addon.company.seat",
        Guid? lineLicenseId = null,
        Guid? licenseId = null,
        string licenseStatus = "Issued",
        int? requestedMaxCompanies = 7,
        int? licenseMaxCompanies = 5,
        DateTimeOffset? supportUntilUtc = null,
        DateTimeOffset? updatesUntilUtc = null,
        DateTimeOffset? now = null)
    {
        var lineLicense = lineLicenseId ?? Guid.NewGuid();
        return VendorLicenseAmendmentRules.ValidateCreate(
            kind, productCode,
            lineLicense,
            licenseId ?? lineLicense,
            licenseStatus, requestedMaxCompanies, licenseMaxCompanies,
            supportUntilUtc, updatesUntilUtc, now ?? Now);
    }

    [Fact]
    public void EligiblePurchasedLineWithCompatibleCodeIsAccepted()
    {
        Assert.Null(Validate(VendorLicenseAmendmentRules.KindMaxCompanies, requestedMaxCompanies: 6));
    }

    [Fact]
    public void LineWithoutMatchingLicenseReferenceIsRejected()
    {
        var licenseId = Guid.NewGuid();
        Assert.Equal("order_line_license_mismatch", Validate(VendorLicenseAmendmentRules.KindMaxCompanies, lineLicenseId: null, licenseId: licenseId));
        Assert.Equal("order_line_license_mismatch", Validate(VendorLicenseAmendmentRules.KindMaxCompanies, lineLicenseId: Guid.NewGuid(), licenseId: licenseId));
    }

    [Fact]
    public void UnknownKindsAreRejectedFailClosed()
    {
        Assert.Equal("invalid_amendment_kind", Validate("userSeats"));
        Assert.Equal("invalid_amendment_kind", Validate(""));
        // Company-scoped add-on kinds do not exist while #392 is unresolved: no builder is exposed.
        Assert.False(VendorLicenseAmendmentRules.IsKindCompatible("companyModules", "addon.company.seat"));
    }

    [Fact]
    public void KindMustMatchThePurchasedProductCode()
    {
        Assert.Equal("amendment_kind_product_mismatch",
            Validate(VendorLicenseAmendmentRules.KindSupportUpdates, productCode: "addon.company.seat"));
        Assert.Equal("amendment_kind_product_mismatch",
            Validate(VendorLicenseAmendmentRules.KindMaxCompanies, productCode: "renewal.support-updates"));
        Assert.Null(Validate(VendorLicenseAmendmentRules.KindSupportUpdates,
            productCode: "renewal.support-updates", requestedMaxCompanies: null,
            supportUntilUtc: Now.AddYears(1)));
    }

    [Fact]
    public void DraftLicensesAreNotAmendable()
    {
        Assert.Equal("license_not_amendable", Validate(VendorLicenseAmendmentRules.KindMaxCompanies, licenseStatus: "Draft"));
    }

    [Fact]
    public void CompanyLimitMustActuallyIncrease()
    {
        Assert.Equal("current_company_limit_missing", Validate(VendorLicenseAmendmentRules.KindMaxCompanies, licenseMaxCompanies: null));
        Assert.Equal("company_limit_not_increased", Validate(VendorLicenseAmendmentRules.KindMaxCompanies, requestedMaxCompanies: 5));
        Assert.Equal("invalid_amendment_request", Validate(VendorLicenseAmendmentRules.KindMaxCompanies, requestedMaxCompanies: null));
    }

    [Fact]
    public void PeriodExtensionRequiresAFutureDate()
    {
        Assert.Equal("amendment_dates_missing", Validate(VendorLicenseAmendmentRules.KindSupportUpdates,
            productCode: "renewal.support-updates", requestedMaxCompanies: null));
        Assert.Equal("amendment_dates_in_past", Validate(VendorLicenseAmendmentRules.KindSupportUpdates,
            productCode: "renewal.support-updates", requestedMaxCompanies: null,
            supportUntilUtc: Now.AddDays(-1)));
        Assert.Null(Validate(VendorLicenseAmendmentRules.KindSupportUpdates,
            productCode: "renewal.support-updates", requestedMaxCompanies: null,
            updatesUntilUtc: Now.AddMonths(6)));
    }

    [Fact]
    public void OnlyPendingApprovalRequestsCanBeDecided()
    {
        Assert.True(VendorLicenseAmendmentRules.CanDecide(VendorLicenseAmendmentRequest.StatusPendingApproval));
        Assert.False(VendorLicenseAmendmentRules.CanDecide(VendorLicenseAmendmentRequest.StatusRequested));
        Assert.False(VendorLicenseAmendmentRules.CanDecide(VendorLicenseAmendmentRequest.StatusIssued));
        Assert.False(VendorLicenseAmendmentRules.CanDecide(VendorLicenseAmendmentRequest.StatusRejected));
    }

    [Fact]
    public void IssuedRequestsBlockNewOnesButRejectedAllowRetries()
    {
        Assert.Contains(VendorLicenseAmendmentRequest.StatusIssued, VendorLicenseAmendmentRules.BlockingStatuses);
        Assert.Contains(VendorLicenseAmendmentRequest.StatusPendingApproval, VendorLicenseAmendmentRules.BlockingStatuses);
        Assert.DoesNotContain(VendorLicenseAmendmentRequest.StatusRejected, VendorLicenseAmendmentRules.BlockingStatuses);
        Assert.DoesNotContain(VendorLicenseAmendmentRequest.StatusFailed, VendorLicenseAmendmentRules.BlockingStatuses);
    }
}
