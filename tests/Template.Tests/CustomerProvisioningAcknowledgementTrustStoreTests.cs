using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Infrastructure.Provisioning;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Template.Tests;

public sealed class CustomerProvisioningAcknowledgementTrustStoreTests
{
    [Fact]
    public void TrustedAcknowledgementKey_IsReturnedUntilExplicitlyRevoked()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        const string keyId = "customer-ack-installation-a-v1";
        var publicKey = key.ExportSubjectPublicKeyInfo();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ConfigurationCustomerProvisioningAcknowledgementTrustStore.SectionPath + ":" + keyId] =
                    Convert.ToBase64String(publicKey),
            })
            .Build();
        var trustStore = new ConfigurationCustomerProvisioningAcknowledgementTrustStore(configuration);

        Assert.True(trustStore.TryGetPublicKey(keyId, out var trusted));
        Assert.Equal(publicKey, trusted);
    }

    [Fact]
    public void RevokedAcknowledgementKey_IsRejectedEvenWhileItsPublicKeyRemainsConfigured()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        const string keyId = "customer-ack-installation-a-v1";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ConfigurationCustomerProvisioningAcknowledgementTrustStore.SectionPath + ":" + keyId] =
                    Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
                [ConfigurationCustomerProvisioningAcknowledgementTrustStore.RevokedSectionPath + ":" + keyId] = "true",
            })
            .Build();
        var trustStore = new ConfigurationCustomerProvisioningAcknowledgementTrustStore(configuration);

        Assert.False(trustStore.TryGetPublicKey(keyId, out var rejected));
        Assert.Empty(rejected);
    }

    [Fact]
    public void MalformedRevocationFlag_FailsClosed()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        const string keyId = "customer-ack-installation-a-v1";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ConfigurationCustomerProvisioningAcknowledgementTrustStore.SectionPath + ":" + keyId] =
                    Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
                [ConfigurationCustomerProvisioningAcknowledgementTrustStore.RevokedSectionPath + ":" + keyId] = "not-a-boolean",
            })
            .Build();
        var trustStore = new ConfigurationCustomerProvisioningAcknowledgementTrustStore(configuration);

        Assert.False(trustStore.TryGetPublicKey(keyId, out var rejected));
        Assert.Empty(rejected);
    }

    [Fact]
    public void Rotation_AllowsNewVersionWhileOldVersionIsRevoked()
    {
        using var oldKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var newKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        const string oldKeyId = "customer-ack-installation-a-v1";
        const string newKeyId = "customer-ack-installation-a-v2";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ConfigurationCustomerProvisioningAcknowledgementTrustStore.SectionPath + ":" + oldKeyId] =
                    Convert.ToBase64String(oldKey.ExportSubjectPublicKeyInfo()),
                [ConfigurationCustomerProvisioningAcknowledgementTrustStore.SectionPath + ":" + newKeyId] =
                    Convert.ToBase64String(newKey.ExportSubjectPublicKeyInfo()),
                [ConfigurationCustomerProvisioningAcknowledgementTrustStore.RevokedSectionPath + ":" + oldKeyId] = "true",
            })
            .Build();
        var trustStore = new ConfigurationCustomerProvisioningAcknowledgementTrustStore(configuration);

        Assert.False(trustStore.TryGetPublicKey(oldKeyId, out _));
        Assert.True(trustStore.TryGetPublicKey(newKeyId, out var trustedNewKey));
        Assert.Equal(newKey.ExportSubjectPublicKeyInfo(), trustedNewKey);
    }
}
