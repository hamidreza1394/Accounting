using Faranou.Vendor.Host.Authorization;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorLicenseEntitlementRulesTests
{
    private static readonly string[] FinanceAndSales = ["finance", "sales"];

    [Fact]
    public void NormalizesTermLicenseAndOrdersSubsystemsDeterministically()
    {
        var validFrom = new DateTimeOffset(2026, 10, 28, 0, 0, 0, TimeSpan.Zero);
        var validUntil = validFrom.AddYears(1);

        var valid = VendorLicenseEntitlementRules.TryNormalize(
            "term", 25, ["sales", "finance"], null, validFrom, validUntil, out var result);

        Assert.True(valid);
        Assert.NotNull(result);
        Assert.Equal("term", result.LicenseKind);
        Assert.Equal(25, result.MaxCompanies);
        Assert.Equal(FinanceAndSales, result.Subsystems);
        Assert.Equal(30, result.GraceDays);
        Assert.Equal("[\"finance\",\"sales\"]", result.SubsystemsJson);
    }

    [Fact]
    public void InfersPerpetualKindOnlyWhenExpiryIsAbsent()
    {
        var validFrom = DateTimeOffset.UtcNow;

        var valid = VendorLicenseEntitlementRules.TryNormalize(
            null, null, ["finance"], null, validFrom, null, out var result);

        Assert.True(valid);
        Assert.NotNull(result);
        Assert.Equal("perpetual", result.LicenseKind);
        Assert.Null(result.MaxCompanies);
    }

    [Theory]
    [InlineData("term", 25, 30, false)]
    [InlineData("perpetual", 25, 30, true)]
    [InlineData("term", 0, 30, true)]
    [InlineData("term", 25, 91, true)]
    public void RejectsInvalidEntitlementCombinations(
        string kind, int maxCompanies, int graceDays, bool hasExpiry)
    {
        var validFrom = new DateTimeOffset(2026, 10, 28, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset? validUntil = hasExpiry ? validFrom.AddDays(1) : null;

        var valid = VendorLicenseEntitlementRules.TryNormalize(
            kind, maxCompanies, ["finance"], graceDays, validFrom, validUntil, out _);

        Assert.False(valid);
    }

    [Fact]
    public void RejectsMissingUnknownOrDuplicateSubsystems()
    {
        var validFrom = DateTimeOffset.UtcNow;
        var end = validFrom.AddDays(30);

        Assert.False(VendorLicenseEntitlementRules.TryNormalize("term", null, [], 30, validFrom, end, out _));
        Assert.False(VendorLicenseEntitlementRules.TryNormalize("term", null, ["finance", "not-a-module"], 30, validFrom, end, out _));
        Assert.False(VendorLicenseEntitlementRules.TryNormalize("term", null, ["finance", "finance"], 30, validFrom, end, out _));
    }

    [Fact]
    public void CompanyScopedNormalizationAcceptsEmptyInstallationSubsystemsWhileV1StillRejectsThem()
    {
        var validFrom = DateTimeOffset.UtcNow;

        Assert.True(VendorLicenseEntitlementRules.TryNormalizeCompanyScoped(
            "perpetual", 5, 30, validFrom, null, out var normalized));
        Assert.Equal("perpetual", normalized!.LicenseKind);
        Assert.Equal(5, normalized.MaxCompanies);
        Assert.Empty(normalized.Subsystems);
        Assert.False(VendorLicenseEntitlementRules.TryNormalize("perpetual", 5, [], 30, validFrom, null, out _));
    }

    [Theory]
    [InlineData("term", 25, 30, false)]
    [InlineData("perpetual", 25, 30, true)]
    [InlineData("perpetual", 0, 30, false)]
    [InlineData("perpetual", 25, 91, false)]
    public void CompanyScopedNormalizationRejectsInvalidKindTermCapacityAndGraceCombinations(
        string kind, int maxCompanies, int graceDays, bool hasExpiry)
    {
        var validFrom = new DateTimeOffset(2026, 10, 28, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset? validUntil = hasExpiry ? validFrom.AddDays(1) : null;

        Assert.False(VendorLicenseEntitlementRules.TryNormalizeCompanyScoped(
            kind, maxCompanies, graceDays, validFrom, validUntil, out _));
    }

    [Theory]
    [InlineData("company_draft_too_large", "company_entitlements_too_large")]
    [InlineData("company_reference_unknown", "company_reference_not_assignable")]
    [InlineData("company_reference_duplicate", "company_reference_not_assignable")]
    [InlineData("company_list_empty", "invalid_company_entitlements")]
    [InlineData("company_descriptor_invalid", "invalid_company_entitlements")]
    [InlineData("company_entitlements_invalid", "invalid_company_entitlements")]
    public void DraftErrorCodesMapToStablePublicApiCodes(string internalCode, string apiCode)
    {
        Assert.Equal(apiCode, VendorLicenseCompanyDraft.ToApiErrorCode(internalCode));
    }
}
