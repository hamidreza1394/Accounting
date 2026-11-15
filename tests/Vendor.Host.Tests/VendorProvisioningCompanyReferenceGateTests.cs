using Faranou.Vendor.Host.Authorization;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

/// <summary>
/// v5 (company-bound) provisioning issuance is prepared but must stay off until the Customer
/// consumer can validate and apply it. These tests pin that interlock.
/// </summary>
public sealed class VendorProvisioningCompanyReferenceGateTests
{
    [Fact]
    public void DefaultGateIsDisabled()
    {
        Assert.False(VendorProvisioningCompanyReferenceGate.Disabled.IsEnabled);
    }

    [Fact]
    public void MissingConfigurationKeepsIssuanceDisabled()
    {
        var gate = VendorProvisioningCompanyReferenceGate.FromConfiguration(
            new ConfigurationBuilder().Build());

        Assert.False(gate.IsEnabled);
    }

    [Theory]
    [InlineData("false")]
    [InlineData("")]
    [InlineData("yes")]
    [InlineData("1")]
    [InlineData("garbage")]
    public void OnlyAnExplicitTrueCanEverEnable(string value)
    {
        var gate = VendorProvisioningCompanyReferenceGate.FromConfiguration(Configure(value));

        Assert.False(gate.IsEnabled);
    }

    [Fact]
    public void ConfigurationCannotEnableIssuanceWhileTheCustomerConsumerIsNotReady()
    {
        // The interlock: the configuration flag is honored only after the Customer side ships
        // v5 verification + CompanyReference mapping and flips CustomerConsumerReady in code.
        var gate = VendorProvisioningCompanyReferenceGate.FromConfiguration(Configure("true"));

        Assert.Equal(VendorProvisioningCompanyReferenceGate.CustomerConsumerReady, gate.IsEnabled);
    }

    [Fact]
    public void CustomerConsumerReadinessMustStayFalseUntilCustomerAppliesV5()
    {
        // Intentional pin. The PR that makes the Customer verify, map and apply schema v5
        // (CompanyReference -> local Company) must flip this value and this test together.
        Assert.False(VendorProvisioningCompanyReferenceGate.CustomerConsumerReady);
    }

    [Fact]
    public void ExplicitConstructionIsAvailableOnlyForTestsAndInternalWiring()
    {
        Assert.True(new VendorProvisioningCompanyReferenceGate(enabled: true).IsEnabled);
        Assert.False(new VendorProvisioningCompanyReferenceGate(enabled: false).IsEnabled);
    }

    private static IConfiguration Configure(string value) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [VendorProvisioningCompanyReferenceGate.ConfigurationKey] = value
            })
            .Build();
}
