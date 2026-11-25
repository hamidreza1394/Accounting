namespace Faranou.Modules.Trade.Application.Authorization;

/// <summary>
/// Decision of the company-scoped gate path. <see cref="Context" /> is set only when the outcome is Allowed.
/// <see cref="TradeAccessOutcome.PeriodReadOnly" /> is never produced by this path because it has no period.
/// <see cref="Fault" /> carries an entitlement failure (already denied as ModuleUnavailable); it is never exposed to clients.
/// </summary>
public sealed record TradeCompanyAccessDecision(
    TradeAccessOutcome Outcome,
    TradeCompanyAccessContext? Context,
    Exception? Fault = null);
