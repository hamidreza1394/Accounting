namespace Faranou.Licensing.Contracts;

/// <summary>
/// Customer-side asynchronous validation pipeline that refuses to treat a license as usable
/// until a current, trusted offline revocation snapshot is available.
/// </summary>
public sealed class RevocationAwareLicenseEnvelopeValidator : ICustomerLicenseEnvelopeValidator
{
    private readonly LicenseEnvelopeValidator _licenseValidator;
    private readonly OfflineRevocationConsumer _revocationConsumer;

    public RevocationAwareLicenseEnvelopeValidator(
        LicenseEnvelopeValidator licenseValidator,
        OfflineRevocationConsumer revocationConsumer)
    {
        _licenseValidator = licenseValidator ?? throw new ArgumentNullException(nameof(licenseValidator));
        _revocationConsumer = revocationConsumer ?? throw new ArgumentNullException(nameof(revocationConsumer));
    }

    public Task<LicenseValidationResult> ValidateAsync(
        LicenseEnvelope envelope,
        string localInstallationId,
        CancellationToken cancellationToken = default) =>
        ValidateCoreAsync(envelope, localInstallationId, null, cancellationToken);

    public Task<LicenseValidationResult> ValidateAsync(
        LicenseEnvelope envelope,
        string localInstallationId,
        string localInstallationKeyFingerprint,
        CancellationToken cancellationToken = default) =>
        ValidateCoreAsync(envelope, localInstallationId, localInstallationKeyFingerprint, cancellationToken);

    private async Task<LicenseValidationResult> ValidateCoreAsync(
        LicenseEnvelope envelope,
        string localInstallationId,
        string? localInstallationKeyFingerprint,
        CancellationToken cancellationToken)
    {
        var licenseResult = _licenseValidator.Validate(envelope, localInstallationId, localInstallationKeyFingerprint);
        if (!licenseResult.Success || licenseResult.Payload is null)
        {
            return licenseResult;
        }

        var revocation = await _revocationConsumer.CheckAsync(
            licenseResult.Payload.LicenseId,
            cancellationToken);
        return revocation.Status switch
        {
            OfflineRevocationCheckStatus.NotRevoked => licenseResult,
            OfflineRevocationCheckStatus.Revoked =>
                LicenseValidationResult.Fail("license_revoked"),
            _ => LicenseValidationResult.Fail("offline_revocation_unavailable")
        };
    }
}
