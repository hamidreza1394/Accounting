namespace Domain.SaaS;

/// <summary>Marks data owned by one tenant. TenantId equals TenantCompany.Id.</summary>
public interface ITenantScopedEntity { Guid TenantId { get; set; } }
