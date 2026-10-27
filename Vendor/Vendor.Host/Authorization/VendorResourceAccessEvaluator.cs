using System.Security.Claims;
using Faranou.Vendor.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Faranou.Vendor.Host.Authorization;

/// <summary>
/// Resolves resource ownership from persisted Vendor records. Route-supplied parent IDs are used only
/// as a consistency check against the database, never as proof of ownership.
/// </summary>
public sealed class VendorResourceAccessEvaluator(
    VendorDbContext db) : IVendorResourceAccessEvaluator
{
    public async Task<bool> HasAccessAsync(
        ClaimsPrincipal principal,
        VendorResourceDescriptor resource,
        string permission,
        CancellationToken cancellationToken)
    {
        if (principal.Identity?.IsAuthenticated != true ||
            !VendorResourceCatalog.Supports(resource.ResourceType, permission) ||
            resource.ResourceId == Guid.Empty)
        {
            return false;
        }

        return resource.ResourceType switch
        {
            "customer" => await db.Customers.AsNoTracking().AnyAsync(
                item => item.Id == resource.ResourceId,
                cancellationToken),
            "installation" => await db.Installations.AsNoTracking().AnyAsync(
                item => item.Id == resource.ResourceId &&
                    (!resource.ParentResourceId.HasValue || item.CustomerId == resource.ParentResourceId.Value),
                cancellationToken),
            "contract" => await db.Contracts.AsNoTracking().AnyAsync(
                item => item.Id == resource.ResourceId &&
                    (!resource.ParentResourceId.HasValue || item.CustomerId == resource.ParentResourceId.Value),
                cancellationToken),
            "license" => await db.Licenses.AsNoTracking().AnyAsync(
                item => item.Id == resource.ResourceId &&
                    (!resource.ParentResourceId.HasValue || item.CustomerId == resource.ParentResourceId.Value) &&
                    (!item.ContractId.HasValue || db.Contracts.Any(contract =>
                        contract.Id == item.ContractId.Value && contract.CustomerId == item.CustomerId)) &&
                    (!item.InstallationId.HasValue || db.Installations.Any(installation =>
                        installation.Id == item.InstallationId.Value && installation.CustomerId == item.CustomerId)),
                cancellationToken),
            _ => false
        };
    }
}
