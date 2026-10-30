using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Text;
using Faranou.Licensing.Contracts;
using Faranou.Vendor.Host.Signing;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Net.Pkcs11Interop.Common;
using Net.Pkcs11Interop.HighLevelAPI;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class Pkcs11SigningModuleSoftHsmTests
{
    [Fact]
    public async Task SoftHsmSignsWithSeparateNonExtractableP256KeysAndIsRejectedInProduction()
    {
        var modulePath = Environment.GetEnvironmentVariable("FARANO_TEST_PKCS11_MODULE_PATH");
        var tokenLabel = Environment.GetEnvironmentVariable("FARANO_TEST_PKCS11_TOKEN_LABEL");
        var pin = Environment.GetEnvironmentVariable("FARANO_TEST_PKCS11_PIN");

        if (string.IsNullOrWhiteSpace(modulePath) ||
            string.IsNullOrWhiteSpace(tokenLabel) ||
            string.IsNullOrWhiteSpace(pin))
        {
            if (Environment.GetEnvironmentVariable("FARANO_REQUIRE_PKCS11") == "1")
                Assert.Fail("Vendor CI requires the SoftHSM PKCS#11 test environment.");

            Assert.Skip("SoftHSM integration is run in Vendor CI; local runs may omit the test token.");
        }

        var suffix = Guid.NewGuid().ToString("N");
        var licenseKeyId = $"vendor-license-{suffix}-v1";
        var provisioningKeyId = $"vendor-package-{suffix}-v1";
        var licensePublicKey = CreateKeyPair(modulePath, tokenLabel, pin, licenseKeyId);
        var provisioningPublicKey = CreateKeyPair(modulePath, tokenLabel, pin, provisioningKeyId);

        var options = new Pkcs11SigningOptions
        {
            ModulePath = modulePath,
            TokenLabel = tokenLabel,
            UserPin = pin,
            LicenseKey = new Pkcs11SigningKeyOptions { KeyId = licenseKeyId },
            ProvisioningKey = new Pkcs11SigningKeyOptions { KeyId = provisioningKeyId }
        };

        try
        {
            using (var module = new Pkcs11SigningModule(
                Options.Create(options),
                new TestHostEnvironment(Environments.Development),
                NullLogger<Pkcs11SigningModule>.Instance))
            {
                Assert.True(await module.IsAvailableAsync());

                var licensePayload = Encoding.UTF8.GetBytes("{\"purpose\":\"license\"}");
                var licenseResult = await module.SignAsync(options.LicenseKey, licensePayload);
                Assert.True(licenseResult.Succeeded);
                Assert.Equal(licenseKeyId, licenseResult.KeyId);
                using var licenseVerifier = ECDsa.Create(licensePublicKey);
                Assert.True(licenseVerifier.VerifyHash(
                    SHA256.HashData(licensePayload),
                    licenseResult.Signature.Span,
                    DSASignatureFormat.IeeeP1363FixedFieldConcatenation));

                var packagePayload = Encoding.UTF8.GetBytes("{\"purpose\":\"provisioning\"}");
                var packageResult = await module.SignAsync(options.ProvisioningKey, packagePayload);
                Assert.True(packageResult.Succeeded);
                Assert.Equal(provisioningKeyId, packageResult.KeyId);
                using var packageVerifier = ECDsa.Create(provisioningPublicKey);
                Assert.True(packageVerifier.VerifyHash(
                    SHA256.HashData(packagePayload),
                    packageResult.Signature.Span,
                    DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
            }

            using var productionModule = new Pkcs11SigningModule(
                Options.Create(options),
                new TestHostEnvironment(Environments.Production),
                NullLogger<Pkcs11SigningModule>.Instance);
            Assert.False(await productionModule.IsAvailableAsync());
            var blocked = await productionModule.SignAsync(
                options.LicenseKey,
                Encoding.UTF8.GetBytes("must-not-sign-in-production-with-softhsm"));
            Assert.False(blocked.Succeeded);
        }
        finally
        {
            DestroyKeyPair(modulePath, tokenLabel, pin, licenseKeyId);
            DestroyKeyPair(modulePath, tokenLabel, pin, provisioningKeyId);
        }
    }

    private static ECParameters CreateKeyPair(
        string modulePath,
        string tokenLabel,
        string pin,
        string keyId)
    {
        var factories = new Pkcs11InteropFactories();
        using var library = factories.Pkcs11LibraryFactory.LoadPkcs11Library(
            factories,
            modulePath,
            AppType.MultiThreaded);
        var slot = FindSlot(library, tokenLabel);

        using var session = slot.OpenSession(SessionType.ReadWrite);
        session.Login(CKU.CKU_USER, pin);
        try
        {
            var id = Encoding.ASCII.GetBytes(keyId);
            var ecParameters = new byte[] { 0x06, 0x08, 0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x03, 0x01, 0x07 };
            var publicTemplate = new List<IObjectAttribute>
            {
                session.Factories.ObjectAttributeFactory.Create(CKA.CKA_TOKEN, true),
                session.Factories.ObjectAttributeFactory.Create(CKA.CKA_PRIVATE, false),
                session.Factories.ObjectAttributeFactory.Create(CKA.CKA_LABEL, keyId),
                session.Factories.ObjectAttributeFactory.Create(CKA.CKA_ID, id),
                session.Factories.ObjectAttributeFactory.Create(CKA.CKA_EC_PARAMS, ecParameters),
                session.Factories.ObjectAttributeFactory.Create(CKA.CKA_VERIFY, true)
            };
            var privateTemplate = new List<IObjectAttribute>
            {
                session.Factories.ObjectAttributeFactory.Create(CKA.CKA_TOKEN, true),
                session.Factories.ObjectAttributeFactory.Create(CKA.CKA_PRIVATE, true),
                session.Factories.ObjectAttributeFactory.Create(CKA.CKA_LABEL, keyId),
                session.Factories.ObjectAttributeFactory.Create(CKA.CKA_ID, id),
                session.Factories.ObjectAttributeFactory.Create(CKA.CKA_SENSITIVE, true),
                session.Factories.ObjectAttributeFactory.Create(CKA.CKA_EXTRACTABLE, false),
                session.Factories.ObjectAttributeFactory.Create(CKA.CKA_SIGN, true)
            };

            session.GenerateKeyPair(
                session.Factories.MechanismFactory.Create(CKM.CKM_EC_KEY_PAIR_GEN),
                publicTemplate,
                privateTemplate,
                out var publicKey,
                out _);

            var pointDer = session.GetAttributeValue(publicKey, new List<CKA> { CKA.CKA_EC_POINT })[0]
                .GetValueAsByteArray();
            var asnReader = new AsnReader(pointDer, AsnEncodingRules.DER);
            var point = asnReader.ReadOctetString();
            asnReader.ThrowIfNotEmpty();
            Assert.Equal(65, point.Length);
            Assert.Equal(0x04, point[0]);

            return new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint
                {
                    X = point[1..33],
                    Y = point[33..65]
                }
            };
        }
        finally
        {
            session.Logout();
        }
    }

    private static void DestroyKeyPair(string modulePath, string tokenLabel, string pin, string keyId)
    {
        var factories = new Pkcs11InteropFactories();
        using var library = factories.Pkcs11LibraryFactory.LoadPkcs11Library(
            factories,
            modulePath,
            AppType.MultiThreaded);
        var slot = FindSlot(library, tokenLabel);

        using var session = slot.OpenSession(SessionType.ReadWrite);
        session.Login(CKU.CKU_USER, pin);
        try
        {
            var template = new List<IObjectAttribute>
            {
                session.Factories.ObjectAttributeFactory.Create(CKA.CKA_LABEL, keyId),
                session.Factories.ObjectAttributeFactory.Create(CKA.CKA_ID, Encoding.ASCII.GetBytes(keyId))
            };
            foreach (var objectHandle in session.FindAllObjects(template))
                session.DestroyObject(objectHandle);
        }
        finally
        {
            session.Logout();
        }
    }

    private static ISlot FindSlot(IPkcs11Library library, string tokenLabel) =>
        library.GetSlotList(SlotsType.WithTokenPresent)
            .Single(slot => string.Equals(slot.GetTokenInfo().Label.Trim(), tokenLabel, StringComparison.Ordinal));

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Faranou.Vendor.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
