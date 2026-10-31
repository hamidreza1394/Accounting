using Faranou.Licensing.Contracts;

namespace Faranou.Vendor.Host.Signing;

/// <summary>
/// Vendor-only signing boundary for offline revocation snapshots. Its key is distinct from
/// license and provisioning keys and its public trust set is configured independently.
/// </summary>
public interface IOfflineRevocationSigningProvider
{
    ValueTask<LicenseSigningResult> SignAsync(
        ReadOnlyMemory<byte> canonicalPayload,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default fail-closed provider until a dedicated offline-revocation signing key is provisioned
/// in the approved PKCS#11 module. It never generates or embeds key material.
/// </summary>
public sealed class UnavailableOfflineRevocationSigningProvider : IOfflineRevocationSigningProvider
{
    public ValueTask<LicenseSigningResult> SignAsync(
        ReadOnlyMemory<byte> canonicalPayload,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(LicenseSigningResult.Unavailable());
    }
}

