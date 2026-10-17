namespace Domain;

/// <summary>
/// Assigns a permission catalog entry to a company-scoped role.
/// </summary>
public sealed class CompanyRolePermission
{
    public Guid RoleId { get; set; }
    public CompanyRole Role { get; set; } = null!;
    public Guid PermissionId { get; set; }
    public PermissionDefinition Permission { get; set; } = null!;
}