using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Faranou.Vendor.Host.Signing;

/// <summary>
/// Read-only key-ceremony helper for the D-107 Windows CNG signer: prints only public
/// SubjectPublicKeyInfo for the configured purpose certificates so deployment can enrol the public
/// keys into the trust-store configuration. It never outputs private key material and never
/// enables runtime signing on its own.
/// </summary>
public static class WindowsCngSigningCertificateCommand
{
    public const string CommandName = "--signing-certificate";
    public const string ExportPublicAction = "export-public";

    public static bool IsRequested(IReadOnlyList<string> args) =>
        args.Count > 0 && string.Equals(args[0], CommandName, StringComparison.OrdinalIgnoreCase);

    public static Task<int> RunAsync(
        IReadOnlyList<string> args,
        IConfiguration configuration,
        TextWriter output,
        TextWriter error)
    {
        if (args.Count < 2 || !string.Equals(args[1], ExportPublicAction, StringComparison.Ordinal))
        {
            error.WriteLine($"Usage: dotnet Vendor.Host.dll {CommandName} {ExportPublicAction}");
            return Task.FromResult(2);
        }

        var configured = configuration
            .GetSection("Signing:CertificateStore")
            .Get<WindowsCngSigningOptions>() ?? new WindowsCngSigningOptions();

        try
        {
            configured.Validate(isProduction: false, isWindowsHost: OperatingSystem.IsWindows());
        }
        catch (InvalidOperationException exception)
        {
            error.WriteLine("Signing certificate configuration is invalid: " + exception.Message);
            return Task.FromResult(2);
        }

        if (!Enum.TryParse(configured.StoreLocation, ignoreCase: true, out StoreLocation storeLocation))
        {
            error.WriteLine("Signing certificate store location is invalid.");
            return Task.FromResult(2);
        }

        using var store = new X509Store(configured.StoreName, storeLocation);
        try
        {
            store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
        }
        catch (CryptographicException)
        {
            error.WriteLine("The configured certificate store could not be opened; verify access and store location.");
            return Task.FromResult(1);
        }

        var exports = new List<object>(2);
        foreach (var (purpose, key) in new (string Purpose, WindowsCngSigningKeyOptions Key)[]
                 {
                     ("license", configured.LicenseKey),
                     ("provisioning", configured.ProvisioningKey)
                 })
        {
            var matches = store.Certificates.Find(
                X509FindType.FindByThumbprint,
                configured.ThumbprintNormalised(key),
                validOnly: false);
            if (matches.Count != 1)
            {
                error.WriteLine($"Exactly one '{purpose}' certificate was not found in the store; nothing was exported.");
                return Task.FromResult(1);
            }

            var certificate = matches[0];
            var now = DateTimeOffset.UtcNow;
            var structuralError = WindowsCngCertificatePolicy.ValidateStructural(certificate, now);
            var privateKeyError = WindowsCngCertificatePolicy.ValidatePrivateKey(certificate);
            if (structuralError is not null || privateKeyError is not null)
            {
                error.WriteLine(
                    $"The '{purpose}' certificate is not acceptable (structural={structuralError ?? "ok"}, privateKey={privateKeyError ?? "ok"}); nothing was exported.");
                return Task.FromResult(1);
            }

            using var publicKey = certificate.PublicKey.GetECDsaPublicKey()
                ?? throw new InvalidOperationException("The selected certificate has no ECDSA public key.");
            exports.Add(new
            {
                purpose,
                keyId = key.KeyId,
                thumbprint = certificate.Thumbprint,
                subject = certificate.Subject,
                notAfterUtc = certificate.NotAfter.ToUniversalTime().ToString("O"),
                publicKeySubjectPublicKeyInfoBase64 = Convert.ToBase64String(
                    publicKey.ExportSubjectPublicKeyInfo())
            });
        }

        output.WriteLine(JsonSerializer.Serialize(exports));
        output.WriteLine(
            "Enrol each keyId + publicKeySubjectPublicKeyInfoBase64 under Licensing:TrustedSigningKeys / ProvisioningSigningTrust:Keys. Private keys never leave the store.");
        return Task.FromResult(0);
    }
}
