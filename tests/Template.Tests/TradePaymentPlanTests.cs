using Faranou.Modules.Trade.Application.Proformas;
using Faranou.Modules.Trade.Application.ReferenceData;
using Xunit;

namespace Template.Tests;

public sealed class TradePaymentPlanTests
{
    private static PaymentPlanStageInput P(string method, decimal percent) => new(method, percent, null);

    private static PaymentPlanStageInput A(string method, decimal amount) => new(method, null, amount);

    private static bool Build(string? currency, decimal total, PaymentPlanStageInput[]? stages, out PaymentPlan? plan, out IReadOnlyList<PaymentPlanIssue> issues, string[]? allowed = null)
        => PaymentPlanCalculator.TryBuild(currency, total, stages, allowed, out plan, out issues);

    private static void AssertIssue(IReadOnlyList<PaymentPlanIssue> issues, PaymentPlanIssueCode code, int? stage = null)
        => Assert.Contains(issues, i => i.Code == code && i.StageNumber == stage);

    [Fact]
    public void PaymentMethods_AreReferenceDataWithPersianLetters()
    {
        Assert.Equal(["TT", "LC", "DP", "DA", "OPEN_ACCOUNT"], TradeReferenceData.PaymentMethods.Select(x => x.Code));
        Assert.All(TradeReferenceData.PaymentMethods, x =>
        {
            Assert.DoesNotContain('ي', x.NameFa);
            Assert.DoesNotContain('ك', x.NameFa);
            Assert.NotEmpty(x.NameEn);
        });
        Assert.True(TradeReferenceData.TryFindPaymentMethod(" lc ", out var lc));
        Assert.Equal("LC", lc!.Code);
        Assert.False(TradeReferenceData.TryFindPaymentMethod("CHEQUE", out _));
        Assert.False(TradeReferenceData.TryFindPaymentMethod(null, out _));
    }

    [Fact]
    public void PaymentMethods_ListIsReadOnly()
    {
        var list = Assert.IsAssignableFrom<IList<PaymentMethodRef>>(TradeReferenceData.PaymentMethods);
        Assert.True(list.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => list.Add(list[0]));
    }

    [Fact]
    public void Percent_ThirtySeventy_SplitsExactly()
    {
        Assert.True(Build("USD", 100_000m, [P("TT", 30m), P("LC", 70m)], out var plan, out var issues));
        Assert.Empty(issues);
        Assert.Equal("USD", plan!.CurrencyCode);
        Assert.Equal([30_000m, 70_000m], plan.Stages.Select(s => s.Amount));
        Assert.Equal([1, 2], plan.Stages.Select(s => s.Sequence));
        Assert.Equal(["TT", "LC"], plan.Stages.Select(s => s.MethodCode));
        Assert.Equal(100_000m, plan.Stages.Sum(s => s.Amount));
    }

    [Fact]
    public void Percent_RemainderGoesToLastStage_SumIsAlwaysExact()
    {
        // 100.01 split in thirds (33.3333 / 33.3333 / 33.3334): the last stage absorbs the rounding difference.
        Assert.True(Build("USD", 100.01m, [P("TT", 33.3333m), P("DA", 33.3333m), P("DP", 33.3334m)], out var plan, out _));
        Assert.Equal(100.01m, plan!.Stages.Sum(s => s.Amount));
        Assert.Equal(33.34m, plan.Stages[0].Amount); // 33.3363 rounds up
        Assert.Equal(33.34m, plan.Stages[1].Amount);
        Assert.Equal(33.33m, plan.Stages[2].Amount); // remainder: 100.01 - 66.68
    }

    [Fact]
    public void Percent_UsesCurrencyMinorUnits()
    {
        Assert.True(Build("JPY", 1001m, [P("TT", 50m), P("TT", 50m)], out var jpy, out _));
        Assert.Equal([501m, 500m], jpy!.Stages.Select(s => s.Amount)); // 500.5 -> away from zero -> 501
        Assert.True(Build("KWD", 1m, [P("TT", 33.3333m), P("TT", 66.6667m)], out var kwd, out _));
        Assert.Equal([0.333m, 0.667m], kwd!.Stages.Select(s => s.Amount));
    }

    [Fact]
    public void Amounts_MustSumToFinalAmount()
    {
        Assert.True(Build("EUR", 1000m, [A("TT", 250.5m), A("LC", 749.5m)], out var plan, out _));
        Assert.All(plan!.Stages, s => Assert.Null(s.Percent));
        Assert.False(Build("EUR", 1000m, [A("TT", 250m), A("LC", 749m)], out _, out var issues));
        AssertIssue(issues, PaymentPlanIssueCode.AmountSumMismatch);
    }

    [Fact]
    public void SingleStage_Works()
    {
        Assert.True(Build("AED", 12.34m, [P("OPEN_ACCOUNT", 100m)], out var plan, out _));
        Assert.Equal(12.34m, plan!.Stages.Single().Amount);
    }

    [Fact]
    public void Percent_SumNotHundred_ReportsPlanLevelIssue()
    {
        Assert.False(Build("USD", 100m, [P("TT", 40m), P("LC", 50m)], out _, out var issues));
        AssertIssue(issues, PaymentPlanIssueCode.PercentSumNot100);
    }

    [Fact]
    public void MixedPercentAndAmount_IsRejected()
    {
        Assert.False(Build("USD", 100m, [P("TT", 50m), A("LC", 50m)], out _, out var issues));
        AssertIssue(issues, PaymentPlanIssueCode.MixedPercentAndAmount);
    }

    [Fact]
    public void StageWithBothOrNeither_IsRejectedWithStageNumber()
    {
        Assert.False(Build("USD", 100m, [new("TT", 50m, 50m), new("LC", null, null)], out _, out var issues));
        AssertIssue(issues, PaymentPlanIssueCode.StageValueAmbiguous, 1);
        AssertIssue(issues, PaymentPlanIssueCode.StageValueMissing, 2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(100.01)]
    public void Percent_OutOfRange_IsRejected(double value)
    {
        Assert.False(Build("USD", 100m, [P("TT", (decimal)value)], out _, out var issues));
        AssertIssue(issues, PaymentPlanIssueCode.PercentNotInRange, 1);
    }

    [Fact]
    public void Percent_TooPrecise_IsRejected()
    {
        Assert.False(Build("USD", 100m, [P("TT", 33.33333m), P("LC", 66.66667m)], out _, out var issues));
        AssertIssue(issues, PaymentPlanIssueCode.PercentPrecisionTooHigh, 1);
        AssertIssue(issues, PaymentPlanIssueCode.PercentPrecisionTooHigh, 2);
    }

    [Fact]
    public void Amount_Invalid_IsRejected()
    {
        Assert.False(Build("USD", 100m, [A("TT", 0m), A("LC", -1m), A("DP", 1.005m), A("DA", PaymentPlanCalculator.MaxAmount + 1)], out _, out var issues));
        AssertIssue(issues, PaymentPlanIssueCode.AmountNotPositive, 1);
        AssertIssue(issues, PaymentPlanIssueCode.AmountNotPositive, 2);
        AssertIssue(issues, PaymentPlanIssueCode.AmountPrecisionTooHigh, 3);
        AssertIssue(issues, PaymentPlanIssueCode.AmountTooLarge, 4);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void FinalAmount_NotPositive_IsRejected(double total)
    {
        Assert.False(Build("USD", (decimal)total, [P("TT", 100m)], out _, out var issues));
        AssertIssue(issues, PaymentPlanIssueCode.FinalAmountNotPositive);
    }

    [Fact]
    public void FinalAmount_TooLargeOrTooPrecise_IsRejected()
    {
        Assert.False(Build("USD", PaymentPlanCalculator.MaxAmount + 1, [P("TT", 100m)], out _, out var big));
        AssertIssue(big, PaymentPlanIssueCode.FinalAmountTooLarge);
        Assert.False(Build("USD", 10.001m, [P("TT", 100m)], out _, out var precise));
        AssertIssue(precise, PaymentPlanIssueCode.FinalAmountPrecisionTooHigh);
        Assert.False(Build("JPY", 10.5m, [P("TT", 100m)], out _, out var jpy));
        AssertIssue(jpy, PaymentPlanIssueCode.FinalAmountPrecisionTooHigh);
    }

    [Fact]
    public void MaxAmount_IsAccepted()
    {
        Assert.True(Build("USD", PaymentPlanCalculator.MaxAmount, [P("TT", 30m), P("LC", 70m)], out var plan, out _));
        Assert.Equal(PaymentPlanCalculator.MaxAmount, plan!.Stages.Sum(s => s.Amount));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("XXX")]
    [InlineData("usd1")]
    public void Currency_Invalid_IsRejected(string? currency)
    {
        Assert.False(Build(currency, 100m, [P("TT", 100m)], out var plan, out var issues));
        Assert.Null(plan);
        AssertIssue(issues, PaymentPlanIssueCode.CurrencyInvalid);
    }

    [Fact]
    public void Currency_IsCaseInsensitive_AndNormalized()
    {
        Assert.True(Build(" usd ", 100m, [P("tt", 100m)], out var plan, out _));
        Assert.Equal("USD", plan!.CurrencyCode);
        Assert.Equal("TT", plan.Stages[0].MethodCode);
    }

    [Fact]
    public void NoStagesAndTooManyStages_AreRejected()
    {
        Assert.False(Build("USD", 100m, null, out _, out var none));
        AssertIssue(none, PaymentPlanIssueCode.NoStages);
        Assert.False(Build("USD", 100m, [], out _, out var empty));
        AssertIssue(empty, PaymentPlanIssueCode.NoStages);

        var many = Enumerable.Range(0, PaymentPlanCalculator.MaxStages + 1).Select(_ => A("TT", 1m)).ToArray();
        Assert.False(Build("USD", many.Length, many, out _, out var tooMany));
        AssertIssue(tooMany, PaymentPlanIssueCode.TooManyStages);

        var max = Enumerable.Range(0, PaymentPlanCalculator.MaxStages).Select(_ => A("TT", 1m)).ToArray();
        Assert.True(Build("USD", max.Length, max, out _, out _));
    }

    [Fact]
    public void Method_UnknownOrDisallowed_IsRejectedPerStage()
    {
        Assert.False(Build("USD", 100m, [P("CHEQUE", 50m), P("DA", 50m)], out _, out var unknown));
        AssertIssue(unknown, PaymentPlanIssueCode.MethodInvalid, 1);

        Assert.False(Build("USD", 100m, [P("TT", 50m), P("DA", 50m)], out _, out var disallowed, allowed: ["TT", "LC"]));
        AssertIssue(disallowed, PaymentPlanIssueCode.MethodNotAllowed, 2);
        Assert.DoesNotContain(disallowed, i => i.StageNumber == 1);

        Assert.True(Build("USD", 100m, [P("TT", 50m), P("lc", 50m)], out _, out _, allowed: ["tt", "LC"]));
    }

    [Fact]
    public void NullStageEntry_DoesNotThrow()
    {
        Assert.False(Build("USD", 100m, [null!], out _, out var issues));
        AssertIssue(issues, PaymentPlanIssueCode.MethodInvalid, 1);
        AssertIssue(issues, PaymentPlanIssueCode.StageValueMissing, 1);
    }

    [Fact]
    public void Issue_CarriesOnlyEnumAndStageNumber_SoInputIsNeverReflected()
    {
        var props = typeof(PaymentPlanIssue).GetProperties();
        Assert.Equal(2, props.Length);
        Assert.Contains(props, p => p.PropertyType == typeof(PaymentPlanIssueCode));
        Assert.Contains(props, p => p.PropertyType == typeof(int?));
    }

    [Theory]
    [InlineData("USD", 0.01)]
    [InlineData("JPY", 10)]
    public void Percent_NonLastStageRoundingToZero_IsRejected(string currency, double total)
    {
        Assert.False(Build(currency, (decimal)total, [P("TT", 1m), P("LC", 98m), P("DP", 1m)], out _, out var issues));
        AssertIssue(issues, PaymentPlanIssueCode.AmountNotPositive, 1);
    }

    [Fact]
    public void Residual_NotPositive_IsRejected()
    {
        // 0.01 USD at 99.9999% rounds to 0.01 for stage 1, leaving nothing for the last stage.
        Assert.False(Build("USD", 0.01m, [P("TT", 99.9999m), P("LC", 0.0001m)], out var plan, out var issues));
        Assert.Null(plan);
        AssertIssue(issues, PaymentPlanIssueCode.ResidualStageNotPositive, 2);
    }

    [Fact]
    public void Plan_StagesAreImmutable()
    {
        Assert.True(Build("USD", 100m, [P("TT", 30m), P("LC", 70m)], out var plan, out _));
        var list = Assert.IsAssignableFrom<IList<PaymentPlanStage>>(plan!.Stages);
        Assert.True(list.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => list.Clear());
    }

    [Fact]
    public void Build_DoesNotMutateInput()
    {
        var input = new[] { P("tt", 30m), P("lc", 70m) };
        Build("USD", 100m, input, out _, out _);
        Assert.Equal("tt", input[0].MethodCode);
        Assert.Equal(70m, input[1].Percent);
    }
}
