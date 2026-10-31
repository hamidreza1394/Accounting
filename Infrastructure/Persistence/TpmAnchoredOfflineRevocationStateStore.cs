using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Faranou.Licensing.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Data;

/// <summary>
/// SQL Server durable store whose accepted snapshot is anchored to a hardware TPM NV counter.
/// The two-phase pending envelope allows recovery if the process stops between the database
/// prepare, TPM increment, and database finalize steps.
/// </summary>
public sealed class TpmAnchoredOfflineRevocationStateStore : IOfflineRevocationStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly DatabaseContext _database;
    private readonly IOfflineRevocationRollbackAnchor _rollbackAnchor;
    private readonly IOfflineRevocationKeyResolver _keyResolver;
    private readonly TimeProvider _timeProvider;

    public TpmAnchoredOfflineRevocationStateStore(
        DatabaseContext database,
        IOfflineRevocationRollbackAnchor rollbackAnchor,
        IOfflineRevocationKeyResolver keyResolver,
        TimeProvider timeProvider)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _rollbackAnchor = rollbackAnchor ?? throw new ArgumentNullException(nameof(rollbackAnchor));
        _keyResolver = keyResolver ?? throw new ArgumentNullException(nameof(keyResolver));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<OfflineRevocationState?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        await using var transaction = await _database.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        try
        {
            var row = await LoadForUpdateAsync(cancellationToken);
            var generation = await _rollbackAnchor.ReadGenerationAsync(cancellationToken);
            if (generation < 0)
            {
                return null;
            }

            if (row is null)
            {
                if (generation != 0)
                {
                    return null;
                }

                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            if (row.PendingEnvelopeJson is null)
            {
                if (row.PendingExpectedGeneration is not null || generation != row.AnchorGeneration)
                {
                    return null;
                }

                await transaction.CommitAsync(cancellationToken);
                return ToState(row);
            }

            var expected = row.PendingExpectedGeneration;
            if (expected is null || expected != checked(row.AnchorGeneration + 1))
            {
                return null;
            }

            if (generation == row.AnchorGeneration)
            {
                generation = await _rollbackAnchor.AdvanceAsync(row.AnchorGeneration, cancellationToken);
            }

            if (generation != expected.Value ||
                !TryReadPendingEnvelope(row, out var pendingList, out var pendingDigest))
            {
                return null;
            }

            ApplyPending(row, pendingList!, pendingDigest!, expected.Value);
            await _database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ToState(row);
        }
        catch (OfflineRevocationRollbackAnchorUnavailableException)
        {
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            return null;
        }
    }

    public async Task<bool> TryApplyAsync(
        OfflineRevocationList list,
        OfflineRevocationListEnvelope envelope,
        string snapshotDigest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(envelope);

        // This also recovers an interrupted earlier apply. A null result is valid only for an
        // empty installation with a TPM generation of zero; the locked check below distinguishes it.
        _ = await GetCurrentAsync(cancellationToken);

        long expectedGeneration;
        await using (var transaction = await _database.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken))
        {
            try
            {
                var row = await LoadForUpdateAsync(cancellationToken);
                var generation = await _rollbackAnchor.ReadGenerationAsync(cancellationToken);
                if (generation < 0)
                {
                    return false;
                }

                if (row is null)
                {
                    if (generation != 0)
                    {
                        return false;
                    }

                    row = new Domain.CustomerOfflineRevocationState
                    {
                        Sequence = 0,
                        SnapshotDigest = null,
                        ExpiresAtUtc = null,
                        RevokedLicenseIdsJson = "[]",
                        AnchorGeneration = 0
                    };
                    _database.CustomerOfflineRevocationStates.Add(row);
                }
                else if (row.PendingEnvelopeJson is not null ||
                         row.PendingExpectedGeneration is not null ||
                         row.AnchorGeneration != generation)
                {
                    return false;
                }

                if (list.Sequence <= row.Sequence ||
                    !TryVerifyEnvelope(envelope, row.Sequence, _timeProvider.GetUtcNow(), out var verifiedList, out var verifiedDigest) ||
                    verifiedList is null ||
                    verifiedList.Sequence != list.Sequence ||
                    !string.Equals(verifiedDigest, snapshotDigest, StringComparison.Ordinal) ||
                    !string.Equals(OfflineRevocationListValidator.Serialize(verifiedList),
                        OfflineRevocationListValidator.Serialize(list), StringComparison.Ordinal))
                {
                    return false;
                }

                expectedGeneration = checked(generation + 1);
                row.PendingEnvelopeJson = JsonSerializer.Serialize(envelope, JsonOptions);
                row.PendingExpectedGeneration = expectedGeneration;
                await _database.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (OfflineRevocationRollbackAnchorUnavailableException)
            {
                return false;
            }
            catch (DbUpdateConcurrencyException)
            {
                return false;
            }
        }

        try
        {
            var advanced = await _rollbackAnchor.AdvanceAsync(expectedGeneration - 1, cancellationToken);
            if (advanced != expectedGeneration)
            {
                return false;
            }

            return await FinalizePendingAsync(expectedGeneration, cancellationToken);
        }
        catch (OfflineRevocationRollbackAnchorUnavailableException)
        {
            return false;
        }
    }

    private async Task<bool> FinalizePendingAsync(long expectedGeneration, CancellationToken cancellationToken)
    {
        await using var transaction = await _database.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var row = await LoadForUpdateAsync(cancellationToken);
        if (row is null ||
            row.PendingEnvelopeJson is null ||
            row.PendingExpectedGeneration != expectedGeneration ||
            await _rollbackAnchor.ReadGenerationAsync(cancellationToken) != expectedGeneration ||
            !TryReadPendingEnvelope(row, out var list, out var digest))
        {
            return false;
        }

        ApplyPending(row, list!, digest!, expectedGeneration);
        await _database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private Task<Domain.CustomerOfflineRevocationState?> LoadForUpdateAsync(CancellationToken cancellationToken) =>
        _database.CustomerOfflineRevocationStates
            .FromSqlRaw(
                "SELECT * FROM [CustomerOfflineRevocationStates] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {0}",
                Domain.CustomerOfflineRevocationState.SingletonId)
            .SingleOrDefaultAsync(cancellationToken);

    private bool TryReadPendingEnvelope(
        Domain.CustomerOfflineRevocationState row,
        out OfflineRevocationList? list,
        out string? digest)
    {
        list = null;
        digest = null;
        if (string.IsNullOrWhiteSpace(row.PendingEnvelopeJson))
        {
            return false;
        }

        OfflineRevocationListEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<OfflineRevocationListEnvelope>(row.PendingEnvelopeJson, JsonOptions);
        }
        catch (JsonException)
        {
            return false;
        }

        if (envelope is null ||
            !TryVerifyEnvelope(envelope, row.Sequence, _timeProvider.GetUtcNow(), out list, out digest))
        {
            return false;
        }

        return true;
    }

    private bool TryVerifyEnvelope(
        OfflineRevocationListEnvelope envelope,
        long minimumSequence,
        DateTimeOffset now,
        out OfflineRevocationList? list,
        out string? digest)
    {
        list = null;
        digest = null;
        if (!_keyResolver.TryResolve(envelope.KeyId, out var trustedKey) || trustedKey.IsEmpty)
        {
            return false;
        }

        // Pending snapshots may be finalized after their validity window elapsed. Verify their
        // signature and canonical structure at the last instant before expiry; the returned state
        // remains expired and therefore unavailable to license validation.
        DateTimeOffset validationTime = now;
        try
        {
            using var document = JsonDocument.Parse(envelope.PayloadJson);
            var expiresAt = document.RootElement.GetProperty("expiresAtUtc").GetDateTimeOffset();
            if (expiresAt <= now)
            {
                validationTime = expiresAt.AddTicks(-1);
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            return false;
        }

        if (!OfflineRevocationListValidator.TryValidateAndVerify(
                envelope,
                trustedKey.Span,
                validationTime,
                minimumSequence,
                out list,
                out _))
        {
            return false;
        }

        digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(envelope.PayloadJson)))
            .ToLowerInvariant();
        return true;
    }

    private static void ApplyPending(
        Domain.CustomerOfflineRevocationState row,
        OfflineRevocationList list,
        string digest,
        long anchorGeneration)
    {
        row.Sequence = list.Sequence;
        row.SnapshotDigest = digest;
        row.ExpiresAtUtc = list.ExpiresAtUtc.UtcDateTime;
        row.RevokedLicenseIdsJson = JsonSerializer.Serialize(list.RevokedLicenseIds, JsonOptions);
        row.AnchorGeneration = anchorGeneration;
        row.PendingEnvelopeJson = null;
        row.PendingExpectedGeneration = null;
    }

    private static OfflineRevocationState? ToState(Domain.CustomerOfflineRevocationState row)
    {
        if (row.Sequence <= 0 || row.SnapshotDigest is null || row.ExpiresAtUtc is null)
        {
            return null;
        }

        try
        {
            var revokedIds = JsonSerializer.Deserialize<string[]>(row.RevokedLicenseIdsJson, JsonOptions);
            if (revokedIds is null)
            {
                return null;
            }

            return new OfflineRevocationState(
                row.Sequence,
                row.SnapshotDigest,
                new DateTimeOffset(DateTime.SpecifyKind(row.ExpiresAtUtc.Value, DateTimeKind.Utc)),
                new HashSet<string>(revokedIds, StringComparer.Ordinal))
            {
                AnchorGeneration = row.AnchorGeneration
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
