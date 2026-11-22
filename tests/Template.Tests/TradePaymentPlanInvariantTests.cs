using Faranou.Modules.Trade.Application.Proformas;
using Xunit;

namespace Template.Tests;

/// <summary>
/// Invariant (property-style) checks for <see cref="PaymentPlanCalculator"/> against the owner-approved Issue #710 contract:
/// decimal arithmetic, non-last stages rounded AwayFromZero to the currency minor units, last stage = real remainder,
/// non-positive remainder rejected explicitly, deterministic, no silent correction.
/// </summary>
public sealed class TradePaymentPlanInvariantTests
{
    private static PaymentPlanStageInput P(decimal percent) => new("TT", percent, null);

    private static PaymentPlanStageInput A(decimal amount) => new("TT", null, amount);

    private static int Scale(decimal value) => (decimal.GetBits(value)[3] >> 16) & 0xFF;

    private static readonly (string Currency, int Minor)[] Currencies = [("USD", 2), ("IRR", 2), ("JPY", 0), ("KWD", 3)];

    private static readonly decimal[][] PercentSplits =
    [
        [100m], [50m, 50m], [33.3333m, 33.3333m, 33.3334m], [10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m],
        [25m, 25m, 25m, 25m], [0.0001m, 99.9999m], [99.9999m, 0.0001m], [1m, 2m, 3m, 4m, 5m, 6m, 7m, 8m, 9m, 10m, 20m, 25m],
        [12.5m, 12.5m, 75m], [0.5m, 0.5m, 99m],
    ];

    private static readonly decimal[] Totals = [0.001m, 0.01m, 0.05m, 0.5m, 1m, 5m, 7m, 99.99m, 100.01m, 1_234_567.891m, 999_999_999_999_999m, 1_000_000_000_000_000m];

    [Fact]
    public void Percent_EveryAcceptedPlan_SumsExactlyToFinalAndAllStagesArePositive()
    {
        var accepted = 0;
        var rejected = 0;
        foreach (var (currency, minor) in Currencies)
        foreach (var total in Totals)
        {
            if (Scale(total) > minor) { continue; }
            foreach (var split in PercentSplits)
            {
                var ok = PaymentPlanCalculator.TryBuild(currency, total, split.Select(P).ToArray(), null, out var plan, out var issues);
                if (!ok)
                {
                    rejected++;
                    Assert.Null(plan);
                    Assert.NotEmpty(issues);
                    continue;
                }

                accepted++;
                Assert.Equal(split.Length, plan!.Stages.Length);
                Assert.Equal(total, plan.Stages.Sum(s => s.Amount));
                Assert.All(plan.Stages, s =>
                {
                    Assert.True(s.Amount > 0, $"{currency} {total} {string.Join('/', split)} stage {s.Sequence}");
                    Assert.True(Scale(s.Amount) <= minor, $"{currency} {total} stage {s.Sequence} scale {Scale(s.Amount)}");
                });
            }
        }

        Assert.True(accepted > 100);
        Assert.True(rejected > 0);
    }

    [Fact]
    public void Percent_NonLastStages_AreExactlyRoundAwayFromZeroOfTotalTimesPercent()
    {
        foreach (var (currency, minor) in Currencies)
        foreach (var total in Totals)
        {
            if (Scale(total) > minor) { continue; }
            foreach (var split in PercentSplits)
            {
                if (!PaymentPlanCalculator.TryBuild(currency, total, split.Select(P).ToArray(), null, out var plan, out _)) { continue; }
                for (var i = 0; i < split.Length - 1; i++)
                {
                    var expected = decimal.Round(total * split[i] / 100m, minor, MidpointRounding.AwayFromZero);
                    Assert.Equal(expected, plan!.Stages[i].Amount);
                }
            }
        }
    }

    [Fact]
    public void Percent_SamePlanTwice_IsDeterministic()
    {
        foreach (var split in PercentSplits)
        {
            var input = split.Select(P).ToArray();
            PaymentPlanCalculator.TryBuild("USD", 100.01m, input, null, out var p1, out var i1);
            PaymentPlanCalculator.TryBuild("USD", 100.01m, input, null, out var p2, out var i2);
            Assert.Equal(p1?.Stages.Select(s => s.Amount), p2?.Stages.Select(s => s.Amount));
            Assert.Equal(i1.Select(i => (i.Code, i.StageNumber)), i2.Select(i => (i.Code, i.StageNumber)));
        }
    }

    [Fact]
    public void Percent_Usd005FourQuarters_IsAcceptedWithExactAmounts()
    {
        // Approved rule: non-last stages 0.0125 -> 0.01 (AwayFromZero, 2 minor units); the last stage takes the real remainder.
        Assert.True(PaymentPlanCalculator.TryBuild("USD", 0.05m, [P(25m), P(25m), P(25m), P(25m)], null, out var plan, out var issues));
        Assert.Empty(issues);
        Assert.Equal([0.01m, 0.01m, 0.01m, 0.02m], plan!.Stages.Select(s => s.Amount));
    }

    [Fact]
    public void Percent_Jpy5TenStagesOfTenPercent_IsRejectedExplicitlyNotOverAllocated()
    {
        // Nine non-last stages each round 0.5 -> 1 (nine yen > 5), so the remainder is -4: explicit rejection, never a silent over-allocation.
        var input = Enumerable.Range(0, 10).Select(_ => P(10m)).ToArray();
        Assert.False(PaymentPlanCalculator.TryBuild("JPY", 5m, input, null, out var plan, out var issues));
        Assert.Null(plan);
        Assert.Contains(issues, i => i.Code == PaymentPlanIssueCode.ResidualStageNotPositive && i.StageNumber == 10);
    }

    [Fact]
    public void Amounts_SumMismatchByMinorUnit_IsRejectedAndMatchIsAccepted()
    {
        Assert.False(PaymentPlanCalculator.TryBuild("KWD", 10m, [A(5m), A(4.999m)], null, out var bad, out var issues));
        Assert.Null(bad);
        Assert.Contains(issues, i => i.Code == PaymentPlanIssueCode.AmountSumMismatch);
        Assert.True(PaymentPlanCalculator.TryBuild("KWD", 10m, [A(5m), A(5.000m)], null, out var good, out _));
        Assert.Equal(10m, good!.Stages.Sum(s => s.Amount));
    }

    [Fact]
    public void Percent_SumJustOffHundred_IsRejectedNotCorrected()
    {
        foreach (var off in new[] { 99.9999m, 100.0001m })
        {
            Assert.False(PaymentPlanCalculator.TryBuild("USD", 100m, [P(50m), P(off - 50m)], null, out var plan, out var issues));
            Assert.Null(plan);
            Assert.Contains(issues, i => i.Code == PaymentPlanIssueCode.PercentSumNot100);
        }
    }

    [Fact]
    public void Boundaries_ZeroNegativeAndAboveMax_AreRejectedExplicitly()
    {
        Assert.False(PaymentPlanCalculator.TryBuild("USD", 0m, [P(100m)], null, out _, out var zero));
        Assert.Contains(zero, i => i.Code == PaymentPlanIssueCode.FinalAmountNotPositive);
        Assert.False(PaymentPlanCalculator.TryBuild("USD", -1m, [P(100m)], null, out _, out var neg));
        Assert.Contains(neg, i => i.Code == PaymentPlanIssueCode.FinalAmountNotPositive);
        Assert.False(PaymentPlanCalculator.TryBuild("USD", PaymentPlanCalculator.MaxAmount + 0.01m, [P(100m)], null, out _, out var big));
        Assert.Contains(big, i => i.Code == PaymentPlanIssueCode.FinalAmountTooLarge);
        Assert.False(PaymentPlanCalculator.TryBuild("JPY", 1.5m, [P(100m)], null, out _, out var prec));
        Assert.Contains(prec, i => i.Code == PaymentPlanIssueCode.FinalAmountPrecisionTooHigh);
    }

    [Fact]
    public void Percent_MaxAmountWithMaxStages_DoesNotOverflowAndSumsExactly()
    {
        var input = new[] { 8.3333m, 8.3333m, 8.3333m, 8.3333m, 8.3333m, 8.3333m, 8.3333m, 8.3333m, 8.3333m, 8.3333m, 8.3333m, 8.3337m }.Select(P).ToArray();
        Assert.Equal(100m, input.Sum(s => s.Percent!.Value));
        Assert.True(PaymentPlanCalculator.TryBuild("KWD", PaymentPlanCalculator.MaxAmount, input, null, out var plan, out _));
        Assert.Equal(PaymentPlanCalculator.MaxAmount, plan!.Stages.Sum(s => s.Amount));
    }

    [Fact]
    public void Percent_FailedBuild_DoesNotMutateCallerInput()
    {
        var input = new[] { P(60m), P(60m) };
        PaymentPlanCalculator.TryBuild("USD", 100m, input, null, out var plan, out _);
        Assert.Null(plan);
        Assert.Equal([60m, 60m], input.Select(s => s.Percent!.Value));
    }
}
