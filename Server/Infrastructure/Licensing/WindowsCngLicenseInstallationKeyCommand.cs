using System;
using System.Security.Cryptography;
using System.Text.Json;
using Infrastructure.Provisioning;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Licensing;

/// <summary>
/// One-time local operator ceremony for a customer's license installation proof key.
/// Only public key material and non-secret identity metadata are written to stdout.
/// </summary>
public static class WindowsCngLicenseInstallationKeyCommand
{
    private const string CommandName = "--license-installation-key";
    private const string SoftwareConsentFlag = "--confirm-software-provider-risk";

    public static bool IsCommand(string[] args) =>
        args.Length > 0 && string.Equals(args[0], CommandName, StringComparison.OrdinalIgnoreCase);

    public static int RunOnConsole(string[] args, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(configuration);

        var explicitSoftwareConsent = args.Length == 3 &&
            string.Equals(args[2], SoftwareConsentFlag, StringComparison.Ordinal);
        if (args.Length is < 2 or > 3 ||
            !string.Equals(args[1], "create", StringComparison.OrdinalIgnoreCase) ||
            (args.Length == 3 && !explicitSoftwareConsent))
        {
            Console.Error.WriteLine(
                "Usage: Server --license-installation-key create [--confirm-software-provider-risk]");
            return 2;
        }

        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("Customer license installation keys require Windows CNG.");
            return 2;
        }

        try
        {
            var options = configuration
                .GetSection(WindowsCngLicenseInstallationKeyOptions.SectionName)
                .Get<WindowsCngLicenseInstallationKeyOptions>()
                ?? new WindowsCngLicenseInstallationKeyOptions();
            var provisioning = configuration
                .GetSection(CustomerProvisioningSettings.SectionName)
                .Get<CustomerProvisioningSettings>()
                ?? new CustomerProvisioningSettings();

            if (string.IsNullOrWhiteSpace(provisioning.InstallationId))
            {
                Console.Error.WriteLine("License installation key provisioning failed closed (installation_id_missing).");
                return 2;
            }

            if (!options.MachineKey)
            {
                Console.Error.WriteLine("License installation key provisioning requires machine-scoped CNG storage.");
                return 2;
            }

            var created = WindowsCngLicenseInstallationKeyProvisioner.Create(
                options,
                provisioning.InstallationId,
                confirmSoftwareProviderRisk: explicitSoftwareConsent);

            Console.Out.WriteLine(JsonSerializer.Serialize(new
            {
                purpose = "license-installation-identity",
                algorithm = "ECDSA-P256-SHA256",
                installationId = provisioning.InstallationId,
                keyId = created.KeyId,
                keyVersion = options.KeyVersion,
                provider = created.ProviderName,
                assurance = created.Assurance.ToString(),
                softwareProviderRiskAccepted = created.SoftwareProviderRiskAccepted,
                softwareProviderRiskAcceptedAtUtc = created.SoftwareProviderRiskAcceptedAtUtc,
                softwareProviderRiskAcceptedBy = created.SoftwareProviderRiskAcceptedBy,
                softwareProviderConsentReceiptJson = created.SoftwareProviderConsentReceiptJson,
                publicKeyFingerprintSha256 = created.PublicKeyFingerprint,
                osInstallationMarkerSha256 = created.OsInstallationMarkerDigest,
                publicKeySubjectPublicKeyInfoBase64 = System.Convert.ToBase64String(
                    created.PublicKeySubjectPublicKeyInfo),
                createdAtUtc = DateTimeOffset.UtcNow,
            }, new JsonSerializerOptions { WriteIndented = true }));

            return 0;
        }
        catch (Exception exception) when (exception is ArgumentException or
            CryptographicException or InvalidOperationException or
            UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // Provider diagnostics may disclose machine/key-store details; expose only the exception type.
            Console.Error.WriteLine(
                "License installation key provisioning failed closed (" +
                exception.GetType().Name + "). No private key was exported.");
            return 1;
        }
    }
}
