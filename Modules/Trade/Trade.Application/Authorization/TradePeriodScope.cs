namespace Faranou.Modules.Trade.Application.Authorization;

/// <summary>
/// A requested company and its shared Core FiscalPeriod for a Trade operation or report.
/// The host must validate that the period belongs to the company before authorization/data access.
/// </summary>
public sealed record TradePeriodScope(Guid TenantId, Guid FiscalPeriodId);
