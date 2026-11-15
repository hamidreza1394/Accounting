using Faranou.Licensing.Contracts;
using Faranou.Vendor.Host.Authorization;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

/// <summary>
/// The CompanyReference of a v5 provisioning payload comes only from the verified signed v2 license
/// payload. The operator-supplied value is a selector that must already be a member of that signed set.
/// </summary>
public sealed class VendorProvisioningCompanyReferenceRulesTests
{
    private const string RefA = "company-ref-0001";
    private const string RefB = "company-ref-0002";
    private const string RefC = "company-ref-0003";

    [Fact]
    public void SelectsTheRequestedReferenceFromTheSignedSetAndUsesItsModules()
    {
        var payload = Payload(Entry(RefA, "sales", "finance"), Entry(RefB, "inventory"));

        var result = VendorProvisioningCompanyReferenceRules.Select(payload, RefA, null);

        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(RefA, result.CompanyReference);
        Assert.Equal(new[] { "finance", "sales" }, result.Subsystems);
        Assert.Equal(new[] { "finance", "sales" }, result.Entitlements);
    }

    [Fact]
    public void ResultDoesNotDependOnTheOrderOfCompanyEntitlementsOrModules()
    {
        var forward = Payload(Entry(RefA, "finance", "sales"), Entry(RefB, "treasury"), Entry(RefC, "inventory"));
        var shuffled = Payload(Entry(RefC, "inventory"), Entry(RefB, "treasury"), Entry(RefA, "sales", "finance"));

        var first = VendorProvisioningCompanyReferenceRules.Select(forward, RefB, null);
        var second = VendorProvisioningCompanyReferenceRules.Select(shuffled, RefB, null);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(first.CompanyReference, second.CompanyReference);
        Assert.Equal(first.Subsystems, second.Subsystems);

        var firstA = VendorProvisioningCompanyReferenceRules.Select(forward, RefA, null);
        var secondA = VendorProvisioningCompanyReferenceRules.Select(shuffled, RefA, null);
        Assert.Equal(firstA.Subsystems, secondA.Subsystems);
    }

    [Fact]
    public void RejectsAReferenceThatIsNotInTheSignedSet()
    {
        var payload = Payload(Entry(RefA, "finance"));

        var result = VendorProvisioningCompanyReferenceRules.Select(payload, RefB, null);

        Assert.False(result.Succeeded);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("provisioning_company_reference_not_entitled", result.ErrorCode);
        Assert.Null(result.CompanyReference);
        Assert.Null(result.Subsystems);
    }

    [Theory]
    [InlineData("COMPANY-REF-0001")]
    [InlineData(" company-ref-0001")]
    [InlineData("company-ref-0001 ")]
    public void SelectorMatchingIsExactAndCaseSensitive(string selector)
    {
        var payload = Payload(Entry(RefA, "finance"));

        var result = VendorProvisioningCompanyReferenceRules.Select(payload, selector, null);

        Assert.False(result.Succeeded);
        Assert.Equal("provisioning_company_reference_not_entitled", result.ErrorCode);
    }

    [Fact]
    public void RequiresASelectorBecauseNothingMayBeInferredFromTheSignedSet()
    {
        // Even a single-company license must not be auto-selected: the operator names the company.
        var payload = Payload(Entry(RefA, "finance"));

        var result = VendorProvisioningCompanyReferenceRules.Select(payload, null, null);

        Assert.False(result.Succeeded);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("provisioning_company_reference_required", result.ErrorCode);
    }

    [Fact]
    public void AnEmptySignedSetGrantsNothing()
    {
        var payload = Payload();

        var result = VendorProvisioningCompanyReferenceRules.Select(payload, RefA, null);

        Assert.False(result.Succeeded);
        Assert.Equal("provisioning_company_reference_not_entitled", result.ErrorCode);
    }

    [Fact]
    public void RejectsADuplicateReferenceInTheSignedPayloadInsteadOfPickingOne()
    {
        // StructuralValidation already rejects this at issuance; defend against a corrupted envelope anyway.
        var payload = Payload(Entry(RefA, "finance"), Entry(RefA, "sales"));

        var result = VendorProvisioningCompanyReferenceRules.Select(payload, RefA, null);

        Assert.False(result.Succeeded);
        Assert.Equal("provisioning_company_reference_invalid", result.ErrorCode);
    }

    [Fact]
    public void RejectsAMalformedReferenceInTheSignedPayload()
    {
        var payload = Payload(Entry("bad ref!", "finance"));

        var result = VendorProvisioningCompanyReferenceRules.Select(payload, "bad ref!", null);

        Assert.False(result.Succeeded);
        Assert.Equal("provisioning_company_reference_invalid", result.ErrorCode);
    }

    [Fact]
    public void RequestedSubsystemsMustBeASubsetOfTheSelectedCompanyModulesOnly()
    {
        var payload = Payload(Entry(RefA, "finance", "sales"), Entry(RefB, "inventory"));

        var subset = VendorProvisioningCompanyReferenceRules.Select(payload, RefA, ["sales"]);
        Assert.True(subset.Succeeded, subset.ErrorCode);
        Assert.Equal(new[] { "sales" }, subset.Subsystems);
        Assert.Equal(new[] { "finance", "sales" }, subset.Entitlements);

        // inventory belongs to ANOTHER company in the same license; it must not leak across companies.
        var other = VendorProvisioningCompanyReferenceRules.Select(payload, RefA, ["inventory"]);
        Assert.False(other.Succeeded);
        Assert.Equal("provisioning_subsystem_not_entitled", other.ErrorCode);
    }

    [Fact]
    public void InvalidRequestedSubsystemsAreRejected()
    {
        var payload = Payload(Entry(RefA, "finance", "sales"));
        string[][] invalidSelections =
        [
            [],
            ["finance", "finance"],
            ["unknown"],
            [""]
        ];

        foreach (var requested in invalidSelections)
        {
            var result = VendorProvisioningCompanyReferenceRules.Select(payload, RefA, requested);

            Assert.False(result.Succeeded);
            Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
            Assert.Equal("invalid_provisioning_subsystems", result.ErrorCode);
        }
    }

    [Fact]
    public void ACompanyWithoutAnyProvisioningSubsystemIsRejected()
    {
        // TRADE_IMPORT is a valid company module but not a provisioning subsystem code.
        var payload = Payload(Entry(RefA, "TRADE_IMPORT"));

        var result = VendorProvisioningCompanyReferenceRules.Select(payload, RefA, null);

        Assert.False(result.Succeeded);
        Assert.Equal("provisioning_license_has_no_subsystems", result.ErrorCode);
    }

    [Fact]
    public void NonProvisioningModulesAreNeverCopiedIntoSubsystems()
    {
        var payload = Payload(Entry(RefA, "TRADE_IMPORT", "finance"));

        var result = VendorProvisioningCompanyReferenceRules.Select(payload, RefA, null);

        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(new[] { "finance" }, result.Subsystems);
    }

    [Fact]
    public void InstallationWideLimitsAreNeverConvertedIntoCompanyEntitlements()
    {
        // MaxCompanies and the legacy installation subsystem list are not company grants.
        var payload = Payload(Entry(RefA, "finance")) with
        {
            Limits = new LicenseLimits(50, ["finance", "sales", "inventory", "treasury"])
        };

        var granted = VendorProvisioningCompanyReferenceRules.Select(payload, RefA, null);
        Assert.True(granted.Succeeded, granted.ErrorCode);
        Assert.Equal(new[] { "finance" }, granted.Subsystems);

        var ungranted = VendorProvisioningCompanyReferenceRules.Select(payload, RefB, null);
        Assert.False(ungranted.Succeeded);
        Assert.Equal("provisioning_company_reference_not_entitled", ungranted.ErrorCode);
    }

    [Fact]
    public void AV1PayloadHasNoCompanyEntitlementsAndNeverAcceptsASelector()
    {
        var v1 = new LicensePayload(
            1, Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D"), "FARANOU",
            LicensePayload.KindPerpetual, DateTimeOffset.UtcNow, null, null, 30, 1,
            new LicenseLimits(5, ["finance"]));

        var result = VendorProvisioningCompanyReferenceRules.Select(v1, RefA, null);

        Assert.False(result.Succeeded);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("provisioning_company_reference_not_applicable", result.ErrorCode);
    }

    private static CompanyLicenseEntitlement Entry(string reference, params string[] modules) =>
        new(reference, Array.AsReadOnly(modules), 5, Array.Empty<LicenseFeatureEntitlement?>());

    private static LicensePayload Payload(params CompanyLicenseEntitlement[] companies) =>
        new(
            LicensePayload.CurrentFormatVersion,
            Guid.NewGuid().ToString("D"),
            Guid.NewGuid().ToString("D"),
            "FARANOU",
            LicensePayload.KindPerpetual,
            DateTimeOffset.UtcNow,
            null,
            null,
            30,
            2,
            new LicenseLimits(10, Array.Empty<string>()))
        {
            CompanyEntitlements = companies
        };
}
