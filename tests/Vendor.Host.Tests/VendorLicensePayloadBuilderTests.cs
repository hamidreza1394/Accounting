using Faranou.Licensing.Contracts;
using Faranou.Vendor.Host.Signing;
using Faranou.Vendor.Persistence.Domain;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorLicensePayloadBuilderTests
{
    [Fact]
    public void BuildsCanonicalizableV2PayloadBoundToMatchingCurrentInstallationIdentity()
    {
        var customerId = Guid.NewGuid();
        var installationId = Guid.NewGuid();
        var license = NewLicense(customerId, installationId);
        var installation = NewInstallation(customerId, installationId);
        var identity = NewIdentity(installationId);
        var issuedAt = new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.Zero);

        var success = VendorLicensePayloadBuilder.TryBuild(
            license, installation, identity, issuedAt, out var payload, out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(payload);
        Assert.Equal(2, payload.FormatVersion);
        Assert.Equal(license.Id.ToString("D"), payload.LicenseId);
        Assert.Equal(installationId.ToString("D"), payload.InstallationId);
        Assert.Equal(identity.PublicKeyFingerprint, payload.InstallationKeyFingerprint);
        Assert.Equal(8, payload.Sequence);
        Assert.Equal(new[] { "finance", "sales" }, payload.Limits.Subsystems);
        var canonical = CanonicalJson.SerializePayload(payload);
        Assert.Contains("\"formatVersion\":2", canonical);
        Assert.Contains("\"installationKeyFingerprint\":", canonical);
    }

    [Fact]
    public void RejectsInstallationOrCustomerMismatch()
    {
        var license = NewLicense(Guid.NewGuid(), Guid.NewGuid());
        var installation = NewInstallation(Guid.NewGuid(), Guid.NewGuid());
        var identity = NewIdentity(installation.Id);

        var success = VendorLicensePayloadBuilder.TryBuild(
            license, installation, identity, DateTimeOffset.UtcNow, out var payload, out var error);

        Assert.False(success);
        Assert.Null(payload);
        Assert.Equal("license_installation_mismatch", error);
    }

    [Theory]
    [InlineData("[\"unknown\"]")]
    [InlineData("[\"finance\",\"finance\"]")]
    [InlineData("not-json")]
    public void RejectsInvalidSubsystemMetadata(string subsystemsJson)
    {
        var customerId = Guid.NewGuid();
        var installationId = Guid.NewGuid();
        var license = NewLicense(customerId, installationId);
        license.SubsystemsJson = subsystemsJson;
        var installation = NewInstallation(customerId, installationId);
        var identity = NewIdentity(installationId);

        var success = VendorLicensePayloadBuilder.TryBuild(
            license, installation, identity, DateTimeOffset.UtcNow, out var payload, out var error);

        Assert.False(success);
        Assert.Null(payload);
        Assert.Equal("license_subsystems_invalid", error);
    }

    [Fact]
    public void RejectsUnregisteredOrRetiredInstallationIdentity()
    {
        var customerId = Guid.NewGuid();
        var installationId = Guid.NewGuid();
        var license = NewLicense(customerId, installationId);
        var installation = NewInstallation(customerId, installationId);
        var identity = NewIdentity(installationId);
        identity.State = VendorInstallationIdentity.StateRetired;

        var success = VendorLicensePayloadBuilder.TryBuild(
            license, installation, identity, DateTimeOffset.UtcNow, out var payload, out var error);

        Assert.False(success);
        Assert.Null(payload);
        Assert.Equal("installation_identity_not_registered", error);
    }

    [Fact]
    public void RejectsAlreadyIssuedOrOverflowedSequenceMetadata()
    {
        var customerId = Guid.NewGuid();
        var installationId = Guid.NewGuid();
        var installation = NewInstallation(customerId, installationId);
        var identity = NewIdentity(installationId);
        var license = NewLicense(customerId, installationId);
        license.Status = "Issued";

        Assert.False(VendorLicensePayloadBuilder.TryBuild(
            license, installation, identity, DateTimeOffset.UtcNow, out _, out var statusError));
        Assert.Equal("license_not_issuable", statusError);

        license.Status = "Draft";
        license.LastIssuedSequence = int.MaxValue;
        Assert.False(VendorLicensePayloadBuilder.TryBuild(
            license, installation, identity, DateTimeOffset.UtcNow, out _, out var sequenceError));
        Assert.Equal("license_metadata_invalid", sequenceError);
    }

    private static VendorInstallation NewInstallation(Guid customerId, Guid installationId) => new()
    {
        Id = installationId,
        CustomerId = customerId,
        Status = "Active"
    };

    private static VendorInstallationIdentity NewIdentity(Guid installationId) => new()
    {
        Id = Guid.NewGuid(),
        InstallationId = installationId,
        KeyId = "license-install-test-v1",
        KeyVersion = 1,
        ProviderName = "Microsoft Platform Crypto Provider",
        AssuranceLevel = VendorInstallationIdentity.AssuranceHardwareTpm,
        OsInstallationMarkerDigest = new string('b', 64),
        PublicKeyFingerprint = new string('a', 64),
        PublicKeySubjectPublicKeyInfoBase64 = "AA==",
        State = VendorInstallationIdentity.StateCurrent,
        RegisteredAtUtc = DateTimeOffset.UtcNow
    };

    private static VendorLicense NewLicense(Guid customerId, Guid installationId) => new()
    {
        Id = Guid.NewGuid(),
        CustomerId = customerId,
        InstallationId = installationId,
        LicenseNumber = "LIC-TEST-001",
        ProductCode = "FARANOU",
        Status = "Draft",
        LicenseKind = LicensePayload.KindPerpetual,
        MaxCompanies = null,
        SubsystemsJson = "[\"sales\",\"finance\"]",
        GraceDays = 30,
        LastIssuedSequence = 7,
        ValidFromUtc = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
        ValidUntilUtc = null
    };
}
