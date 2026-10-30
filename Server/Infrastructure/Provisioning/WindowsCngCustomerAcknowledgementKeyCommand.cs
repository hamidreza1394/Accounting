using System;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Provisioning;

/// <summary>
/// Offline operator command for creating a per-installation ACK key.
/// It emits public metadata only and does not enable the runtime signer.
/// </summary>
public static class WindowsCngCustomerAcknowledgementKeyCommand
{
    private const string CommandName = "--provisioning-ack-key";

    public static bool IsCommand(string[] args) =>
        args.Length > 0 && string.Equals(args[0], CommandName, StringComparison.OrdinalIgnoreCase);

    public static int RunOnConsole(string[] args, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(configuration);

        if (args.Length != 2 || !string.Equals(args[1], "create", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("Usage: Server --provisioning-ack-key create");
            return 2;
        }

        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("Customer acknowledgement key provisioning requires Windows CNG.");
            return 2;
        }

        try
        {
            var signingOptions = configuration
                .GetSection(WindowsCngCustomerAcknowledgementSigningOptions.SectionName)
                .Get<WindowsCngCustomerAcknowledgementSigningOptions>()
                ?? new WindowsCngCustomerAcknowledgementSigningOptions();
            var customerSettings = configuration
                .GetSection(CustomerProvisioningSettings.SectionName)
                .Get<CustomerProvisioningSettings>()
                ?? new CustomerProvisioningSettings();

            // Key creation is an explicit console operation and is intentionally independent
            // from Enabled: the public key must be registered in both trust stores before Sign is enabled.
            var created = WindowsCngCustomerAcknowledgementKeyProvisioner.Create(
                signingOptions, customerSettings.InstallationId);

            Console.Out.WriteLine(JsonSerializer.Serialize(new
            {
                purpose = "customer-ack",
                algorithm = "ECDSA-P256-SHA256",
                keyId = created.KeyId,
                keyVersion = signingOptions.KeyVersion,
                provider = signingOptions.ProviderName,
                machineKey = signingOptions.MachineKey,
                publicKeySubjectPublicKeyInfoBase64 = System.Convert.ToBase64String(
                    created.PublicKeySubjectPublicKeyInfo),
                createdAtUtc = DateTimeOffset.UtcNow,
            }, new JsonSerializerOptions { WriteIndented = true }));

            return 0;
        }
        catch (Exception exception) when (exception is ArgumentException or
            CryptographicException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // Do not print provider diagnostics; they may disclose local key-store details.
            Console.Error.WriteLine(
                "Customer acknowledgement key provisioning failed closed (" +
                exception.GetType().Name + "). No private key was exported.");
            return 1;
        }
    }
}
