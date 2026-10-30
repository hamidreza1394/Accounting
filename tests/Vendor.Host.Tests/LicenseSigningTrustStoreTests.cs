using System.Security.Cryptography;
using Faranou.Vendor.Host.Signing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class LicenseSigningTrustStoreTests
{
    [Fact]
    public void MalformedRevocationStateFailsClosed()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Licensing:TrustedSigningKeys:test-key:PublicKeySubjectPublicKeyInfoBase64"] =
                    Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
                ["Licensing:TrustedSigningKeys:test-key:Revoked"] = "not-a-boolean"
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() => new LicenseSigningTrustStore(configuration));
    }

    [Fact]
    public void UnknownAndRevokedKeysAreNotTrusted()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Licensing:TrustedSigningKeys:revoked-key:PublicKeySubjectPublicKeyInfoBase64"] =
                    Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
                ["Licensing:TrustedSigningKeys:revoked-key:Revoked"] = "true"
            })
            .Build();
        var trustStore = new LicenseSigningTrustStore(configuration);

        Assert.False(trustStore.TryGetTrustedPublicKey("missing-key", out _));
        Assert.False(trustStore.TryGetTrustedPublicKey("revoked-key", out _));
    }
}
