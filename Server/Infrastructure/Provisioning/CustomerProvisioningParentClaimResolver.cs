using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Provisioning;

/// <summary>
/// Resolves a Vendor-issued parent request only through the already-applied local claim.
/// No company name, NationalId, or operator-supplied local identifier is accepted.
/// </summary>
public static class CustomerProvisioningParentClaimResolver
{
    public const string InvalidParentReferenceError = "provisioning_parent_reference_invalid";

    public static bool TryParseRequestId(string? reference, out Guid requestId)
    {
        requestId = Guid.Empty;
        return Guid.TryParseExact(reference, "D", out requestId) &&
            requestId != Guid.Empty &&
            string.Equals(reference, requestId.ToString("D"), StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<(Guid? ParentCompanyId, string? ErrorCode)> ResolveAsync(
        Data.DatabaseContext database,
        string companyType,
        string? parentCompanyReference,
        CustomerProvisioningSettings settings,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(settings);

        if (!string.Equals(companyType, "subsidiary", StringComparison.Ordinal))
        {
            return parentCompanyReference is null
                ? (null, null)
                : (null, InvalidParentReferenceError);
        }

        if (settings.CustomerBoundaryId == Guid.Empty ||
            !TryParseRequestId(parentCompanyReference, out var parentRequestId))
            return (null, InvalidParentReferenceError);

        var matches = await (
            from claim in database.ProvisioningRequestClaims.AsNoTracking()
            join company in database.Companies.AsNoTracking()
                on claim.CompanyId equals company.Id
            where claim.RequestId == parentRequestId &&
                claim.CustomerReference == settings.CustomerReference &&
                claim.InstallationId == settings.InstallationId &&
                claim.AppliedAtUtc <= utcNow &&
                (claim.CompanyType == "parent" || claim.CompanyType == "subsidiary") &&
                company.IsActive &&
                company.CustomerBoundaryId == settings.CustomerBoundaryId
            select new { claim.Id, claim.CompanyId })
            .Take(2)
            .ToListAsync(cancellationToken);

        if (matches.Count == 1)
            return (matches[0].CompanyId, null);

        return (null, InvalidParentReferenceError);
    }
}
