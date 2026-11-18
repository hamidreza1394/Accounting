using System.Diagnostics.CodeAnalysis;

namespace Faranou.Modules.Trade.Domain.Units;

/// <summary>
/// Exact conversion of a traded quantity (for example TON) to the product base unit (for example KG)
/// with a factor and a rounding rule that belong to one product.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Factor" /> is the amount of the base unit in one trade unit (TON to KG is 1000). The result is
/// computed with <see cref="decimal" /> arithmetic, never with floating point, and then rounded once with the
/// explicit <see cref="Rounding" /> policy.
/// </para>
/// <para>
/// The numeric ceilings below exist so that the product of quantity and factor is always exact in a 96-bit decimal.
/// Premise: both inputs are accepted only if they equal their own value rounded to <see cref="MaxScale" /> decimals,
/// so at that effective scale the quantity coefficient is at most 10^15 (10^9 x 10^6) and the factor coefficient is
/// at most 10^12 (10^6 x 10^6). Their product is at most 10^27 at scale twelve, below 2^96 (about 7.9 x 10^28).
/// The bound holds even when an accepted representation stores a larger scale (for example 1.0000000000000000000000000000m):
/// the extra digits are trailing zeros, so the multiplication never has to round away a significant digit.
/// They are arithmetic limits, not business rules; raising them needs a review of that argument.
/// </para>
/// <para>
/// This type knows nothing about reference data, persistence or ownership. Whether a unit code exists in the unit
/// reference list is checked by the Application layer that builds the conversion.
/// </para>
/// </remarks>
public sealed class TradeUnitConversion : IEquatable<TradeUnitConversion>
{
    public const int MaxUnitCodeLength = 16;

    public const decimal MaxFactor = 1_000_000m;

    public const decimal MaxQuantity = 1_000_000_000m;

    /// <summary>Largest number of decimal places accepted for a factor or a quantity.</summary>
    public const int MaxScale = 6;

    private TradeUnitConversion(string tradeUnitCode, string baseUnitCode, decimal factor, TradeRoundingPolicy rounding)
    {
        TradeUnitCode = tradeUnitCode;
        BaseUnitCode = baseUnitCode;
        Factor = factor;
        Rounding = rounding;
    }

    /// <summary>Upper-case ASCII code of the unit the parties trade in.</summary>
    public string TradeUnitCode { get; }

    /// <summary>Upper-case ASCII code of the product base unit.</summary>
    public string BaseUnitCode { get; }

    /// <summary>Base-unit amount contained in one trade unit; always positive.</summary>
    public decimal Factor { get; }

    public TradeRoundingPolicy Rounding { get; }

    public static bool TryCreate(
        string? tradeUnitCode,
        string? baseUnitCode,
        decimal factor,
        TradeRoundingPolicy rounding,
        [NotNullWhen(true)] out TradeUnitConversion? conversion,
        out TradeUnitConversionError error)
    {
        conversion = null;

        if (!TryNormalizeUnitCode(tradeUnitCode, TradeUnitConversionError.TradeUnitMissing, out var trade, out error)
            || !TryNormalizeUnitCode(baseUnitCode, TradeUnitConversionError.BaseUnitMissing, out var baseUnit, out error))
        {
            return false;
        }

        if (factor <= 0m)
        {
            error = TradeUnitConversionError.FactorNotPositive;
            return false;
        }

        if (factor > MaxFactor)
        {
            error = TradeUnitConversionError.FactorTooLarge;
            return false;
        }

        if (HasMoreDecimalsThan(factor, MaxScale))
        {
            error = TradeUnitConversionError.FactorPrecisionTooHigh;
            return false;
        }

        if (string.Equals(trade, baseUnit, StringComparison.Ordinal) && factor != 1m)
        {
            error = TradeUnitConversionError.SameUnitFactorMustBeOne;
            return false;
        }

        if (!rounding.IsValid)
        {
            error = TradeUnitConversionError.InvalidRoundingPolicy;
            return false;
        }

        conversion = new TradeUnitConversion(trade, baseUnit, factor, rounding);
        error = TradeUnitConversionError.None;
        return true;
    }

    /// <summary>
    /// Converts <paramref name="tradeQuantity" /> to the base unit and returns the snapshot to store with the deal.
    /// A positive quantity that rounds to zero in the base unit is rejected, because a zero line is never meant.
    /// </summary>
    public bool TryConvert(
        decimal tradeQuantity,
        [NotNullWhen(true)] out TradeQuantitySnapshot? snapshot,
        out TradeUnitConversionError error)
    {
        snapshot = null;

        if (tradeQuantity <= 0m)
        {
            error = TradeUnitConversionError.QuantityNotPositive;
            return false;
        }

        if (tradeQuantity > MaxQuantity)
        {
            error = TradeUnitConversionError.QuantityTooLarge;
            return false;
        }

        if (HasMoreDecimalsThan(tradeQuantity, MaxScale))
        {
            error = TradeUnitConversionError.QuantityPrecisionTooHigh;
            return false;
        }

        var exact = tradeQuantity * Factor;
        var baseQuantity = Math.Round(exact, Rounding.Decimals, ToMidpointRounding(Rounding.Mode));
        if (baseQuantity <= 0m)
        {
            error = TradeUnitConversionError.ResultRoundsToZero;
            return false;
        }

        snapshot = new TradeQuantitySnapshot(
            TradeUnitCode,
            tradeQuantity,
            BaseUnitCode,
            Factor,
            baseQuantity,
            Rounding.Decimals,
            Rounding.Mode);
        error = TradeUnitConversionError.None;
        return true;
    }

    public bool Equals(TradeUnitConversion? other) =>
        other is not null
        && string.Equals(TradeUnitCode, other.TradeUnitCode, StringComparison.Ordinal)
        && string.Equals(BaseUnitCode, other.BaseUnitCode, StringComparison.Ordinal)
        && Factor == other.Factor
        && Rounding == other.Rounding;

    public override bool Equals(object? obj) => Equals(obj as TradeUnitConversion);

    public override int GetHashCode() => HashCode.Combine(TradeUnitCode, BaseUnitCode, Factor, Rounding);

    /// <summary>Trims and upper-cases a unit code; only ASCII letters and digits are accepted (as for reference unit codes).</summary>
    internal static bool TryNormalizeUnitCode(
        string? raw,
        TradeUnitConversionError missingError,
        [NotNullWhen(true)] out string? code,
        out TradeUnitConversionError error)
    {
        code = null;
        var trimmed = raw?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            error = missingError;
            return false;
        }

        if (trimmed.Length > MaxUnitCodeLength || !trimmed.All(char.IsAsciiLetterOrDigit))
        {
            error = TradeUnitConversionError.InvalidUnitCode;
            return false;
        }

        code = trimmed.ToUpperInvariant();
        error = TradeUnitConversionError.None;
        return true;
    }

    private static bool HasMoreDecimalsThan(decimal value, int scale) => decimal.Round(value, scale) != value;

    private static MidpointRounding ToMidpointRounding(TradeRoundingMode mode) => mode switch
    {
        TradeRoundingMode.HalfUp => MidpointRounding.AwayFromZero,
        TradeRoundingMode.HalfEven => MidpointRounding.ToEven,
        TradeRoundingMode.Truncate => MidpointRounding.ToZero,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), "Rounding mode is not defined."),
    };
}
