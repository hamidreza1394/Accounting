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

    [Fact]
    public async Task SignerTimeoutCancelsProviderAndFailsClosed()
    {
        var provider = new BlockingProvider();

        var result = await ProvisioningSigningExecution.SignWithTimeoutAsync(
            provider, Encoding.UTF8.GetBytes("{}"),
            TimeSpan.FromMilliseconds(30));

        Assert.False(result.Succeeded);
        Assert.Equal("signing_provider_unavailable", result.ErrorCode);
        Assert.True(provider.CancellationObserved);
    }

    [Fact]
    public async Task CallerCancellationIsNotConvertedToSignerUnavailable()
    {
        var provider = new BlockingProvider();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await ProvisioningSigningExecution.SignWithTimeoutAsync(
                provider, Encoding.UTF8.GetBytes("{}"), TimeSpan.FromSeconds(2), cancellation.Token));
    }

    [Fact]
    public async Task InvalidTimeoutIsRejected()
    {
        var provider = new UnavailableProvisioningSigningProvider();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await ProvisioningSigningExecution.SignWithTimeoutAsync(
                provider, Encoding.UTF8.GetBytes("{}"), TimeSpan.Zero));
    }

    private sealed class BlockingProvider : IProvisioningSigningProvider
    {
        public bool CancellationObserved { get; private set; }

        public async ValueTask<LicenseSigningResult> SignAsync(
            ReadOnlyMemory<byte> canonicalPayload,
            CancellationToken cancellationToken = default)
        {
            using var registration = cancellationToken.Register(() => CancellationObserved = true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return LicenseSigningResult.Unavailable();
        }
    }
}
