using System.Security.Cryptography;
using Faranou.Vendor.Host.Signing;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class ProvisioningAcknowledgementTrustStoreTests
{
    [Fact]
    public void ResolvesOnlyConfiguredActiveKeyIds()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var store = new ProvisioningAcknowledgementTrustStore(
        [
            new ProvisioningAcknowledgementTrustedKeyOptions
            {
                KeyId = "customer-key-1",
                PublicKeySubjectPublicKeyInfoBase64 = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
                Revoked = false
            },
            new ProvisioningAcknowledgementTrustedKeyOptions
            {
                KeyId = "retired-key",
                PublicKeySubjectPublicKeyInfoBase64 = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
                Revoked = true
            }
        ]);

        Assert.True(store.TryGetTrustedPublicKey("customer-key-1", out var publicKey));
        Assert.NotEmpty(publicKey.ToArray());
        Assert.False(store.TryGetTrustedPublicKey("retired-key", out _));
        Assert.False(store.TryGetTrustedPublicKey("untrusted-key", out _));
    }

    [Fact]
    public void MissingConfigurationFailsClosed()
    {
        var store = new ProvisioningAcknowledgementTrustStore([]);

        Assert.False(store.TryGetTrustedPublicKey("any-key", out _));
    }

    [Fact]
    public void RejectsDuplicateKeyIdsInsteadOfChoosingAnArbitraryKey()
    {
        using var first = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var second = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var keys = new[]
        {
            new ProvisioningAcknowledgementTrustedKeyOptions
            {
                KeyId = "duplicate",
                PublicKeySubjectPublicKeyInfoBase64 = Convert.ToBase64String(first.ExportSubjectPublicKeyInfo())
            },
            new ProvisioningAcknowledgementTrustedKeyOptions
            {
                KeyId = "duplicate",
                PublicKeySubjectPublicKeyInfoBase64 = Convert.ToBase64String(second.ExportSubjectPublicKeyInfo())
            }
        };

        Assert.Throws<InvalidOperationException>(() => new ProvisioningAcknowledgementTrustStore(keys));
    }

    [Fact]
    public void RejectsMalformedAndNonP256PublicKeysAtConstruction()
    {
        var malformed = new[]
        {
            new ProvisioningAcknowledgementTrustedKeyOptions
            {
                KeyId = "bad",
                PublicKeySubjectPublicKeyInfoBase64 = "not-base64"
            }
        };
        Assert.Throws<InvalidOperationException>(() => new ProvisioningAcknowledgementTrustStore(malformed));

        var missing = new[]
        {
            new ProvisioningAcknowledgementTrustedKeyOptions
            {
                KeyId = "missing-material",
                PublicKeySubjectPublicKeyInfoBase64 = ""
            }
        };
        Assert.Throws<InvalidOperationException>(() => new ProvisioningAcknowledgementTrustStore(missing));

        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        var wrongCurve = new[]
        {
            new ProvisioningAcknowledgementTrustedKeyOptions
            {
                KeyId = "wrong-curve",
                PublicKeySubjectPublicKeyInfoBase64 = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo())
            }
        };
        Assert.Throws<InvalidOperationException>(() => new ProvisioningAcknowledgementTrustStore(wrongCurve));
    }
}
