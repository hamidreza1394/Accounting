using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Faranou.Licensing.Contracts;
using Infrastructure.Provisioning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace Template.Tests;

public sealed class CustomerProvisioningPackageVerifierTests
{
    [Fact]
    public void ValidPackage_IsVerifiedAgainstIndependentTrustAndInstallationSettings()
    {
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var verifier = CreateVerifier(signingKey, "customer-001", "install-001");
        var package = Sign(signingKey, Sample("customer-001", "install-001"));

        Assert.True(verifier.TryVerify(package, out var payload, out var error), error);
        Assert.NotNull(payload);
        Assert.Equal("customer-001", payload!.CustomerReference);
        Assert.Equal("install-001", payload.InstallationId);
    }

    [Fact]
    public void UnknownKeyId_IsRejectedWithoutTrustingPackageKeyMaterial()
    {
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var verifier = CreateVerifier(signingKey, "customer-001", "install-001");
        var package = Sign(signingKey, Sample("customer-001", "install-001")) with { KeyId = "attacker-key" };

        Assert.False(verifier.TryVerify(package, out var payload, out var error));
        Assert.Null(payload);
        Assert.Equal("provisioning_trusted_key_unknown", error);
    }

    [Fact]
    public void WrongCustomerReference_IsRejectedAfterSignatureVerification()
    {
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var verifier = CreateVerifier(signingKey, "customer-001", "install-001");
        var package = Sign(signingKey, Sample("customer-other", "install-001"));

        Assert.False(verifier.TryVerify(package, out var payload, out var error));
        Assert.Null(payload);
        Assert.Equal("provisioning_customer_mismatch", error);
    }

    [Fact]
    public void WrongInstallation_IsRejectedAfterSignatureVerification()
    {
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var verifier = CreateVerifier(signingKey, "customer-001", "install-001");
        var package = Sign(signingKey, Sample("customer-001", "install-other"));

        Assert.False(verifier.TryVerify(package, out var payload, out var error));
        Assert.Null(payload);
        Assert.Equal("provisioning_installation_mismatch", error);
    }

    [Fact]
    public void ExpiredPackage_IsRejected()
    {
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var verifier = CreateVerifier(signingKey, "customer-001", "install-001");
        var now = DateTimeOffset.UtcNow;
        var package = Sign(signingKey, Sample("customer-001", "install-001") with
        {
            IssuedAtUtc = now.AddHours(-25),
            ExpiresAtUtc = now.AddHours(-1)
        });

        Assert.False(verifier.TryVerify(package, out var payload, out var error));
        Assert.Null(payload);
        Assert.Equal("provisioning_payload_invalid", error);
    }

    [Fact]
    public void TamperedPayload_IsRejectedBeforeAnyConsumerStateCanBeApplied()
    {
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var verifier = CreateVerifier(signingKey, "customer-001", "install-001");
        var package = Sign(signingKey, Sample("customer-001", "install-001")) with
        {
            PayloadJson = Sign(signingKey, Sample("customer-001", "install-001")).PayloadJson
                .Replace("Company Example", "Other Company", StringComparison.Ordinal)
        };

        Assert.False(verifier.TryVerify(package, out var payload, out var error));
        Assert.Null(payload);
        Assert.Equal("provisioning_signature_invalid", error);
    }

    [Fact]
    public void MissingExpectedInstallationIdentity_FailsClosed()
    {
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var verifier = CreateVerifier(signingKey, " ", "install-001");
        var package = Sign(signingKey, Sample("customer-001", "install-001"));

        Assert.False(verifier.TryVerify(package, out var payload, out var error));
        Assert.Null(payload);
        Assert.Equal("provisioning_consumer_not_configured", error);
    }

    [Fact]
    public void MalformedConfiguredPublicKey_IsNotAcceptedAsTrusted()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Provisioning:TrustedVendorKeys:vendor-key-1"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("not-a-public-key"))
            })
            .Build();
        var trustStore = new ConfigurationCustomerProvisioningTrustStore(configuration);

        Assert.False(trustStore.TryGetPublicKey("vendor-key-1", out var key));
        Assert.Empty(key);
    }

    private static CustomerProvisioningPackageVerifier CreateVerifier(
        ECDsa signingKey,
        string customerReference,
        string installationId)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Provisioning:TrustedVendorKeys:vendor-key-1"] =
                    Convert.ToBase64String(signingKey.ExportSubjectPublicKeyInfo())
            })
            .Build();
        return new CustomerProvisioningPackageVerifier(
            new ConfigurationCustomerProvisioningTrustStore(configuration),
            Options.Create(new CustomerProvisioningSettings
            {
                CustomerReference = customerReference,
                InstallationId = installationId
            }),
            TimeProvider.System);
    }

    private static CustomerProvisioningPackage Sign(
        ECDsa signingKey,
        CustomerProvisioningPayload payload)
    {
        var payloadJson = CustomerProvisioningCanonicalJson.Serialize(payload);
        var signature = signingKey.SignData(
            Encoding.UTF8.GetBytes(payloadJson),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return new CustomerProvisioningPackage(
            CustomerProvisioningPackageValidator.CurrentFormatVersion,
            "vendor-key-1",
            LicenseAlgorithms.EcdsaP256Sha256,
            payloadJson,
            Convert.ToBase64String(signature));
    }

    private static CustomerProvisioningPayload Sample(string customerReference, string installationId)
    {
        var now = DateTimeOffset.UtcNow;
        return new CustomerProvisioningPayload(
            CustomerProvisioningPackageValidator.CurrentPayloadSchemaVersion,
            Guid.NewGuid().ToString("D"),
            customerReference,
            installationId,
            "Company Example",
            "independent",
            "initial-admin",
            new[] { "finance", "inventory" },
            now.AddMinutes(-1),
            now.AddHours(1),
            "0123456789abcdefghijklmnopqrstuv",
            "سال مالی ۱۴۰۵",
            new DateOnly(2026, 3, 21),
            new DateOnly(2027, 3, 20),
            null);
    }
}
