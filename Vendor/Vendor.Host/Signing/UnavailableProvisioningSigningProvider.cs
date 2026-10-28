using Faranou.Licensing.Contracts;

namespace Faranou.Vendor.Host.Signing;

/// <summary>
/// Default fail-closed provider for provisioning packages until an approved external signer using a
/// dedicated provisioning key is configured. It never generates or embeds a signing key.
/// </summary>
public sealed class UnavailableProvisioningSigningProvider : IProvisioningSigningProvider
{
    public ValueTask<LicenseSigningResult> SignAsync(
        ReadOnlyMemory<byte> canonicalPayload,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(LicenseSigningResult.Unavailable());
    }
}
