using Faranou.Vendor.Host.Authorization;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorProvisioningLicenseAvailabilityRulesTests
{
    [Fact]
    public void CompanyScopedLicenseBlockMapsToStableOperatorReason()
    {
        Assert.Equal(
            VendorProvisioningLicenseAvailabilityRules.CompanyReferenceMappingRequired,
            VendorProvisioningLicenseAvailabilityRules.GetOperatorVisibleBlockReason(
                "provisioning_company_reference_mapping_required"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("provisioning_license_signing_key_untrusted")]
    [InlineData("provisioning_license_signature_or_payload_invalid")]
    [InlineData("provisioning_license_outside_validity")]
    [InlineData("unexpected-internal-error")]
    public void OtherResolverFailuresAreNotExposedToTheBrowser(string? resolverErrorCode)
    {
        Assert.Null(
            VendorProvisioningLicenseAvailabilityRules.GetOperatorVisibleBlockReason(resolverErrorCode));
    }
}
