namespace Domain;

/// <summary>
/// A job position of one company (S2, D-072). A position is an independent HR record:
/// it is not attached to one unit and never grants a permission, a data scope or a
/// company role (D-064). It can be reused by assignments of any unit of the company.
/// </summary>
public sealed class CompanyPosition : Seedwork.Entity
{
    public Guid TenantId { get; set; }
    public Company Company { get; set; } = null!;

    /// <summary>Stable code inside the company, unique per company.</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Optional job grade; descriptive only and never a permission input.</summary>
    public int? Rank { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid CreatedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = [];

    /// <summary>The assignments that currently or historically point at this position.</summary>
    public ICollection<CompanyEmployeeAssignment> Assignments { get; set; } = new List<CompanyEmployeeAssignment>();
}
