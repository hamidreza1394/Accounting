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

        using var licenseCertificate = WindowsCngCertificateLookup.TakeExactlyOne(
            store.Certificates,
            configured.ThumbprintNormalised(configured.LicenseKey));
        using var provisioningCertificate = WindowsCngCertificateLookup.TakeExactlyOne(
            store.Certificates,
            configured.ThumbprintNormalised(configured.ProvisioningKey));
        if (licenseCertificate is null || provisioningCertificate is null)
        {
            error.WriteLine("Exactly one certificate for each signing purpose must be present; nothing was exported.");
            return Task.FromResult(1);
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var (purpose, certificate) in new[]
                 {
                     ("license", licenseCertificate),
                     ("provisioning", provisioningCertificate)
                 })
        {
            var structuralError = WindowsCngCertificatePolicy.ValidateStructural(certificate, now);
            var privateKeyError = WindowsCngCertificatePolicy.ValidatePrivateKey(certificate);
            if (structuralError is not null || privateKeyError is not null)
            {
                error.WriteLine(
                    $"The '{purpose}' certificate is not acceptable (structural={structuralError ?? "ok"}, privateKey={privateKeyError ?? "ok"}); nothing was exported.");
                return Task.FromResult(1);
            }
        }

        if (WindowsCngCertificatePolicy.HaveSamePublicKey(licenseCertificate, provisioningCertificate))
        {
            error.WriteLine("License and provisioning certificates must use independent public keys; nothing was exported.");
            return Task.FromResult(1);
        }

        var exports = new List<object>(2)
        {
            CreateExport("license", configured.LicenseKey, licenseCertificate),
            CreateExport("provisioning", configured.ProvisioningKey, provisioningCertificate)
        };

        output.WriteLine(JsonSerializer.Serialize(exports));
        output.WriteLine(
            "Enrol each keyId + publicKeySubjectPublicKeyInfoBase64 under Licensing:TrustedSigningKeys / ProvisioningSigningTrust:Keys. Private keys never leave the store.");
        return Task.FromResult(0);
    }
    private static object CreateExport(
        string purpose,
        WindowsCngSigningKeyOptions key,
        X509Certificate2 certificate)
    {
        using var publicKey = certificate.GetECDsaPublicKey()
            ?? throw new InvalidOperationException("The selected certificate has no ECDSA public key.");
        return new
        {
            purpose,
            keyId = key.KeyId,
            thumbprint = certificate.Thumbprint,
            subject = certificate.Subject,
            notAfterUtc = certificate.NotAfter.ToUniversalTime().ToString("O"),
            publicKeySubjectPublicKeyInfoBase64 = Convert.ToBase64String(
                publicKey.ExportSubjectPublicKeyInfo())
        };
    }

}
