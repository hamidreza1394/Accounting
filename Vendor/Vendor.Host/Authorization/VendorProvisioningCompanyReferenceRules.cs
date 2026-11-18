using Faranou.Licensing.Contracts;
using Microsoft.AspNetCore.Http;

namespace Faranou.Vendor.Host.Authorization;

/// <summary>
/// Selects the one CompanyReference a company-bound provisioning request may carry. The reference is
/// read only from the verified, signed LicensePayload v2 (<c>CompanyEntitlements</c>). The operator value
/// is a selector: it must already be a member of the signed set and is never used as the issued value.
/// Installation-wide limits (<c>MaxCompanies</c>, legacy <c>Subsystems</c>) are never company grants.
/// </summary>
public static class VendorProvisioningCompanyReferenceRules
{
    public static VendorProvisioningCompanySelection Select(
        LicensePayload signedPayload,
        string? requestedCompanyReference,
        IReadOnlyList<string>? requestedSubsystems)
    {
        ArgumentNullException.ThrowIfNull(signedPayload);

        if (signedPayload.FormatVersion != LicensePayload.CurrentFormatVersion)
            return VendorProvisioningCompanySelection.BadRequest("provisioning_company_reference_not_applicable");

        if (requestedCompanyReference is null)
            return VendorProvisioningCompanySelection.BadRequest("provisioning_company_reference_required");

        var companies = signedPayload.CompanyEntitlements ?? [];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var company in companies)
        {
            // A well-formed signed payload never contains these; refuse rather than choose between them.
            if (company is null ||
                !StructuralValidation.IsValidCompanyReference(company.CompanyReference) ||
                !seen.Add(company.CompanyReference!))
                return VendorProvisioningCompanySelection.Conflict("provisioning_company_reference_invalid");
        }

        var selected = companies.SingleOrDefault(company =>
            string.Equals(company!.CompanyReference, requestedCompanyReference, StringComparison.Ordinal));
        if (selected is null)
            return VendorProvisioningCompanySelection.Conflict("provisioning_company_reference_not_entitled");

        // TRADE_IMPORT is a company module but not a provisioning subsystem code.
        var entitlements = (selected.Modules ?? [])
            .Where(LicenseSubsystemCodes.IsAllowed)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (entitlements.Length == 0)
            return VendorProvisioningCompanySelection.Conflict("provisioning_license_has_no_subsystems");

        string[] chosen;
        if (requestedSubsystems is null)
        {
            chosen = entitlements;
        }
        else
        {
            if (requestedSubsystems.Count is < 1 or > 4 ||
                requestedSubsystems.Any(code => string.IsNullOrWhiteSpace(code) ||
                    !LicenseSubsystemCodes.IsAllowed(code)) ||
                requestedSubsystems.Distinct(StringComparer.Ordinal).Count() != requestedSubsystems.Count)
                return VendorProvisioningCompanySelection.BadRequest("invalid_provisioning_subsystems");

            chosen = requestedSubsystems.Order(StringComparer.Ordinal).ToArray();
            if (chosen.Except(entitlements, StringComparer.Ordinal).Any())
                return VendorProvisioningCompanySelection.Conflict("provisioning_subsystem_not_entitled");
        }

        return VendorProvisioningCompanySelection.Success(selected.CompanyReference!, chosen, entitlements);
    }
}

public sealed record VendorProvisioningCompanySelection(
    bool Succeeded,
    int StatusCode,
    string? ErrorCode,
    string? CompanyReference = null,
    IReadOnlyList<string>? Subsystems = null,
    IReadOnlyList<string>? Entitlements = null)
{
    public static VendorProvisioningCompanySelection Success(
        string companyReference, IReadOnlyList<string> subsystems, IReadOnlyList<string> entitlements) =>
        new(true, StatusCodes.Status200OK, null, companyReference, subsystems, entitlements);

    public static VendorProvisioningCompanySelection BadRequest(string code) =>
        new(false, StatusCodes.Status400BadRequest, code);

    public static VendorProvisioningCompanySelection Conflict(string code) =>
        new(false, StatusCodes.Status409Conflict, code);
}
