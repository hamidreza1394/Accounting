namespace Infrastructure.Licensing;

/// <summary>
/// Deployment modes of the customer host (D-098). Dedicated/Offline keeps the accepted
/// Restricted behaviour (read/export/recovery stay reachable); Cloud blocks normal user
/// operations on expiry while renewal/recovery paths stay reachable for authorized operators.
/// </summary>
public static class LicenseDeploymentModes
{
    public const string Dedicated = "Dedicated";
    public const string Cloud = "Cloud";
}

/// <summary>
/// Configuration of the license-first runtime gate (D-060 §startup gate, Issue #396).
/// The gate is opt-in: with Enabled=false the host behaves exactly as before, which keeps the
/// rollout phased and the existing E2E suites valid. Deployment configuration decides when the
/// enforcement turns on; the gate itself never silently disables itself when enabled.
/// </summary>
public sealed class LicenseRuntimeGateOptions
{
    public const string SectionName = "Licensing:RuntimeGate";
    public const string DeploymentModeSectionName = "Licensing:DeploymentMode";

    public bool Enabled { get; set; }

    /// <summary>Deployment mode from <see cref="LicenseDeploymentModes"/>; decides expiry behaviour.</summary>
    public string DeploymentMode { get; set; } = LicenseDeploymentModes.Dedicated;

    public void Validate()
    {
        if (!string.Equals(DeploymentMode, LicenseDeploymentModes.Dedicated, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(DeploymentMode, LicenseDeploymentModes.Cloud, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Licensing:DeploymentMode must be either Dedicated or Cloud.");
        }
    }

    /// <summary>Normalised deployment mode for decisions and the status endpoint.</summary>
    public string NormalisedDeploymentMode =>
        string.Equals(DeploymentMode, LicenseDeploymentModes.Cloud, StringComparison.OrdinalIgnoreCase)
            ? LicenseDeploymentModes.Cloud
            : LicenseDeploymentModes.Dedicated;
}

/// <summary>
/// Runtime license state of this installation. D-060/D-026/D-098: Unlicensed keeps only the
/// activation/recovery paths reachable; Active grants normal operations; Restricted follows the
/// deployment-specific expiry policy; Invalid is fail-closed and needs a corrected license.
/// </summary>
public enum LicenseRuntimeState
{
    Active = 0,
    Restricted = 1,
    Unlicensed = 2,
    Invalid = 3
}
