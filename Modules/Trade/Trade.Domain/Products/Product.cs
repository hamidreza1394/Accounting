using Faranou.Modules.Trade.Domain.Common;

namespace Faranou.Modules.Trade.Domain.Products;

/// <summary>Company-scoped product/commodity master data. It intentionally has no FiscalPeriodId.</summary>
public sealed class Product : TradeEntity
{
    private readonly List<ProductIdentifier> _identifiers = [];

    private Product()
    {
    }

    public Product(
        Guid tenantId,
        string code,
        string name,
        Guid createdByUserId,
        DateTime createdAtUtc,
        string? productGroupCode = null,
        string? hsCode = null,
        string baseUnitCode = "TON")
        : base(tenantId, createdByUserId, createdAtUtc)
    {
        Code = RequireText(code, 64, nameof(code));
        Name = RequireText(name, 200, nameof(name));
        ProductGroupCode = OptionalText(productGroupCode, 64, nameof(productGroupCode));
        HsCode = OptionalText(hsCode, 16, nameof(hsCode));
        BaseUnitCode = RequireText(baseUnitCode, 16, nameof(baseUnitCode)).ToUpperInvariant();
        IsActive = true;
    }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string? ProductGroupCode { get; private set; }

    /// <summary>Customs tariff code is kept as text to preserve formatting and leading zeroes.</summary>
    public string? HsCode { get; private set; }

    public string BaseUnitCode { get; private set; } = null!;

    public bool IsActive { get; private set; }

    public IReadOnlyCollection<ProductIdentifier> Identifiers => _identifiers.AsReadOnly();

    public ProductIdentifier AddIdentifier(
        Guid? supplierPartyId,
        string? registrationSourceCode,
        string? originCountryCode,
        string? commodityTypeCode,
        string? gtin,
        string? irc,
        DateOnly validUntil,
        bool isLinked,
        Guid createdByUserId,
        DateTime createdAtUtc)
    {
        var identifier = new ProductIdentifier(
            TenantId,
            Id,
            supplierPartyId,
            registrationSourceCode,
            originCountryCode,
            commodityTypeCode,
            gtin,
            irc,
            validUntil,
            isLinked,
            createdByUserId,
            createdAtUtc);

        if (IsDeleted)
        {
            throw new InvalidOperationException("A deleted product cannot be changed.");
        }
        RecordUpdate(createdByUserId, createdAtUtc);
        _identifiers.Add(identifier);
        return identifier;
    }

    public void RemoveIdentifier(Guid identifierId, Guid removedByUserId, DateTime removedAtUtc)
    {
        var identifier = _identifiers.SingleOrDefault(item => item.Id == identifierId && !item.IsDeleted)
            ?? throw new InvalidOperationException("The product identifier does not exist.");

        if (IsDeleted) throw new InvalidOperationException("A deleted product cannot be changed.");
        RecordUpdate(removedByUserId, removedAtUtc);
        identifier.SoftDelete(removedByUserId, removedAtUtc);
    }

    public void UpdateDetails(
        string code,
        string name,
        string? productGroupCode,
        string? hsCode,
        string baseUnitCode,
        Guid updatedByUserId,
        DateTime updatedAtUtc)
    {
        var normalizedCode = RequireText(code, 64, nameof(code));
        var normalizedName = RequireText(name, 200, nameof(name));
        var normalizedGroup = OptionalText(productGroupCode, 64, nameof(productGroupCode));
        var normalizedHsCode = OptionalText(hsCode, 16, nameof(hsCode));
        var normalizedUnit = RequireText(baseUnitCode, 16, nameof(baseUnitCode)).ToUpperInvariant();
        RecordUpdate(updatedByUserId, updatedAtUtc);
        Code = normalizedCode;
        Name = normalizedName;
        ProductGroupCode = normalizedGroup;
        HsCode = normalizedHsCode;
        BaseUnitCode = normalizedUnit;
    }

    public void SetActive(bool isActive, Guid updatedByUserId, DateTime updatedAtUtc)
    {
        RecordUpdate(updatedByUserId, updatedAtUtc);
        IsActive = isActive;
    }
}
