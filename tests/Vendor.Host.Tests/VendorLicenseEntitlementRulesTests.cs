using Faranou.Vendor.Host.Authorization;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorLicenseEntitlementRulesTests
{
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
        Assert.Equal(["finance", "sales"], result.Subsystems);
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
    [InlineData("term", 25, 30, true, false, false)]
    [InlineData("perpetual", 25, 30, false, true, false)]
    [InlineData("term", 0, 30, true, false, false)]
    [InlineData("term", 25, 91, true, false, false)]
    public void RejectsInvalidEntitlementCombinations(
        string kind, int maxCompanies, int graceDays, bool hasExpiry, bool forcePerpetualExpiry, bool ignored)
    {
        var validFrom = new DateTimeOffset(2026, 10, 28, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset? validUntil = hasExpiry || forcePerpetualExpiry ? validFrom.AddDays(1) : null;
        var actualKind = forcePerpetualExpiry ? "perpetual" : kind;

        var valid = VendorLicenseEntitlementRules.TryNormalize(
            actualKind, maxCompanies, ["finance"], graceDays, validFrom, validUntil, out _);

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
}
