namespace Domain.SaaS;

/// <summary>Membership is intentionally not tenant-filtered globally: the company picker must find all of a user's companies.</summary>
public sealed class CompanyMembership
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid CompanyRoleId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
}
