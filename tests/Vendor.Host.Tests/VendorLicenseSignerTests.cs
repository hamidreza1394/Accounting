using Faranou.Vendor.Host.Licensing;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorLicenseSignerTests
{
    [Fact]
    public async Task UnconfiguredSignerFailsClosedWithoutReturningSignature()
    {
        var signer = new UnconfiguredVendorLicenseSigner();

        var exception = await Assert.ThrowsAsync<VendorLicenseSignerUnavailableException>(
            () => signer.SignAsync(new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken));

        Assert.Equal("A trusted Vendor license signing provider is not configured.", exception.Message);
    }
}
