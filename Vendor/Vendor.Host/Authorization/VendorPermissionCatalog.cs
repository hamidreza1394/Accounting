namespace Faranou.Vendor.Host.Authorization;

public sealed record VendorPermissionDefinition(string Code, string Description);

/// <summary>
/// Versioned allowlist of permissions. Unknown codes fail closed. Permission grants are stored
/// as Identity role claims and checked against this catalog on every protected request.
/// </summary>
public static class VendorPermissionCatalog
{
    public const int Version = 1;
    public const string ClaimType = "vendor.permission.v1";
    public const string OwnerRoleName = "VendorOwner";
    public const string PanelAccess = "vendor.panel.access";

    private static readonly VendorPermissionDefinition[] Definitions =
    [
        new(PanelAccess, "Access the Vendor control panel"),
        new("dashboard.read", "Read Vendor operational dashboard"),
        new("customers.read", "Read customer accounts"),
        new("customers.manage", "Create and update customer accounts"),
        new("installations.read", "Read customer installations"),
        new("installations.manage", "Create and update installations"),
        new("contracts.read", "Read commercial contracts"),
        new("contracts.manage", "Create and update commercial contracts"),
        new("products.read", "Read products and modules"),
        new("products.manage", "Manage products and modules"),
        new("licenses.read", "Read issued licenses"),
        new("licenses.issue", "Issue signed licenses"),
        new("licenses.revoke", "Revoke eligible time-limited licenses"),
        new("support.read", "Read support entitlements"),
        new("support.manage", "Manage support entitlements"),
        new("admins.read", "Read Vendor administrator accounts"),
        new("admins.manage", "Manage Vendor administrator accounts"),
        new("roles.manage", "Manage role and permission grants"),
        new("audit.read", "Read Vendor audit events"),
        new("approvals.read", "Read maker-checker approval requests"),
        new("approvals.manage", "Create and approve eligible maker-checker requests"),
        new("signing-keys.rotate", "Request an approved signing-key rotation")
    ];

    public static IReadOnlyList<VendorPermissionDefinition> All { get; } =
        Array.AsReadOnly(Definitions);

    public static bool Contains(string code) =>
        Definitions.Any(definition => string.Equals(definition.Code, code, StringComparison.Ordinal));

    public static bool TryGet(string code, out VendorPermissionDefinition? definition)
    {
        definition = Definitions.FirstOrDefault(candidate =>
            string.Equals(candidate.Code, code, StringComparison.Ordinal));
        return definition is not null;
    }
}
