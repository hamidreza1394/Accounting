namespace Faranou.Modules.Trade.Application.Authorization;

/// <summary>The result of the Trade access gate. Anything but <see cref="Allowed" /> denies access.</summary>
public enum TradeAccessOutcome
{
    Allowed = 1,
    Unauthenticated = 2,
    ContextRequired = 3,
    PeriodReadOnly = 4,
    ModuleUnavailable = 5,
    Forbidden = 6,
}
