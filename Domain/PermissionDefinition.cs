namespace Domain;

/// <summary>
/// A stable permission catalog entry, such as accounting.document.create.
/// </summary>
public sealed class PermissionDefinition : Seedwork.Entity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ICollection<CompanyRolePermission> RolePermissions { get; set; } = new List<CompanyRolePermission>();
}