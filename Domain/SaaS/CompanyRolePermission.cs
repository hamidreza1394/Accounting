namespace Domain.SaaS;

public sealed class CompanyRolePermission : ITenantScopedEntity
{
    public Guid TenantId { get; set; }
    public Guid CompanyRoleId { get; set; }
    public Guid PermissionId { get; set; }
}
