namespace Domain;

public static class CompanyMembershipRequestStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

/// <summary>
/// A non-operational request to join another company. Only an explicit decision by an
/// authorized destination-company member may create or reactivate UserCompanyMembership.
/// </summary>
public sealed class CompanyMembershipRequest : Seedwork.Entity
{
    public Guid UserId { get; set; }
    public Guid DestinationTenantId { get; set; }
    public Company DestinationCompany { get; set; } = null!;
    public string Status { get; set; } = CompanyMembershipRequestStatuses.Pending;
    public DateTime RequestedAtUtc { get; set; }
    public Guid? ApprovedRoleId { get; set; }
    public CompanyRole? ApprovedRole { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionReason { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
