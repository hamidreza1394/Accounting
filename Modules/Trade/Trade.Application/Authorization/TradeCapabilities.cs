namespace Faranou.Modules.Trade.Application.Authorization;

/// <summary>Period-scoped access of the selected fiscal period; see <see cref="TradeCapabilities.AccessMode" />.</summary>
public enum TradeAccessMode
{
    ReadWrite = 1,
    ReadOnly = 2,
}

/// <summary>What the current user may see of Trade. Permissions are empty unless the module is available.</summary>
public sealed record TradeCapabilities(
    TradeAccessOutcome Outcome,
    // Describes period-scoped operations only: ReadOnly means the selected fiscal period is closed. Party master-data
    // writes are period-independent (D-084) and are decided by the permissions list, not by this mode.
    TradeAccessMode AccessMode,
    IReadOnlyList<string> Permissions,
    Exception? Fault = null)
{
    public bool Available => Outcome == TradeAccessOutcome.Allowed;
}
