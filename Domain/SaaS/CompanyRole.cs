namespace Domain.SaaS;

/// <summary>Role scoped to a company; it grants no access in other companies.</summary>
public sealed class CompanyRole : ITenantScopedEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public bool IsSystemRole { get; set; }
    public bool IsActive { get; set; } = true;
}
