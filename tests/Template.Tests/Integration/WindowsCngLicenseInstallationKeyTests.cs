using System;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Infrastructure.Licensing;
using Xunit;
using Microsoft.Extensions.Options;

namespace Template.Tests.Integration;

[SupportedOSPlatform("windows")]
public sealed class WindowsCngLicenseInstallationKeyTests
{
    private const string SoftwareKsp = "Microsoft Software Key Storage Provider";

    [Fact]
    public void SoftwareProvider_RequiresExplicitConsent()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Windows CNG is available only on Windows.");

        var installationId = "license-test-" + Guid.NewGuid().ToString("N");
        var options = new WindowsCngLicenseInstallationKeyOptions
        {
            ProviderName = SoftwareKsp,
            KeyVersion = 1,
            MachineKey = false,
        };

        Assert.Throws<InvalidOperationException>(() =>
            WindowsCngLicenseInstallationKeyProvisioner.Create(
                options, installationId, confirmSoftwareProviderRisk: false));
    }

    [Fact]
    public void SoftwareProvider_CreatesNonExportableKey_AndSignsChallenge()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Windows CNG is available only on Windows.");

        var installationId = "license-test-" + Guid.NewGuid().ToString("N");
        var options = new WindowsCngLicenseInstallationKeyOptions
        {
            ProviderName = SoftwareKsp,
            KeyVersion = 1,
            MachineKey = true,
        };

        try
        {
            var created = WindowsCngLicenseInstallationKeyProvisioner.Create(
                options, installationId, confirmSoftwareProviderRisk: true);

            Assert.Equal(LicenseInstallationKeyAssurance.SoftwareProtected, created.Assurance);
            Assert.Equal(SoftwareKsp, created.ProviderName);
            Assert.NotEmpty(created.PublicKeySubjectPublicKeyInfo);
            Assert.Matches("^[a-f0-9]{64}$", created.PublicKeyFingerprint);
            Assert.Matches("^[a-f0-9]{64}$", created.OsInstallationMarkerDigest);

            using (var key = CngKey.Open(created.KeyName, new CngProvider(SoftwareKsp), CngKeyOpenOptions.MachineKey))
            {
                Assert.Equal(CngAlgorithm.ECDsaP256, key.Algorithm);
                Assert.Equal(256, key.KeySize);
                Assert.Equal(CngExportPolicies.None, key.ExportPolicy);
                Assert.Throws<CryptographicException>(() => key.Export(CngKeyBlobFormat.EccPrivateBlob));
            }

            var challenge = Encoding.UTF8.GetBytes("faranou-license-installation-proof-v1");
            var signature = WindowsCngLicenseInstallationKeyProvisioner.SignChallenge(
                options, installationId, challenge, softwareProviderRiskConsentRecorded: true);
            Assert.Equal(64, signature.Length);

            using var verifier = ECDsa.Create();
            verifier.ImportSubjectPublicKeyInfo(created.PublicKeySubjectPublicKeyInfo, out var read);
            Assert.Equal(created.PublicKeySubjectPublicKeyInfo.Length, read);
            Assert.True(verifier.VerifyData(
                challenge, signature, HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
            Assert.False(verifier.VerifyData(
                Encoding.UTF8.GetBytes("different-challenge"), signature,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation));

            Assert.Throws<InvalidOperationException>(() =>
                WindowsCngLicenseInstallationKeyProvisioner.ReadExisting(
                    options, installationId, softwareProviderRiskConsentRecorded: false));
            Assert.Throws<InvalidOperationException>(() =>
                WindowsCngLicenseInstallationKeyProvisioner.SignChallenge(
                    options, installationId, challenge, softwareProviderRiskConsentRecorded: false));

            // Simulate an application restart: read the existing key, do not provision another.
            var afterRestart = WindowsCngLicenseInstallationKeyProvisioner.ReadExisting(
                options, installationId, softwareProviderRiskConsentRecorded: true);
            Assert.Equal(created.KeyId, afterRestart.KeyId);
            Assert.Equal(created.PublicKeyFingerprint, afterRestart.PublicKeyFingerprint);
            Assert.Equal(created.OsInstallationMarkerDigest, afterRestart.OsInstallationMarkerDigest);
            Assert.Equal(created.Assurance, afterRestart.Assurance);
        }
        finally
        {
            try
            {
                using var key = CngKey.Open(
                    WindowsCngLicenseInstallationKeyProvisioner.GetKeyName(installationId, options.KeyVersion),
                    new CngProvider(options.ProviderName), CngKeyOpenOptions.MachineKey);
                key.Delete();
            }
            catch (CryptographicException)
            {
                // A failed test may not have created a key.
            }
        }
    }

    [Fact]
    public async Task IdentityProofVerifier_ValidatesExistingKeyAndDetectsOsMarkerChange()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Windows CNG is available only on Windows.");

        var installationId = "license-proof-" + Guid.NewGuid().ToString("N");
        var options = new WindowsCngLicenseInstallationKeyOptions
        {
            ProviderName = SoftwareKsp,
            KeyVersion = 1,
            MachineKey = true,
        };

        try
        {
            var created = WindowsCngLicenseInstallationKeyProvisioner.Create(
                options, installationId, confirmSoftwareProviderRisk: true);
            var identity = new Domain.InstallationIdentity
            {
                Id = Guid.NewGuid(),
                InstallationId = installationId,
                KeyId = created.KeyId,
                KeyVersion = options.KeyVersion,
                ProviderName = created.ProviderName,
                AssuranceLevel = created.Assurance.ToString(),
                OsInstallationMarkerDigest = created.OsInstallationMarkerDigest,
                PublicKeyFingerprint = created.PublicKeyFingerprint,
                PublicKeySubjectPublicKeyInfoBase64 = Convert.ToBase64String(
                    created.PublicKeySubjectPublicKeyInfo),
                SoftwareProviderRiskAcceptedAtUtc = DateTime.UtcNow,
                SoftwareProviderRiskAcceptedBy = Guid.NewGuid().ToString("D"),
                State = Domain.InstallationIdentity.StateCurrent,
            };

            var verifier = new WindowsCngLicenseInstallationIdentityProofVerifier(Options.Create(options));
            var valid = await verifier.VerifyAsync(identity);
            Assert.Equal(InstallationIdentityProofStatus.Valid, valid);

            identity.OsInstallationMarkerDigest = new string('0', 64);
            var changed = await verifier.VerifyAsync(identity);
            Assert.Equal(InstallationIdentityProofStatus.InstallationChanged, changed);
        }
        finally
        {
            try
            {
                using var key = CngKey.Open(
                    WindowsCngLicenseInstallationKeyProvisioner.GetKeyName(installationId, options.KeyVersion),
                    new CngProvider(options.ProviderName), CngKeyOpenOptions.MachineKey);
                key.Delete();
            }
            catch (CryptographicException)
            {
                // A failed test may not have created a key.
            }
        }
    }

    [Fact]
    public void UnsupportedProvider_IsRejectedBeforeKeyCreation()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Windows CNG is available only on Windows.");

        var options = new WindowsCngLicenseInstallationKeyOptions
        {
            ProviderName = "Untrusted Third Party KSP",
            KeyVersion = 1,
            MachineKey = false,
        };

        Assert.Throws<ArgumentException>(() =>
            WindowsCngLicenseInstallationKeyProvisioner.Create(
                options, "license-test-" + Guid.NewGuid().ToString("N"),
                confirmSoftwareProviderRisk: true));
    }
}
