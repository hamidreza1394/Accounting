using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Faranou.Licensing.Contracts;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.Licensing;

public sealed record CustomerInstallationIdentityEnrollmentResult(
    bool Success,
    bool AlreadyEnrolled,
    string? ErrorCode,
    string? KeyId,
    string? AssuranceLevel);

/// <summary>
/// Persists public metadata for a key that was already created by the explicit Windows CNG
/// ceremony. Enrollment never creates, replaces, or exports a private key.
/// </summary>
public sealed class CustomerInstallationIdentityEnrollmentService
{
    private const string EnrollmentLockResource = "faranou:licensing:installation-identity-enrollment";
    private const int LockTimeoutMilliseconds = 15_000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly Data.DatabaseContext _database;
    private readonly WindowsCngLicenseInstallationKeyOptions _keyOptions;
    private readonly string _installationId;
    private readonly IInstallationIdentityProofVerifier _proofVerifier;
    private readonly TimeProvider _timeProvider;

    public CustomerInstallationIdentityEnrollmentService(
        Data.DatabaseContext database,
        IOptions<WindowsCngLicenseInstallationKeyOptions> keyOptions,
        IOptions<Infrastructure.Provisioning.CustomerProvisioningSettings> provisioningSettings,
        IInstallationIdentityProofVerifier proofVerifier,
        TimeProvider timeProvider)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _keyOptions = keyOptions.Value;
        _installationId = provisioningSettings.Value.InstallationId ?? string.Empty;
        _proofVerifier = proofVerifier ?? throw new ArgumentNullException(nameof(proofVerifier));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<CustomerInstallationIdentityEnrollmentResult> EnrollAsync(
        Guid actorUserId,
        bool confirmSoftwareProviderRisk,
        CancellationToken cancellationToken = default)
    {
        if (actorUserId == Guid.Empty || string.IsNullOrWhiteSpace(_installationId))
            return Failure("installation_identity_configuration_invalid");

        if (!OperatingSystem.IsWindows())
            return Failure("installation_identity_platform_unsupported");

        if (_keyOptions.ProviderName == WindowsCngLicenseInstallationKeyProvisioner.SoftwareProviderName &&
            !confirmSoftwareProviderRisk)
            return Failure("software_provider_explicit_consent_required");

        WindowsCngLicenseInstallationKeyProvisioningResult metadata;
        try
        {
            metadata = WindowsCngLicenseInstallationKeyProvisioner.ReadExisting(
                _keyOptions, _installationId, confirmSoftwareProviderRisk);
        }
        catch (Exception exception) when (exception is CryptographicException or
            UnauthorizedAccessException or InvalidOperationException or ArgumentException or
            PlatformNotSupportedException)
        {
            return Failure("installation_identity_key_unavailable");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var identity = new Domain.InstallationIdentity
        {
            Id = Guid.NewGuid(),
            InstallationId = _installationId,
            KeyId = metadata.KeyId,
            KeyVersion = _keyOptions.KeyVersion,
            ProviderName = metadata.ProviderName,
            AssuranceLevel = metadata.Assurance.ToString(),
            OsInstallationMarkerDigest = metadata.OsInstallationMarkerDigest,
            PublicKeyFingerprint = metadata.PublicKeyFingerprint,
            PublicKeySubjectPublicKeyInfoBase64 = Convert.ToBase64String(metadata.PublicKeySubjectPublicKeyInfo),
            SoftwareProviderRiskAcceptedAtUtc = metadata.Assurance == LicenseInstallationKeyAssurance.SoftwareProtected
                ? now
                : null,
            SoftwareProviderRiskAcceptedBy = metadata.Assurance == LicenseInstallationKeyAssurance.SoftwareProtected
                ? actorUserId.ToString("D")
                : null,
            State = Domain.InstallationIdentity.StateCurrent,
            ProvisionedAtUtc = now
        };

        if (await _proofVerifier.VerifyAsync(identity, cancellationToken) != InstallationIdentityProofStatus.Valid)
            return Failure("installation_identity_proof_failed");

        await using var transaction = _database.Database.IsRelational()
            ? await _database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        if (!await TryAcquireTransactionLockAsync(cancellationToken))
        {
            if (transaction is not null)
                await transaction.RollbackAsync(cancellationToken);
            return Failure("installation_identity_enrollment_busy");
        }

        try
        {
            var identities = await _database.InstallationIdentities
                .Where(item => item.InstallationId == _installationId)
                .OrderByDescending(item => item.KeyVersion)
                .ToListAsync(cancellationToken);
            var current = identities.SingleOrDefault(item =>
                item.State == Domain.InstallationIdentity.StateCurrent);

            if (current is not null &&
                current.KeyId == identity.KeyId &&
                current.KeyVersion == identity.KeyVersion &&
                current.ProviderName == identity.ProviderName &&
                current.AssuranceLevel == identity.AssuranceLevel &&
                current.OsInstallationMarkerDigest == identity.OsInstallationMarkerDigest &&
                current.PublicKeyFingerprint == identity.PublicKeyFingerprint)
            {
                if (transaction is not null)
                    await transaction.CommitAsync(cancellationToken);
                return new CustomerInstallationIdentityEnrollmentResult(
                    true, true, null, current.KeyId, current.AssuranceLevel);
            }

            var highestVersion = identities.Select(item => item.KeyVersion).DefaultIfEmpty(0).Max();
            if (identity.KeyVersion <= highestVersion)
                return await RejectWithinTransactionAsync(
                    transaction, "installation_identity_key_version_not_increasing",
                    actorUserId, identity.KeyId, cancellationToken);

            if (current is not null)
            {
                current.State = Domain.InstallationIdentity.StateRetired;
                current.RetiredAtUtc = now;
            }

            _database.InstallationIdentities.Add(identity);
            _database.SecurityAuditEvents.Add(CreateAudit(
                current is null ? "InstallationIdentityEnrolled" : "InstallationIdentityReplaced",
                actorUserId,
                identity,
                now));

            await _database.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);

            return new CustomerInstallationIdentityEnrollmentResult(
                true, false, null, identity.KeyId, identity.AssuranceLevel);
        }
        catch (DbUpdateException)
        {
            if (transaction is not null)
                await transaction.RollbackAsync(cancellationToken);
            _database.ChangeTracker.Clear();
            return await RecordFailureAsync(
                "installation_identity_enrollment_conflict", actorUserId, identity.KeyId, cancellationToken);
        }
    }

    private async Task<CustomerInstallationIdentityEnrollmentResult> RejectWithinTransactionAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction,
        string errorCode,
        Guid actorUserId,
        string keyId,
        CancellationToken cancellationToken)
    {
        _database.SecurityAuditEvents.Add(CreateAudit(
            "InstallationIdentityEnrollmentRejected", actorUserId, null,
            _timeProvider.GetUtcNow().UtcDateTime, new { errorCode, keyId }));
        await _database.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
        return Failure(errorCode);
    }

    private async Task<CustomerInstallationIdentityEnrollmentResult> RecordFailureAsync(
        string errorCode,
        Guid actorUserId,
        string? keyId,
        CancellationToken cancellationToken)
    {
        try
        {
            _database.SecurityAuditEvents.Add(CreateAudit(
                "InstallationIdentityEnrollmentRejected", actorUserId, null,
                _timeProvider.GetUtcNow().UtcDateTime, new { errorCode, keyId }));
            await _database.SaveChangesAsync(cancellationToken);
            return Failure(errorCode);
        }
        catch (DbUpdateException)
        {
            return Failure("license_audit_unavailable");
        }
    }

    private static Domain.SecurityAuditEvent CreateAudit(
        string eventType,
        Guid actorUserId,
        Domain.InstallationIdentity? identity,
        DateTime occurredAtUtc,
        object? details = null) => new()
    {
        UserId = actorUserId,
        EventType = eventType,
        EntityType = "InstallationIdentity",
        EntityId = identity is null
            ? null
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity.InstallationId))).ToLowerInvariant(),
        OccurredAtUtc = occurredAtUtc,
        DetailsJson = JsonSerializer.Serialize(new
        {
            keyId = identity?.KeyId,
            assuranceLevel = identity?.AssuranceLevel,
            keyVersion = identity?.KeyVersion,
            details
        }, JsonOptions)
    };

    private async Task<bool> TryAcquireTransactionLockAsync(CancellationToken cancellationToken)
    {
        if (!_database.Database.IsSqlServer())
            return true;

        var result = new SqlParameter("@result", SqlDbType.Int)
        {
            Direction = ParameterDirection.Output
        };
        await _database.Database.ExecuteSqlRawAsync(
            "EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', " +
            "@LockOwner = 'Transaction', @LockTimeout = @timeout;",
            new object[]
            {
                result,
                new SqlParameter("@resource", EnrollmentLockResource),
                new SqlParameter("@timeout", LockTimeoutMilliseconds)
            },
            cancellationToken);
        return result.Value is int code && code >= 0;
    }

    private static CustomerInstallationIdentityEnrollmentResult Failure(string errorCode) =>
        new(false, false, errorCode, null, null);
}
