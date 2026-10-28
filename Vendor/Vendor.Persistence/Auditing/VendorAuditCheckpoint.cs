using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace Faranou.Vendor.Persistence.Auditing;

/// <summary>
/// Versioned, metadata-only checkpoint for a verified Vendor audit-chain prefix.
/// The checkpoint hash detects accidental or partial modification; trust against a database administrator
/// requires an independently permissioned append-only store.
/// </summary>
public sealed record VendorAuditCheckpoint(
    int SchemaVersion,
    int HashVersion,
    int EventCount,
    string HeadHash,
    DateTimeOffset CreatedAtUtc,
    string CheckpointHash)
{
    public const int CurrentSchemaVersion = 1;
    public const int CurrentHashVersion = 1;

    private static readonly JsonSerializerOptions CanonicalJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static VendorAuditCheckpoint Create(int eventCount, string headHash, DateTimeOffset createdAtUtc)
    {
        ValidateChainPosition(eventCount, headHash);
        var normalizedTimestamp = createdAtUtc.ToUniversalTime();
        var checkpointHash = ComputeHash(
            CurrentSchemaVersion,
            CurrentHashVersion,
            eventCount,
            headHash,
            normalizedTimestamp);

        return new VendorAuditCheckpoint(
            CurrentSchemaVersion,
            CurrentHashVersion,
            eventCount,
            headHash,
            normalizedTimestamp,
            checkpointHash);
    }

    public bool IsValid()
    {
        if (SchemaVersion != CurrentSchemaVersion ||
            HashVersion != CurrentHashVersion ||
            CreatedAtUtc.Offset != TimeSpan.Zero ||
            !IsHash(CheckpointHash))
        {
            return false;
        }

        try
        {
            ValidateChainPosition(EventCount, HeadHash);
            var expectedHash = ComputeHash(SchemaVersion, HashVersion, EventCount, HeadHash, CreatedAtUtc);
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(expectedHash),
                Convert.FromHexString(CheckpointHash));
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static void ValidateChainPosition(int eventCount, string? headHash)
    {
        if (eventCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(eventCount), "Event count cannot be negative.");
        }

        if (!IsHash(headHash))
        {
            throw new ArgumentException("Audit checkpoint head must be a lowercase SHA-256 hash.", nameof(headHash));
        }

        var isGenesis = string.Equals(headHash, VendorAuditChainVerifier.GenesisHash, StringComparison.Ordinal);
        if ((eventCount == 0) != isGenesis)
        {
            throw new ArgumentException("The genesis hash is valid only for an empty audit chain.", nameof(headHash));
        }
    }

    private static string ComputeHash(
        int schemaVersion,
        int hashVersion,
        int eventCount,
        string headHash,
        DateTimeOffset createdAtUtc)
    {
        var canonical = new CanonicalCheckpoint(
            schemaVersion,
            hashVersion,
            eventCount,
            headHash,
            createdAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(canonical, CanonicalJsonOptions);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static bool IsHash(string? hash) =>
        hash is { Length: 64 } && hash.All(character =>
            (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'));

    private sealed record CanonicalCheckpoint(
        int SchemaVersion,
        int HashVersion,
        int EventCount,
        string HeadHash,
        string CreatedAtUtc);
}

/// <summary>
/// Contract for a separately permissioned, append-only checkpoint destination.
/// Implementations must not store checkpoints only in Vendor SQL Server or on the same writable local filesystem.
/// The backing destination must use separate credentials and enforce immutability/retention where supported.
/// Append must be atomic and reject duplicate or non-increasing event counts so concurrent checkpoint writers cannot fork the checkpoint history.
/// No default implementation is registered until a deployment provider is selected.
/// </summary>
public interface IVendorAuditCheckpointStore
{
    Task<VendorAuditCheckpoint?> ReadLatestAsync(CancellationToken cancellationToken = default);

    Task AppendAsync(VendorAuditCheckpoint checkpoint, CancellationToken cancellationToken = default);
}

/// <summary>
/// Creates checkpoints only after full chain verification and rejects rollback or divergence from a
/// previously stored checkpoint. This service deliberately has no local-file or database fallback.
/// </summary>
public sealed class VendorAuditCheckpointService(
    VendorDbContext dbContext,
    IVendorAuditCheckpointStore checkpointStore,
    TimeProvider timeProvider)
{
    public async Task<VendorAuditCheckpoint> CreateCheckpointAsync(CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(checkpointStore);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var events = await dbContext.AuditEvents
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var verification = VendorAuditChainVerifier.Verify(events);
        if (!verification.IsValid || verification.HeadHash is null)
        {
            throw new VendorAuditChainIntegrityException(
                verification.FailureCode ?? "audit-chain-verification-failed");
        }

        var latestCheckpoint = await checkpointStore.ReadLatestAsync(cancellationToken);
        if (latestCheckpoint is not null)
        {
            if (!latestCheckpoint.IsValid())
            {
                throw new VendorAuditChainIntegrityException("invalid-independent-checkpoint");
            }

            if (latestCheckpoint.EventCount > verification.EventCount)
            {
                throw new VendorAuditChainIntegrityException("audit-database-behind-checkpoint");
            }

            if (!IsVerifiedPrefix(events, latestCheckpoint))
            {
                throw new VendorAuditChainIntegrityException("checkpoint-head-not-in-current-chain");
            }

            if (latestCheckpoint.EventCount == verification.EventCount)
            {
                return latestCheckpoint;
            }
        }

        var checkpoint = VendorAuditCheckpoint.Create(
            verification.EventCount,
            verification.HeadHash,
            timeProvider.GetUtcNow());
        await checkpointStore.AppendAsync(checkpoint, cancellationToken);
        return checkpoint;
    }

    private static bool IsVerifiedPrefix(
        List<VendorAuditEvent> events,
        VendorAuditCheckpoint checkpoint)
    {
        if (checkpoint.EventCount == 0)
        {
            return string.Equals(
                checkpoint.HeadHash,
                VendorAuditChainVerifier.GenesisHash,
                StringComparison.Ordinal);
        }

        if (checkpoint.EventCount > events.Count)
        {
            return false;
        }

        var childrenByParentHash = events
            .GroupBy(entry => entry.PreviousHash, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);

        var current = events.SingleOrDefault(entry =>
            string.Equals(entry.PreviousHash, VendorAuditChainVerifier.GenesisHash, StringComparison.Ordinal));
        if (current is null)
        {
            return false;
        }

        for (var position = 1; position < checkpoint.EventCount; position++)
        {
            if (!childrenByParentHash.TryGetValue(current.EventHash, out var children) || children.Length != 1)
            {
                return false;
            }

            current = children[0];
        }

        return string.Equals(current.EventHash, checkpoint.HeadHash, StringComparison.Ordinal);
    }
}
