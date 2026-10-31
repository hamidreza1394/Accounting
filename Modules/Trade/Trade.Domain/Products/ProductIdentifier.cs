using Faranou.Modules.Trade.Domain.Common;

namespace Faranou.Modules.Trade.Domain.Products;

/// <summary>
/// A registration/source identifier for a product. GTIN and IRC are independent text values.
/// </summary>
public sealed class ProductIdentifier : TradeEntity
{
    private ProductIdentifier()
    {
    }

    internal ProductIdentifier(
        Guid tenantId,
        Guid productId,
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
        : base(tenantId, createdByUserId, createdAtUtc)
    {
        ProductId = RequireIdentifier(productId, nameof(productId));
        SupplierPartyId = supplierPartyId is null
            ? null
            : RequireIdentifier(supplierPartyId.Value, nameof(supplierPartyId));
        RegistrationSourceCode = OptionalText(registrationSourceCode, 64, nameof(registrationSourceCode));
        OriginCountryCode = NormalizeCountryCode(originCountryCode);
        CommodityTypeCode = OptionalText(commodityTypeCode, 64, nameof(commodityTypeCode));
        Gtin = OptionalText(gtin, 32, nameof(gtin));
        Irc = OptionalText(irc, 64, nameof(irc));

        if (Gtin is null && Irc is null)
        {
            throw new ArgumentException("At least one of GTIN or IRC is required.");
        }

        ValidUntil = RequireValidUntil(validUntil);
        IsLinked = isLinked;
    }

    public Guid ProductId { get; private set; }

    /// <summary>Optional supplier/counterparty associated with this source identifier.</summary>
    public Guid? SupplierPartyId { get; private set; }

    public string? RegistrationSourceCode { get; private set; }

    public string? OriginCountryCode { get; private set; }

    public string? CommodityTypeCode { get; private set; }

    /// <summary>GTIN is text to preserve leading zeroes.</summary>
    public string? Gtin { get; private set; }

    /// <summary>IRC is a separate text identifier and must not be merged with GTIN.</summary>
    public string? Irc { get; private set; }

    public DateOnly ValidUntil { get; private set; }

    public bool IsLinked { get; private set; }

    public void Update(
        Guid? supplierPartyId,
        string? registrationSourceCode,
        string? originCountryCode,
        string? commodityTypeCode,
        string? gtin,
        string? irc,
        DateOnly validUntil,
        bool isLinked,
        Guid updatedByUserId,
        DateTime updatedAtUtc)
    {
        var normalizedGtin = OptionalText(gtin, 32, nameof(gtin));
        var normalizedIrc = OptionalText(irc, 64, nameof(irc));
        if (normalizedGtin is null && normalizedIrc is null)
        {
            throw new ArgumentException("At least one of GTIN or IRC is required.");
        }

        var normalizedSupplierId = supplierPartyId is null
            ? null
            : RequireIdentifier(supplierPartyId.Value, nameof(supplierPartyId));
        var normalizedSource = OptionalText(registrationSourceCode, 64, nameof(registrationSourceCode));
        var normalizedOrigin = NormalizeCountryCode(originCountryCode);
        var normalizedCommodityType = OptionalText(commodityTypeCode, 64, nameof(commodityTypeCode));
        var normalizedValidUntil = RequireValidUntil(validUntil);
        RecordUpdate(updatedByUserId, updatedAtUtc);
        SupplierPartyId = normalizedSupplierId;
        RegistrationSourceCode = normalizedSource;
        OriginCountryCode = normalizedOrigin;
        CommodityTypeCode = normalizedCommodityType;
        Gtin = normalizedGtin;
        Irc = normalizedIrc;
        ValidUntil = normalizedValidUntil;
        IsLinked = isLinked;
    }

    private static DateOnly RequireValidUntil(DateOnly value)
    {
        if (value == default)
        {
            throw new ArgumentException("A validity date is required.", nameof(value));
        }

        return value;
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
