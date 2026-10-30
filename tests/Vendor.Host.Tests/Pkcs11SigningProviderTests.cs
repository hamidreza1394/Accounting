using System.Security.Cryptography;
using System.Text;
using Faranou.Licensing.Contracts;
using Faranou.Vendor.Host.Signing;
using Faranou.Vendor.Persistence.Auditing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class Pkcs11SigningProviderTests
{
    [Fact]
    public void OptionsRequireDistinctVersionedPurposeSpecificKeyIds()
    {
        var options = ValidOptions();
        options.Validate();

        options.ProvisioningKey.KeyId = options.LicenseKey.KeyId;
        Assert.Throws<InvalidOperationException>(options.Validate);

        options.ProvisioningKey.KeyId = "vendor-package";
        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public async Task LicenseProviderSignsCanonicalBytesAndWritesMetadataOnlyAudit()
    {
        var payload = Encoding.UTF8.GetBytes("{\"license\":\"canonical\"}");
        var module = new RecordingPkcs11SigningModule(
            LicenseSigningResult.Success("vendor-license-2026-v1", LicenseAlgorithms.EcdsaP256Sha256, new byte[64]));
        var audit = new RecordingAuditWriter();
        var provider = new Pkcs11LicenseSigningProvider(
            module,
            Options.Create(ValidOptions()),
            audit,
            NullLogger<Pkcs11LicenseSigningProvider>.Instance);

        var result = await provider.SignAsync(payload);

        Assert.True(result.Succeeded);
        Assert.Equal(payload, module.LastPayload);
        Assert.Equal("vendor-license-2026-v1", module.LastKey?.KeyId);
        Assert.NotNull(audit.LastRequest);
        Assert.Equal("vendor.signing.license.vendor-license-2026-v1.succeeded", audit.LastRequest.ActionCode);
        Assert.Equal("signing_operation", audit.LastRequest.SubjectType);
        Assert.Null(audit.LastRequest.SubjectId);
    }

    [Fact]
    public async Task ProvisioningProviderUsesItsOwnKeyAndAuditsFailure()
    {
        var module = new RecordingPkcs11SigningModule(LicenseSigningResult.Unavailable());
        var audit = new RecordingAuditWriter();
        var provider = new Pkcs11ProvisioningSigningProvider(
            module,
            Options.Create(ValidOptions()),
            audit,
            NullLogger<Pkcs11ProvisioningSigningProvider>.Instance);

        var result = await provider.SignAsync(Encoding.UTF8.GetBytes("{\"package\":1}"));

        Assert.False(result.Succeeded);
        Assert.Equal("vendor-package-2026-v1", module.LastKey?.KeyId);
        Assert.Equal("vendor.signing.provisioning.vendor-package-2026-v1.failed", audit.LastRequest?.ActionCode);
    }

    [Fact]
    public async Task AuditFailureDiscardsSuccessfulSignatureAndFailsClosed()
    {
        var module = new RecordingPkcs11SigningModule(
            LicenseSigningResult.Success("vendor-license-2026-v1", LicenseAlgorithms.EcdsaP256Sha256, new byte[64]));
        var audit = new RecordingAuditWriter { Fail = true };
        var provider = new Pkcs11LicenseSigningProvider(
            module,
            Options.Create(ValidOptions()),
            audit,
            NullLogger<Pkcs11LicenseSigningProvider>.Instance);

        var result = await provider.SignAsync(Encoding.UTF8.GetBytes("canonical"));

        Assert.False(result.Succeeded);
        Assert.Equal("signing_provider_unavailable", result.ErrorCode);
    }

    private static Pkcs11SigningOptions ValidOptions() => new()
    {
        ModulePath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "pkcs11-test-module")),
        TokenLabel = "FaranouVendorCI",
        UserPin = "test-only-pin",
        LicenseKey = new Pkcs11SigningKeyOptions { KeyId = "vendor-license-2026-v1" },
        ProvisioningKey = new Pkcs11SigningKeyOptions { KeyId = "vendor-package-2026-v1" }
    };

    private sealed class RecordingPkcs11SigningModule(LicenseSigningResult result) : IPkcs11SigningModule
    {
        public Pkcs11SigningKeyOptions? LastKey { get; private set; }
        public byte[]? LastPayload { get; private set; }

        public ValueTask<LicenseSigningResult> SignAsync(
            Pkcs11SigningKeyOptions key,
            ReadOnlyMemory<byte> canonicalPayload,
            CancellationToken cancellationToken = default)
        {
            LastKey = key;
            LastPayload = canonicalPayload.ToArray();
            return ValueTask.FromResult(result);
        }

        public ValueTask<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(true);
    }

    private sealed class RecordingAuditWriter : IVendorAuditWriter
    {
        public VendorAuditWriteRequest? LastRequest { get; private set; }
        public bool Fail { get; init; }

        public Task<VendorAuditWriteResult> AppendAsync(
            VendorAuditWriteRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            if (Fail)
                throw new InvalidOperationException("simulated audit failure");

            return Task.FromResult(new VendorAuditWriteResult(Guid.NewGuid(), new string('a', 64), new string('b', 64)));
        }
    }
}
