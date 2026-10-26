using Microsoft.AspNetCore.Authorization;

namespace Faranou.Vendor.Host.Authorization;

public sealed class VendorPermissionRequirement(string permissionCode) : IAuthorizationRequirement
{
    public string PermissionCode { get; } =
        VendorPermissionCatalog.Contains(permissionCode)
            ? permissionCode
            : throw new ArgumentException("Permission code is not in the versioned Vendor catalog.", nameof(permissionCode));
}
