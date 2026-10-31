namespace Infrastructure.Licensing;

public interface ILocalInstallationKeyMetadataProvider
{
    WindowsCngLicenseInstallationKeyProvisioningResult ReadExisting(
        string installationId,
        bool softwareProviderRiskConsentRecorded);
}

/// <summary>Reads metadata from the explicitly configured Windows CNG key; it never creates keys.</summary>
public sealed class WindowsCngLocalInstallationKeyMetadataProvider(
    Microsoft.Extensions.Options.IOptions<WindowsCngLicenseInstallationKeyOptions> options)
    : ILocalInstallationKeyMetadataProvider
{
    private readonly WindowsCngLicenseInstallationKeyOptions _options = options.Value;

    public WindowsCngLicenseInstallationKeyProvisioningResult ReadExisting(
        string installationId,
        bool softwareProviderRiskConsentRecorded)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Windows CNG installation keys are unavailable on this platform.");

        return WindowsCngLicenseInstallationKeyProvisioner.ReadExisting(
            _options,
            installationId,
            softwareProviderRiskConsentRecorded);
    }
}
