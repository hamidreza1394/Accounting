using Microsoft.Extensions.Configuration;

namespace Faranou.Vendor.Host.Authorization;

/// <summary>
/// Gate for company-bound provisioning schema v5 issuance. v5 is prepared on the Vendor side but
/// stays disabled: the Customer consumer does not yet verify v5 or map CompanyReference to a local
/// Company, so a package issued now could never be applied.
///
/// Two independent keys must both be on: the deployment flag (default off, the same opt-in style as
/// <c>Signing:Pkcs11:Enabled</c>) and <see cref="CustomerConsumerReady"/>, a code-level interlock that
/// only the PR shipping Customer v5 verification and CompanyReference mapping may flip.
/// </summary>
public sealed class VendorProvisioningCompanyReferenceGate
{
    public const string ConfigurationKey = "Provisioning:CompanyReferenceIssuance:Enabled";

    /// <summary>
    /// False until the Customer verifies schema v5 and maps its CompanyReference to a local Company
    /// atomically with Company creation (#392). Configuration alone must never enable v5 issuance before that.
    /// </summary>
    public static bool CustomerConsumerReady => false;

    public static VendorProvisioningCompanyReferenceGate Disabled { get; } = new(enabled: false);

    /// <summary>Direct construction is for tests and internal wiring; production uses <see cref="FromConfiguration"/>.</summary>
    public VendorProvisioningCompanyReferenceGate(bool enabled) => IsEnabled = enabled;

    public bool IsEnabled { get; }

    /// <summary>Only an explicit, parseable <c>true</c> opts in, and only when the Customer consumer is ready.</summary>
    public static VendorProvisioningCompanyReferenceGate FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var requested = bool.TryParse(configuration[ConfigurationKey], out var parsed) && parsed;
        return new VendorProvisioningCompanyReferenceGate(requested && CustomerConsumerReady);
    }
}
