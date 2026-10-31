namespace Faranou.Vendor.Host.Authorization;

/// <summary>
/// In-memory operational state for the latest full audit-chain/checkpoint verification.
/// Only timestamps and exception type names are retained; provider messages and secrets are never stored.
/// </summary>
public sealed class VendorAuditCheckpointHealthState
{
    private readonly object _sync = new();
    private DateTimeOffset? _lastSuccessUtc;
    private string? _lastFailureType;

    public void MarkSuccess(DateTimeOffset verifiedAtUtc)
    {
        lock (_sync)
        {
            _lastSuccessUtc = verifiedAtUtc.ToUniversalTime();
            _lastFailureType = null;
        }
    }

    public void MarkFailure(string failureType)
    {
        lock (_sync)
        {
            _lastFailureType = string.IsNullOrWhiteSpace(failureType) ? "UnknownFailure" : failureType;
        }
    }

    public (DateTimeOffset? LastSuccessUtc, string? LastFailureType) Snapshot()
    {
        lock (_sync)
        {
            return (_lastSuccessUtc, _lastFailureType);
        }
    }
}
