namespace Faranou.Modules.Trade.Domain.Parties;

/// <summary>Business roles a counterparty may hold in the import workflow.</summary>
public enum PartyRole
{
    Importer = 1,
    Seller = 2,
    FxBroker = 3,
    Carrier = 4,
    Bank = 5,
    Beneficiary = 6,
    Inspector = 7,
    VesselAgent = 8,
    LivestockSupport = 9,
}
