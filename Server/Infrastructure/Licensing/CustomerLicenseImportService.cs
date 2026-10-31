using System.Data;
using Microsoft.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Faranou.Licensing.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Licensing;

public sealed record CustomerLicenseImportResult(
    bool Success,
    bool AlreadyInstalled,
    string? ErrorCode,
    LicenseStatus? Status);

/// <summary>
/// Imports a signed customer license only after the revocation-aware validator accepts it.
/// The database row is durable state, never an independent source of license validity.
/// </summary>
public sealed class CustomerLicenseImportService
{
    private const int MaximumEnvelopeJsonLength = 300_000;
    private const string ImportLockResource = "faranou:licensing:customer-import";
    private const int LockTimeoutMilliseconds = 15_000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly Data.DatabaseContext _database;
    private readonly ICustomerLicenseEnvelopeValidator _validator;
    private readonly IInstallationIdentityProofVerifier _identityProofVerifier;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CustomerLicenseImportService> _logger;

    public CustomerLicenseImportService(
        Data.DatabaseContext database,
        ICustomerLicenseEnvelopeValidator validator,
        IInstallationIdentityProofVerifier identityProofVerifier,
        TimeProvider timeProvider,
        ILogger<CustomerLicenseImportService> logger)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _identityProofVerifier = identityProofVerifier ?? throw new ArgumentNullException(nameof(identityProofVerifier));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CustomerLicenseImportResult> ImportAsync(
        string envelopeJson,
        string localInstallationId,
        Guid? actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(localInstallationId) || localInstallationId.Length > 128)
            return await RejectAsync("installation_id_invalid", localInstallationId, null, actorUserId, cancellationToken);

        if (string.IsNullOrWhiteSpace(envelopeJson) || envelopeJson.Length > MaximumEnvelopeJsonLength)
            return await RejectAsync("envelope_json_invalid", localInstallationId, null, actorUserId, cancellationToken);

        LicenseEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<LicenseEnvelope>(envelopeJson, JsonOptions);
        }
        catch (JsonException)
        {
            return await RejectAsync("envelope_json_invalid", localInstallationId, null, actorUserId, cancellationToken);
        }

        if (envelope is null)
            return await RejectAsync("envelope_json_invalid", localInstallationId, null, actorUserId, cancellationToken);

        var validation = await _validator.ValidateAsync(envelope, localInstallationId, cancellationToken);
        if (!validation.Success || validation.Payload is null)
            return await RejectAsync(validation.ErrorCode ?? "license_validation_failed",
                localInstallationId, null, actorUserId, cancellationToken);

        // OD-03/04 are intentionally unresolved. Do not silently activate Grace or Restricted
        // states until the product owner approves their operational semantics.
        if (validation.Status is not (LicenseStatus.Valid or LicenseStatus.ExpiringSoon))
            return await RejectAsync("license_status_policy_unapproved",
                localInstallationId, validation.Payload.LicenseId, actorUserId, cancellationToken);

        var payload = validation.Payload;
        var payloadDigest = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(envelope.PayloadJson))).ToLowerInvariant();

        IDbContextTransaction? transaction = null;
        try
        {
            if (_database.Database.IsRelational())
                transaction = await _database.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable, cancellationToken);

            if (!await TryAcquireTransactionLockAsync(localInstallationId, cancellationToken))
            {
                await RollbackQuietlyAsync(transaction, cancellationToken);
                if (transaction is not null)
                {
                    await transaction.DisposeAsync();
                    transaction = null;
                }
                return await RejectAsync("license_import_busy", localInstallationId,
                    payload.LicenseId, actorUserId, cancellationToken);
            }

            var identity = await _database.InstallationIdentities
                .SingleOrDefaultAsync(item =>
                    item.InstallationId == localInstallationId &&
                    item.State == Domain.InstallationIdentity.StateCurrent,
                    cancellationToken);

            if (identity is null)
                return await RejectWithinTransactionAsync(
                    transaction, "installation_identity_missing", localInstallationId,
                    payload.LicenseId, actorUserId, cancellationToken);

            var identityProof = await _identityProofVerifier.VerifyAsync(identity, cancellationToken);
            if (identityProof == InstallationIdentityProofStatus.InstallationChanged)
            {
                var changedAt = _timeProvider.GetUtcNow().UtcDateTime;
                identity.State = Domain.InstallationIdentity.StateReactivationRequired;
                identity.RetiredAtUtc = changedAt;
                _database.SecurityAuditEvents.Add(CreateAudit(
                    "InstallationIdentityChanged", localInstallationId, payload.LicenseId,
                    actorUserId, changedAt, new { reasonCode = "os_installation_or_key_changed" }));
                await _database.SaveChangesAsync(cancellationToken);
                if (transaction is not null)
                    await transaction.CommitAsync(cancellationToken);
                return new CustomerLicenseImportResult(
                    false, false, "installation_identity_changed_reactivation_required", LicenseStatus.Invalid);
            }

            if (identityProof != InstallationIdentityProofStatus.Valid)
                return await RejectWithinTransactionAsync(
                    transaction, "installation_identity_proof_unavailable", localInstallationId,
                    payload.LicenseId, actorUserId, cancellationToken);

            identity.LastVerifiedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;

            var current = await _database.InstalledLicenses
                .SingleOrDefaultAsync(item =>
                    item.InstallationId == localInstallationId &&
                    item.State == Domain.InstalledLicense.StateCurrent,
                    cancellationToken);

            var highestSequence = await _database.InstalledLicenses
                .Where(item => item.InstallationId == localInstallationId)
                .Select(item => (int?)item.Sequence)
                .MaxAsync(cancellationToken) ?? 0;

            if (current is not null &&
                current.LicenseId == payload.LicenseId &&
                current.Sequence == payload.Sequence &&
                string.Equals(current.PayloadDigest, payloadDigest, StringComparison.Ordinal))
            {
                await _database.SaveChangesAsync(cancellationToken);
                if (transaction is not null)
                    await transaction.CommitAsync(cancellationToken);
                return new CustomerLicenseImportResult(true, true, null, validation.Status);
            }

            if (current is not null &&
                !string.Equals(current.LicenseId, payload.LicenseId, StringComparison.Ordinal))
            {
                return await RejectWithinTransactionAsync(
                    transaction, "license_id_change_requires_reactivation", localInstallationId,
                    payload.LicenseId, actorUserId, cancellationToken);
            }

            if (payload.Sequence <= highestSequence)
            {
                return await RejectWithinTransactionAsync(
                    transaction, "license_sequence_replay_or_downgrade", localInstallationId,
                    payload.LicenseId, actorUserId, cancellationToken);
            }

            if (current is not null)
                current.State = Domain.InstalledLicense.StateSuperseded;

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            _database.InstalledLicenses.Add(new Domain.InstalledLicense
            {
                InstallationIdentityId = identity.Id,
                InstallationId = payload.InstallationId,
                LicenseId = payload.LicenseId,
                Sequence = payload.Sequence,
                FormatVersion = envelope.FormatVersion,
                Kid = envelope.Kid,
                Algorithm = envelope.Algorithm,
                PayloadJson = envelope.PayloadJson,
                SignatureBase64 = envelope.SignatureBase64,
                EnvelopeJson = envelopeJson,
                PayloadDigest = payloadDigest,
                ImportedAtUtc = now,
                State = Domain.InstalledLicense.StateCurrent
            });

            _database.SecurityAuditEvents.Add(CreateAudit(
                current is null ? "LicenseImported" : "LicenseReplaced",
                localInstallationId,
                payload.LicenseId,
                actorUserId,
                now,
                new { payload.Sequence, payloadDigest, validation.Status }));

            await _database.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
            return new CustomerLicenseImportResult(true, false, null, validation.Status);
        }
        catch (DbUpdateConcurrencyException)
        {
            await RollbackQuietlyAsync(transaction, cancellationToken);
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
                transaction = null;
            }
            _database.ChangeTracker.Clear();
            return await RejectAsync("license_import_concurrency_conflict",
                localInstallationId, payload.LicenseId, actorUserId, cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            await RollbackQuietlyAsync(transaction, cancellationToken);
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
                transaction = null;
            }
            _database.ChangeTracker.Clear();
            _logger.LogWarning(exception, "Customer license import conflicted with persisted state.");
            return await RejectAsync("license_import_conflict",
                localInstallationId, payload.LicenseId, actorUserId, cancellationToken);
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }

    private async Task<CustomerLicenseImportResult> RejectWithinTransactionAsync(
        IDbContextTransaction? transaction,
        string errorCode,
        string installationId,
        string? licenseId,
        Guid? actorUserId,
        CancellationToken cancellationToken)
    {
        _database.SecurityAuditEvents.Add(CreateAudit(
            "LicenseImportRejected", installationId, licenseId, actorUserId,
            _timeProvider.GetUtcNow().UtcDateTime, new { errorCode }));
        await _database.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
        return new CustomerLicenseImportResult(false, false, errorCode, LicenseStatus.Invalid);
    }

    private async Task<CustomerLicenseImportResult> RejectAsync(
        string errorCode,
        string installationId,
        string? licenseId,
        Guid? actorUserId,
        CancellationToken cancellationToken)
    {
        try
        {
            _database.SecurityAuditEvents.Add(CreateAudit(
                "LicenseImportRejected", installationId, licenseId, actorUserId,
                _timeProvider.GetUtcNow().UtcDateTime, new { errorCode }));
            await _database.SaveChangesAsync(cancellationToken);
            return new CustomerLicenseImportResult(false, false, errorCode, LicenseStatus.Invalid);
        }
        catch (Exception exception) when (exception is DbUpdateException or InvalidOperationException)
        {
            _logger.LogError(exception, "Could not persist customer license rejection audit event.");
            return new CustomerLicenseImportResult(false, false, "license_audit_unavailable", LicenseStatus.Invalid);
        }
    }

    private static Domain.SecurityAuditEvent CreateAudit(
        string eventType,
        string installationId,
        string? licenseId,
        Guid? actorUserId,
        DateTime occurredAtUtc,
        object details) => new()
    {
        UserId = actorUserId,
        EventType = eventType,
        EntityType = "Installation",
        EntityId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(installationId))).ToLowerInvariant(),
        OccurredAtUtc = occurredAtUtc,
        DetailsJson = JsonSerializer.Serialize(new
        {
            licenseId,
            details
        }, JsonOptions)
    };

    private async Task<bool> TryAcquireTransactionLockAsync(string installationId, CancellationToken cancellationToken)
    {
        if (!_database.Database.IsSqlServer())
            return true;

        var result = new SqlParameter("@result", SqlDbType.Int)
        {
            Direction = System.Data.ParameterDirection.Output
        };
        await _database.Database.ExecuteSqlRawAsync(
            "EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', " +
            "@LockOwner = 'Transaction', @LockTimeout = @timeout;",
            new object[]
            {
                result,
                new SqlParameter("@resource", ImportLockResource + ":" + installationId),
                new SqlParameter("@timeout", LockTimeoutMilliseconds)
            },
            cancellationToken);

        return result.Value is int code && code >= 0;
    }

    private static async Task RollbackQuietlyAsync(
        IDbContextTransaction? transaction,
        CancellationToken cancellationToken)
    {
        if (transaction is null)
            return;

        try
        {
            await transaction.RollbackAsync(cancellationToken);
        }
        catch (Exception)
        {
            // Preserve the original persistence error; the transaction is disposed by the caller.
        }
    }
}
