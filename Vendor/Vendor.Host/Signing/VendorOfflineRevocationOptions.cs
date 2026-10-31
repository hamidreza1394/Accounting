namespace Faranou.Vendor.Host.Signing;

public sealed class VendorOfflineRevocationOptions
{
    public const string SectionName = "Vendor:OfflineRevocation";

    /// <summary>Maximum lifetime of a published offline revocation snapshot.</summary>
    public int SnapshotValidityDays { get; set; } = 7;
}
