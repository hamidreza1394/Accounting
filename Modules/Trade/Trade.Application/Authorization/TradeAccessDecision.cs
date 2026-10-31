namespace Faranou.Modules.Trade.Application.Authorization;

/// <summary>
/// Gate decision. <see cref="Context" /> is set only when the outcome is Allowed.
/// <see cref="Fault" /> carries an entitlement failure (already denied as ModuleUnavailable) so the
/// host can log it; it is never exposed to clients.
/// </summary>
public sealed record TradeAccessDecision(
    TradeAccessOutcome Outcome,
    TradeAccessContext? Context,
    Exception? Fault = null);
