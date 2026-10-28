using Faranou.Licensing.Contracts;

namespace Faranou.Vendor.Host.Signing;

/// <summary>
/// Dedicated signing boundary for customer-provisioning packages. Implementations must use a
/// provisioning-specific key and credentials, distinct from the license-signing key and provider.
/// The input is the exact canonical UTF-8 payload bytes; implementations must not deserialize or
/// reserialize the payload. Private key material must remain in an approved external signer.
/// </summary>
public interface IProvisioningSigningProvider
{
    /// <summary>
    /// Signs the exact canonical provisioning payload with the configured provisioning-only key.
    /// Missing or unavailable credentials must return an unavailable result; never use a software-key
    /// fallback or silently reuse the license-signing key.
    /// </summary>
    ValueTask<LicenseSigningResult> SignAsync(
        ReadOnlyMemory<byte> canonicalPayload,
        CancellationToken cancellationToken = default);
}
