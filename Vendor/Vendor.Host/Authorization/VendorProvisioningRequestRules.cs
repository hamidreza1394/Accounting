using Faranou.Licensing.Contracts;

namespace Faranou.Vendor.Host.Authorization;

public sealed record CreateVendorProvisioningRequest(
    Guid RequestId,
    Guid CustomerId,
    Guid InstallationId,
    string? CompanyName,
    string? CompanyType,
    string? InitialAdminUserName,
    IReadOnlyList<string>? Subsystems,
    string? FiscalPeriodTitle = null,
    DateOnly? FiscalPeriodStartDate = null,
    DateOnly? FiscalPeriodEndDate = null,
    string? ParentCompanyReference = null);

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

        var fiscalPeriodTitle = request.FiscalPeriodTitle?.Trim();
        if (string.IsNullOrWhiteSpace(fiscalPeriodTitle) ||
            fiscalPeriodTitle.Length > 100 ||
            fiscalPeriodTitle.Any(char.IsControl) ||
            request.FiscalPeriodStartDate is null ||
            request.FiscalPeriodEndDate is null ||
            request.FiscalPeriodStartDate.Value >= request.FiscalPeriodEndDate.Value)
            return "invalid_provisioning_fiscal_period";

        var parentReference = request.ParentCompanyReference?.Trim();
        var hasValidParentReference = !string.IsNullOrWhiteSpace(parentReference) &&
            parentReference.Length <= 128 &&
            !parentReference.Any(char.IsControl);
        if (companyType == "subsidiary" ? !hasValidParentReference : request.ParentCompanyReference is not null)
            return "invalid_provisioning_parent_company_reference";

        return null;
    }

    public static bool IsTargetActive(string? customerStatus, string? installationStatus) =>
        string.Equals(customerStatus, "Active", StringComparison.Ordinal) &&
        string.Equals(installationStatus, "Active", StringComparison.Ordinal);

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
               existing.SchemaVersion == CustomerProvisioningPackageValidator.CurrentPayloadSchemaVersion &&
               string.Equals(existing.FiscalPeriodTitle, request.FiscalPeriodTitle?.Trim(), StringComparison.Ordinal) &&
               existing.FiscalPeriodStartDate == request.FiscalPeriodStartDate &&
               existing.FiscalPeriodEndDate == request.FiscalPeriodEndDate &&
               string.Equals(existing.ParentCompanyReference, request.ParentCompanyReference?.Trim(), StringComparison.Ordinal) &&
               request.Subsystems is not null &&
               existing.Subsystems.Order(StringComparer.Ordinal)
                   .SequenceEqual(request.Subsystems.Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }
}
