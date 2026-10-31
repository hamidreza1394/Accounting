namespace Faranou.Licensing.Contracts;

/// <summary>
/// The only customer runtime license-validation contract. Implementations must include the
/// current offline-revocation check; callers must not depend directly on LicenseEnvelopeValidator.
/// </summary>
public interface ICustomerLicenseEnvelopeValidator
{
    Task<LicenseValidationResult> ValidateAsync(
        LicenseEnvelope envelope,
        string localInstallationId,
        CancellationToken cancellationToken = default);

    Task<LicenseValidationResult> ValidateAsync(
        LicenseEnvelope envelope,
        string localInstallationId,
        string localInstallationKeyFingerprint,
        CancellationToken cancellationToken = default);
}
