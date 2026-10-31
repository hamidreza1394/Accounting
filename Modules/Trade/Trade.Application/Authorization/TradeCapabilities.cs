namespace Faranou.Modules.Trade.Application.Authorization;

public enum TradeAccessMode
{
    ReadWrite = 1,
    ReadOnly = 2,
}

/// <summary>What the current user may see of Trade. Permissions are empty unless the module is available.</summary>
public sealed record TradeCapabilities(
    TradeAccessOutcome Outcome,
    TradeAccessMode AccessMode,
    IReadOnlyList<string> Permissions,
    Exception? Fault = null)
{
    public bool Available => Outcome == TradeAccessOutcome.Allowed;
}
