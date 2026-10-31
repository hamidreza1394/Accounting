using System;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure.Licensing;
using Infrastructure.Provisioning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Template.Tests;

public sealed class CustomerInstallationIdentityEnrollmentServiceTests
{
    [Fact]
    public async Task EnrollAsync_PersistsPublicIdentityAndAuditsSoftwareConsent()
    {
        await using var database = CreateDatabase();
        var service = CreateService(
            database,
            new WindowsCngLicenseInstallationKeyOptions
            {
                ProviderName = WindowsCngLicenseInstallationKeyProvisioner.SoftwareProviderName,
                KeyVersion = 1,
                MachineKey = true
            },
            CreateMetadata(1, LicenseInstallationKeyAssurance.SoftwareProtected),
            InstallationIdentityProofStatus.Valid);

        var result = await service.EnrollAsync(Guid.NewGuid(), confirmSoftwareProviderRisk: true);

        Assert.True(result.Success, result.ErrorCode);
        Assert.Equal("SoftwareProtected", result.AssuranceLevel);
        var identity = await database.InstallationIdentities.SingleAsync();
        Assert.NotNull(identity.SoftwareProviderRiskAcceptedAtUtc);
        Assert.False(string.IsNullOrWhiteSpace(identity.SoftwareProviderRiskAcceptedBy));
        Assert.Equal(Domain.InstallationIdentity.StateCurrent, identity.State);
        Assert.Contains(database.SecurityAuditEvents, item => item.EventType == "InstallationIdentityEnrolled");
    }

    [Fact]
    public async Task EnrollAsync_IsIdempotentForTheSameCurrentKey()
    {
        await using var database = CreateDatabase();
        var options = new WindowsCngLicenseInstallationKeyOptions
        {
            ProviderName = WindowsCngLicenseInstallationKeyProvisioner.TpmProviderName,
            KeyVersion = 1,
            MachineKey = true
        };
        var metadata = CreateMetadata(1, LicenseInstallationKeyAssurance.HardwareTpm);
        var actor = Guid.NewGuid();

        var first = await CreateService(database, options, metadata, InstallationIdentityProofStatus.Valid)
            .EnrollAsync(actor, confirmSoftwareProviderRisk: false);
        var second = await CreateService(database, options, metadata, InstallationIdentityProofStatus.Valid)
            .EnrollAsync(actor, confirmSoftwareProviderRisk: false);

        Assert.True(first.Success, first.ErrorCode);
        Assert.True(second.Success, second.ErrorCode);
        Assert.True(second.AlreadyEnrolled);
        Assert.Single(database.InstallationIdentities);
        Assert.Single(database.SecurityAuditEvents.Where(item => item.EventType == "InstallationIdentityEnrolled"));
    }

    [Fact]
    public async Task EnrollAsync_RetiresPreviousIdentityWhenKeyVersionIncreases()
    {
        await using var database = CreateDatabase();
        var actor = Guid.NewGuid();
        var first = await CreateService(
            database,
            new WindowsCngLicenseInstallationKeyOptions
            {
                ProviderName = WindowsCngLicenseInstallationKeyProvisioner.TpmProviderName,
                KeyVersion = 1,
                MachineKey = true
            },
            CreateMetadata(1, LicenseInstallationKeyAssurance.HardwareTpm),
            InstallationIdentityProofStatus.Valid).EnrollAsync(actor, false);

        var rotated = await CreateService(
            database,
            new WindowsCngLicenseInstallationKeyOptions
            {
                ProviderName = WindowsCngLicenseInstallationKeyProvisioner.TpmProviderName,
                KeyVersion = 2,
                MachineKey = true
            },
            CreateMetadata(2, LicenseInstallationKeyAssurance.HardwareTpm),
            InstallationIdentityProofStatus.Valid).EnrollAsync(actor, false);

        Assert.True(first.Success, first.ErrorCode);
        Assert.True(rotated.Success, rotated.ErrorCode);
        Assert.Equal(2, await database.InstallationIdentities.CountAsync());
        Assert.Single(database.InstallationIdentities.Where(item =>
            item.State == Domain.InstallationIdentity.StateCurrent && item.KeyVersion == 2));
        Assert.Single(database.InstallationIdentities.Where(item =>
            item.State == Domain.InstallationIdentity.StateRetired && item.KeyVersion == 1));
    }

    [Fact]
    public async Task EnrollAsync_RejectsSoftwareProviderWithoutExplicitConsent()
    {
        await using var database = CreateDatabase();
        var service = CreateService(
            database,
            new WindowsCngLicenseInstallationKeyOptions
            {
                ProviderName = WindowsCngLicenseInstallationKeyProvisioner.SoftwareProviderName,
                KeyVersion = 1,
                MachineKey = true
            },
            CreateMetadata(1, LicenseInstallationKeyAssurance.SoftwareProtected),
            InstallationIdentityProofStatus.Valid);

        var result = await service.EnrollAsync(Guid.NewGuid(), confirmSoftwareProviderRisk: false);

        Assert.False(result.Success);
        Assert.Equal("software_provider_explicit_consent_required", result.ErrorCode);
        Assert.Empty(database.InstallationIdentities);
        Assert.Contains(database.SecurityAuditEvents, item =>
            item.EventType == "InstallationIdentityEnrollmentRejected");
    }

    private static Data.DatabaseContext CreateDatabase() =>
        new(new DbContextOptionsBuilder<Data.DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static CustomerInstallationIdentityEnrollmentService CreateService(
        Data.DatabaseContext database,
        WindowsCngLicenseInstallationKeyOptions options,
        WindowsCngLicenseInstallationKeyProvisioningResult metadata,
        InstallationIdentityProofStatus proofStatus) =>
        new(
            database,
            Options.Create(options),
            Options.Create(new CustomerProvisioningSettings { InstallationId = "install-123" }),
            new FixedMetadataProvider(metadata),
            new FixedProofVerifier(proofStatus),
            TimeProvider.System);

    private static WindowsCngLicenseInstallationKeyProvisioningResult CreateMetadata(
        int version,
        LicenseInstallationKeyAssurance assurance) =>
        new(
            "key-name-" + version,
            "key-" + version,
            assurance == LicenseInstallationKeyAssurance.HardwareTpm
                ? WindowsCngLicenseInstallationKeyProvisioner.TpmProviderName
                : WindowsCngLicenseInstallationKeyProvisioner.SoftwareProviderName,
            assurance,
            new byte[] { 1, 2, 3, (byte)version },
            new string((char)('a' + version), 64),
            new string((char)('b' + version), 64),
            assurance == LicenseInstallationKeyAssurance.SoftwareProtected);

    private sealed class FixedMetadataProvider(
        WindowsCngLicenseInstallationKeyProvisioningResult metadata)
        : ILocalInstallationKeyMetadataProvider
    {
        public WindowsCngLicenseInstallationKeyProvisioningResult ReadExisting(
            string installationId,
            bool softwareProviderRiskConsentRecorded) => metadata;
    }

    private sealed class FixedProofVerifier(InstallationIdentityProofStatus status)
        : IInstallationIdentityProofVerifier
    {
        public Task<InstallationIdentityProofStatus> VerifyAsync(
            Domain.InstallationIdentity identity,
            CancellationToken cancellationToken = default) => Task.FromResult(status);
    }
}
