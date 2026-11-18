using System;
using System.Globalization;
using System.Numerics;
using Faranou.Modules.Trade.Application.ReferenceData;
using Faranou.Modules.Trade.Domain.Units;
using Xunit;

namespace Template.Tests;

public sealed class TradeUnitConversionTests
{
    private static readonly TradeRoundingPolicy ThreeDecimalsHalfUp = new(3, TradeRoundingMode.HalfUp);

    private static TradeUnitConversion Conversion(
        string trade,
        string baseUnit,
        decimal factor,
        TradeRoundingPolicy? rounding = null)
    {
        Assert.True(
            TradeUnitConversion.TryCreate(trade, baseUnit, factor, rounding ?? ThreeDecimalsHalfUp, out var conversion, out var error),
            $"unexpected error {error}");
        Assert.Equal(TradeUnitConversionError.None, error);
        return conversion!;
    }

    private static void InvalidConversion(
        string? trade,
        string? baseUnit,
        decimal factor,
        TradeRoundingPolicy rounding,
        TradeUnitConversionError expected)
    {
        Assert.False(TradeUnitConversion.TryCreate(trade, baseUnit, factor, rounding, out var conversion, out var error));
        Assert.Null(conversion);
        Assert.Equal(expected, error);
    }

    private static TradeQuantitySnapshot Convert(TradeUnitConversion conversion, decimal quantity)
    {
        Assert.True(conversion.TryConvert(quantity, out var snapshot, out var error), $"unexpected error {error}");
        Assert.Equal(TradeUnitConversionError.None, error);
        return snapshot!;
    }

    private static void InvalidQuantity(TradeUnitConversion conversion, decimal quantity, TradeUnitConversionError expected)
    {
        Assert.False(conversion.TryConvert(quantity, out var snapshot, out var error));
        Assert.Null(snapshot);
        Assert.Equal(expected, error);
    }

    // --- exact conversion -------------------------------------------------------------------------------------

    [Theory]
    [InlineData("1", "1000")]
    [InlineData("24.5", "24500")]
    [InlineData("0.001", "1")]
    [InlineData("50000", "50000000")]
    [InlineData("0.0015", "1.5")]
    public void TonToKilogram_IsExact(string tons, string expectedKilograms)
    {
        var snapshot = Convert(Conversion("TON", "KG", 1000m), decimal.Parse(tons, CultureInfo.InvariantCulture));

        Assert.Equal(decimal.Parse(expectedKilograms, CultureInfo.InvariantCulture), snapshot.BaseQuantity);
    }

    [Fact]
    public void Conversion_UsesDecimalArithmeticWithoutFloatingPointDrift()
    {
        // 0.1 * 3 style drift would appear with double; decimal keeps it exact.
        var snapshot = Convert(Conversion("PCS", "KG", 0.3m), 0.1m);

        Assert.Equal(0.03m, snapshot.BaseQuantity);
    }

    [Fact]
    public void Conversion_OfLargestAllowedInputs_IsExact()
    {
        // Largest factor and quantity below the ceilings, both with six decimals. Their unscaled integers are
        // 1e12 - 1 and 1e15 - 1, so the exact product is known from integer arithmetic and has twelve decimals.
        const decimal factor = 999_999.999999m;
        const decimal quantity = 999_999_999.999999m;
        var product = new BigInteger(999_999_999_999L) * new BigInteger(999_999_999_999_999L);
        var halfUpAtSixDecimals = (product + 500_000) / 1_000_000;

        var snapshot = Convert(Conversion("TON", "KG", factor, new TradeRoundingPolicy(6, TradeRoundingMode.HalfUp)), quantity);

        Assert.Equal((decimal)halfUpAtSixDecimals / 1_000_000m, snapshot.BaseQuantity);
    }

    [Fact]
    public void Conversion_AtBothCeilings_IsExact()
    {
        // 1,000,000 x 1,000,000,000 = 10^15; the largest values the ceilings allow.
        var snapshot = Convert(
            Conversion("TON", "KG", TradeUnitConversion.MaxFactor, new TradeRoundingPolicy(0, TradeRoundingMode.Truncate)),
            TradeUnitConversion.MaxQuantity);

        Assert.Equal(1_000_000_000_000_000m, snapshot.BaseQuantity);
    }

    [Fact]
    public void FactorWithALargeStoredScale_ButSmallEffectiveScale_IsConvertedExactly()
    {
        // 29-digit coefficient, scale 28, value exactly 1. The exactness argument works on the effective scale (six),
        // not on the stored one, so this representation is safe and the result is exact.
        const decimal factor = 1.0000000000000000000000000000m;
        Assert.Equal(28, (decimal.GetBits(factor)[3] >> 16) & 0xFF);

        var snapshot = Convert(Conversion("PCS", "KG", factor, new TradeRoundingPolicy(6, TradeRoundingMode.HalfUp)), 999_999_999.999999m);

        Assert.Equal(999_999_999.999999m, snapshot.BaseQuantity);
    }

    // --- rounding ----------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(TradeRoundingMode.HalfUp, "1.3")]
    [InlineData(TradeRoundingMode.HalfEven, "1.2")]
    [InlineData(TradeRoundingMode.Truncate, "1.2")]
    public void Rounding_AtMidpoint_FollowsTheChosenMode(TradeRoundingMode mode, string expected)
    {
        // 0.25 * 5 = 1.25, rounded to one decimal place.
        var snapshot = Convert(Conversion("PCS", "KG", 5m, new TradeRoundingPolicy(1, mode)), 0.25m);

        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), snapshot.BaseQuantity);
        Assert.Equal(mode, snapshot.RoundingMode);
        Assert.Equal(1, snapshot.RoundingDecimals);
    }

    [Theory]
    [InlineData(TradeRoundingMode.HalfUp, "1.4")]
    [InlineData(TradeRoundingMode.HalfEven, "1.4")]
    [InlineData(TradeRoundingMode.Truncate, "1.3")]
    public void Rounding_AboveMidpoint_OnlyTruncateDiffers(TradeRoundingMode mode, string expected)
    {
        // 0.27 * 5 = 1.35 is a midpoint for one decimal; 0.2701 * 5 = 1.3505 is above it.
        var snapshot = Convert(Conversion("PCS", "KG", 5m, new TradeRoundingPolicy(1, mode)), 0.2701m);

        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), snapshot.BaseQuantity);
    }

    [Fact]
    public void Rounding_WithZeroDecimals_ReturnsWholeBaseUnits()
    {
        var snapshot = Convert(Conversion("CARTON", "KG", 12.5m, new TradeRoundingPolicy(0, TradeRoundingMode.HalfUp)), 3m);

        Assert.Equal(38m, snapshot.BaseQuantity); // 37.5 rounds half up
    }

    [Fact]
    public void Rounding_ToThreeDecimals_KeepsTheStatedPrecision()
    {
        // 3 lb * 0.453592 kg/lb = 1.360776 kg
        var snapshot = Convert(Conversion("LB", "KG", 0.453592m, new TradeRoundingPolicy(3, TradeRoundingMode.HalfUp)), 3m);

        Assert.Equal(1.361m, snapshot.BaseQuantity); // 1.360776 rounded half up to three decimals
    }

    [Fact]
    public void PositiveQuantity_ThatRoundsToZero_IsRejected()
    {
        var conversion = Conversion("TON", "KG", 1000m, new TradeRoundingPolicy(0, TradeRoundingMode.HalfUp));

        InvalidQuantity(conversion, 0.0004m, TradeUnitConversionError.ResultRoundsToZero);
        InvalidQuantity(
            Conversion("TON", "KG", 1000m, new TradeRoundingPolicy(0, TradeRoundingMode.Truncate)),
            0.0009m,
            TradeUnitConversionError.ResultRoundsToZero);
    }

    // --- snapshot ----------------------------------------------------------------------------------------------

    [Fact]
    public void Snapshot_CarriesEveryInputOfTheConversion()
    {
        var snapshot = Convert(Conversion("ton", "kg", 1000m, new TradeRoundingPolicy(2, TradeRoundingMode.HalfEven)), 24.5m);

        Assert.Equal("TON", snapshot.TradeUnitCode);
        Assert.Equal(24.5m, snapshot.TradeQuantity);
        Assert.Equal("KG", snapshot.BaseUnitCode);
        Assert.Equal(1000m, snapshot.ConversionFactor);
        Assert.Equal(24500m, snapshot.BaseQuantity);
        Assert.Equal(2, snapshot.RoundingDecimals);
        Assert.Equal(TradeRoundingMode.HalfEven, snapshot.RoundingMode);
    }

    [Fact]
    public void Snapshot_HasNoPublicConstructor_AndNoWritableProperty()
    {
        // Neither a public constructor nor an init accessor exists, so an inconsistent snapshot can only come from
        // TryConvert or TryRestore; `snapshot with { ConversionFactor = ... }` does not compile.
        Assert.Empty(typeof(TradeQuantitySnapshot).GetConstructors());
        foreach (var property in typeof(TradeQuantitySnapshot).GetProperties(
                     System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
        {
            if (property.Name == "EqualityContract")
            {
                continue;
            }

            Assert.Null(property.SetMethod);
        }
    }

    private static bool Restore(
        string? trade, decimal quantity, string? baseUnit, decimal factor, decimal baseQuantity, int decimals, TradeRoundingMode mode,
        out TradeQuantitySnapshot? snapshot, out TradeUnitConversionError error) =>
        TradeQuantitySnapshot.TryRestore(trade, quantity, baseUnit, factor, baseQuantity, decimals, mode, out snapshot, out error);

    [Fact]
    public void Restore_OfAStoredSnapshot_ReturnsTheSameSnapshot()
    {
        var original = Convert(Conversion("TON", "KG", 1000.5m, new TradeRoundingPolicy(2, TradeRoundingMode.HalfEven)), 24.5m);

        Assert.True(Restore(
            original.TradeUnitCode, original.TradeQuantity, original.BaseUnitCode, original.ConversionFactor,
            original.BaseQuantity, original.RoundingDecimals, original.RoundingMode, out var restored, out var error));

        Assert.Equal(TradeUnitConversionError.None, error);
        Assert.Equal(original, restored);
    }

    [Theory]
    [InlineData("TON", "24.5", "KG", "1000", "24501")] // base quantity does not match
    [InlineData("TON", "24.5", "KG", "2000", "24500")] // factor changed alone
    [InlineData("ton", "24.5", "KG", "1000", "24500")] // trade unit not in stored (upper-case) form
    [InlineData("TON", "24.5", " KG", "1000", "24500")] // base unit not in stored (trimmed) form
    public void Restore_OfInconsistentStoredValues_IsRejected(
        string trade, string quantity, string baseUnit, string factor, string baseQuantity)
    {
        Assert.False(Restore(
            trade, Dec(quantity), baseUnit, Dec(factor), Dec(baseQuantity), 2, TradeRoundingMode.HalfEven, out var snapshot, out var error));

        Assert.Null(snapshot);
        Assert.Equal(TradeUnitConversionError.SnapshotInconsistent, error);
    }

    [Fact]
    public void Restore_WhereTheRoundingModeChangesTheResult_IsRejected()
    {
        // 0.25 x 5 = 1.25 -> HalfUp 1.3, Truncate 1.2. Stored as HalfUp but claiming the Truncate result.
        Assert.False(Restore("PCS", 5m, "KG", 0.25m, 1.2m, 1, TradeRoundingMode.HalfUp, out var snapshot, out var error));
        Assert.Null(snapshot);
        Assert.Equal(TradeUnitConversionError.SnapshotInconsistent, error);
        Assert.True(Restore("PCS", 5m, "KG", 0.25m, 1.3m, 1, TradeRoundingMode.HalfUp, out _, out _));
    }

    [Theory]
    [InlineData("0", "1000", 2, TradeUnitConversionError.QuantityNotPositive)]
    [InlineData("-1", "1000", 2, TradeUnitConversionError.QuantityNotPositive)]
    [InlineData("1000000000.000001", "1000", 2, TradeUnitConversionError.QuantityTooLarge)]
    [InlineData("1", "0", 2, TradeUnitConversionError.FactorNotPositive)]
    [InlineData("1", "1000000.000001", 2, TradeUnitConversionError.FactorTooLarge)]
    [InlineData("1", "1000", 7, TradeUnitConversionError.InvalidRoundingPolicy)]
    public void Restore_OutsideTheArithmeticCeilings_IsRejected(string quantity, string factor, int decimals, TradeUnitConversionError expected)
    {
        Assert.False(Restore("TON", Dec(quantity), "KG", Dec(factor), 1m, decimals, TradeRoundingMode.HalfUp, out var snapshot, out var error));
        Assert.Null(snapshot);
        Assert.Equal(expected, error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void Restore_WithAnUndefinedRoundingMode_IsRejected(int mode)
    {
        Assert.False(Restore("TON", 1m, "KG", 1000m, 1000m, 2, (TradeRoundingMode)mode, out var snapshot, out var error));
        Assert.Null(snapshot);
        Assert.Equal(TradeUnitConversionError.InvalidRoundingPolicy, error);
    }

    [Fact]
    public void Restore_WithMissingOrMalformedUnitCodes_IsRejected()
    {
        Assert.False(Restore(null, 1m, "KG", 1000m, 1000m, 2, TradeRoundingMode.HalfUp, out _, out var e1));
        Assert.Equal(TradeUnitConversionError.TradeUnitMissing, e1);
        Assert.False(Restore("TON", 1m, "K G", 1000m, 1000m, 2, TradeRoundingMode.HalfUp, out _, out var e2));
        Assert.Equal(TradeUnitConversionError.InvalidUnitCode, e2);
    }

    private static decimal Dec(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);

    [Fact]
    public void Snapshot_IsNotChangedByALaterFactorOrRoundingChange()
    {
        var before = Convert(Conversion("TON", "KG", 1000m), 10m);
        var copy = before with { };

        // The product conversion is corrected later; a new conversion produces a new snapshot only.
        var after = Convert(Conversion("TON", "KG", 1016.047m), 10m);

        Assert.Equal(copy, before);
        Assert.Equal(10_000m, before.BaseQuantity);
        Assert.Equal(1000m, before.ConversionFactor);
        Assert.Equal(10_160.47m, after.BaseQuantity);
        Assert.NotEqual(before, after);
    }

    // --- conversion validation ---------------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingTradeUnit_IsRejected(string? unit) =>
        InvalidConversion(unit, "KG", 1000m, ThreeDecimalsHalfUp, TradeUnitConversionError.TradeUnitMissing);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingBaseUnit_IsRejected(string? unit) =>
        InvalidConversion("TON", unit, 1000m, ThreeDecimalsHalfUp, TradeUnitConversionError.BaseUnitMissing);

    [Theory]
    [InlineData("T N")]
    [InlineData("TON-1")]
    [InlineData("\u062A\u0646")]
    [InlineData("ABCDEFGHIJKLMNOPQ")]
    public void MalformedUnitCode_IsRejected(string unit)
    {
        InvalidConversion(unit, "KG", 1000m, ThreeDecimalsHalfUp, TradeUnitConversionError.InvalidUnitCode);
        InvalidConversion("TON", unit, 1000m, ThreeDecimalsHalfUp, TradeUnitConversionError.InvalidUnitCode);
    }

    [Fact]
    public void UnitCodes_AreTrimmedAndUpperCased()
    {
        var conversion = Conversion("  ton\n", " kg", 1000m);

        Assert.Equal("TON", conversion.TradeUnitCode);
        Assert.Equal("KG", conversion.BaseUnitCode);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("-1000")]
    public void NonPositiveFactor_IsRejected(string factor) =>
        InvalidConversion("TON", "KG", decimal.Parse(factor, CultureInfo.InvariantCulture), ThreeDecimalsHalfUp, TradeUnitConversionError.FactorNotPositive);

    [Fact]
    public void FactorAboveCeiling_IsRejected_AndCeilingItselfIsAccepted()
    {
        InvalidConversion("TON", "KG", TradeUnitConversion.MaxFactor + 0.000001m, ThreeDecimalsHalfUp, TradeUnitConversionError.FactorTooLarge);
        Assert.Equal(TradeUnitConversion.MaxFactor, Conversion("TON", "KG", TradeUnitConversion.MaxFactor).Factor);
    }

    [Fact]
    public void FactorWithTooManyDecimals_IsRejected_AndTrailingZerosAreAccepted()
    {
        InvalidConversion("LB", "KG", 0.4535923m, ThreeDecimalsHalfUp, TradeUnitConversionError.FactorPrecisionTooHigh);
        Assert.Equal(0.453592m, Conversion("LB", "KG", 0.453592m).Factor);
        Assert.Equal(1000m, Conversion("TON", "KG", 1000.0000000m).Factor);
    }

    [Fact]
    public void SameUnit_RequiresFactorOne()
    {
        InvalidConversion("KG", "KG", 2m, ThreeDecimalsHalfUp, TradeUnitConversionError.SameUnitFactorMustBeOne);
        InvalidConversion("kg", "KG", 0.5m, ThreeDecimalsHalfUp, TradeUnitConversionError.SameUnitFactorMustBeOne);
        Assert.Equal(1m, Conversion("KG", "KG", 1m).Factor);
    }

    [Theory]
    [InlineData(-1, TradeRoundingMode.HalfUp)]
    [InlineData(7, TradeRoundingMode.HalfUp)]
    [InlineData(2, (TradeRoundingMode)0)]
    [InlineData(2, (TradeRoundingMode)99)]
    public void InvalidRoundingPolicy_IsRejected(int decimals, TradeRoundingMode mode) =>
        InvalidConversion("TON", "KG", 1000m, new TradeRoundingPolicy(decimals, mode), TradeUnitConversionError.InvalidRoundingPolicy);

    [Fact]
    public void DefaultRoundingPolicy_IsNotAcceptedAsAnImplicitChoice() =>
        InvalidConversion("TON", "KG", 1000m, default, TradeUnitConversionError.InvalidRoundingPolicy);

    // --- quantity validation -----------------------------------------------------------------------------------

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void NonPositiveQuantity_IsRejected(string quantity) =>
        InvalidQuantity(Conversion("TON", "KG", 1000m), decimal.Parse(quantity, CultureInfo.InvariantCulture), TradeUnitConversionError.QuantityNotPositive);

    [Fact]
    public void QuantityAboveCeiling_IsRejected()
    {
        var conversion = Conversion("TON", "KG", 1000m);

        InvalidQuantity(conversion, TradeUnitConversion.MaxQuantity + 0.000001m, TradeUnitConversionError.QuantityTooLarge);
        Assert.Equal(1_000_000_000_000m, Convert(conversion, TradeUnitConversion.MaxQuantity).BaseQuantity);
    }

    [Fact]
    public void QuantityWithTooManyDecimals_IsRejected() =>
        InvalidQuantity(Conversion("TON", "KG", 1000m), 1.0000001m, TradeUnitConversionError.QuantityPrecisionTooHigh);

    [Fact]
    public void Equality_IsByAllFields()
    {
        Assert.Equal(Conversion("TON", "KG", 1000m), Conversion("ton", "kg", 1000.0m));
        Assert.NotEqual(Conversion("TON", "KG", 1000m), Conversion("TON", "KG", 1001m));
        Assert.NotEqual(
            Conversion("TON", "KG", 1000m, new TradeRoundingPolicy(3, TradeRoundingMode.HalfUp)),
            Conversion("TON", "KG", 1000m, new TradeRoundingPolicy(3, TradeRoundingMode.HalfEven)));
    }

    // --- the unit set of one product ---------------------------------------------------------------------------

    private static ProductUnitConversionSet Set(string baseUnit, params TradeUnitConversion[] conversions)
    {
        Assert.True(ProductUnitConversionSet.TryCreate(baseUnit, conversions, out var set, out var error), $"unexpected error {error}");
        return set!;
    }

    private static ProductUnitConversionSet WheatSet() => Set(
        "KG",
        Conversion("KG", "KG", 1m),
        Conversion("TON", "KG", 1000m),
        Conversion("CARTON", "KG", 25m));

    [Fact]
    public void Set_ConvertsOnlyAllowedUnits()
    {
        var set = WheatSet();

        Assert.True(set.TryConvert("ton", 24.5m, out var snapshot, out var error));
        Assert.Equal(TradeUnitConversionError.None, error);
        Assert.Equal(24500m, snapshot!.BaseQuantity);

        Assert.False(set.TryConvert("PALLET", 2m, out var denied, out var deniedError));
        Assert.Null(denied);
        Assert.Equal(TradeUnitConversionError.UnitNotAllowed, deniedError);
        Assert.False(set.TryResolve("PALLET", out _));
        Assert.True(set.TryResolve(" carton ", out var carton));
        Assert.Equal(25m, carton!.Factor);
    }

    [Fact]
    public void Set_BaseUnit_IsAllowedOnlyWithAnExplicitIdentityEntry()
    {
        var withoutBase = Set("KG", Conversion("TON", "KG", 1000m));

        Assert.False(withoutBase.TryConvert("KG", 5m, out _, out var error));
        Assert.Equal(TradeUnitConversionError.UnitNotAllowed, error);
        Assert.True(WheatSet().TryConvert("KG", 5m, out var snapshot, out _));
        Assert.Equal(5m, snapshot!.BaseQuantity);
    }

    [Fact]
    public void Set_MalformedOrMissingTradeUnit_ReportsTheUnitError()
    {
        var set = WheatSet();

        Assert.False(set.TryConvert(null, 1m, out _, out var missing));
        Assert.Equal(TradeUnitConversionError.TradeUnitMissing, missing);
        Assert.False(set.TryConvert("T-N", 1m, out _, out var invalid));
        Assert.Equal(TradeUnitConversionError.InvalidUnitCode, invalid);
    }

    [Fact]
    public void Set_PropagatesQuantityErrors()
    {
        Assert.False(WheatSet().TryConvert("TON", 0m, out _, out var error));
        Assert.Equal(TradeUnitConversionError.QuantityNotPositive, error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Set_RequiresABaseUnit(string? baseUnit)
    {
        Assert.False(ProductUnitConversionSet.TryCreate(baseUnit, [Conversion("TON", "KG", 1000m)], out var set, out var error));
        Assert.Null(set);
        Assert.Equal(TradeUnitConversionError.BaseUnitMissing, error);
    }

    [Fact]
    public void Set_RejectsConversionToAnotherBaseUnit()
    {
        Assert.False(ProductUnitConversionSet.TryCreate("KG", [Conversion("TON", "KG", 1000m), Conversion("PALLET", "PCS", 40m)], out var set, out var error));
        Assert.Null(set);
        Assert.Equal(TradeUnitConversionError.BaseUnitMismatch, error);
    }

    [Fact]
    public void Set_RejectsDuplicateTradeUnit()
    {
        Assert.False(ProductUnitConversionSet.TryCreate("KG", [Conversion("TON", "KG", 1000m), Conversion("ton", "KG", 1016m)], out var set, out var error));
        Assert.Null(set);
        Assert.Equal(TradeUnitConversionError.DuplicateTradeUnit, error);
    }

    [Fact]
    public void Set_RequiresAtLeastOneConversion()
    {
        foreach (var conversions in new TradeUnitConversion[]?[] { null, [] })
        {
            Assert.False(ProductUnitConversionSet.TryCreate("KG", conversions, out var set, out var error));
            Assert.Null(set);
            Assert.Equal(TradeUnitConversionError.NoConversions, error);
        }
    }

    [Fact]
    public void Set_BaseUnitCode_IsNormalized_AndItsEntriesAreStable()
    {
        var set = Set(" kg ", Conversion("TON", "KG", 1000m));

        Assert.Equal("KG", set.BaseUnitCode);
        Assert.Single(set.Conversions);
    }

    // --- agreement with the unit reference list ----------------------------------------------------------------

    [Fact]
    public void EveryReferenceUnitCode_IsAValidConversionUnitCode()
    {
        foreach (var unit in TradeReferenceData.Units)
        {
            Assert.True(
                TradeUnitConversion.TryCreate(unit.Code, unit.Code, 1m, ThreeDecimalsHalfUp, out var conversion, out var error),
                $"reference unit {unit.Code} rejected: {error}");
            Assert.Equal(unit.Code, conversion!.TradeUnitCode);
        }
    }

    [Fact]
    public void TonAndKilogram_ExistInTheUnitReferenceList()
    {
        Assert.True(TradeReferenceData.TryFindUnit("TON", out _));
        Assert.True(TradeReferenceData.TryFindUnit("KG", out _));
    }
}
