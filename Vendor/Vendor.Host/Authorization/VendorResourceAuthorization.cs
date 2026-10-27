using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Faranou.Vendor.Host.Authorization;

/// <summary>Typed reference to a Vendor-owned resource. Never construct this from an unvalidated route value alone.</summary>
public sealed record VendorResourceDescriptor(string ResourceType, Guid ResourceId, Guid? ParentResourceId = null);

/// <summary>Authorization requirement for one operation on one concrete resource.</summary>
public sealed record VendorResourcePermissionRequirement(string ResourceType, string Permission) : IAuthorizationRequirement;

/// <summary>
/// Domain adapter that must resolve the actual resource and enforce its parent/customer boundary.
/// Implementations must not trust submitted tenant/customer IDs without loading the persisted resource.
/// </summary>
public interface IVendorResourceAccessEvaluator
{
    Task<bool> HasAccessAsync(
        ClaimsPrincipal principal,
        VendorResourceDescriptor resource,
        string permission,
        CancellationToken cancellationToken);
}

/// <summary>
/// Safe default until a concrete Vendor domain adapter is registered. Missing domain authorization
/// integration must deny rather than accidentally allow cross-resource access.
/// </summary>
public sealed class DenyAllVendorResourceAccessEvaluator : IVendorResourceAccessEvaluator
{
    public Task<bool> HasAccessAsync(
        ClaimsPrincipal principal,
        VendorResourceDescriptor resource,
        string permission,
        CancellationToken cancellationToken) => Task.FromResult(false);
}

public static class VendorResourceCatalog
{
    public const string PolicyPrefix = "VendorResource:v1:";

    public static string PolicyFor(string resourceType, string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceType);
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        return PolicyPrefix + resourceType + ":" + permission;
    }

    private static readonly Dictionary<string, IReadOnlySet<string>> Operations =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["customer"] = new HashSet<string>(StringComparer.Ordinal)
            {
                VendorPermissionCatalog.CustomersRead,
                VendorPermissionCatalog.CustomersManage
            },
            ["installation"] = new HashSet<string>(StringComparer.Ordinal)
            {
                VendorPermissionCatalog.InstallationsRead,
                VendorPermissionCatalog.InstallationsManage
            },
            ["contract"] = new HashSet<string>(StringComparer.Ordinal)
            {
                VendorPermissionCatalog.ContractsRead,
                VendorPermissionCatalog.ContractsManage
            },
            ["license"] = new HashSet<string>(StringComparer.Ordinal)
            {
                VendorPermissionCatalog.LicensesRead,
                VendorPermissionCatalog.LicensesManage,
                VendorPermissionCatalog.LicensesIssue
            }
        };

    public static bool Supports(string? resourceType, string? permission) =>
        !string.IsNullOrWhiteSpace(resourceType) &&
        !string.IsNullOrWhiteSpace(permission) &&
        Operations.TryGetValue(resourceType, out var permissions) &&
        permissions.Contains(permission);
}

public sealed class VendorResourcePermissionAuthorizationHandler(
    IVendorResourceAccessEvaluator resourceAccess,
    Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<VendorResourcePermissionRequirement, VendorResourceDescriptor>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        VendorResourcePermissionRequirement requirement,
        VendorResourceDescriptor resource)
    {
        if (!string.Equals(resource.ResourceType, requirement.ResourceType, StringComparison.Ordinal) ||
            !VendorResourceCatalog.Supports(requirement.ResourceType, requirement.Permission) ||
            resource.ResourceId == Guid.Empty ||
            context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        // The dynamic resource policy separately requires VendorAdminAccessRequirement and
        // VendorPermissionRequirement; this handler adds the persisted resource-boundary check.
        var cancellationToken = httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;
        if (await resourceAccess.HasAccessAsync(
                context.User,
                resource,
                requirement.Permission,
                cancellationToken))
        {
            context.Succeed(requirement);
        }
    }
}
