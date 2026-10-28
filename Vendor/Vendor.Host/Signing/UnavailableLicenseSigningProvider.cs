using Faranou.Licensing.Contracts;

namespace Faranou.Vendor.Host.Signing;

/// <summary>
/// Default fail-closed provider used until an approved production KMS/HSM adapter is configured.
/// It intentionally never generates or embeds a signing key.
/// </summary>
public sealed class UnavailableLicenseSigningProvider : ILicenseSigningProvider
{
    public ValueTask<LicenseSigningResult> SignAsync(
        ReadOnlyMemory<byte> canonicalPayload,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(LicenseSigningResult.Unavailable());
    }
}
