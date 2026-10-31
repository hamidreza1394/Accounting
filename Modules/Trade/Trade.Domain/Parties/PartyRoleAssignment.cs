using Faranou.Modules.Trade.Domain.Common;

namespace Faranou.Modules.Trade.Domain.Parties;

/// <summary>Tenant-scoped assignment of one business role to a counterparty.</summary>
public sealed class PartyRoleAssignment : TradeEntity
{
    private PartyRoleAssignment()
    {
    }

    internal PartyRoleAssignment(
        Guid tenantId,
        Guid partyId,
        PartyRole role,
        Guid createdByUserId,
        DateTime createdAtUtc)
        : base(tenantId, createdByUserId, createdAtUtc)
    {
        PartyId = RequireIdentifier(partyId, nameof(partyId));
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role));
        }

        Role = role;
    }

    public Guid PartyId { get; private set; }

    public PartyRole Role { get; private set; }
}
