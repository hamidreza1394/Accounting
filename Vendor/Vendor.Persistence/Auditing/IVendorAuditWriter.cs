namespace Faranou.Vendor.Persistence.Auditing;

/// <summary>
/// Appends metadata-only security events to the Vendor audit chain.
/// Callers must not include passwords, tokens, private keys, full payloads, or financial data.
/// </summary>
public interface IVendorAuditWriter
{
    Task<VendorAuditWriteResult> AppendAsync(
        VendorAuditWriteRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Allow-listed identifiers for one audit event; free-form metadata is deliberately omitted.</summary>
public sealed record VendorAuditWriteRequest(
    Guid? ActorUserId,
    string ActionCode,
    string? SubjectType = null,
    string? SubjectId = null,
    string? CorrelationId = null);

/// <summary>Non-sensitive acknowledgement that an audit record was appended.</summary>
public sealed record VendorAuditWriteResult(Guid EventId, string EventHash, string PreviousHash);
