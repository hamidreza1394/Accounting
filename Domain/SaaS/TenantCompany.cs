namespace Domain.SaaS;

public sealed class TenantCompany
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NationalId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
}
