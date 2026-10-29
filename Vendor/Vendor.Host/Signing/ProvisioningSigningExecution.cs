namespace Faranou.Vendor.Host.Signing;

/// <summary>
/// Bounds the time spent waiting for the external provisioning signer. Timeout and signer-side
/// cancellation fail closed; caller cancellation is preserved. Providers must honor the linked token.
/// </summary>
public static class ProvisioningSigningExecution
{
    public static async Task<LicenseSigningResult> SignWithTimeoutAsync(
        IProvisioningSigningProvider provider,
        ReadOnlyMemory<byte> canonicalPayload,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (timeout <= TimeSpan.Zero || timeout == Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(timeout), "Signing timeout must be positive and finite.");

        using var providerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task<LicenseSigningResult> signingTask;
        try
        {
            signingTask = provider.SignAsync(canonicalPayload, providerCancellation.Token).AsTask();
            return await signingTask.WaitAsync(timeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            providerCancellation.Cancel();
            return LicenseSigningResult.Unavailable();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            providerCancellation.Cancel();
            return LicenseSigningResult.Unavailable();
        }
    }
}
