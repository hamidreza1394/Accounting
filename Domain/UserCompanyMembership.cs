namespace Domain;

/// <summary>
/// Associates one central user account with a company and its company-scoped role.
/// UserId intentionally remains a scalar until the legacy user store is migrated to ASP.NET Core Identity.
/// </summary>
public sealed class UserCompanyMembership : Seedwork.Entity,
    Dtat.Seedwork.Abstractions.IEntityHasIsActive
{
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public Company Company { get; set; } = null!;
    public Guid RoleId { get; set; }
    public CompanyRole Role { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public byte[] RowVersion { get; set; } = [];
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The HR employee records anchored to this membership (S2, D-072). At most one employee
    /// per membership of the same company (filtered unique index); the link is bookkeeping
    /// only and never grants or changes a permission (D-064).
    /// </summary>
    public ICollection<CompanyEmployee> Employees { get; set; } = new List<CompanyEmployee>();
}