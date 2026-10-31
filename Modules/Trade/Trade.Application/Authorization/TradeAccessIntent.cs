namespace Faranou.Modules.Trade.Application.Authorization;

/// <summary>
/// Whether a Trade permission check is for a read/report operation or a data-changing operation.
/// The caller is trusted application code; this is never accepted directly from the client.
/// </summary>
public enum TradeAccessIntent
{
    Read = 1,
    Write = 2,
}
