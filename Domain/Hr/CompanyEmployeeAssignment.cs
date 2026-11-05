namespace Domain;

/// <summary>The lifecycle state of an assignment (D-072).</summary>
public enum CompanyEmployeeAssignmentStatus
{
    Active = 1,
    Ended = 2,
}

/// <summary>
/// One organizational assignment of an employee (S2, D-072): the unit, the position and
/// the direct manager over an effective period. Assignments are an append-only history —
/// a transfer ends the active assignment and starts a new one in the same transaction;
/// history rows are never rewritten. An assignment is HR data and grants no permission
/// and no approval right; a manager reference never authorizes anything (D-064/D-071).
/// </summary>
public sealed class CompanyEmployeeAssignment : Seedwork.Entity
{
    public Guid TenantId { get; set; }
    public Company Company { get; set; } = null!;

    public Guid EmployeeId { get; set; }
    public CompanyEmployee Employee { get; set; } = null!;

    public Guid? OrganizationalUnitId { get; set; }
    public CompanyOrganizationalUnit? OrganizationalUnit { get; set; }

    public Guid? PositionId { get; set; }
    public CompanyPosition? Position { get; set; }

    /// <summary>
    /// Direct manager of the employee inside the same company. The chain is cycle-checked
    /// by the application service; the database check constraint rejects the trivial
    /// self-reference only.
    /// </summary>
    public Guid? ManagerEmployeeId { get; set; }
    public CompanyEmployee? ManagerEmployee { get; set; }

    /// <summary>Effective start (UTC).</summary>
    public DateTime StartOnUtc { get; set; }

    /// <summary>Effective end (UTC); <c>null</c> while the assignment is the active one.</summary>
    public DateTime? EndOnUtc { get; set; }
    public Guid? EndedByUserId { get; set; }

    public CompanyEmployeeAssignmentStatus Status { get; set; } =
        CompanyEmployeeAssignmentStatus.Active;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid CreatedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
