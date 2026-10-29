using System;

namespace Infrastructure.Provisioning;

/// <summary>
/// Customer-installation identity and independently provisioned Vendor public-key trust.
/// These values are deployment configuration, never read from an untrusted package.
/// </summary>
public sealed class CustomerProvisioningSettings
{
    public const string SectionName = "Provisioning";

    public string CustomerReference { get; set; } = string.Empty;
    public string InstallationId { get; set; } = string.Empty;
    public Guid CustomerBoundaryId { get; set; }
}
