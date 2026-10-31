using Faranou.Modules.Trade.Domain.Common;

namespace Faranou.Modules.Trade.Domain.Parties;

/// <summary>
/// Company-scoped counterparty master data. It intentionally has no FiscalPeriodId.
/// </summary>
public sealed class Party : TradeEntity
{
    private readonly List<PartyRoleAssignment> _roles = [];
    private readonly List<PartyBankAccount> _bankAccounts = [];

    private Party()
    {
    }

    public Party(
        Guid tenantId,
        string code,
        string legalName,
        Guid createdByUserId,
        DateTime createdAtUtc,
        string? legalIdentifier = null,
        string? countryCode = null,
        string? displayName = null)
        : base(tenantId, createdByUserId, createdAtUtc)
    {
        Code = RequireText(code, 64, nameof(code));
        LegalName = RequireText(legalName, 200, nameof(legalName));
        LegalIdentifier = OptionalText(legalIdentifier, 64, nameof(legalIdentifier));
        CountryCode = NormalizeCountryCode(countryCode);
        DisplayName = OptionalText(displayName, 200, nameof(displayName));
        IsActive = true;
    }

    public string Code { get; private set; } = null!;

    public string LegalName { get; private set; } = null!;

    public string? DisplayName { get; private set; }

    public string? LegalIdentifier { get; private set; }

    public string? CountryCode { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyCollection<PartyRoleAssignment> Roles => _roles.AsReadOnly();

    public IReadOnlyCollection<PartyBankAccount> BankAccounts => _bankAccounts.AsReadOnly();

    public void AddRole(PartyRole role, Guid createdByUserId, DateTime createdAtUtc)
    {
        if (_roles.Any(existing => !existing.IsDeleted && existing.Role == role))
        {
            throw new InvalidOperationException("The counterparty already has this role.");
        }

        if (IsDeleted) throw new InvalidOperationException("A deleted counterparty cannot be changed.");
        var assignment = new PartyRoleAssignment(TenantId, Id, role, createdByUserId, createdAtUtc);
        RecordUpdate(createdByUserId, createdAtUtc);
        _roles.Add(assignment);
    }

    public void RemoveRole(PartyRole role, Guid removedByUserId, DateTime removedAtUtc)
    {
        var assignment = _roles.SingleOrDefault(existing => !existing.IsDeleted && existing.Role == role)
            ?? throw new InvalidOperationException("The counterparty does not have this role.");

        if (IsDeleted) throw new InvalidOperationException("A deleted counterparty cannot be changed.");
        RecordUpdate(removedByUserId, removedAtUtc);
        assignment.SoftDelete(removedByUserId, removedAtUtc);
    }

    public PartyBankAccount AddBankAccount(
        string bankName,
        string accountNumber,
        string? iban,
        string accountHolder,
        Guid createdByUserId,
        DateTime createdAtUtc)
    {
        var account = new PartyBankAccount(
            TenantId,
            Id,
            bankName,
            accountNumber,
            iban,
            accountHolder,
            createdByUserId,
            createdAtUtc);

        if (IsDeleted) throw new InvalidOperationException("A deleted counterparty cannot be changed.");
        RecordUpdate(createdByUserId, createdAtUtc);
        _bankAccounts.Add(account);
        return account;
    }

    public void SetActive(bool isActive, Guid updatedByUserId, DateTime updatedAtUtc)
    {
        RecordUpdate(updatedByUserId, updatedAtUtc);
        IsActive = isActive;
    }

    public void UpdateIdentity(
        string code,
        string legalName,
        string? legalIdentifier,
        string? countryCode,
        string? displayName,
        Guid updatedByUserId,
        DateTime updatedAtUtc)
    {
        var normalizedCode = RequireText(code, 64, nameof(code));
        var normalizedLegalName = RequireText(legalName, 200, nameof(legalName));
        var normalizedIdentifier = OptionalText(legalIdentifier, 64, nameof(legalIdentifier));
        var normalizedCountry = NormalizeCountryCode(countryCode);
        var normalizedDisplayName = OptionalText(displayName, 200, nameof(displayName));
        RecordUpdate(updatedByUserId, updatedAtUtc);
        Code = normalizedCode;
        LegalName = normalizedLegalName;
        LegalIdentifier = normalizedIdentifier;
        CountryCode = normalizedCountry;
        DisplayName = normalizedDisplayName;
    }

    private static string? NormalizeCountryCode(string? value)
    {
        var normalized = OptionalText(value, 2, nameof(value));
        if (normalized is not null &&
            (normalized.Length != 2 || normalized.Any(character => !char.IsAsciiLetter(character))))
        {
            throw new ArgumentException("Country code must be a two-letter ISO-style code.", nameof(value));
        }

        return normalized?.ToUpperInvariant();
    }
}
