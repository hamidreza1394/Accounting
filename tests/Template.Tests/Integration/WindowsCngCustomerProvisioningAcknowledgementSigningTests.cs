using System;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure.Provisioning;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Template.Tests.Integration;

[SupportedOSPlatform("windows")]
public sealed class WindowsCngCustomerProvisioningAcknowledgementSigningTests
{
    private const string SoftwareKsp = "Microsoft Software Key Storage Provider";

    [Fact]
    public async Task ProvisionedKey_IsNonExportableP256_AndSignsExactAcknowledgementBytes()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Windows CNG is available only on Windows.");

        var suffix = Guid.NewGuid().ToString("N");
        var options = new WindowsCngCustomerAcknowledgementSigningOptions
        {
            Enabled = true,
            ProviderName = SoftwareKsp,
            KeyName = "Faranou.Tests.CustomerAck." + suffix,
            KeyId = "customer-ack-test-" + suffix + "-v1",
            MachineKey = false,
        };

        try
        {
            var provisioned = WindowsCngCustomerAcknowledgementKeyProvisioner.Create(options);

            Assert.Equal(options.KeyId, provisioned.KeyId);
            Assert.NotEmpty(provisioned.PublicKeySubjectPublicKeyInfo);

            using (var key = CngKey.Open(options.KeyName, new CngProvider(options.ProviderName)))
            {
                Assert.Equal(CngAlgorithm.ECDsaP256, key.Algorithm);
                Assert.Equal(256, key.KeySize);
                Assert.Equal(CngExportPolicies.None, key.ExportPolicy);
                Assert.Throws<CryptographicException>(() => key.Export(CngKeyBlobFormat.EccPrivateBlob));
            }

            var signer = new WindowsCngCustomerProvisioningAcknowledgementSigningProvider(
                Options.Create(options),
                NullLogger<WindowsCngCustomerProvisioningAcknowledgementSigningProvider>.Instance);
            var canonicalBytes = Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"requestId\":\"exact-bytes\"}");

            var result = await signer.SignAsync(canonicalBytes, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(options.KeyId, result.KeyId);
            Assert.Null(result.ErrorCode);
            var signature = Convert.FromBase64String(result.SignatureBase64!);
            Assert.Equal(64, signature.Length);
            using var verifier = ECDsa.Create();
            verifier.ImportSubjectPublicKeyInfo(provisioned.PublicKeySubjectPublicKeyInfo, out var read);
            Assert.Equal(provisioned.PublicKeySubjectPublicKeyInfo.Length, read);
            Assert.True(verifier.VerifyData(
                canonicalBytes, signature, HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
            Assert.False(verifier.VerifyData(
                Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"requestId\":\"changed\"}"),
                signature, HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        }
        finally
        {
            try
            {
                using var key = CngKey.Open(options.KeyName, new CngProvider(options.ProviderName));
                key.Delete();
            }
            catch (CryptographicException)
            {
                // A failed provisioning test may not have created a key.
            }
        }
    }

    [Fact]
    public async Task MissingKey_FailsClosed_AndDoesNotCreateAReplacementKey()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Windows CNG is available only on Windows.");

        var suffix = Guid.NewGuid().ToString("N");
        var options = new WindowsCngCustomerAcknowledgementSigningOptions
        {
            Enabled = true,
            ProviderName = SoftwareKsp,
            KeyName = "Faranou.Tests.MissingAck." + suffix,
            KeyId = "customer-ack-test-" + suffix + "-v1",
            MachineKey = false,
        };
        var signer = new WindowsCngCustomerProvisioningAcknowledgementSigningProvider(
            Options.Create(options),
            NullLogger<WindowsCngCustomerProvisioningAcknowledgementSigningProvider>.Instance);

        var result = await signer.SignAsync(
            Encoding.UTF8.GetBytes("{\"schemaVersion\":1}"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Null(result.KeyId);
        Assert.Null(result.SignatureBase64);
        Assert.Equal("provisioning_acknowledgement_signer_unavailable", result.ErrorCode);
        Assert.Throws<CryptographicException>(() =>
        {
            using var unexpected = CngKey.Open(options.KeyName, new CngProvider(options.ProviderName));
        });
    }

    [Fact]
    public void Provisioning_RejectsInvalidOrUnapprovedProviderConfiguration()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Windows CNG is available only on Windows.");

        var invalid = new WindowsCngCustomerAcknowledgementSigningOptions
        {
            Enabled = true,
            ProviderName = "Untrusted Third Party KSP",
            KeyName = "Faranou.Tests.InvalidProvider",
            KeyId = "customer-ack-test-invalid-v1",
            MachineKey = false,
        };

        Assert.Throws<ArgumentException>(() => WindowsCngCustomerAcknowledgementKeyProvisioner.Create(invalid));
    }
}
