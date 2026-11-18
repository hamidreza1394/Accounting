using System.Diagnostics.CodeAnalysis;

namespace Faranou.Modules.Trade.Domain.Units;

/// <summary>
/// Immutable record of one converted quantity exactly as it was at deal time: the unit and quantity the parties
/// traded, the conversion factor and rounding rule that were applied, and the resulting quantity in the product base unit.
/// </summary>
/// <remarks>
/// <para>
/// A later change of a product's conversion factor or rounding rule never touches an existing snapshot, because
/// the snapshot carries its own copy of every input (D-080/D-086 snapshot rule).
/// </para>
/// <para>
/// There is no public constructor and no <c>init</c> accessor, so a snapshot whose values disagree with each other
/// cannot be built or produced with a <c>with</c> expression. A snapshot comes from exactly two paths:
/// <see cref="TradeUnitConversion.TryConvert" /> for a new deal, and <see cref="TryRestore" /> for reading a stored
/// snapshot back. <see cref="TryRestore" /> accepts the stored values only if they are the result the same inputs give
/// today under the arithmetic ceilings and the snapshot's own rounding rule; otherwise it reports an error.
/// </para>
/// </remarks>
public sealed record TradeQuantitySnapshot
{
    internal TradeQuantitySnapshot(
        string tradeUnitCode,
        decimal tradeQuantity,
        string baseUnitCode,
        decimal conversionFactor,
        decimal baseQuantity,
        int roundingDecimals,
        TradeRoundingMode roundingMode)
    {
        TradeUnitCode = tradeUnitCode;
        TradeQuantity = tradeQuantity;
        BaseUnitCode = baseUnitCode;
        ConversionFactor = conversionFactor;
        BaseQuantity = baseQuantity;
        RoundingDecimals = roundingDecimals;
        RoundingMode = roundingMode;
    }

    public string TradeUnitCode { get; }

    public decimal TradeQuantity { get; }

    public string BaseUnitCode { get; }

    public decimal ConversionFactor { get; }

    public decimal BaseQuantity { get; }

    public int RoundingDecimals { get; }

    public TradeRoundingMode RoundingMode { get; }

    /// <summary>
    /// Restores a stored snapshot. The stored values are validated, not trusted: the inputs must satisfy the same
    /// limits as a new conversion and <paramref name="baseQuantity" /> must equal what those inputs produce.
    /// Unit codes must already be in their stored (normalized upper-case) form.
    /// </summary>
    public static bool TryRestore(
        string? tradeUnitCode,
        decimal tradeQuantity,
        string? baseUnitCode,
        decimal conversionFactor,
        decimal baseQuantity,
        int roundingDecimals,
        TradeRoundingMode roundingMode,
        [NotNullWhen(true)] out TradeQuantitySnapshot? snapshot,
        out TradeUnitConversionError error)
    {
        snapshot = null;

        if (!TradeUnitConversion.TryCreate(
                tradeUnitCode,
                baseUnitCode,
                conversionFactor,
                new TradeRoundingPolicy(roundingDecimals, roundingMode),
                out var conversion,
                out error)
            || !conversion.TryConvert(tradeQuantity, out var recomputed, out error))
        {
            return false;
        }

        // Stored codes are normalized by construction; a stored value that only matches after trimming or
        // case-folding is not what was stored, so it is rejected rather than silently corrected.
        if (!string.Equals(recomputed.TradeUnitCode, tradeUnitCode, StringComparison.Ordinal)
            || !string.Equals(recomputed.BaseUnitCode, baseUnitCode, StringComparison.Ordinal)
            || recomputed.BaseQuantity != baseQuantity)
        {
            error = TradeUnitConversionError.SnapshotInconsistent;
            return false;
        }

        snapshot = recomputed;
        error = TradeUnitConversionError.None;
        return true;
    }
}
