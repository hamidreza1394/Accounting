namespace Faranou.Modules.Trade.Domain.Parties;

/// <summary>Explicit review state for a counterparty bank account.</summary>
public enum BankAccountVerificationStatus
{
    Pending = 1,
    Verified = 2,
    Rejected = 3,
}
