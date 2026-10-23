namespace Domain;

/// <summary>
/// A customer company and the tenant boundary for accounting data.
/// </summary>
public sealed class Company : Seedwork.Entity,
    Dtat.Seedwork.Abstractions.IEntityHasIsActive
{
    public string Name { get; set; } = string.Empty;
    public string? NationalId { get; set; }
    /// <summary>Opaque, operator-provisioned customer boundary; null means not provisioned and must fail closed for group relationships.</summary>
    public Guid? CustomerBoundaryId { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<UserCompanyMembership> Memberships { get; set; } = new List<UserCompanyMembership>();
    public ICollection<FiscalPeriod> FiscalPeriods { get; set; } = new List<FiscalPeriod>();
    public ICollection<CompanyRole> Roles { get; set; } = new List<CompanyRole>();

    // Navigations of the corporate group model (D-033). They add no column to Companies;
    // a group membership or an ownership grants no access to the company data (D-034).
    public ICollection<CorporateGroup> AdministeredGroups { get; set; } = new List<CorporateGroup>();
    public ICollection<CorporateGroupMember> GroupMemberships { get; set; } = new List<CorporateGroupMember>();
    public ICollection<OwnershipRelationship> OwnedCompanies { get; set; } = new List<OwnershipRelationship>();
    public ICollection<OwnershipRelationship> Owners { get; set; } = new List<OwnershipRelationship>();
    public ICollection<ConsolidationRunCompany> ConsolidationRuns { get; set; } = new List<ConsolidationRunCompany>();
}