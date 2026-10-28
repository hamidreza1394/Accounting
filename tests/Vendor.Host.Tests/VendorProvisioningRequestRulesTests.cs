using Faranou.Licensing.Contracts;
using Faranou.Vendor.Host.Authorization;
using Xunit;

namespace Vendor.Host.Tests;

public sealed class VendorProvisioningRequestRulesTests
{
    [Fact]
    public void ValidConfiguration_IsAccepted()
    {
        var error = VendorProvisioningRequestRules.Validate(ValidRequest());

        Assert.Null(error);
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

    [Fact]
    public void RepeatedRequestId_WithSameConfiguration_IsIdempotent()
    {
        var request = ValidRequest();
        var payload = new CustomerProvisioningPayload(
            1,
            request.RequestId.ToString("D"),
            "customer-001",
            "install-001",
            request.CompanyName!,
            request.CompanyType!,
            request.InitialAdminUserName!,
            request.Subsystems!,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddHours(1),
            "abcdefghijklmnopqrstuvwxyz0123456789_-");

        Assert.True(VendorProvisioningRequestRules.MatchesExisting(payload, request, "customer-001", "install-001"));
    }

    [Fact]
    public void RepeatedRequestId_WithChangedConfiguration_IsNotIdempotent()
    {
        var request = ValidRequest();
        var payload = new CustomerProvisioningPayload(
            1,
            request.RequestId.ToString("D"),
            "customer-001",
            "install-001",
            request.CompanyName!,
            request.CompanyType!,
            request.InitialAdminUserName!,
            request.Subsystems!,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddHours(1),
            "abcdefghijklmnopqrstuvwxyz0123456789_-");

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
        new[] { "inventory", "finance" });
}
