using Faranou.Licensing.Contracts;
using Faranou.Vendor.Host.Authorization;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorProvisioningRequestRulesTests
{
    [Fact]
    public void ValidConfiguration_IsAccepted()
    {
        var error = VendorProvisioningRequestRules.Validate(ValidRequest());

        Assert.Null(error);
    }

    [Fact]
    public void MissingOrInvalidFiscalPeriod_IsRejected()
    {
        var request = ValidRequest();

        Assert.Equal("invalid_provisioning_fiscal_period",
            VendorProvisioningRequestRules.Validate(request with { FiscalPeriodTitle = null }));
        Assert.Equal("invalid_provisioning_fiscal_period",
            VendorProvisioningRequestRules.Validate(request with
            {
                FiscalPeriodStartDate = new DateOnly(2027, 1, 1),
                FiscalPeriodEndDate = new DateOnly(2026, 1, 1)
            }));
    }

    [Fact]
    public void ParentCompanyReference_IsRequiredOnlyForSubsidiaries()
    {
        var request = ValidRequest();

        Assert.Equal("invalid_provisioning_parent_company_reference",
            VendorProvisioningRequestRules.Validate(request with
            {
                CompanyType = "subsidiary",
                ParentCompanyReference = null
            }));
        Assert.Equal("invalid_provisioning_parent_company_reference",
            VendorProvisioningRequestRules.Validate(request with
            {
                CompanyType = "independent",
                ParentCompanyReference = "parent-001"
            }));
        Assert.Null(VendorProvisioningRequestRules.Validate(request with
        {
            CompanyType = "subsidiary",
            ParentCompanyReference = "parent-001"
        }));
    }

    [Theory]
    [InlineData("future-module")]
    [InlineData("finance")]
    public void UnknownOrDuplicateSubsystems_AreRejected(string subsystem)
    {
        var request = ValidRequest() with
        {
            Subsystems = subsystem == "finance"
                ? new[] { "finance", "finance" }
                : new[] { subsystem }
        };

        Assert.Equal("invalid_provisioning_subsystems", VendorProvisioningRequestRules.Validate(request));
    }

    [Theory]
    [InlineData("company", "invalid", "administrator", "invalid_provisioning_configuration")]
    [InlineData("", "independent", "administrator", "invalid_provisioning_configuration")]
    [InlineData("Company", "independent", "x", "invalid_provisioning_configuration")]
    public void InvalidCompanyOrAdminSettings_AreRejected(
        string companyName, string companyType, string adminUserName, string expectedError)
    {
        var request = ValidRequest() with
        {
            CompanyName = companyName,
            CompanyType = companyType,
            InitialAdminUserName = adminUserName
        };

        Assert.Equal(expectedError, VendorProvisioningRequestRules.Validate(request));
    }

    [Theory]
    [InlineData("Active", "Active", true)]
    [InlineData("Inactive", "Active", false)]
    [InlineData("Suspended", "Active", false)]
    [InlineData("Active", "Inactive", false)]
    [InlineData(null, "Active", false)]
    [InlineData("Active", null, false)]
    public void ProvisioningTargetMustBeActive(string? customerStatus, string? installationStatus, bool expected)
    {
        Assert.Equal(expected, VendorProvisioningRequestRules.IsTargetActive(customerStatus, installationStatus));
    }

    [Fact]
    public void RepeatedRequestId_WithSameConfiguration_IsIdempotent()
    {
        var request = ValidRequest();
        var payload = new CustomerProvisioningPayload(
            CustomerProvisioningPackageValidator.CurrentPayloadSchemaVersion,
            request.RequestId.ToString("D"),
            "customer-001",
            "install-001",
            request.CompanyName!,
            request.CompanyType!,
            request.InitialAdminUserName!,
            request.Subsystems!,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddHours(1),
            "abcdefghijklmnopqrstuvwxyz0123456789_-",
            request.FiscalPeriodTitle,
            request.FiscalPeriodStartDate,
            request.FiscalPeriodEndDate,
            request.ParentCompanyReference,
            request.InitialAdminEmail);

        Assert.True(VendorProvisioningRequestRules.MatchesExisting(payload, request, "customer-001", "install-001"));
    }

    [Fact]
    public void RepeatedRequestId_WithChangedConfiguration_IsNotIdempotent()
    {
        var request = ValidRequest();
        var payload = new CustomerProvisioningPayload(
            CustomerProvisioningPackageValidator.CurrentPayloadSchemaVersion,
            request.RequestId.ToString("D"),
            "customer-001",
            "install-001",
            request.CompanyName!,
            request.CompanyType!,
            request.InitialAdminUserName!,
            request.Subsystems!,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddHours(1),
            "abcdefghijklmnopqrstuvwxyz0123456789_-",
            request.FiscalPeriodTitle,
            request.FiscalPeriodStartDate,
            request.FiscalPeriodEndDate,
            request.ParentCompanyReference,
            request.InitialAdminEmail);

        Assert.False(VendorProvisioningRequestRules.MatchesExisting(
            payload, request with { CompanyName = "Different Company" }, "customer-001", "install-001"));
    }

    private static CreateVendorProvisioningRequest ValidRequest() => new(
        Guid.Parse("9b0d2c4d-53bc-4bb2-a22c-90b30f2b58e8"),
        Guid.Parse("8b0d2c4d-53bc-4bb2-a22c-90b30f2b58e8"),
        Guid.Parse("7b0d2c4d-53bc-4bb2-a22c-90b30f2b58e8"),
        "Company Example",
        "independent",
        "initial-admin",
        new[] { "inventory", "finance" },
        "سال مالی ۱۴۰۵",
        new DateOnly(2026, 3, 21),
        new DateOnly(2027, 3, 20),
        null,
        "admin@example.com");
}
