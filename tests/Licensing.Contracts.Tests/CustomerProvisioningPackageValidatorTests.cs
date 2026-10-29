using System.Security.Cryptography;
using Faranou.Licensing.Contracts;
using Xunit;

namespace Licensing.Contracts.Tests;

public sealed class CustomerProvisioningPackageValidatorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ValidPackage_VerifiesExactCanonicalPayload()
    {
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var payload = Sample();
        var json = CustomerProvisioningCanonicalJson.Serialize(payload);
        var signature = signingKey.SignData(
            System.Text.Encoding.UTF8.GetBytes(json),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        var package = new CustomerProvisioningPackage(
            1, "provisioning-key-1", LicenseAlgorithms.EcdsaP256Sha256, json, Convert.ToBase64String(signature));

        var valid = CustomerProvisioningPackageValidator.TryValidateAndVerify(
            package, signingKey.ExportSubjectPublicKeyInfo(), Now, out var parsed, out var error);

        Assert.True(valid, error);
        Assert.NotNull(parsed);
        Assert.Equal(payload.RequestId, parsed!.RequestId);
        Assert.Equal(new[] { "finance", "inventory" }, parsed.Subsystems.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ModifiedPayload_FailsSignatureVerification()
    {
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var json = CustomerProvisioningCanonicalJson.Serialize(Sample());
        var signature = signingKey.SignData(
            System.Text.Encoding.UTF8.GetBytes(json),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        var package = new CustomerProvisioningPackage(
            1, "provisioning-key-1", LicenseAlgorithms.EcdsaP256Sha256,
            json.Replace("Company Example", "Other Company", StringComparison.Ordinal),
            Convert.ToBase64String(signature));

        Assert.False(CustomerProvisioningPackageValidator.TryValidateAndVerify(
            package, signingKey.ExportSubjectPublicKeyInfo(), Now, out _, out var error));
        Assert.Contains(error, new[] { "provisioning_payload_noncanonical", "provisioning_signature_invalid" });
    }

    [Fact]
    public void InvalidSignature_IsRejectedBeforePayloadParsing()
    {
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var validJson = CustomerProvisioningCanonicalJson.Serialize(Sample());
        var signature = signingKey.SignData(
            System.Text.Encoding.UTF8.GetBytes(validJson),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        var package = new CustomerProvisioningPackage(
            1, "provisioning-key-1", LicenseAlgorithms.EcdsaP256Sha256,
            "not-json", Convert.ToBase64String(signature));

        Assert.False(CustomerProvisioningPackageValidator.TryValidateAndVerify(
            package, signingKey.ExportSubjectPublicKeyInfo(), Now, out var parsed, out var error));
        Assert.Null(parsed);
        Assert.Equal("provisioning_signature_invalid", error);
    }

    [Fact]
    public void ExpiredPackage_IsRejected()
    {
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var payload = Sample() with
        {
            IssuedAtUtc = Now.AddHours(-25),
            ExpiresAtUtc = Now.AddHours(-1)
        };
        var package = Sign(signingKey, payload);

        Assert.False(CustomerProvisioningPackageValidator.TryValidateAndVerify(
            package, signingKey.ExportSubjectPublicKeyInfo(), Now, out _, out var error));
        Assert.Equal("provisioning_payload_invalid", error);
    }

    [Fact]
    public void DuplicateSubsystems_AreRejected()
    {
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var package = Sign(signingKey, Sample() with { Subsystems = new[] { "finance", "finance" } });

        Assert.False(CustomerProvisioningPackageValidator.TryValidateAndVerify(
            package, signingKey.ExportSubjectPublicKeyInfo(), Now, out _, out var error));
        Assert.Equal("provisioning_payload_invalid", error);
    }

    [Fact]
    public void PayloadValidation_AcceptsValidCanonicalPayloadWithoutRequiringSignature()
    {
        var json = CustomerProvisioningCanonicalJson.Serialize(Sample());

        Assert.True(CustomerProvisioningPackageValidator.TryValidatePayload(
            json, Now, out var parsed, out var error), error);
        Assert.NotNull(parsed);
        Assert.Equal(Sample().RequestId, parsed!.RequestId);
    }

    [Fact]
    public void PayloadValidation_RejectsInvalidCompanyType()
    {
        var json = CustomerProvisioningCanonicalJson.Serialize(Sample() with { CompanyType = "unknown" });

        Assert.False(CustomerProvisioningPackageValidator.TryValidatePayload(
            json, Now, out var parsed, out var error));
        Assert.Null(parsed);
        Assert.Equal("provisioning_payload_invalid", error);
    }

    [Fact]
    public void PayloadValidation_RejectsOversizedPayload()
    {
        var oversized = new string(' ', CustomerProvisioningPackageValidator.MaxPayloadJsonChars + 1);

        Assert.False(CustomerProvisioningPackageValidator.TryValidatePayload(
            oversized, Now, out var parsed, out var error));
        Assert.Null(parsed);
        Assert.Equal("provisioning_payload_invalid", error);
    }

    [Fact]
    public void PayloadValidation_RejectsUnknownSubsystemWithoutRequiringSignature()
    {
        var json = CustomerProvisioningCanonicalJson.Serialize(
            Sample() with { Subsystems = new[] { "future-module" } });

        Assert.False(CustomerProvisioningPackageValidator.TryValidatePayload(
            json, Now, out var parsed, out var error));
        Assert.Null(parsed);
        Assert.Equal("provisioning_payload_invalid", error);
    }

    [Fact]
    public void PayloadValidation_RejectsExpiredPayloadWithoutRequiringSignature()
    {
        var json = CustomerProvisioningCanonicalJson.Serialize(Sample() with
        {
            IssuedAtUtc = Now.AddHours(-25),
            ExpiresAtUtc = Now.AddHours(-1)
        });

        Assert.False(CustomerProvisioningPackageValidator.TryValidatePayload(
            json, Now, out var parsed, out var error));
        Assert.Null(parsed);
        Assert.Equal("provisioning_payload_invalid", error);
    }

    [Fact]
    public void PayloadValidation_RejectsNonCanonicalJson()
    {
        var canonical = CustomerProvisioningCanonicalJson.Serialize(Sample());
        var nonCanonical = canonical.Replace("\"Company Example\"", "\"Company Example\", \"extra\": true", StringComparison.Ordinal);

        Assert.False(CustomerProvisioningPackageValidator.TryValidatePayload(
            nonCanonical, Now, out var parsed, out var error));
        Assert.Null(parsed);
        Assert.Equal("provisioning_payload_noncanonical", error);
    }

    [Fact]
    public void UnknownSubsystem_IsRejected()
    {
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var package = Sign(signingKey, Sample() with { Subsystems = new[] { "future-module" } });

        Assert.False(CustomerProvisioningPackageValidator.TryValidateAndVerify(
            package, signingKey.ExportSubjectPublicKeyInfo(), Now, out _, out var error));
        Assert.Equal("provisioning_payload_invalid", error);
    }

    [Fact]
    public void UnknownVersion_IsRejected()
    {
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var package = Sign(signingKey, Sample() with { SchemaVersion = 2 });

        Assert.False(CustomerProvisioningPackageValidator.TryValidateAndVerify(
            package, signingKey.ExportSubjectPublicKeyInfo(), Now, out _, out var error));
        Assert.Equal("provisioning_payload_invalid", error);
    }

    [Fact]
    public void PlaintextPasswordIsNotPartOfProvisioningPayloadContract()
    {
        var names = typeof(CustomerProvisioningPayload).GetProperties().Select(property => property.Name);

        Assert.DoesNotContain(names, name => name.Contains("password", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("secret", StringComparison.OrdinalIgnoreCase));
    }

    private static CustomerProvisioningPackage Sign(ECDsa key, CustomerProvisioningPayload payload)
    {
        var json = CustomerProvisioningCanonicalJson.Serialize(payload);
        var signature = key.SignData(
            System.Text.Encoding.UTF8.GetBytes(json),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return new CustomerProvisioningPackage(
            1, "provisioning-key-1", LicenseAlgorithms.EcdsaP256Sha256, json, Convert.ToBase64String(signature));
    }

    private static CustomerProvisioningPayload Sample() => new(
        1,
        "9b0d2c4d-53bc-4bb2-a22c-90b30f2b58e8",
        "customer-ref-001",
        "installation-001",
        "Company Example",
        "independent",
        "initial-admin",
        new[] { "inventory", "finance" },
        Now.AddMinutes(-1),
        Now.AddHours(1),
        "abcdefghijklmnopqrstuvwxyz0123456789_-");
}
