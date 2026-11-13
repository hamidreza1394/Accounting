namespace Faranou.Modules.Trade.Application.Authorization;

/// <summary>
/// Optional, additive per-operation gate options. The default is the strict behaviour every Trade
/// operation gets unless it explicitly opts in.
/// </summary>
public sealed record TradeAccessOptions
{
    public static TradeAccessOptions Default { get; } = new();

    /// <summary>Tenant-level master data write that stays allowed in a closed period (D-063 item 3, D-084).</summary>
    public bool PeriodIndependent { get; init; }

    /// <summary>Read permissions that must also be held (for example view for update).</summary>
    public IReadOnlyList<string> AdditionalReadPermissions { get; init; } = [];
}
