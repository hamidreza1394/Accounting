using Faranou.Licensing.Contracts;

namespace Faranou.Vendor.Host.Authorization;

/// <summary>
/// Compares a signed provisioning payload with a freshly resolved current license binding.
/// Used at signing and every package export; never infers or fills missing legacy fields.
/// </summary>
public static class VendorProvisioningLicenseBindingRules
{
    public static bool MatchesCurrentBinding(
        CustomerProvisioningPayload payload,
        VendorProvisioningLicenseBindingResult binding)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(binding);

        return binding.Succeeded &&
               payload.SchemaVersion == CustomerProvisioningPackageValidator.CurrentPayloadSchemaVersion &&
               string.Equals(payload.LicenseId, binding.LicenseId, StringComparison.OrdinalIgnoreCase) &&
               payload.Sequence == binding.Sequence &&
               binding.Subsystems is not null &&
               payload.Subsystems.Order(StringComparer.Ordinal)
                   .SequenceEqual(binding.Subsystems.Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }
}
