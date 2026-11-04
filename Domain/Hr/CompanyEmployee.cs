namespace Domain;

/// <summary>The employment status of a company employee (D-072).</summary>
public enum CompanyEmployeeEmploymentStatus
{
    Active = 1,
    Suspended = 2,
    Terminated = 3,
}

/// <summary>
/// A company-scoped personnel record (S2, D-072). An employee is independent of the login
/// identity: it may exist without a <see cref="User"/> account and without a
/// <see cref="UserCompanyMembership"/>, and being an employee never grants a permission
/// (D-064). The optional one-to-one link to a membership of the SAME company is only a
/// bookkeeping reference; it creates, approves or revokes nothing.
/// </summary>
public sealed class CompanyEmployee : Seedwork.Entity
{
    public Guid TenantId { get; set; }
    public Company Company { get; set; } = null!;

    /// <summary>Personnel number inside the company, unique per company and immutable.</summary>
    public string EmployeeNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    public CompanyEmployeeEmploymentStatus EmploymentStatus { get; set; } =
        CompanyEmployeeEmploymentStatus.Active;

    /// <summary>Optional start of employment (UTC).</summary>
    public DateTime? HiredOnUtc { get; set; }

    /// <summary>Set only with <see cref="CompanyEmployeeEmploymentStatus.Terminated"/>.</summary>
    public DateTime? TerminatedOnUtc { get; set; }

    /// <summary>
    /// Optional link to an approved, active membership of the same company. Terminating the
    /// employee does NOT revoke the membership or the sessions; those run through the
    /// authorized access-management workflows (D-064/D-065).
    /// </summary>
    public Guid? UserCompanyMembershipId { get; set; }
    public UserCompanyMembership? Membership { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid CreatedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = [];

    /// <summary>The organizational history of this employee (own assignments).</summary>
    public ICollection<CompanyEmployeeAssignment> Assignments { get; set; } = new List<CompanyEmployeeAssignment>();

    /// <summary>The assignments in which this employee is the declared direct manager.</summary>
    public ICollection<CompanyEmployeeAssignment> ManagedAssignments { get; set; } = new List<CompanyEmployeeAssignment>();
}
