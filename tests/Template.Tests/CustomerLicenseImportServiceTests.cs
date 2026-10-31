using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Faranou.Licensing.Contracts;
using Infrastructure.Licensing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Template.Tests;

public sealed class CustomerLicenseImportServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ImportAsync_PersistsValidatedEnvelopeAndAudit()
    {
        await using var database = CreateDatabase();
        var identity = AddCurrentIdentity(database);
        var (envelope, payload) = CreateEnvelope(sequence: 1);
        var validator = new TestValidator(envelope, LicenseValidationResult.Ok(LicenseStatus.Valid, payload));
        var service = CreateService(database, validator, InstallationIdentityProofStatus.Valid);

        var result = await service.ImportAsync(
            JsonSerializer.Serialize(envelope, JsonOptions),
            identity.InstallationId,
            Guid.NewGuid());

        Assert.True(result.Success, result.ErrorCode);
        Assert.False(result.AlreadyInstalled);
        var installed = await database.InstalledLicenses.SingleAsync();
        Assert.Equal(envelope.PayloadJson, installed.PayloadJson);
        Assert.Equal(envelope.SignatureBase64, installed.SignatureBase64);
        Assert.Equal(Domain.InstalledLicense.StateCurrent, installed.State);
        Assert.Contains(database.SecurityAuditEvents, item => item.EventType == "LicenseImported");
    }

    [Fact]
    public async Task ImportAsync_IsIdempotentForTheExactCurrentEnvelope()
    {
        await using var database = CreateDatabase();
        var identity = AddCurrentIdentity(database);
        var (envelope, payload) = CreateEnvelope(sequence: 1);
        var validator = new TestValidator(envelope, LicenseValidationResult.Ok(LicenseStatus.Valid, payload));
        var service = CreateService(database, validator, InstallationIdentityProofStatus.Valid);
        var raw = JsonSerializer.Serialize(envelope, JsonOptions);

        var first = await service.ImportAsync(raw, identity.InstallationId, null);
        var second = await service.ImportAsync(raw, identity.InstallationId, null);

        Assert.True(first.Success, first.ErrorCode);
        Assert.True(second.Success, second.ErrorCode);
        Assert.True(second.AlreadyInstalled);
        Assert.Single(database.InstalledLicenses);
        Assert.Single(database.SecurityAuditEvents.Where(item => item.EventType == "LicenseImported"));
    }

    [Fact]
    public async Task ImportAsync_RejectsSequenceDowngradeAndPreservesCurrentLicense()
    {
        await using var database = CreateDatabase();
        var identity = AddCurrentIdentity(database);
        var (firstEnvelope, firstPayload) = CreateEnvelope(sequence: 2);
        var (olderEnvelope, olderPayload) = CreateEnvelope(sequence: 1);
        var validator = new TestValidator(
            (firstEnvelope, LicenseValidationResult.Ok(LicenseStatus.Valid, firstPayload)),
            (olderEnvelope, LicenseValidationResult.Ok(LicenseStatus.Valid, olderPayload)));
        var service = CreateService(database, validator, InstallationIdentityProofStatus.Valid);

        var first = await service.ImportAsync(
            JsonSerializer.Serialize(firstEnvelope, JsonOptions), identity.InstallationId, null);
        var downgrade = await service.ImportAsync(
            JsonSerializer.Serialize(olderEnvelope, JsonOptions), identity.InstallationId, null);

        Assert.True(first.Success, first.ErrorCode);
        Assert.False(downgrade.Success);
        Assert.Equal("license_sequence_replay_or_downgrade", downgrade.ErrorCode);
        var current = await database.InstalledLicenses.SingleAsync();
        Assert.Equal(2, current.Sequence);
        Assert.Equal(Domain.InstalledLicense.StateCurrent, current.State);
        Assert.Contains(database.SecurityAuditEvents, item =>
            item.EventType == "LicenseImportRejected" &&
            item.DetailsJson!.Contains("license_sequence_replay_or_downgrade", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ImportAsync_SupersedesPreviousCurrentEnvelopeOnHigherSequence()
    {
        await using var database = CreateDatabase();
        var identity = AddCurrentIdentity(database);
        var (firstEnvelope, firstPayload) = CreateEnvelope(sequence: 1);
        var (nextEnvelope, nextPayload) = CreateEnvelope(sequence: 2);
        var validator = new TestValidator(
            (firstEnvelope, LicenseValidationResult.Ok(LicenseStatus.Valid, firstPayload)),
            (nextEnvelope, LicenseValidationResult.Ok(LicenseStatus.ExpiringSoon, nextPayload)));
        var service = CreateService(database, validator, InstallationIdentityProofStatus.Valid);

        var first = await service.ImportAsync(
            JsonSerializer.Serialize(firstEnvelope, JsonOptions), identity.InstallationId, null);
        var replacement = await service.ImportAsync(
            JsonSerializer.Serialize(nextEnvelope, JsonOptions), identity.InstallationId, null);

        Assert.True(first.Success, first.ErrorCode);
        Assert.True(replacement.Success, replacement.ErrorCode);
        Assert.False(replacement.AlreadyInstalled);
        Assert.Equal(2, await database.InstalledLicenses.CountAsync());
        Assert.Single(database.InstalledLicenses.Where(item =>
            item.State == Domain.InstalledLicense.StateCurrent && item.Sequence == 2));
        Assert.Single(database.InstalledLicenses.Where(item =>
            item.State == Domain.InstalledLicense.StateSuperseded && item.Sequence == 1));
        Assert.Contains(database.SecurityAuditEvents, item => item.EventType == "LicenseReplaced");
    }

    [Fact]
    public async Task ImportAsync_RequiresExplicitlyApprovedLicenseState()
    {
        await using var database = CreateDatabase();
        var identity = AddCurrentIdentity(database);
        var (envelope, payload) = CreateEnvelope(sequence: 1);
        var validator = new TestValidator(envelope, LicenseValidationResult.Ok(LicenseStatus.Grace, payload));
        var service = CreateService(database, validator, InstallationIdentityProofStatus.Valid);

        var result = await service.ImportAsync(
            JsonSerializer.Serialize(envelope, JsonOptions), identity.InstallationId, null);

        Assert.False(result.Success);
        Assert.Equal("license_status_policy_unapproved", result.ErrorCode);
        Assert.Empty(database.InstalledLicenses);
        Assert.Contains(database.SecurityAuditEvents, item => item.EventType == "LicenseImportRejected");
    }

    [Fact]
    public async Task ImportAsync_RequiresLiveInstallationProofBeforeActivation()
    {
        await using var database = CreateDatabase();
        var identity = AddCurrentIdentity(database);
        var (envelope, payload) = CreateEnvelope(sequence: 1);
        var validator = new TestValidator(envelope, LicenseValidationResult.Ok(LicenseStatus.Valid, payload));
        var service = CreateService(database, validator, InstallationIdentityProofStatus.InstallationChanged);

        var result = await service.ImportAsync(
            JsonSerializer.Serialize(envelope, JsonOptions), identity.InstallationId, null);

        Assert.False(result.Success);
        Assert.Equal("installation_identity_changed_reactivation_required", result.ErrorCode);
        Assert.Empty(database.InstalledLicenses);
        Assert.Equal(Domain.InstallationIdentity.StateReactivationRequired,
            (await database.InstallationIdentities.SingleAsync()).State);
        Assert.Contains(database.SecurityAuditEvents, item => item.EventType == "InstallationIdentityChanged");
    }

    private static Data.DatabaseContext CreateDatabase() =>
        new(new DbContextOptionsBuilder<Data.DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static Domain.InstallationIdentity AddCurrentIdentity(Data.DatabaseContext database)
    {
        var identity = new Domain.InstallationIdentity
        {
            Id = Guid.NewGuid(),
            InstallationId = "install-123",
            KeyId = "key-123",
            KeyVersion = 1,
            ProviderName = "Microsoft Platform Crypto Provider",
            AssuranceLevel = Domain.InstallationIdentity.AssuranceHardwareTpm,
            OsInstallationMarkerDigest = new string('a', 64),
            PublicKeyFingerprint = new string('b', 64),
            PublicKeySubjectPublicKeyInfoBase64 = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
            State = Domain.InstallationIdentity.StateCurrent
        };
        database.InstallationIdentities.Add(identity);
        database.SaveChanges();
        return identity;
    }

    private static (LicenseEnvelope Envelope, LicensePayload Payload) CreateEnvelope(int sequence)
    {
        var payload = new LicensePayload(
            1,
            "license-123",
            "install-123",
            "FARANOU",
            LicensePayload.KindPerpetual,
            DateTimeOffset.UtcNow,
            null,
            null,
            0,
            sequence,
            LicenseLimits.Empty);
        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        return (
            new LicenseEnvelope(1, "license-key-1", "ECDSA-P256-SHA256", payloadJson, "c2lnbmF0dXJl"),
            payload);
    }

    private static CustomerLicenseImportService CreateService(
        Data.DatabaseContext database,
        ICustomerLicenseEnvelopeValidator validator,
        InstallationIdentityProofStatus proofStatus) =>
        new(
            database,
            validator,
            new FixedIdentityProofVerifier(proofStatus),
            TimeProvider.System,
            NullLogger<CustomerLicenseImportService>.Instance);

    private sealed class FixedIdentityProofVerifier(InstallationIdentityProofStatus status)
        : IInstallationIdentityProofVerifier
    {
        public Task<InstallationIdentityProofStatus> VerifyAsync(
            Domain.InstallationIdentity identity,
            CancellationToken cancellationToken = default) => Task.FromResult(status);
    }

    private sealed class TestValidator : ICustomerLicenseEnvelopeValidator
    {
        private readonly Dictionary<string, LicenseValidationResult> _results;

        public TestValidator(
            params (LicenseEnvelope Envelope, LicenseValidationResult Result)[] values)
        {
            _results = values.ToDictionary(item => item.Envelope.PayloadJson, item => item.Result);
        }

        public Task<LicenseValidationResult> ValidateAsync(
            LicenseEnvelope envelope,
            string localInstallationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_results[envelope.PayloadJson]);
    }
}
