using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Faranou.Licensing.Contracts;
using Infrastructure.Licensing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Template.Tests.Infrastructure;

public sealed class ConfigurationLicenseTrustStoreTests
{
    [Fact]
    public void TryResolve_ReturnsOnlyConfiguredP256PublicKey()
    {
        using var keyPair = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var keyId = "license-signing-2026";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"Licensing:TrustedLicenseKeys:{keyId}"] = Convert.ToBase64String(keyPair.ExportSubjectPublicKeyInfo())
            })
            .Build();
        var store = new ConfigurationLicenseTrustStore(configuration);

        Assert.True(store.TryResolve(keyId, out var trusted));
        Assert.NotNull(trusted);
        Assert.Equal(keyId, trusted!.Kid);
        Assert.Equal(LicenseAlgorithms.EcdsaP256Sha256, trusted.Algorithm);
        Assert.False(trusted.Retired);
    }

    [Fact]
    public void TryResolve_RejectsUnknownAndMalformedRetirementConfiguration()
    {
        using var keyPair = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var keyId = "license-signing-2026";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"Licensing:TrustedLicenseKeys:{keyId}"] = Convert.ToBase64String(keyPair.ExportSubjectPublicKeyInfo()),
                [$"Licensing:RetiredLicenseKeys:{keyId}"] = "not-a-boolean"
            })
            .Build();
        var store = new ConfigurationLicenseTrustStore(configuration);

        Assert.False(store.TryResolve("unknown-key", out _));
        Assert.False(store.TryResolve(keyId, out _));
    }

    [Fact]
    public void TryResolve_PreservesExplicitRetiredKeyStatus()
    {
        using var keyPair = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var keyId = "license-signing-2025";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"Licensing:TrustedLicenseKeys:{keyId}"] = Convert.ToBase64String(keyPair.ExportSubjectPublicKeyInfo()),
                [$"Licensing:RetiredLicenseKeys:{keyId}"] = "true"
            })
            .Build();
        var store = new ConfigurationLicenseTrustStore(configuration);

        Assert.True(store.TryResolve(keyId, out var trusted));
        Assert.NotNull(trusted);
        Assert.True(trusted!.Retired);
    }
}
