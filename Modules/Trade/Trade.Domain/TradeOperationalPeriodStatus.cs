namespace Faranou.Modules.Trade.Domain;

/// <summary>Lifecycle state; user permission to view or modify is evaluated separately.</summary>
public enum TradeOperationalPeriodStatus
{
    Draft = 0,
    Open = 1,
    Closed = 2,
    Archived = 3,
}
