namespace Faranou.Vendor.Host.Authorization;

/// <summary>
/// Exposes only stable, operator-safe reasons for licenses that are valid but cannot yet be used
/// for Vendor provisioning. Internal resolver errors are never reflected to the browser.
/// </summary>
public static class VendorProvisioningLicenseAvailabilityRules
{
    public const string CompanyReferenceMappingRequired = "company_reference_mapping_required";

    public static string? GetOperatorVisibleBlockReason(string? resolverErrorCode) =>
        resolverErrorCode switch
        {
            "provisioning_company_reference_mapping_required" => CompanyReferenceMappingRequired,
            _ => null
        };
}

public sealed record BlockedProvisioningLicenseResponse(
    string LicenseNumber,
    string ProductCode,
    string Reason);
