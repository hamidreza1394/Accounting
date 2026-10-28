using System.Text;
using Faranou.Vendor.Host.Signing;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class ProvisioningSigningProviderContractTests
{
    [Fact]
    public async Task DefaultProviderFailsClosedWithoutSigningMaterial()
    {
        var provider = new UnavailableProvisioningSigningProvider();

        var result = await provider.SignAsync(Encoding.UTF8.GetBytes("{\"schemaVersion\":1}"));

        Assert.False(result.Succeeded);
        Assert.Equal("signing_provider_unavailable", result.ErrorCode);
        Assert.Null(result.KeyId);
        Assert.Null(result.Algorithm);
        Assert.Empty(result.Signature.ToArray());
    }

    [Fact]
    public async Task DefaultProviderHonorsCancellation()
    {
        var provider = new UnavailableProvisioningSigningProvider();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await provider.SignAsync(Encoding.UTF8.GetBytes("{}"), cancellation.Token));
    }
}
