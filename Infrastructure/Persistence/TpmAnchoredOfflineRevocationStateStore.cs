using System.Buffers.Binary;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Faranou.Licensing.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Data;

/// <summary>
/// SQL Server durable state whose accepted snapshot is bound to a TPM 2.0 NV Extend hash-chain.
/// The pending envelope and expected TPM digest make the DB/TPM boundary recoverable after a crash.
/// </summary>
public sealed class TpmAnchoredOfflineRevocationStateStore : IOfflineRevocationStateStore
{
    public const string InitialAnchorDigest = "0000000000000000000000000000000000000000000000000000000000000000";
    private static readonly byte[] DomainSeparator = Encoding.ASCII.GetBytes("FARANOU-OFFLINE-REVOCATION-V1\0");
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

    /// <summary>
    /// Computes the value TPM2_NV_Extend must produce from the current NV digest and the
    /// generation-bound snapshot digest. The TPM performs SHA-256(current || extensionData).
    /// </summary>
    public static string ComputeExpectedAnchorDigest(
        string currentAnchorDigest,
        long nextGeneration,
        string snapshotDigest)
    {
        if (nextGeneration <= 0 || !IsSha256Digest(currentAnchorDigest) || !IsSha256Digest(snapshotDigest))
        {
            throw new ArgumentException("The TPM anchor input is invalid.");
        }

        var extensionData = BuildExtensionData(nextGeneration, snapshotDigest);
        var current = Convert.FromHexString(currentAnchorDigest);
        var input = new byte[current.Length + extensionData.Length];
        Buffer.BlockCopy(current, 0, input, 0, current.Length);
        Buffer.BlockCopy(extensionData, 0, input, current.Length, extensionData.Length);
        return Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
    }

    public async Task<OfflineRevocationState?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        await using var applicationLock = await AcquireApplicationLockAsync(cancellationToken);
        return await GetCurrentCoreAsync(cancellationToken);
    }

    private async Task<OfflineRevocationState?> GetCurrentCoreAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await _database.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        try
        {
            var row = await LoadForUpdateAsync(cancellationToken);
            var hardwareDigest = NormalizeDigest(
                await _rollbackAnchor.ReadDigestAsync(cancellationToken));

            if (row is null)
            {
                if (!string.Equals(hardwareDigest, InitialAnchorDigest, StringComparison.Ordinal))
                {
                    return null;
                }

                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            if (row.PendingEnvelopeJson is null)
            {
                if (row.PendingExpectedGeneration is not null ||
                    row.PendingExpectedAnchorDigest is not null ||
                    !string.Equals(hardwareDigest, row.AnchorDigest, StringComparison.Ordinal))
                {
                    return null;
                }

                if (row.Sequence == 0)
                {
                    if (!IsInitialState(row))
                    {
                        return null;
                    }

                    await transaction.CommitAsync(cancellationToken);
                    return null;
                }

                if (!TryReadAcceptedEnvelope(row, out var acceptedState))
                {
                    return null;
                }

                await transaction.CommitAsync(cancellationToken);
                return acceptedState;
            }

            var expectedGeneration = row.PendingExpectedGeneration;
            if (expectedGeneration is null ||
                expectedGeneration != checked(row.AnchorGeneration + 1) ||
                !TryReadPendingEnvelope(row, out var pendingList, out var pendingDigest))
            {
                return null;
            }

            var expectedDigest = ComputeExpectedAnchorDigest(row.AnchorDigest, expectedGeneration.Value, pendingDigest!);
            if (!string.Equals(row.PendingExpectedAnchorDigest, expectedDigest, StringComparison.Ordinal))
            {
                return null;
            }

            if (string.Equals(hardwareDigest, row.AnchorDigest, StringComparison.Ordinal))
            {
                hardwareDigest = NormalizeDigest(await _rollbackAnchor.ExtendAsync(
                    row.AnchorDigest,
                    BuildExtensionData(expectedGeneration.Value, pendingDigest!),
                    expectedDigest,
                    cancellationToken));
            }

            if (!string.Equals(hardwareDigest, expectedDigest, StringComparison.Ordinal))
            {
                return null;
            }

            ApplyPending(row, pendingList!, pendingDigest!, expectedGeneration.Value, expectedDigest);
            await _database.SaveChangesAsync(cancellationToken);

            if (!TryReadAcceptedEnvelope(row, out var recoveredState))
            {
                return null;
            }

            await transaction.CommitAsync(cancellationToken);
            return recoveredState;
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

        // Hold a SQL Server session application lock across the DB prepare, TPM NV_Extend,
        // and DB finalize phases. This prevents two processes from extending the same pending
        // generation concurrently while still allowing the prepare transaction to commit first.
        await using var applicationLock = await AcquireApplicationLockAsync(cancellationToken);
        _ = await GetCurrentCoreAsync(cancellationToken);

        long expectedGeneration;
        string expectedDigest;
        string currentDigest;
        await using (var transaction = await _database.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken))
        {
            try
            {
                var row = await LoadForUpdateAsync(cancellationToken);
                currentDigest = NormalizeDigest(await _rollbackAnchor.ReadDigestAsync(cancellationToken));

                if (row is null)
                {
                    if (!string.Equals(currentDigest, InitialAnchorDigest, StringComparison.Ordinal))
                    {
                        return false;
                    }

                    row = new Domain.CustomerOfflineRevocationState
                    {
                        Sequence = 0,
                        SnapshotDigest = null,
                        ExpiresAtUtc = null,
                        RevokedLicenseIdsJson = "[]",
                        PreviousAnchorDigest = InitialAnchorDigest,
                        AnchorDigest = InitialAnchorDigest,
                        AnchorGeneration = 0
                    };
                    _database.CustomerOfflineRevocationStates.Add(row);
                }
                else
                {
                    if (row.PendingEnvelopeJson is not null ||
                        row.PendingExpectedGeneration is not null ||
                        row.PendingExpectedAnchorDigest is not null ||
                        !string.Equals(currentDigest, row.AnchorDigest, StringComparison.Ordinal))
                    {
                        return false;
                    }

                    if (row.Sequence == 0)
                    {
                        if (!IsInitialState(row))
                        {
                            return false;
                        }
                    }
                    else if (!TryReadAcceptedEnvelope(row, out _))
                    {
                        return false;
                    }
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

                expectedGeneration = checked(row.AnchorGeneration + 1);
                expectedDigest = ComputeExpectedAnchorDigest(row.AnchorDigest, expectedGeneration, snapshotDigest);
                row.PendingEnvelopeJson = JsonSerializer.Serialize(envelope, JsonOptions);
                row.PendingExpectedGeneration = expectedGeneration;
                row.PendingExpectedAnchorDigest = expectedDigest;
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
            var extendedDigest = NormalizeDigest(await _rollbackAnchor.ExtendAsync(
                currentDigest,
                BuildExtensionData(expectedGeneration, snapshotDigest),
                expectedDigest,
                cancellationToken));
            if (!string.Equals(extendedDigest, expectedDigest, StringComparison.Ordinal))
            {
                return false;
            }

            return await FinalizePendingAsync(expectedGeneration, expectedDigest, cancellationToken);
        }
        catch (OfflineRevocationRollbackAnchorUnavailableException)
        {
            return false;
        }
    }

    private async Task<bool> FinalizePendingAsync(
        long expectedGeneration,
        string expectedDigest,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _database.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var row = await LoadForUpdateAsync(cancellationToken);
        if (row is null ||
            row.PendingEnvelopeJson is null ||
            row.PendingExpectedGeneration != expectedGeneration ||
            !string.Equals(row.PendingExpectedAnchorDigest, expectedDigest, StringComparison.Ordinal) ||
            !string.Equals(
                NormalizeDigest(await _rollbackAnchor.ReadDigestAsync(cancellationToken)),
                expectedDigest,
                StringComparison.Ordinal) ||
            !TryReadPendingEnvelope(row, out var list, out var digest) ||
            !string.Equals(
                ComputeExpectedAnchorDigest(row.AnchorDigest, expectedGeneration, digest!),
                expectedDigest,
                StringComparison.Ordinal))
        {
            return false;
        }

        ApplyPending(row, list!, digest!, expectedGeneration, expectedDigest);
        await _database.SaveChangesAsync(cancellationToken);
        if (!TryReadAcceptedEnvelope(row, out _))
        {
            return false;
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task<IAsyncDisposable> AcquireApplicationLockAsync(CancellationToken cancellationToken)
    {
        await _database.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = _database.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock
                    @Resource = N'Faranou.CustomerOfflineRevocationState',
                    @LockMode = N'Exclusive',
                    @LockOwner = N'Session',
                    @LockTimeout = 15000;
                SELECT @result;
                """;
            var result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            if (result < 0)
            {
                throw new InvalidOperationException("The customer revocation state lock could not be acquired.");
            }

            return new ApplicationLockLease(_database);
        }
        catch
        {
            await _database.Database.CloseConnectionAsync();
            throw;
        }
    }

    private sealed class ApplicationLockLease(DatabaseContext database) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                var connection = database.Database.GetDbConnection();
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    EXEC sys.sp_releaseapplock
                        @Resource = N'Faranou.CustomerOfflineRevocationState',
                        @LockOwner = N'Session';
                    """;
                await command.ExecuteNonQueryAsync();
            }
            finally
            {
                await database.Database.CloseConnectionAsync();
            }
        }
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

        return envelope is not null &&
            TryVerifyEnvelope(envelope, row.Sequence, _timeProvider.GetUtcNow(), out list, out digest);
    }

    private bool TryReadAcceptedEnvelope(
        Domain.CustomerOfflineRevocationState row,
        out OfflineRevocationState? state)
    {
        state = null;
        if (row.Sequence <= 0 ||
            row.AnchorGeneration <= 0 ||
            row.SnapshotDigest is null ||
            row.ExpiresAtUtc is null ||
            string.IsNullOrWhiteSpace(row.AcceptedEnvelopeJson) ||
            !IsSha256Digest(row.PreviousAnchorDigest) ||
            !IsSha256Digest(row.AnchorDigest))
        {
            return false;
        }

        OfflineRevocationListEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<OfflineRevocationListEnvelope>(row.AcceptedEnvelopeJson, JsonOptions);
        }
        catch (JsonException)
        {
            return false;
        }

        if (envelope is null ||
            !TryVerifyEnvelope(envelope, 0, _timeProvider.GetUtcNow(), out var list, out var digest) ||
            list is null ||
            list.Sequence != row.Sequence ||
            !string.Equals(digest, row.SnapshotDigest, StringComparison.Ordinal) ||
            !string.Equals(
                JsonSerializer.Serialize(list.RevokedLicenseIds, JsonOptions),
                row.RevokedLicenseIdsJson,
                StringComparison.Ordinal) ||
            list.ExpiresAtUtc.UtcDateTime != DateTime.SpecifyKind(row.ExpiresAtUtc.Value, DateTimeKind.Utc) ||
            !string.Equals(
                ComputeExpectedAnchorDigest(row.PreviousAnchorDigest, row.AnchorGeneration, row.SnapshotDigest),
                row.AnchorDigest,
                StringComparison.Ordinal))
        {
            return false;
        }

        state = new OfflineRevocationState(
            row.Sequence,
            row.SnapshotDigest,
            new DateTimeOffset(DateTime.SpecifyKind(row.ExpiresAtUtc.Value, DateTimeKind.Utc)),
            new HashSet<string>(list.RevokedLicenseIds, StringComparer.Ordinal))
        {
            AnchorGeneration = row.AnchorGeneration
        };
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

        // A pending/accepted envelope may be recovered after expiry. Verify signature and
        // canonical structure at the last instant before expiry; the state remains expired
        // and the consumer still reports Unavailable for authorization decisions.
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
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException or FormatException)
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
        long generation,
        string expectedAnchorDigest)
    {
        row.PreviousAnchorDigest = row.AnchorDigest;
        row.AnchorDigest = expectedAnchorDigest;
        row.AnchorGeneration = generation;
        row.Sequence = list.Sequence;
        row.SnapshotDigest = digest;
        row.ExpiresAtUtc = list.ExpiresAtUtc.UtcDateTime;
        row.RevokedLicenseIdsJson = JsonSerializer.Serialize(list.RevokedLicenseIds, JsonOptions);
        row.AcceptedEnvelopeJson = row.PendingEnvelopeJson;
        row.PendingEnvelopeJson = null;
        row.PendingExpectedGeneration = null;
        row.PendingExpectedAnchorDigest = null;
    }

    private static bool IsInitialState(Domain.CustomerOfflineRevocationState row) =>
        row.Sequence == 0 &&
        row.AnchorGeneration == 0 &&
        row.SnapshotDigest is null &&
        row.ExpiresAtUtc is null &&
        row.AcceptedEnvelopeJson is null &&
        row.PendingEnvelopeJson is null &&
        row.PendingExpectedGeneration is null &&
        row.PendingExpectedAnchorDigest is null &&
        string.Equals(row.PreviousAnchorDigest, InitialAnchorDigest, StringComparison.Ordinal) &&
        string.Equals(row.AnchorDigest, InitialAnchorDigest, StringComparison.Ordinal) &&
        string.Equals(row.RevokedLicenseIdsJson, "[]", StringComparison.Ordinal);

    private static byte[] BuildExtensionData(long generation, string snapshotDigest)
    {
        if (generation <= 0 || !IsSha256Digest(snapshotDigest))
        {
            throw new ArgumentException("The TPM extension data is invalid.");
        }

        var generationBytes = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(generationBytes, generation);
        var snapshotBytes = Convert.FromHexString(snapshotDigest);
        var result = new byte[DomainSeparator.Length + generationBytes.Length + snapshotBytes.Length];
        Buffer.BlockCopy(DomainSeparator, 0, result, 0, DomainSeparator.Length);
        Buffer.BlockCopy(generationBytes, 0, result, DomainSeparator.Length, generationBytes.Length);
        Buffer.BlockCopy(snapshotBytes, 0, result, DomainSeparator.Length + generationBytes.Length, snapshotBytes.Length);
        return result;
    }

    private static bool IsSha256Digest(string? value)
    {
        if (value is null || value.Length != 64)
        {
            return false;
        }

        try
        {
            return Convert.FromHexString(value).Length == 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string NormalizeDigest(string digest)
    {
        if (!IsSha256Digest(digest))
        {
            throw new OfflineRevocationRollbackAnchorUnavailableException();
        }

        return digest.ToLowerInvariant();
    }
}
