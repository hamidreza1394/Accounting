using System.Text;
using Faranou.Licensing.Contracts;
using Faranou.Vendor.Host.Signing;
using Faranou.Vendor.Persistence.Auditing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class WindowsCngSigningProviderTests
{
    [Fact]
    public void ValidOptionsPassValidation()
    {
        var options = ValidOptions();
        options.Validate(isProduction: false, isWindowsHost: true);
        options.Validate(isProduction: true, isWindowsHost: true);
    }

    [Fact]
    public void NonWindowsHostIsRejected()
    {
        var options = ValidOptions();
        Assert.Throws<InvalidOperationException>(
            () => options.Validate(isProduction: false, isWindowsHost: false));
    }

    [Fact]
    public void AssuranceLevelMustBeExactlySoftwareProtected()
    {
        var options = ValidOptions();
        options.AssuranceLevel = "Software";
        Assert.Throws<InvalidOperationException>(
            () => options.Validate(isProduction: false, isWindowsHost: true));

        options.AssuranceLevel = "Hardware";
        Assert.Throws<InvalidOperationException>(
            () => options.Validate(isProduction: false, isWindowsHost: true));

        options.AssuranceLevel = WindowsCngSigningOptions.RequiredAssuranceLevel;
        options.Validate(isProduction: false, isWindowsHost: true);
    }

    [Fact]
    public void ProductionRequiresTheMachineCertificateStore()
    {
        var options = ValidOptions();
        options.StoreLocation = "CurrentUser";
        Assert.Throws<InvalidOperationException>(
            () => options.Validate(isProduction: true, isWindowsHost: true));
        options.Validate(isProduction: false, isWindowsHost: true);

        options.StoreLocation = "LocalMachine";
        options.Validate(isProduction: true, isWindowsHost: true);
    }

    [Fact]
    public void UnsupportedStoreSettingsAreRejected()
    {
        var options = ValidOptions();

        options.StoreName = "TrustedPeople";
        Assert.Throws<InvalidOperationException>(
            () => options.Validate(isProduction: false, isWindowsHost: true));

        options.StoreName = "My";
        options.StoreLocation = "FileSystem";
        Assert.Throws<InvalidOperationException>(
            () => options.Validate(isProduction: false, isWindowsHost: true));
    }

    [Fact]
    public void MalformedThumbprintsAreRejected()
    {
        var options = ValidOptions();

        options.LicenseKey.Thumbprint = "not-a-thumbprint";
        Assert.Throws<InvalidOperationException>(
            () => options.Validate(isProduction: false, isWindowsHost: true));

        options.LicenseKey.Thumbprint = new string('A', 39);
        Assert.Throws<InvalidOperationException>(
            () => options.Validate(isProduction: false, isWindowsHost: true));

        options.LicenseKey.Thumbprint = new string('A', 40);
        options.Validate(isProduction: false, isWindowsHost: true);
    }

    [Fact]
    public void PurposeKeysMustStayDistinct()
    {
        var options = ValidOptions();

        options.ProvisioningKey.KeyId = options.LicenseKey.KeyId;
        Assert.Throws<InvalidOperationException>(
            () => options.Validate(isProduction: false, isWindowsHost: true));

        options.ProvisioningKey.KeyId = "vendor-provisioning-v1";
        options.ProvisioningKey.Thumbprint = options.LicenseKey.Thumbprint;
        Assert.Throws<InvalidOperationException>(
            () => options.Validate(isProduction: false, isWindowsHost: true));

        options.ProvisioningKey.Thumbprint = new string('B', 40);
        options.Validate(isProduction: false, isWindowsHost: true);
    }

    [Fact]
    public void KeyIdsMustBeVersionedLowercaseIdentifiers()
    {
        var options = ValidOptions();

        options.LicenseKey.KeyId = "vendor-license";
        Assert.Throws<InvalidOperationException>(
            () => options.Validate(isProduction: false, isWindowsHost: true));

        options.LicenseKey.KeyId = "Vendor-License-v1";
        Assert.Throws<InvalidOperationException>(
            () => options.Validate(isProduction: false, isWindowsHost: true));

        options.LicenseKey.KeyId = "vendor-license-v0";
        Assert.Throws<InvalidOperationException>(
            () => options.Validate(isProduction: false, isWindowsHost: true));

        options.LicenseKey.KeyId = "vendor-license-v1";
        options.Validate(isProduction: false, isWindowsHost: true);
    }

    [Fact]
    public async Task LicenseAndProvisioningProvidersUseTheirOwnPurposeKeys()
    {
        var module = new RecordingWindowsCngSigningModule(LicenseSigningResult.Success(
            "vendor-license-v1",
            LicenseAlgorithms.EcdsaP256Sha256,
            stackalloc byte[64].ToArray()));
        var options = ValidOptions();
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

        var licenseResult = await licenseProvider.SignAsync(Encoding.UTF8.GetBytes("{}"));
        var provisioningResult = await provisioningProvider.SignAsync(Encoding.UTF8.GetBytes("{}"));

        Assert.True(licenseResult.Succeeded);
        Assert.True(provisioningResult.Succeeded);
        Assert.Equal(options.LicenseKey, module.RequestedKeys[0]);
        Assert.Equal(options.ProvisioningKey, module.RequestedKeys[1]);
        Assert.Equal("vendor.signing.license.vendor-license-v1.succeeded", audit.Requests[0].ActionCode);
        Assert.Equal(
            "vendor.signing.provisioning.vendor-provisioning-v1.succeeded",
            audit.Requests[1].ActionCode);
    }

    [Fact]
    public async Task AuditFailureDiscardsTheSignatureAndFailsClosed()
    {
        var module = new RecordingWindowsCngSigningModule(LicenseSigningResult.Success(
            "vendor-license-v1",
            LicenseAlgorithms.EcdsaP256Sha256,
            stackalloc byte[64].ToArray()));
        var audit = new RecordingAuditWriter { Fail = true };
        var provider = new WindowsCngLicenseSigningProvider(
            module,
            Options.Create(ValidOptions()),
            audit,
            NullLogger<WindowsCngLicenseSigningProvider>.Instance);

        var result = await provider.SignAsync(Encoding.UTF8.GetBytes("{}"));

        Assert.False(result.Succeeded);
        Assert.Equal("signing_provider_unavailable", result.ErrorCode);
    }

    [Fact]
    public async Task ProductionAssuranceIsAuditedBeforeStartupCompletes()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Windows CNG is available only on Windows.");

        var options = ValidOptions();
        var audit = new RecordingAuditWriter();
        var services = new ServiceCollection();
        services.AddScoped<IVendorAuditWriter>(_ => audit);
        await using var provider = services.BuildServiceProvider();
        var service = new WindowsCngSigningAssuranceStartupService(
            Options.Create(options),
            new TestHostEnvironment(Environments.Production),
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<WindowsCngSigningAssuranceStartupService>.Instance);

        await service.StartAsync(CancellationToken.None);
        Assert.Equal(
            "vendor.signing.assurance.software-protected-selected",
            audit.LastRequest?.ActionCode);
    }

    [Fact]
    public async Task NonWindowsHostFailsStartupFast()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "This guard behaviour applies to non-Windows hosts only.");

        var services = new ServiceCollection();
        await using var provider = services.BuildServiceProvider();
        var service = new WindowsCngSigningAssuranceStartupService(
            Options.Create(ValidOptions()),
            new TestHostEnvironment(Environments.Development),
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<WindowsCngSigningAssuranceStartupService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.StartAsync(CancellationToken.None));
    }

    private static WindowsCngSigningOptions ValidOptions() => new()
    {
        AssuranceLevel = WindowsCngSigningOptions.RequiredAssuranceLevel,
        StoreLocation = "LocalMachine",
        StoreName = "My",
        LicenseKey = new WindowsCngSigningKeyOptions
        {
            KeyId = "vendor-license-v1",
            Thumbprint = new string('A', 40)
        },
        ProvisioningKey = new WindowsCngSigningKeyOptions
        {
            KeyId = "vendor-provisioning-v1",
            Thumbprint = new string('B', 40)
        }
    };

    private sealed class RecordingWindowsCngSigningModule(LicenseSigningResult result)
        : IWindowsCngSigningModule
    {
        public List<WindowsCngSigningKeyOptions> RequestedKeys { get; } = [];
        public WindowsCngSigningKeyOptions? LastRequestedKey => RequestedKeys.Count > 0 ? RequestedKeys[^1] : null;

        public ValueTask<LicenseSigningResult> SignAsync(
            WindowsCngSigningKeyOptions key,
            ReadOnlyMemory<byte> canonicalPayload,
            CancellationToken cancellationToken = default)
        {
            RequestedKeys.Add(key);
            return ValueTask.FromResult(result);
        }

        public ValueTask<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(true);
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
        public VendorAuditWriteRequest? LastRequest => Requests.Count > 0 ? Requests[^1] : null;
        public bool Fail { get; init; }

        public Task<VendorAuditWriteResult> AppendAsync(
            VendorAuditWriteRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (Fail)
            {
                throw new InvalidOperationException("simulated audit failure");
            }

            return Task.FromResult(
                new VendorAuditWriteResult(Guid.NewGuid(), new string('a', 64), new string('b', 64)));
        }
    }
}
