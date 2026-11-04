namespace Domain;

/// <summary>
/// An organizational unit of one company (S2, D-072): the reporting structure is human
/// resources data and never a permission source (D-064). Units form a tree inside the
/// company; a unit of one company can never be the parent of a unit of another company.
/// </summary>
public sealed class CompanyOrganizationalUnit : Seedwork.Entity
{
    /// <summary>The routed company; identical to the tenant identifier of this row.</summary>
    public Guid TenantId { get; set; }
    public Company Company { get; set; } = null!;

    /// <summary>Stable code inside the company, unique per company.</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Parent unit inside the same company; <c>null</c> for a root unit.</summary>
    public Guid? ParentUnitId { get; set; }
    public CompanyOrganizationalUnit? ParentUnit { get; set; }
    public ICollection<CompanyOrganizationalUnit> ChildUnits { get; set; } = new List<CompanyOrganizationalUnit>();

    public bool IsActive { get; set; } = true;

    /// <summary>Optional validity window (UTC); open-ended when <see cref="ValidToUtc"/> is null.</summary>
    public DateTime? ValidFromUtc { get; set; }
    public DateTime? ValidToUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid CreatedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = [];

    /// <summary>The assignments that currently or historically point at this unit.</summary>
    public ICollection<CompanyEmployeeAssignment> Assignments { get; set; } = new List<CompanyEmployeeAssignment>();
}

public enum CompanyUnitDeactivationBlock
{
    None = 0,
    HasActiveChildUnit = 1,
    HasActiveAssignment = 2,
}

public enum CompanyUnitCycleViolation
{
    None = 0,
    EmptyUnitId = 1,
    SelfParent = 2,
    ParentOutsideProvidedChain = 3,
    Cycle = 4,
    DepthLimitExceeded = 5,
}
