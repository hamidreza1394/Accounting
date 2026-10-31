using Faranou.Modules.Trade.Domain.Common;

namespace Faranou.Modules.Trade.Domain.Parties;

/// <summary>
/// Bank account belonging to a counterparty. Payment eligibility is explicit and requires verification.
/// </summary>
public sealed class PartyBankAccount : TradeEntity
{
    private PartyBankAccount()
    {
    }

    internal PartyBankAccount(
        Guid tenantId,
        Guid partyId,
        string bankName,
        string accountNumber,
        string? iban,
        string accountHolder,
        Guid createdByUserId,
        DateTime createdAtUtc)
        : base(tenantId, createdByUserId, createdAtUtc)
    {
        PartyId = RequireIdentifier(partyId, nameof(partyId));
        BankName = RequireText(bankName, 160, nameof(bankName));
        AccountNumber = RequireText(accountNumber, 80, nameof(accountNumber));
        Iban = NormalizeIban(iban);
        AccountHolder = RequireText(accountHolder, 160, nameof(accountHolder));
        VerificationStatus = BankAccountVerificationStatus.Pending;
    }

    public Guid PartyId { get; private set; }

    public string BankName { get; private set; } = null!;

    public string AccountNumber { get; private set; } = null!;

    public string? Iban { get; private set; }

    public string AccountHolder { get; private set; } = null!;

    public BankAccountVerificationStatus VerificationStatus { get; private set; }

    public Guid? VerifiedByUserId { get; private set; }

    public DateTime? VerifiedAtUtc { get; private set; }

    public string? RejectionReason { get; private set; }

    /// <summary>Account-level check; payment use cases must also verify the owning Party is active.</summary>
    public bool IsPaymentEligible =>
        !IsDeleted && VerificationStatus == BankAccountVerificationStatus.Verified;

    public void Verify(Guid verifiedByUserId, DateTime verifiedAtUtc)
    {
        if (IsDeleted)
        {
            throw new InvalidOperationException("A deleted bank account cannot be verified.");
        }

        if (VerificationStatus != BankAccountVerificationStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending bank account can be verified.");
        }

        var actor = RequireIdentifier(verifiedByUserId, nameof(verifiedByUserId));
        var timestamp = RequireUtc(verifiedAtUtc, nameof(verifiedAtUtc));
        RecordUpdate(actor, timestamp);
        VerifiedByUserId = actor;
        VerifiedAtUtc = timestamp;
        VerificationStatus = BankAccountVerificationStatus.Verified;
        RejectionReason = null;
    }

    public void Reject(Guid rejectedByUserId, DateTime rejectedAtUtc, string reason)
    {
        if (IsDeleted)
        {
            throw new InvalidOperationException("A deleted bank account cannot be rejected.");
        }

        if (VerificationStatus != BankAccountVerificationStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending bank account can be rejected.");
        }

        var actor = RequireIdentifier(rejectedByUserId, nameof(rejectedByUserId));
        var timestamp = RequireUtc(rejectedAtUtc, nameof(rejectedAtUtc));
        var normalizedReason = RequireText(reason, 500, nameof(reason));
        RecordUpdate(actor, timestamp);
        RejectionReason = normalizedReason;
        VerificationStatus = BankAccountVerificationStatus.Rejected;
    }

    public void ReplaceDetails(
        string bankName,
        string accountNumber,
        string? iban,
        string accountHolder,
        Guid updatedByUserId,
        DateTime updatedAtUtc)
    {
        var normalizedBankName = RequireText(bankName, 160, nameof(bankName));
        var normalizedAccountNumber = RequireText(accountNumber, 80, nameof(accountNumber));
        var normalizedIban = NormalizeIban(iban);
        var normalizedHolder = RequireText(accountHolder, 160, nameof(accountHolder));
        RecordUpdate(updatedByUserId, updatedAtUtc);

        BankName = normalizedBankName;
        AccountNumber = normalizedAccountNumber;
        Iban = normalizedIban;
        AccountHolder = normalizedHolder;
        VerificationStatus = BankAccountVerificationStatus.Pending;
        VerifiedByUserId = null;
        VerifiedAtUtc = null;
        RejectionReason = null;
    }

    private static string? NormalizeIban(string? value)
    {
        var compact = value is null
            ? null
            : new string(value.Where(character => !char.IsWhiteSpace(character)).ToArray());
        var normalized = OptionalText(compact, 34, nameof(value));
        if (normalized is not null && normalized.Any(character => !char.IsAsciiLetterOrDigit(character)))
        {
            throw new ArgumentException("IBAN must contain only ASCII letters and digits.", nameof(value));
        }

        return normalized?.ToUpperInvariant();
    }
}
