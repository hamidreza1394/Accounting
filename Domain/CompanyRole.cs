namespace Domain;

/// <summary>
/// A role scoped to one company; a role in one company grants no access to another company.
/// </summary>
public sealed class CompanyRole : Seedwork.Entity,
    Dtat.Seedwork.Abstractions.IEntityHasIsActive
{
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsSystemRole { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<UserCompanyMembership> Memberships { get; set; } = new List<UserCompanyMembership>();
    public ICollection<CompanyRolePermission> RolePermissions { get; set; } = new List<CompanyRolePermission>();
}