namespace Faranou.Modules.Trade.Domain.Units;

/// <summary>
/// Immutable record of one converted quantity exactly as it was at deal time: the unit and quantity the parties
/// traded, the conversion factor and rounding rule that were applied, and the resulting quantity in the product base unit.
/// </summary>
/// <remarks>
/// A later change of a product's conversion factor or rounding rule never touches an existing snapshot, because
/// the snapshot carries its own copy of every input (D-080/D-086 snapshot rule). It is produced only by
/// <see cref="TradeUnitConversion.TryConvert" />; a persisted snapshot is read back as stored and never recomputed.
/// </remarks>
public sealed record TradeQuantitySnapshot(
    string TradeUnitCode,
    decimal TradeQuantity,
    string BaseUnitCode,
    decimal ConversionFactor,
    decimal BaseQuantity,
    int RoundingDecimals,
    TradeRoundingMode RoundingMode);
