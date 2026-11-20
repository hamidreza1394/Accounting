using System.Text.Json;
using Faranou.Vendor.Host.Signing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class WindowsCngSigningCertificateCommandTests
{
    private static readonly IConfigurationRoot EmptyConfiguration =
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

    [Fact]
    public async Task UnknownOrMissingActionPrintsUsageAndExits2()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await WindowsCngSigningCertificateCommand.RunAsync(
            new[] { WindowsCngSigningCertificateCommand.CommandName, "sign-private" },
            EmptyConfiguration,
            output,
            error);

        Assert.Equal(2, exitCode);
        Assert.Empty(output.ToString());
        Assert.Contains("Usage", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("private", error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvalidConfigurationExits2WithoutStoreAccess()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await WindowsCngSigningCertificateCommand.RunAsync(
            new[] { WindowsCngSigningCertificateCommand.CommandName, WindowsCngSigningCertificateCommand.ExportPublicAction },
            EmptyConfiguration,
            output,
            error);

        Assert.Equal(2, exitCode);
        Assert.Empty(output.ToString());
        Assert.Contains("configuration is invalid", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NonWindowsHostWithValidConfigurationExits2()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "This guard behaviour applies to non-Windows hosts only.");

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Signing:CertificateStore:AssuranceLevel"] = "SoftwareProtected",
                ["Signing:CertificateStore:StoreLocation"] = "LocalMachine",
                ["Signing:CertificateStore:StoreName"] = "My",
                ["Signing:CertificateStore:LicenseKey:KeyId"] = "vendor-license-v1",
                ["Signing:CertificateStore:LicenseKey:Thumbprint"] = new string('A', 40),
                ["Signing:CertificateStore:ProvisioningKey:KeyId"] = "vendor-provisioning-v1",
                ["Signing:CertificateStore:ProvisioningKey:Thumbprint"] = new string('B', 40)
            }).Build();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await WindowsCngSigningCertificateCommand.RunAsync(
            new[] { WindowsCngSigningCertificateCommand.CommandName, WindowsCngSigningCertificateCommand.ExportPublicAction },
            configuration,
            output,
            error);

        Assert.Equal(2, exitCode);
        Assert.Empty(output.ToString());
        Assert.Contains("Windows host", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void IsRequestedOnlyMatchesTheExactCommandName()
    {
        Assert.True(WindowsCngSigningCertificateCommand.IsRequested(new[] { "--signing-certificate", "export-public" }));
        Assert.False(WindowsCngSigningCertificateCommand.IsRequested(Array.Empty<string>()));
        Assert.False(WindowsCngSigningCertificateCommand.IsRequested(new[] { "bootstrap-admin" }));
    }
}
