using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace Faranou.Vendor.Persistence.Auditing;

/// <summary>
/// Verifies a complete audit chain without disclosing event contents in the result.
/// A hash chain detects changed/broken records; it does not protect against an administrator who can rewrite
/// the entire database. Production still requires restricted SQL permissions and independent checkpoints.
/// </summary>
public sealed class VendorAuditChainVerifier
{
    public const string GenesisHash = "0000000000000000000000000000000000000000000000000000000000000000";

    private static readonly JsonSerializerOptions CanonicalJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public async Task<VendorAuditChainVerificationResult> VerifyAsync(
        VendorDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        var events = await dbContext.AuditEvents
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return Verify(events);
    }

    public VendorAuditChainVerificationResult Verify(IEnumerable<VendorAuditEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var entries = events.ToArray();
        if (entries.Length == 0)
        {
            return VendorAuditChainVerificationResult.Valid(0, GenesisHash);
        }

        var byHash = new Dictionary<string, VendorAuditEvent>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (entry.Id == Guid.Empty ||
                !VendorAuditWriter.IsValidActionCode(entry.ActionCode) ||
                !VendorAuditWriter.IsValidSubjectType(entry.SubjectType) ||
                !VendorAuditWriter.IsSafeOpaqueId(entry.SubjectId, 200) ||
                !VendorAuditWriter.IsSafeOpaqueId(entry.CorrelationId, 100) ||
                entry.MetadataJson is not null)
            {
                return VendorAuditChainVerificationResult.Invalid(entries.Length, "invalid-event-fields-or-unapproved-metadata");
            }

            if (!IsHash(entry.EventHash) || !IsHash(entry.PreviousHash))
            {
                return VendorAuditChainVerificationResult.Invalid(entries.Length, "invalid-hash-format");
            }

            var calculatedHash = ComputeEventHash(entry);
            if (!CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(calculatedHash),
                    Convert.FromHexString(entry.EventHash)))
            {
                return VendorAuditChainVerificationResult.Invalid(entries.Length, "event-content-mismatch");
            }

            if (!byHash.TryAdd(entry.EventHash, entry))
            {
                return VendorAuditChainVerificationResult.Invalid(entries.Length, "duplicate-event-hash");
            }
        }

        var roots = entries.Where(entry =>
            string.Equals(entry.PreviousHash, GenesisHash, StringComparison.Ordinal)).ToArray();
        if (roots.Length != 1)
        {
            return VendorAuditChainVerificationResult.Invalid(entries.Length, "invalid-root-count");
        }

        var childrenByParentHash = entries
            .Where(entry => !string.Equals(entry.PreviousHash, GenesisHash, StringComparison.Ordinal))
            .GroupBy(entry => entry.PreviousHash, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            if (string.Equals(entry.PreviousHash, GenesisHash, StringComparison.Ordinal))
            {
                continue;
            }

            if (!byHash.ContainsKey(entry.PreviousHash))
            {
                return VendorAuditChainVerificationResult.Invalid(entries.Length, "missing-parent");
            }
        }

        var visited = new HashSet<string>(StringComparer.Ordinal);
        var current = roots[0];
        while (true)
        {
            if (!visited.Add(current.EventHash))
            {
                return VendorAuditChainVerificationResult.Invalid(entries.Length, "cycle-detected");
            }

            if (!childrenByParentHash.TryGetValue(current.EventHash, out var children))
            {
                break;
            }

            if (children.Length != 1)
            {
                return VendorAuditChainVerificationResult.Invalid(entries.Length, "chain-fork-detected");
            }

            current = children[0];
        }

        if (visited.Count != entries.Length)
        {
            return VendorAuditChainVerificationResult.Invalid(entries.Length, "disconnected-or-cyclic-events");
        }

        return VendorAuditChainVerificationResult.Valid(entries.Length, current.EventHash);
    }

    internal static string ComputeEventHash(VendorAuditEvent entry)
    {
        var canonical = new CanonicalAuditEvent(
            Version: 1,
            Id: entry.Id.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            OccurredAtUtc: entry.OccurredAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            ActorUserId: entry.ActorUserId?.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            ActionCode: entry.ActionCode,
            SubjectType: entry.SubjectType,
            SubjectId: entry.SubjectId,
            CorrelationId: entry.CorrelationId,
            MetadataJson: entry.MetadataJson,
            PreviousHash: entry.PreviousHash);

        var bytes = JsonSerializer.SerializeToUtf8Bytes(canonical, CanonicalJsonOptions);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    internal static bool IsHash(string? hash) =>
        hash is { Length: 64 } && hash.All(character =>
            (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'));

    private sealed record CanonicalAuditEvent(
        int Version,
        string Id,
        string OccurredAtUtc,
        string? ActorUserId,
        string ActionCode,
        string? SubjectType,
        string? SubjectId,
        string? CorrelationId,
        string? MetadataJson,
        string PreviousHash);
}

public sealed record VendorAuditChainVerificationResult(
    bool IsValid,
    int EventCount,
    string? HeadHash,
    string? FailureCode)
{
    public static VendorAuditChainVerificationResult Valid(int eventCount, string headHash) =>
        new(true, eventCount, headHash, null);

    public static VendorAuditChainVerificationResult Invalid(int eventCount, string failureCode) =>
        new(false, eventCount, null, failureCode);
}
