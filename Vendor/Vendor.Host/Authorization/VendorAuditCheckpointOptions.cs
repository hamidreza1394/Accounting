namespace Faranou.Vendor.Host.Authorization;

public sealed class VendorAuditCheckpointOptions
{
    public int IntervalSeconds { get; set; } = 300;
    public int MaximumStalenessSeconds { get; set; } = 900;

    public void Validate()
    {
        if (IntervalSeconds is < 30 or > 3600)
            throw new InvalidOperationException("Audit checkpoint interval must be between 30 and 3600 seconds.");

        if (MaximumStalenessSeconds < IntervalSeconds * 2 || MaximumStalenessSeconds > 86400)
            throw new InvalidOperationException("Audit checkpoint maximum staleness must be at least two intervals and no more than one day.");
    }
}
