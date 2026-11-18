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

    [Fact]
    public void PlanAndStage_HaveNoPublicConstructorAndNoWritableProperty()
    {
        // An invalid plan cannot be built by hand or altered with `with`; TryBuild is the only producer.
        foreach (var type in new[] { typeof(PaymentPlan), typeof(PaymentPlanStage) })
        {
            Assert.Empty(type.GetConstructors());
            Assert.All(
                type.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                    .Where(p => p.Name != "EqualityContract"),
                p => Assert.Null(p.SetMethod));
        }
    }

    [Fact]
    public void StoredPlan_IsRestoredByBuildingAgainFromItsStoredInputs()
    {
        Assert.True(Build("KWD", 1000.001m, [P("TT", 33.3333m), P("LC", 33.3333m), P("DP", 33.3334m)], out var percentPlan, out _));
        var percentInputs = percentPlan!.Stages.Select(s => P(s.MethodCode, s.Percent!.Value)).ToArray();
        Assert.True(Build(percentPlan.CurrencyCode, percentPlan.FinalAmount, percentInputs, out var percentRestored, out _));
        Assert.Equal(percentPlan.Stages.Select(s => s.Amount), percentRestored!.Stages.Select(s => s.Amount));

        Assert.True(Build("USD", 100m, [A("TT", 40m), A("LC", 60m)], out var amountPlan, out _));
        var amountInputs = amountPlan!.Stages.Select(s => A(s.MethodCode, s.Amount)).ToArray();
        Assert.True(Build(amountPlan.CurrencyCode, amountPlan.FinalAmount, amountInputs, out var amountRestored, out _));
        Assert.Equal(amountPlan.Stages.Select(s => s.Amount), amountRestored!.Stages.Select(s => s.Amount));
    }

    [Fact]
    public void EmptyAllowedList_RejectsEveryMethod_WhileNullAllowsAllReferenceMethods()
    {
        Assert.False(Build("USD", 100m, [P("TT", 100m)], out _, out var issues, allowed: []));
        AssertIssue(issues, PaymentPlanIssueCode.MethodNotAllowed, 1);
        Assert.True(Build("USD", 100m, [P("TT", 100m)], out _, out _, allowed: null));
    }

    [Fact]
    public void NonLastStages_RoundAwayFromZero_AndTheLastStageTakesTheRealRemainder()
    {
        // 0.05 USD x 50% = 0.025 -> 0.03 (midpoint away from zero); the last stage receives 0.05 - 0.03 = 0.02.
        Assert.True(Build("USD", 0.05m, [P("TT", 50m), P("LC", 50m)], out var plan, out _));
        Assert.Equal([0.03m, 0.02m], plan!.Stages.Select(s => s.Amount));
        Assert.Equal(0.05m, plan.Stages.Sum(s => s.Amount));
    }

    [Theory]
    [InlineData("USD", "1000000000000000", "333333000000000.00", "333334000000000.00")]
    [InlineData("JPY", "1000000000000000", "333333000000000", "333334000000000")]
    [InlineData("KWD", "999999999999999.999", "333333000000000.000", "333333999999999.999")]
    public void LargestAmounts_InPercentPlans_GiveTheHandComputedAmountsInEveryMinorUnitScale(
        string currency, string total, string nonLastAmount, string lastAmount)
    {
        // Expected values were computed by hand: total x 33.3333 / 100 for the two non-last stages (already whole minor
        // units, so no rounding applies) and total - 2 x that for the last stage. They do not come from the code under test.
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        Assert.True(Build(currency, decimal.Parse(total, inv), [P("TT", 33.3333m), P("LC", 33.3333m), P("DP", 33.3334m)], out var plan, out _));

        Assert.Equal(
            [decimal.Parse(nonLastAmount, inv), decimal.Parse(nonLastAmount, inv), decimal.Parse(lastAmount, inv)],
            plan!.Stages.Select(s => s.Amount));
    }

    // A list that serves different values on each read of the same entry, like a caller changing it concurrently.
    private sealed class ShiftingStages : IReadOnlyList<PaymentPlanStageInput>
    {
        private readonly int[] _reads = new int[2];

        public int Count => 2;

        public int Reads(int index) => _reads[index];

        public PaymentPlanStageInput this[int index]
        {
            get
            {
                // First read: a valid 40 + 60 plan. Any later read of the same entry: a different, invalid value.
                var first = _reads[index]++ == 0;
                return index == 0 ? A("TT", first ? 40m : -40m) : A("LC", first ? 60m : 140m);
            }
        }

        public IEnumerator<PaymentPlanStageInput> GetEnumerator() => throw new NotSupportedException();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => throw new NotSupportedException();
    }

    [Fact]
    public void Build_ReadsEachCallerStageExactlyOnce_SoValidatedValuesAreTheBuiltValues()
    {
        var stages = new ShiftingStages();

        Assert.True(PaymentPlanCalculator.TryBuild("USD", 100m, stages, null, out var plan, out _));

        Assert.Equal(1, stages.Reads(0));
        Assert.Equal(1, stages.Reads(1));
        Assert.Equal([40m, 60m], plan!.Stages.Select(x => x.Amount));
    }

    [Fact]
    public void ChangingTheCallerList_AfterBuild_DoesNotChangeThePlan()
    {
        var input = new List<PaymentPlanStageInput> { A("TT", 40m), A("LC", 60m) };
        Assert.True(PaymentPlanCalculator.TryBuild("USD", 100m, input, null, out var plan, out _));

        input[0] = A("TT", 70m);
        input.Add(A("DP", 5m));
        input.Clear();

        Assert.Equal([40m, 60m], plan!.Stages.Select(x => x.Amount));
        Assert.Equal(["TT", "LC"], plan.Stages.Select(x => x.MethodCode));
        Assert.Equal(100m, plan.FinalAmount);
    }

    [Fact]
    public void Stages_CannotBeChangedThroughAnyInterfaceOrCast()
    {
        Assert.True(Build("USD", 100m, [A("TT", 40m), A("LC", 60m)], out var plan, out _));
        object boxed = plan!.Stages;

        var list = Assert.IsAssignableFrom<IList<PaymentPlanStage>>(boxed);
        Assert.True(list.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => list[0] = list[1]);
        Assert.Throws<NotSupportedException>(() => list.Add(list[0]));
        Assert.Throws<NotSupportedException>(() => list.RemoveAt(0));
        Assert.Throws<NotSupportedException>(() => list.Clear());

        // The route that exposed the backing array of a read-only wrapper: ICollection.SyncRoot, and a cast to an array.
        var nonGeneric = Assert.IsAssignableFrom<System.Collections.ICollection>(boxed);
        Assert.Throws<NotSupportedException>(() => nonGeneric.SyncRoot);
        Assert.Null(boxed as PaymentPlanStage[]);
        Assert.Null(boxed as System.Collections.ObjectModel.ReadOnlyCollection<PaymentPlanStage>);

        Assert.Equal([40m, 60m], plan.Stages.Select(x => x.Amount));
    }

    [Fact]
    public void PaymentMethods_CannotBeChangedAndTheLookupStaysConsistentWithTheList()
    {
        object boxed = TradeReferenceData.PaymentMethods;
        var before = TradeReferenceData.PaymentMethods.ToArray();

        var list = Assert.IsAssignableFrom<IList<PaymentMethodRef>>(boxed);
        Assert.Throws<NotSupportedException>(() => list[0] = list[1]);
        Assert.Throws<NotSupportedException>(() => list.Add(list[0]));
        Assert.Throws<NotSupportedException>(() => list.Clear());
        var nonGeneric = Assert.IsAssignableFrom<System.Collections.ICollection>(boxed);
        Assert.Throws<NotSupportedException>(() => nonGeneric.SyncRoot);
        Assert.Null(boxed as PaymentMethodRef[]);

        Assert.Equal(before, TradeReferenceData.PaymentMethods);
        foreach (var method in before)
        {
            Assert.True(TradeReferenceData.TryFindPaymentMethod(method.Code, out var found));
            Assert.Same(method, found);
        }
    }
}
