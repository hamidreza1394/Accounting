namespace Domain;

/// <summary>
/// A customer company and the tenant boundary for accounting data.
/// </summary>
public sealed class Company : Seedwork.Entity,
    Dtat.Seedwork.Abstractions.IEntityHasIsActive
{
    public string Name { get; set; } = string.Empty;
    public string? NationalId { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<UserCompanyMembership> Memberships { get; set; } = new List<UserCompanyMembership>();
    public ICollection<FiscalPeriod> FiscalPeriods { get; set; } = new List<FiscalPeriod>();
    public ICollection<CompanyRole> Roles { get; set; } = new List<CompanyRole>();
}