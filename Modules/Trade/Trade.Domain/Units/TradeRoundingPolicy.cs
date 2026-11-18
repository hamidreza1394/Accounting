namespace Faranou.Modules.Trade.Domain.Units;

/// <summary>How a converted quantity is rounded to <see cref="TradeRoundingPolicy.Decimals" /> decimal places.</summary>
public enum TradeRoundingMode
{
    /// <summary>Midpoints go away from zero (2.5 becomes 3).</summary>
    HalfUp = 1,

    /// <summary>Midpoints go to the nearest even digit (2.5 becomes 2, 3.5 becomes 4).</summary>
    HalfEven = 2,

    /// <summary>The extra digits are dropped (toward zero); no midpoint rule applies.</summary>
    Truncate = 3,
}

/// <summary>
/// Explicit rounding rule for a converted quantity. There is intentionally no default: the number of
/// decimals and the mode are business choices that the caller must state.
/// </summary>
public readonly record struct TradeRoundingPolicy(int Decimals, TradeRoundingMode Mode)
{
    public const int MaxDecimals = 6;

    public bool IsValid => Decimals is >= 0 and <= MaxDecimals && Enum.IsDefined(Mode);
}
