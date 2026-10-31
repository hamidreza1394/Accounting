using System.Security.Cryptography;
using System.Text;

namespace Faranou.Licensing.Contracts;

/// <summary>
/// Resolves revocation-signing key IDs from a trust store dedicated to offline revocation.
/// Implementations must not silently fall back to the license or provisioning key stores.
/// </summary>
public interface IOfflineRevocationKeyResolver
{
    bool TryResolve(string keyId, out ReadOnlyMemory<byte> trustedPublicKeySpki);
}

/// <summary>
/// Durable customer-installation state for the highest accepted revocation snapshot.
/// TryApplyAsync must atomically compare sequence and replace the snapshot state; implementations
/// must reject any sequence that is not strictly greater than the stored high-water mark.
/// </summary>
public interface IOfflineRevocationStateStore
{
    Task<OfflineRevocationState?> GetCurrentAsync(CancellationToken cancellationToken = default);

    Task<bool> TryApplyAsync(
        OfflineRevocationList list,
        OfflineRevocationListEnvelope envelope,
        string snapshotDigest,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Hardware-backed monotonic generation outside the customer database backup domain.
/// Implementations must fail when the TPM NV counter is unavailable, reset, or inconsistent.
/// </summary>
public interface IOfflineRevocationRollbackAnchor
{
    Task<long> ReadGenerationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Advances exactly once only when the current generation equals expectedCurrentGeneration.
    /// The implementation must verify the observed post-increment value before returning.
    /// </summary>
    Task<long> AdvanceAsync(
        long expectedCurrentGeneration,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Current accepted revocation state. The digest is SHA-256 over the exact signed payload bytes.
/// </summary>
public sealed record OfflineRevocationState(
    long Sequence,
    string SnapshotDigest,
    DateTimeOffset ExpiresAtUtc,
    IReadOnlySet<string> RevokedLicenseIds)
{
    /// <summary>The TPM generation that anchors this state outside the database backup domain.</summary>
    public long AnchorGeneration { get; init; }
}

public enum OfflineRevocationCheckStatus
{
    Unavailable = 0,
    NotRevoked = 1,
    Revoked = 2
}

public sealed record OfflineRevocationCheckResult(
    OfflineRevocationCheckStatus Status,
    long? Sequence)
{
    public bool IsAvailable => Status != OfflineRevocationCheckStatus.Unavailable;
}

public sealed record OfflineRevocationApplyResult(bool Success, long? AcceptedSequence, string? ErrorCode)
{
    public static OfflineRevocationApplyResult Applied(long sequence) => new(true, sequence, null);

    public static OfflineRevocationApplyResult Rejected(string errorCode) => new(false, null, errorCode);
}

/// <summary>
/// Verifies and applies signed offline revocation snapshots. The state store is the atomic
/// anti-replay boundary; this class does not claim rollback protection if that store is restored
/// from an older backup or shares the same rollback domain as the customer database.
/// </summary>
public sealed class OfflineRevocationConsumer
{
    private readonly IOfflineRevocationKeyResolver _keyResolver;
    private readonly IOfflineRevocationStateStore _stateStore;
    private readonly TimeProvider _timeProvider;

    public OfflineRevocationConsumer(
        IOfflineRevocationKeyResolver keyResolver,
        IOfflineRevocationStateStore stateStore,
        TimeProvider timeProvider)
    {
        _keyResolver = keyResolver ?? throw new ArgumentNullException(nameof(keyResolver));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<OfflineRevocationApplyResult> ApplyAsync(
        OfflineRevocationListEnvelope? envelope,
        CancellationToken cancellationToken = default)
    {
        if (envelope is null)
        {
            return OfflineRevocationApplyResult.Rejected("offline_revocation_envelope_invalid");
        }

        if (!_keyResolver.TryResolve(envelope.KeyId, out var trustedKey) || trustedKey.IsEmpty)
        {
            return OfflineRevocationApplyResult.Rejected("offline_revocation_key_unknown");
        }

        var current = await _stateStore.GetCurrentAsync(cancellationToken);
        var minimumSequence = current?.Sequence ?? 0;
        if (!OfflineRevocationListValidator.TryValidateAndVerify(
                envelope,
                trustedKey.Span,
                _timeProvider.GetUtcNow(),
                minimumSequence,
                out var list,
                out var errorCode) || list is null)
        {
            return OfflineRevocationApplyResult.Rejected(errorCode);
        }

        var payloadBytes = Encoding.UTF8.GetBytes(envelope.PayloadJson);
        var digest = Convert.ToHexString(SHA256.HashData(payloadBytes)).ToLowerInvariant();
        var applied = await _stateStore.TryApplyAsync(list, envelope, digest, cancellationToken);
        return applied
            ? OfflineRevocationApplyResult.Applied(list.Sequence)
            : OfflineRevocationApplyResult.Rejected("offline_revocation_sequence_invalid");
    }

    /// <summary>
    /// A missing or expired snapshot is Unavailable, never equivalent to NotRevoked.
    /// Callers must fail closed when IsAvailable is false.
    /// </summary>
    public async Task<OfflineRevocationCheckResult> CheckAsync(
        string licenseId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(licenseId))
        {
            return new OfflineRevocationCheckResult(OfflineRevocationCheckStatus.Unavailable, null);
        }

        var current = await _stateStore.GetCurrentAsync(cancellationToken);
        if (current is null || current.ExpiresAtUtc <= _timeProvider.GetUtcNow())
        {
            return new OfflineRevocationCheckResult(OfflineRevocationCheckStatus.Unavailable, current?.Sequence);
        }

        var status = current.RevokedLicenseIds.Contains(licenseId)
            ? OfflineRevocationCheckStatus.Revoked
            : OfflineRevocationCheckStatus.NotRevoked;
        return new OfflineRevocationCheckResult(status, current.Sequence);
    }
}
