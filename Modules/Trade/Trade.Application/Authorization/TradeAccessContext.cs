namespace Faranou.Modules.Trade.Application.Authorization;

/// <summary>
/// Server-validated company/period context of one request. Produced only by the host adapter
/// (ITradeAuthorization.ResolveCurrentContextAsync); never built from client input. The adapter
/// rejects any instance it did not resolve itself in the current request.
/// </summary>
public sealed record TradeAccessContext(Guid UserId, Guid TenantId, Guid FiscalPeriodId, bool AllowsWrites);
