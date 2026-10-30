using System.Security.Cryptography;
using Faranou.Vendor.Host.Signing;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class ProvisioningPackageSigningTrustStoreTests
{
    [Fact]
    public void ResolvesOnlyConfiguredActiveKeys()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var store = new ProvisioningPackageSigningTrustStore(
        [
            new ProvisioningPackageSigningTrustedKeyOptions
            {
                KeyId = "vendor-key-1",
                PublicKeySubjectPublicKeyInfoBase64 = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo())
            },
            new ProvisioningPackageSigningTrustedKeyOptions
            {
                KeyId = "retired-key",
                PublicKeySubjectPublicKeyInfoBase64 = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
                Revoked = true
            }
        ]);

        Assert.True(store.TryGetTrustedPublicKey("vendor-key-1", out var publicKey));
        Assert.NotEmpty(publicKey.ToArray());
        Assert.False(store.TryGetTrustedPublicKey("retired-key", out _));
        Assert.False(store.TryGetTrustedPublicKey("unknown-key", out _));
        Assert.False(store.TryGetTrustedPublicKey("vendor:key:1", out _));
    }

    [Fact]
    public void EmptyTrustSetFailsClosed()
    {
        var store = new ProvisioningPackageSigningTrustStore([]);

        Assert.False(store.TryGetTrustedPublicKey("vendor-key-1", out _));
    }

    [Fact]
    public void RejectsDuplicateKeyIds()
    {
        using var first = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var second = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var entries = new[]
        {
            new ProvisioningPackageSigningTrustedKeyOptions
            {
                KeyId = "duplicate",
                PublicKeySubjectPublicKeyInfoBase64 = Convert.ToBase64String(first.ExportSubjectPublicKeyInfo())
            },
            new ProvisioningPackageSigningTrustedKeyOptions
            {
                KeyId = "duplicate",
                PublicKeySubjectPublicKeyInfoBase64 = Convert.ToBase64String(second.ExportSubjectPublicKeyInfo())
            }
        };

        Assert.Throws<InvalidOperationException>(() => new ProvisioningPackageSigningTrustStore(entries));
    }

    [Fact]
    public void RejectsInvalidKeyIdMalformedEncodingAndNonP256Keys()
    {
        using var validKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var invalidId = new[]
        {
            new ProvisioningPackageSigningTrustedKeyOptions
            {
                KeyId = "vendor:key/1",
                PublicKeySubjectPublicKeyInfoBase64 = Convert.ToBase64String(validKey.ExportSubjectPublicKeyInfo())
            }
        };
        Assert.Throws<InvalidOperationException>(() => new ProvisioningPackageSigningTrustStore(invalidId));

        var malformed = new[]
        {
            new ProvisioningPackageSigningTrustedKeyOptions
            {
                KeyId = "vendor-key-1",
                PublicKeySubjectPublicKeyInfoBase64 = "not-base64"
            }
        };
        Assert.Throws<InvalidOperationException>(() => new ProvisioningPackageSigningTrustStore(malformed));

        using var wrongCurve = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        var nonP256 = new[]
        {
            new ProvisioningPackageSigningTrustedKeyOptions
            {
                KeyId = "vendor-key-1",
                PublicKeySubjectPublicKeyInfoBase64 = Convert.ToBase64String(wrongCurve.ExportSubjectPublicKeyInfo())
            }
        };
        Assert.Throws<InvalidOperationException>(() => new ProvisioningPackageSigningTrustStore(nonP256));
    }
}
