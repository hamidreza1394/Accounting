namespace Faranou.Modules.Trade.Domain.Units;

/// <summary>Why a unit conversion, a conversion set or a quantity conversion was rejected. Never carries rejected values.</summary>
public enum TradeUnitConversionError
{
    None = 0,
    TradeUnitMissing,
    BaseUnitMissing,
    InvalidUnitCode,
    FactorNotPositive,
    FactorTooLarge,
    FactorPrecisionTooHigh,
    SameUnitFactorMustBeOne,
    InvalidRoundingPolicy,
    QuantityNotPositive,
    QuantityTooLarge,
    QuantityPrecisionTooHigh,
    ResultRoundsToZero,
    NoConversions,
    DuplicateTradeUnit,
    BaseUnitMismatch,
    UnitNotAllowed,
}
