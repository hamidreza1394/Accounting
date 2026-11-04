using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Faranou.Licensing.Contracts;
using Infrastructure.Licensing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Template.Tests;

/// <summary>
/// Proves the shared customer runtime evaluation path is fail-closed for offline
/// revocation, missing/expired snapshots, trust failures and state-store problems.
/// These tests exercise the evaluator contract; they do not claim hardware TPM verification.
/// </summary>
public sealed class CustomerLicenseRuntimeEvaluatorTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void AddCustomerLicenseRuntimeEvaluator_RegistersScopedEvaluator()
    {
        var services = new ServiceCollection();

        services.AddCustomerLicenseRuntimeEvaluator();

        var descriptor = Assert.Single(
            services,
            item => item.ServiceType == typeof(ICustomerLicenseRuntimeEvaluator));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal(typeof(CustomerLicenseRuntimeEvaluator), descriptor.ImplementationType);
    }

    [Fact]
    public async Task EvaluateCurrentAsync_ValidLicenseNotRevoked_IsUsable()
    {
        await using var database = CreateDatabase();
        var identity = AddCurrentIdentity(database);
        var (envelope, payload) = CreateEnvelope(sequence: 3, installationId: identity.InstallationId);
        AddCurrentLicense(database, identity, envelope, payload);
        var validator = new ScriptedValidator(LicenseValidationResult.Ok(LicenseStatus.Valid, payload));
        var evaluator = CreateEvaluator(database, validator);

        var result = await evaluator.EvaluateCurrentAsync(identity.InstallationId);

        Assert.True(result.IsUsable, result.ErrorCode);
        Assert.Equal(LicenseStatus.Valid, result.Status);
        Assert.Equal(payload.LicenseId, result.LicenseId);
        Assert.Equal(payload.Sequence, result.Sequence);
        Assert.Same(payload, result.ValidatedPayload);
        var serialized = JsonSerializer.Serialize(result, JsonOptions);
        Assert.False(serialized.Contains("validatedPayload", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(payload.ToString(), result.ToString());
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public async Task EvaluateCurrentAsync_RevokedLicense_IsRejectedWithStableCode()
    {
        await using var database = CreateDatabase();
        var identity = AddCurrentIdentity(database);
        var (envelope, payload) = CreateEnvelope(sequence: 4, installationId: identity.InstallationId);
        AddCurrentLicense(database, identity, envelope, payload);
        var validator = new ScriptedValidator(LicenseValidationResult.Fail("license_revoked"));
        var evaluator = CreateEvaluator(database, validator);

        var result = await evaluator.EvaluateCurrentAsync(identity.InstallationId);

        Assert.False(result.IsUsable);
        Assert.Equal("license_revoked", result.ErrorCode);
        Assert.Equal(payload.LicenseId, result.LicenseId);
        Assert.Null(result.ValidatedPayload);
    }

    [Fact]
    public async Task EvaluateCurrentAsync_MissingSnapshot_IsUnavailableNotNotRevoked()
    {
        await using var database = CreateDatabase();
        var identity = AddCurrentIdentity(database);
        var (envelope, payload) = CreateEnvelope(sequence: 5, installationId: identity.InstallationId);
        AddCurrentLicense(database, identity, envelope, payload);
        var validator = new ScriptedValidator(LicenseValidationResult.Fail("offline_revocation_unavailable"));
        var evaluator = CreateEvaluator(database, validator);

        var result = await evaluator.EvaluateCurrentAsync(identity.InstallationId);

        Assert.False(result.IsUsable);
        Assert.Equal("offline_revocation_unavailable", result.ErrorCode);
        Assert.NotEqual("not_revoked", result.ErrorCode);
    }

    [Fact]
    public async Task EvaluateCurrentAsync_ExpiredSnapshot_IsUnavailable()
    {
        await using var database = CreateDatabase();
        var identity = AddCurrentIdentity(database);
        var (envelope, payload) = CreateEnvelope(sequence: 6, installationId: identity.InstallationId);
        AddCurrentLicense(database, identity, envelope, payload);
        var validator = new ScriptedValidator(LicenseValidationResult.Fail("offline_revocation_unavailable"));
        var evaluator = CreateEvaluator(database, validator);

        var result = await evaluator.EvaluateCurrentAsync(identity.InstallationId);

        Assert.False(result.IsUsable);
        Assert.Equal("offline_revocation_unavailable", result.ErrorCode);
    }

    [Fact]
    public async Task EvaluateCurrentAsync_InvalidSnapshotSignature_IsRejected()
    {
        await using var database = CreateDatabase();
        var identity = AddCurrentIdentity(database);
        var (envelope, payload) = CreateEnvelope(sequence: 7, installationId: identity.InstallationId);
        AddCurrentLicense(database, identity, envelope, payload);
        var validator = new ScriptedValidator(LicenseValidationResult.Fail("offline_revocation_signature_invalid"));
        var evaluator = CreateEvaluator(database, validator);

        var result = await evaluator.EvaluateCurrentAsync(identity.InstallationId);

        Assert.False(result.IsUsable);
        Assert.Equal("offline_revocation_signature_invalid", result.ErrorCode);
    }

    [Fact]
    public async Task EvaluateCurrentAsync_UnknownRevocationTrustKey_IsRejected()
    {
        await using var database = CreateDatabase();
        var identity = AddCurrentIdentity(database);
        var (envelope, payload) = CreateEnvelope(sequence: 8, installationId: identity.InstallationId);
        AddCurrentLicense(database, identity, envelope, payload);
        var validator = new ScriptedValidator(LicenseValidationResult.Fail("offline_revocation_key_unknown"));
        var evaluator = CreateEvaluator(database, validator);

        var result = await evaluator.EvaluateCurrentAsync(identity.InstallationId);

        Assert.False(result.IsUsable);
        Assert.Equal("offline_revocation_key_unknown", result.ErrorCode);
    }

    [Fact]
    public async Task EvaluateCurrentAsync_TpmOrStateStoreFailure_FailsClosed()
    {
        await using var database = CreateDatabase();
        var identity = AddCurrentIdentity(database);
        var (envelope, payload) = CreateEnvelope(sequence: 9, installationId: identity.InstallationId);
        AddCurrentLicense(database, identity, envelope, payload);
        var validator = new ThrowingValidator(new OfflineRevocationRollbackAnchorUnavailableException());
        var evaluator = CreateEvaluator(database, validator);

        var result = await evaluator.EvaluateCurrentAsync(identity.InstallationId);

        Assert.False(result.IsUsable);
        Assert.Equal("offline_revocation_unavailable", result.ErrorCode);
    }

    [Fact]
    public async Task EvaluateCurrentAsync_MissingInstalledLicense_IsRejected()
    {
        await using var database = CreateDatabase();
        var identity = AddCurrentIdentity(database);
        var (_, payload) = CreateEnvelope(sequence: 1, installationId: identity.InstallationId);
        var validator = new ScriptedValidator(LicenseValidationResult.Ok(LicenseStatus.Valid, payload));
        var evaluator = CreateEvaluator(database, validator);

        var result = await evaluator.EvaluateCurrentAsync(identity.InstallationId);

        Assert.False(result.IsUsable);
        Assert.Equal("license_missing", result.ErrorCode);
    }

    [Fact]
    public async Task EvaluateCurrentAsync_IsDeterministicAcrossRetries()
    {
        await using var database = CreateDatabase();
        var identity = AddCurrentIdentity(database);
        var (envelope, payload) = CreateEnvelope(sequence: 10, installationId: identity.InstallationId);
        AddCurrentLicense(database, identity, envelope, payload);
        var validator = new ScriptedValidator(LicenseValidationResult.Fail("license_revoked"));
        var evaluator = CreateEvaluator(database, validator);

        var first = await evaluator.EvaluateCurrentAsync(identity.InstallationId);
        var second = await evaluator.EvaluateCurrentAsync(identity.InstallationId);

        Assert.False(first.IsUsable);
        Assert.False(second.IsUsable);
        Assert.Equal(first.ErrorCode, second.ErrorCode);
        Assert.Equal("license_revoked", first.ErrorCode);
    }

    [Fact]
    public async Task EvaluateCurrentAsync_DoesNotTreatGraceAsUsable()
    {
        await using var database = CreateDatabase();
        var identity = AddCurrentIdentity(database);
        var (envelope, payload) = CreateEnvelope(sequence: 11, installationId: identity.InstallationId);
        AddCurrentLicense(database, identity, envelope, payload);
        var validator = new ScriptedValidator(LicenseValidationResult.Ok(LicenseStatus.Grace, payload));
        var evaluator = CreateEvaluator(database, validator);

        var result = await evaluator.EvaluateCurrentAsync(identity.InstallationId);

        Assert.False(result.IsUsable);
        Assert.Equal("license_status_policy_unapproved", result.ErrorCode);
        Assert.Null(result.ValidatedPayload);
    }

    [Fact]
    public async Task EvaluateCurrentAsync_InvalidInstallationId_IsRejected()
    {
        await using var database = CreateDatabase();
        var validator = new ScriptedValidator(LicenseValidationResult.Fail("should_not_be_called"));
        var evaluator = CreateEvaluator(database, validator);

        var result = await evaluator.EvaluateCurrentAsync(" ");

        Assert.False(result.IsUsable);
        Assert.Equal("installation_id_invalid", result.ErrorCode);
    }

    private static CustomerLicenseRuntimeEvaluator CreateEvaluator(
        Data.DatabaseContext database,
        ICustomerLicenseEnvelopeValidator validator) =>
        new(database, validator, NullLogger<CustomerLicenseRuntimeEvaluator>.Instance);

    private static Data.DatabaseContext CreateDatabase() =>
        new(new DbContextOptionsBuilder<Data.DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static Domain.InstallationIdentity AddCurrentIdentity(Data.DatabaseContext database)
    {
        var identity = new Domain.InstallationIdentity
        {
            InstallationId = "install-" + Guid.NewGuid().ToString("N")[..12],
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

    private static void AddCurrentLicense(
        Data.DatabaseContext database,
        Domain.InstallationIdentity identity,
        LicenseEnvelope envelope,
        LicensePayload payload)
    {
        database.InstalledLicenses.Add(new Domain.InstalledLicense
        {
            InstallationIdentityId = identity.Id,
            InstallationId = identity.InstallationId,
            LicenseId = payload.LicenseId,
            Sequence = payload.Sequence,
            FormatVersion = envelope.FormatVersion,
            Kid = envelope.Kid,
            Algorithm = envelope.Algorithm,
            PayloadJson = envelope.PayloadJson,
            SignatureBase64 = envelope.SignatureBase64,
            EnvelopeJson = JsonSerializer.Serialize(envelope, JsonOptions),
            PayloadDigest = "abc",
            ImportedAtUtc = DateTime.UtcNow,
            State = Domain.InstalledLicense.StateCurrent
        });
        database.SaveChanges();
    }

    private static (LicenseEnvelope Envelope, LicensePayload Payload) CreateEnvelope(
        int sequence,
        string installationId)
    {
        var payload = new LicensePayload(
            1,
            "license-" + sequence.ToString("D4"),
            installationId,
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
            new LicenseEnvelope(1, "license-key-1", LicenseAlgorithms.EcdsaP256Sha256, payloadJson, "c2lnbmF0dXJl"),
            payload);
    }

    private sealed class ScriptedValidator : ICustomerLicenseEnvelopeValidator
    {
        private readonly LicenseValidationResult _result;

        public ScriptedValidator(LicenseValidationResult result) => _result = result;

        public Task<LicenseValidationResult> ValidateAsync(
            LicenseEnvelope envelope,
            string localInstallationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_result);
    }

    private sealed class ThrowingValidator : ICustomerLicenseEnvelopeValidator
    {
        private readonly Exception _exception;

        public ThrowingValidator(Exception exception) => _exception = exception;

        public Task<LicenseValidationResult> ValidateAsync(
            LicenseEnvelope envelope,
            string localInstallationId,
            CancellationToken cancellationToken = default) =>
            Task.FromException<LicenseValidationResult>(_exception);
    }
}
