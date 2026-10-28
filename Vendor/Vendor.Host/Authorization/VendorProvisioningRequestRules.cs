using Faranou.Licensing.Contracts;

namespace Faranou.Vendor.Host.Authorization;

public sealed record CreateVendorProvisioningRequest(
    Guid RequestId,
    Guid CustomerId,
    Guid InstallationId,
    string? CompanyName,
    string? CompanyType,
    string? InitialAdminUserName,
    IReadOnlyList<string>? Subsystems);

/// <summary>Pure validation rules for a Vendor-side provisioning request; no customer DB access.</summary>
public static class VendorProvisioningRequestRules
{
    private static readonly HashSet<string> CompanyTypes =
        new(StringComparer.Ordinal) { "independent", "parent", "subsidiary" };

    private static readonly HashSet<string> Subsystems =
        new(StringComparer.Ordinal) { "finance", "sales", "inventory", "treasury" };

    public static string? Validate(CreateVendorProvisioningRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.RequestId == Guid.Empty || request.CustomerId == Guid.Empty || request.InstallationId == Guid.Empty)
            return "invalid_provisioning_request_id";

        var companyName = request.CompanyName?.Trim();
        var companyType = request.CompanyType?.Trim();
        var adminUserName = request.InitialAdminUserName?.Trim();
        if (string.IsNullOrWhiteSpace(companyName) || companyName.Length > 200 ||
            companyName.Any(char.IsControl) ||
            string.IsNullOrWhiteSpace(companyType) || !CompanyTypes.Contains(companyType) ||
            string.IsNullOrWhiteSpace(adminUserName) || adminUserName.Length is < 3 or > 64 ||
            adminUserName.Any(char.IsControl))
            return "invalid_provisioning_configuration";

        if (request.Subsystems is null || request.Subsystems.Count is < 1 or > 4 ||
            request.Subsystems.Any(code => string.IsNullOrWhiteSpace(code) || !Subsystems.Contains(code)) ||
            request.Subsystems.Distinct(StringComparer.Ordinal).Count() != request.Subsystems.Count)
            return "invalid_provisioning_subsystems";

        return null;
    }

    public static bool MatchesExisting(
        CustomerProvisioningPayload existing,
        CreateVendorProvisioningRequest request,
        string customerReference,
        string installationId)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(request);

        return string.Equals(existing.RequestId, request.RequestId.ToString("D"), StringComparison.OrdinalIgnoreCase) &&
               string.Equals(existing.CustomerReference, customerReference, StringComparison.Ordinal) &&
               string.Equals(existing.InstallationId, installationId, StringComparison.Ordinal) &&
               string.Equals(existing.CompanyName, request.CompanyName?.Trim(), StringComparison.Ordinal) &&
               string.Equals(existing.CompanyType, request.CompanyType?.Trim(), StringComparison.Ordinal) &&
               string.Equals(existing.InitialAdminUserName, request.InitialAdminUserName?.Trim(), StringComparison.Ordinal) &&
               request.Subsystems is not null &&
               existing.Subsystems.Order(StringComparer.Ordinal)
                   .SequenceEqual(request.Subsystems.Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }
}
