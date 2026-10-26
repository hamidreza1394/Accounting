namespace Faranou.Vendor.Host.Authorization;

/// <summary>
/// Versioned server-side permission identifiers for Vendor management operations.
/// Permission identifiers are API/security contracts, not UI labels or role names.
/// </summary>
public static class VendorPermissionCatalog
{
    public const int Version = 1;
    public const string ClaimType = "vendor:permission:v1";
    public const string PolicyPrefix = "VendorPermission:v1:";

    public const string CustomersRead = "vendor.customers.read";
    public const string CustomersManage = "vendor.customers.manage";
    public const string InstallationsRead = "vendor.installations.read";
    public const string InstallationsManage = "vendor.installations.manage";
    public const string ContractsRead = "vendor.contracts.read";
    public const string ContractsManage = "vendor.contracts.manage";
    public const string LicensesRead = "vendor.licenses.read";
    public const string LicensesManage = "vendor.licenses.manage";
    public const string LicensesIssue = "vendor.licenses.issue";
    public const string AdministratorsManage = "vendor.administrators.manage";
    public const string AuditRead = "vendor.audit.read";

    private static readonly HashSet<string> Permissions = new(StringComparer.Ordinal)
    {
        CustomersRead,
        CustomersManage,
        InstallationsRead,
        InstallationsManage,
        ContractsRead,
        ContractsManage,
        LicensesRead,
        LicensesManage,
        LicensesIssue,
        AdministratorsManage,
        AuditRead
    };

    public static IReadOnlyCollection<string> All { get; } =
        Array.AsReadOnly(Permissions.Order(StringComparer.Ordinal).ToArray());

    public static bool Contains(string permission) =>
        !string.IsNullOrWhiteSpace(permission) && Permissions.Contains(permission);

    public static string PolicyFor(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        return PolicyPrefix + permission;
    }
}
