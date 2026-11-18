using System.Diagnostics.CodeAnalysis;

namespace Faranou.Modules.Trade.Domain.Units;

/// <summary>
/// The trade units one product may be dealt in, each with its own exact conversion to the product base unit.
/// </summary>
/// <remarks>
/// <para>
/// A unit is allowed if and only if the set has an entry for it. The base unit itself is allowed only when it has
/// an explicit entry (factor 1), so the rounding rule for quantities stated in the base unit is a visible choice too.
/// </para>
/// <para>
/// Pure in-memory type: no persistence, no reference-data lookup, no ownership. How a product's set is stored is a
/// schema decision that waits for the Tenant/Company gate (D-087, D-090).
/// </para>
/// </remarks>
public sealed class ProductUnitConversionSet
{
    private readonly Dictionary<string, TradeUnitConversion> _byTradeUnit;

    private ProductUnitConversionSet(string baseUnitCode, Dictionary<string, TradeUnitConversion> byTradeUnit)
    {
        BaseUnitCode = baseUnitCode;
        _byTradeUnit = byTradeUnit;
    }

    public string BaseUnitCode { get; }

    public IReadOnlyCollection<TradeUnitConversion> Conversions => _byTradeUnit.Values;

    public static bool TryCreate(
        string? baseUnitCode,
        IEnumerable<TradeUnitConversion>? conversions,
        [NotNullWhen(true)] out ProductUnitConversionSet? set,
        out TradeUnitConversionError error)
    {
        set = null;

        if (!TradeUnitConversion.TryNormalizeUnitCode(baseUnitCode, TradeUnitConversionError.BaseUnitMissing, out var baseUnit, out error))
        {
            return false;
        }

        var byTradeUnit = new Dictionary<string, TradeUnitConversion>(StringComparer.Ordinal);
        foreach (var conversion in conversions ?? [])
        {
            if (conversion is null || !string.Equals(conversion.BaseUnitCode, baseUnit, StringComparison.Ordinal))
            {
                error = TradeUnitConversionError.BaseUnitMismatch;
                return false;
            }

            if (!byTradeUnit.TryAdd(conversion.TradeUnitCode, conversion))
            {
                error = TradeUnitConversionError.DuplicateTradeUnit;
                return false;
            }
        }

        if (byTradeUnit.Count == 0)
        {
            error = TradeUnitConversionError.NoConversions;
            return false;
        }

        set = new ProductUnitConversionSet(baseUnit, byTradeUnit);
        error = TradeUnitConversionError.None;
        return true;
    }

    public bool TryResolve(string? tradeUnitCode, [NotNullWhen(true)] out TradeUnitConversion? conversion)
    {
        conversion = null;
        return TradeUnitConversion.TryNormalizeUnitCode(tradeUnitCode, TradeUnitConversionError.TradeUnitMissing, out var code, out _)
            && _byTradeUnit.TryGetValue(code, out conversion);
    }

    /// <summary>Converts a quantity stated in an allowed trade unit; a unit without an entry is rejected as not allowed.</summary>
    public bool TryConvert(
        string? tradeUnitCode,
        decimal tradeQuantity,
        [NotNullWhen(true)] out TradeQuantitySnapshot? snapshot,
        out TradeUnitConversionError error)
    {
        snapshot = null;

        if (!TradeUnitConversion.TryNormalizeUnitCode(tradeUnitCode, TradeUnitConversionError.TradeUnitMissing, out var code, out error))
        {
            return false;
        }

        if (!_byTradeUnit.TryGetValue(code, out var conversion))
        {
            error = TradeUnitConversionError.UnitNotAllowed;
            return false;
        }

        return conversion.TryConvert(tradeQuantity, out snapshot, out error);
    }
}
