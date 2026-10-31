using System.Security.Cryptography;
using Faranou.Vendor.Host.Signing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorOfflineRevocationKeyResolverTests
{
    [Fact]
    public void ResolvesOnlyKeysFromDedicatedOfflineRevocationSection()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var spki = key.ExportSubjectPublicKeyInfo();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Licensing:OfflineRevocation:TrustedKeys:vendor-revocation-2026-v1"] =
                    Convert.ToBase64String(spki),
                ["Licensing:TrustedLicenseKeys:vendor-license-2026-v1"] =
                    Convert.ToBase64String(spki)
            })
            .Build();
        var resolver = new VendorOfflineRevocationKeyResolver(configuration);

        Assert.True(resolver.TryResolve("vendor-revocation-2026-v1", out var resolved));
        Assert.Equal(spki, resolved.ToArray());
        Assert.False(resolver.TryResolve("vendor-license-2026-v1", out _));
    }

    [Fact]
    public void RejectsMalformedConfiguredPublicKeyAtConstruction()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Licensing:OfflineRevocation:TrustedKeys:vendor-revocation-2026-v1"] = "not-base64"
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() => new VendorOfflineRevocationKeyResolver(configuration));
    }

    [Fact]
    public void RejectsNonP256ConfiguredPublicKeyAtConstruction()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Licensing:OfflineRevocation:TrustedKeys:vendor-revocation-2026-v1"] =
                    Convert.ToBase64String(key.ExportSubjectPublicKeyInfo())
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() => new VendorOfflineRevocationKeyResolver(configuration));
    }
}
