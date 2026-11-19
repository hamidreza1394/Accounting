using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Faranou.Licensing.Contracts;
using Faranou.Vendor.Host.Signing;
using Faranou.Vendor.Persistence.Auditing;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

[SupportedOSPlatform("windows")]
public sealed class WindowsCngCertificateStoreSigningTests
{
    private const string SoftwareKsp = "Microsoft Software Key Storage Provider";

    [Fact]
    public async Task NonExportableCertificatesSignExactPayloadBytesAndStayPurposeSeparated()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Windows CNG is available only on Windows.");

        var licenseKeyName = "faranou-vendor-signer-test-" + Guid.NewGuid().ToString("N");
        var provisioningKeyName = licenseKeyName + "-p";
        var (licenseThumbprint, licenseSpki) = CreateUserCertificate(licenseKeyName, CngExportPolicies.None);
        var (provisioningThumbprint, _) = CreateUserCertificate(provisioningKeyName, CngExportPolicies.None);
        var options = TestOptions(licenseThumbprint, provisioningThumbprint);
        var module = CreateModule(options);
        var audit = new RecordingAuditWriter();
        var licenseProvider = new WindowsCngLicenseSigningProvider(
            module,
            Options.Create(options),
            audit,
            NullLogger<WindowsCngLicenseSigningProvider>.Instance);
        var provisioningProvider = new WindowsCngProvisioningSigningProvider(
            module,
            Options.Create(options),
            audit,
            NullLogger<WindowsCngProvisioningSigningProvider>.Instance);

        try
        {
            var payload = Encoding.UTF8.GetBytes("{\"sequence\":1,\"canonical\":true}");
            var licenseResult = await licenseProvider.SignAsync(payload);
            var provisioningResult = await provisioningProvider.SignAsync(payload);

            Assert.True(licenseResult.Succeeded);
            Assert.Equal("vendor-license-v1", licenseResult.KeyId);
            Assert.Equal(LicenseAlgorithms.EcdsaP256Sha256, licenseResult.Algorithm);
            Assert.Null(licenseResult.ErrorCode);
            Assert.Equal(64, licenseResult.Signature.Length);
            Assert.True(audit.Requests.Exists(request =>
                request.ActionCode == "vendor.signing.license.vendor-license-v1.succeeded"));

            Assert.True(provisioningResult.Succeeded);
            Assert.Equal("vendor-provisioning-v1", provisioningResult.KeyId);
            Assert.True(audit.Requests.Exists(request =>
                request.ActionCode == "vendor.signing.provisioning.vendor-provisioning-v1.succeeded"));

            using var verifier = ECDsa.Create();
            verifier.ImportSubjectPublicKeyInfo(licenseSpki, out var bytesRead);
            Assert.Equal(licenseSpki.Length, bytesRead);
            Assert.True(verifier.VerifyData(
                payload,
                licenseResult.Signature.ToArray(),
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
            Assert.False(verifier.VerifyData(
                Encoding.UTF8.GetBytes("{\"sequence\":2,\"canonical\":true}"),
                licenseResult.Signature.ToArray(),
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation));

            using var openedKey = CngKey.Open(licenseKeyName, new CngProvider(SoftwareKsp));
            Assert.Equal(CngAlgorithm.ECDsaP256, openedKey.Algorithm);
            Assert.Equal(CngExportPolicies.None, openedKey.ExportPolicy);
            Assert.Throws<CryptographicException>(
                () => openedKey.Export(CngKeyBlobFormat.EccPrivateBlob));
        }
        finally
        {
            DeleteUserCertificate(licenseThumbprint, licenseKeyName);
            DeleteUserCertificate(provisioningThumbprint, provisioningKeyName);
        }
    }

    [Fact]
    public async Task ExportablePrivateKeyIsRejectedFailClosed()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Windows CNG is available only on Windows.");

        var keyName = "faranou-vendor-signer-exportable-" + Guid.NewGuid().ToString("N");
        var (thumbprint, _) = CreateUserCertificate(keyName, CngExportPolicies.AllowPlaintextExport);
        var options = TestOptions(thumbprint, new string('B', 40));
        var module = CreateModule(options);

        try
        {
            var result = await module.SignAsync(
                options.LicenseKey,
                Encoding.UTF8.GetBytes("{\"sequence\":1}"));

            Assert.False(result.Succeeded);
            Assert.Equal("signing_provider_unavailable", result.ErrorCode);
            Assert.False(await module.IsAvailableAsync());
        }
        finally
        {
            DeleteUserCertificate(thumbprint, keyName);
        }
    }

    [Fact]
    public async Task AllowExportOnlyPrivateKeyIsRejectedFailClosed()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Windows CNG is available only on Windows.");

        var keyName = "faranou-vendor-signer-allow-export-" + Guid.NewGuid().ToString("N");
        var (thumbprint, _) = CreateUserCertificate(keyName, CngExportPolicies.AllowExport);
        var options = TestOptions(thumbprint, new string('B', 40));
        var module = CreateModule(options);

        try
        {
            var result = await module.SignAsync(
                options.LicenseKey,
                Encoding.UTF8.GetBytes("{\"sequence\":1}"));

            Assert.False(result.Succeeded);
            Assert.Equal("signing_provider_unavailable", result.ErrorCode);
        }
        finally
        {
            DeleteUserCertificate(thumbprint, keyName);
        }
    }

    [Fact]
    public async Task MissingCertificateFailsClosedWithoutCreatingAnything()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Windows CNG is available only on Windows.");

        var options = TestOptions(new string('C', 40), new string('D', 40));
        var module = CreateModule(options);

        var result = await module.SignAsync(
            options.LicenseKey,
            Encoding.UTF8.GetBytes("{\"sequence\":1}"));

        Assert.False(result.Succeeded);
        Assert.Equal("signing_provider_unavailable", result.ErrorCode);
        Assert.False(await module.IsAvailableAsync());
    }

    [Fact]
    public async Task RsaCertificateIsRejectedFailClosed()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Windows CNG is available only on Windows.");

        var keyName = "faranou-vendor-signer-rsa-" + Guid.NewGuid().ToString("N");
        var thumbprint = CreateUserRsaCertificate(keyName);
        var options = TestOptions(thumbprint, new string('B', 40));
        var module = CreateModule(options);

        try
        {
            var result = await module.SignAsync(
                options.LicenseKey,
                Encoding.UTF8.GetBytes("{\"sequence\":1}"));

            Assert.False(result.Succeeded);
            Assert.Equal("signing_provider_unavailable", result.ErrorCode);
        }
        finally
        {
            DeleteUserCertificate(thumbprint, keyName);
        }
    }

    [Fact]
    public async Task ExpiredCertificateIsRejectedFailClosed()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Windows CNG is available only on Windows.");

        var keyName = "faranou-vendor-signer-expired-" + Guid.NewGuid().ToString("N");
        var (thumbprint, _) = CreateUserCertificate(
            keyName,
            CngExportPolicies.None,
            notBefore: DateTime.UtcNow.AddHours(-2),
            notAfter: DateTime.UtcNow.AddHours(-1));
        var options = TestOptions(thumbprint, new string('B', 40));
        var module = CreateModule(options);

        try
        {
            var result = await module.SignAsync(
                options.LicenseKey,
                Encoding.UTF8.GetBytes("{\"sequence\":1}"));

            Assert.False(result.Succeeded);
            Assert.Equal("signing_provider_unavailable", result.ErrorCode);
        }
        finally
        {
            DeleteUserCertificate(thumbprint, keyName);
        }
    }

    [Fact]
    public async Task HealthCheckReflectsConfiguredCertificates()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Windows CNG is available only on Windows.");

        var licenseKeyName = "faranou-vendor-signer-health-" + Guid.NewGuid().ToString("N");
        var provisioningKeyName = licenseKeyName + "-p";
        var (licenseThumbprint, _) = CreateUserCertificate(licenseKeyName, exportable: false);
        var (provisioningThumbprint, _) = CreateUserCertificate(provisioningKeyName, exportable: false);
        var missingOptions = TestOptions(new string('E', 40), new string('F', 40));
        var healthyOptions = TestOptions(licenseThumbprint, provisioningThumbprint);

        try
        {
            var missingModule = CreateModule(missingOptions);
            var missingCheck = new WindowsCngSigningHealthCheck(missingModule, Options.Create(missingOptions));
            var missingResult = await missingCheck.CheckHealthAsync(new HealthCheckContext());
            Assert.Equal(HealthStatus.Unhealthy, missingResult.Status);

            var healthyModule = CreateModule(healthyOptions);
            var healthyCheck = new WindowsCngSigningHealthCheck(healthyModule, Options.Create(healthyOptions));
            var healthyResult = await healthyCheck.CheckHealthAsync(new HealthCheckContext());
            Assert.Equal(HealthStatus.Healthy, healthyResult.Status);
            Assert.Equal(
                WindowsCngSigningOptions.RequiredAssuranceLevel,
                Assert.IsType<string>(healthyResult.Data["assuranceLevel"]));
        }
        finally
        {
            DeleteUserCertificate(licenseThumbprint, licenseKeyName);
            DeleteUserCertificate(provisioningThumbprint, provisioningKeyName);
        }
    }

    private static WindowsCngCertificateStoreSigningModule CreateModule(WindowsCngSigningOptions options) =>
        new(
            Options.Create(options),
            new TestHostEnvironment(Environments.Development),
            NullLogger<WindowsCngCertificateStoreSigningModule>.Instance);

    private static WindowsCngSigningOptions TestOptions(string licenseThumbprint, string provisioningThumbprint) => new()
    {
        AssuranceLevel = WindowsCngSigningOptions.RequiredAssuranceLevel,
        StoreLocation = "CurrentUser",
        StoreName = "My",
        LicenseKey = new WindowsCngSigningKeyOptions
        {
            KeyId = "vendor-license-v1",
            Thumbprint = licenseThumbprint
        },
        ProvisioningKey = new WindowsCngSigningKeyOptions
        {
            KeyId = "vendor-provisioning-v1",
            Thumbprint = provisioningThumbprint
        }
    };

    private static (string Thumbprint, byte[] PublicKeySpki) CreateUserCertificate(
        string keyName,
        CngExportPolicies exportPolicy,
        DateTime? notBefore = null,
        DateTime? notAfter = null)
    {
        var creationParameters = new CngKeyCreationParameters
        {
            ExportPolicy = exportPolicy,
            KeyCreationOptions = CngKeyCreationOptions.None
        };
        string thumbprint;
        byte[] publicKeySpki;
        using (var cngKey = CngKey.Create(CngAlgorithm.ECDsaP256, keyName, creationParameters))
        using (var ecdsa = new ECDsaCng(cngKey))
        {
            var request = new CertificateRequest("CN=Faranou Vendor CNG Test", ecdsa, HashAlgorithmName.SHA256);
            using var certificate = request.CreateSelfSigned(
                notBefore ?? DateTime.UtcNow.AddMinutes(-5),
                notAfter ?? DateTime.UtcNow.AddHours(1));
            thumbprint = certificate.Thumbprint;
            publicKeySpki = certificate.PublicKey.ExportSubjectPublicKeyInfo();
            using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
            {
                store.Open(OpenFlags.ReadWrite);
                store.Add(certificate);
            }
        }

        return (thumbprint, publicKeySpki);
    }

    private static string CreateUserRsaCertificate(string keyName)
    {
        // The RSA key is only a certificate-creation fixture; the signer must reject it.
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Faranou Vendor RSA Test",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(
            DateTime.UtcNow.AddMinutes(-5),
            DateTime.UtcNow.AddHours(1));
        using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
        {
            store.Open(OpenFlags.ReadWrite);
            store.Add(certificate);
        }

        return certificate.Thumbprint;
    }

    private static void DeleteUserCertificate(string thumbprint, string keyName)
    {
        try
        {
            using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);
            var found = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false);
            foreach (var certificate in found)
            {
                store.Remove(certificate);
                certificate.Dispose();
            }
        }
        catch (CryptographicException)
        {
            // A failed fixture may not have created a certificate.
        }

        try
        {
            using var key = CngKey.Open(keyName, new CngProvider(SoftwareKsp));
            key.Delete();
        }
        catch (CryptographicException)
        {
            // A failed fixture may not have created a key.
        }
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Faranou.Vendor.Host.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class RecordingAuditWriter : IVendorAuditWriter
    {
        public List<VendorAuditWriteRequest> Requests { get; } = [];

        public Task<VendorAuditWriteResult> AppendAsync(
            VendorAuditWriteRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(new VendorAuditWriteResult(
                Guid.NewGuid(),
                new string('a', 64),
                new string('b', 64)));
        }
    }
}
