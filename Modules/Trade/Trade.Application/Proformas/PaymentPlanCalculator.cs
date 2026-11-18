using System.Collections.Immutable;
using Faranou.Modules.Trade.Application.ReferenceData;

namespace Faranou.Modules.Trade.Application.Proformas;

/// <summary>
/// Pure, schema-free validation and splitting of a Proforma payment plan. Amounts use the currency
/// minor units; percentage plans put the rounding remainder on the last stage so the sum is exact.
/// No persistence, banking, ledger or line-total formula (D-086 formula is still open).
/// </summary>
public static class PaymentPlanCalculator
{
    public const int MaxStages = 12;
    public const int MaxPercentScale = 4;
    public const decimal MaxAmount = 1_000_000_000_000_000m;

    /// <param name="allowedMethodCodes">Optional restriction (e.g. per Incoterm or contract); null allows every reference method.</param>
    public static bool TryBuild(
        string? currencyCode,
        decimal finalAmount,
        IReadOnlyList<PaymentPlanStageInput>? stages,
        IReadOnlyCollection<string>? allowedMethodCodes,
        out PaymentPlan? plan,
        out IReadOnlyList<PaymentPlanIssue> issues)
    {
        plan = null;
        var found = new List<PaymentPlanIssue>();
        issues = found;

        if (!TradeReferenceData.TryFindCurrency(currencyCode, out var currency))
        {
            found.Add(new(PaymentPlanIssueCode.CurrencyInvalid, null));
            return false;
        }

        if (finalAmount <= 0)
        {
            found.Add(new(PaymentPlanIssueCode.FinalAmountNotPositive, null));
        }
        else if (finalAmount > MaxAmount)
        {
            found.Add(new(PaymentPlanIssueCode.FinalAmountTooLarge, null));
        }
        else if (HasMoreDecimals(finalAmount, currency.MinorUnits))
        {
            found.Add(new(PaymentPlanIssueCode.FinalAmountPrecisionTooHigh, null));
        }

        // The caller owns `stages` and `allowedMethodCodes` and may change them at any time. Each is read exactly once
        // into a private copy; validation and allocation below use only the copies, so what is validated is what is built.
        var count = stages?.Count ?? 0;
        if (stages is null || count == 0)
        {
            found.Add(new(PaymentPlanIssueCode.NoStages, null));
            return false;
        }

        if (count > MaxStages)
        {
            found.Add(new(PaymentPlanIssueCode.TooManyStages, null));
            return false;
        }

        var snapshot = new PaymentPlanStageInput?[count];
        for (var i = 0; i < count; i++)
        {
            snapshot[i] = stages[i];
        }

        var allowed = allowedMethodCodes is null
            ? null
            : new HashSet<string>(allowedMethodCodes, StringComparer.OrdinalIgnoreCase);

        var methods = new string[count];
        var percentStages = 0;
        var amountStages = 0;
        for (var i = 0; i < count; i++)
        {
            var n = i + 1;
            var stage = snapshot[i];
            methods[i] = ResolveMethod(stage?.MethodCode, allowed, n, found);
            if (stage is null || (stage.Percent is null && stage.Amount is null))
            {
                found.Add(new(PaymentPlanIssueCode.StageValueMissing, n));
            }
            else if (stage.Percent is not null && stage.Amount is not null)
            {
                found.Add(new(PaymentPlanIssueCode.StageValueAmbiguous, n));
            }
            else if (stage.Percent is { } percent)
            {
                percentStages++;
                if (percent <= 0 || percent > 100)
                {
                    found.Add(new(PaymentPlanIssueCode.PercentNotInRange, n));
                }
                else if (HasMoreDecimals(percent, MaxPercentScale))
                {
                    found.Add(new(PaymentPlanIssueCode.PercentPrecisionTooHigh, n));
                }
            }
            else
            {
                amountStages++;
                var amount = stage.Amount!.Value;
                if (amount <= 0)
                {
                    found.Add(new(PaymentPlanIssueCode.AmountNotPositive, n));
                }
                else if (amount > MaxAmount)
                {
                    found.Add(new(PaymentPlanIssueCode.AmountTooLarge, n));
                }
                else if (HasMoreDecimals(amount, currency.MinorUnits))
                {
                    found.Add(new(PaymentPlanIssueCode.AmountPrecisionTooHigh, n));
                }
            }
        }

        if (percentStages > 0 && amountStages > 0)
        {
            found.Add(new(PaymentPlanIssueCode.MixedPercentAndAmount, null));
        }

        if (found.Count > 0)
        {
            return false;
        }

        var result = percentStages > 0
            ? BuildFromPercent(currency, finalAmount, snapshot, methods, found)
            : BuildFromAmounts(finalAmount, snapshot, methods, found);
        if (result is null)
        {
            return false;
        }

        plan = new PaymentPlan(currency.Code, finalAmount, result.Value);
        return true;
    }

    private static ImmutableArray<PaymentPlanStage>? BuildFromPercent(
        CurrencyRef currency, decimal finalAmount, PaymentPlanStageInput?[] stages, string[] methods, List<PaymentPlanIssue> found)
    {
        var percentSum = 0m;
        foreach (var stage in stages)
        {
            percentSum += stage!.Percent!.Value;
        }

        if (percentSum != 100m)
        {
            found.Add(new(PaymentPlanIssueCode.PercentSumNot100, null));
            return null;
        }

        var result = new PaymentPlanStage[stages.Length];
        var allocated = 0m;
        for (var i = 0; i < stages.Length - 1; i++)
        {
            var percent = stages[i]!.Percent!.Value;
            var amount = decimal.Round(finalAmount * percent / 100m, currency.MinorUnits, MidpointRounding.AwayFromZero);
            if (amount <= 0)
            {
                found.Add(new(PaymentPlanIssueCode.AmountNotPositive, i + 1));
                continue;
            }

            allocated += amount;
            result[i] = new(i + 1, methods[i], percent, amount);
        }

        if (found.Count > 0)
        {
            return null;
        }

        var last = stages.Length - 1;
        var residual = finalAmount - allocated;
        if (residual <= 0)
        {
            found.Add(new(PaymentPlanIssueCode.ResidualStageNotPositive, last + 1));
            return null;
        }

        result[last] = new(last + 1, methods[last], stages[last]!.Percent!.Value, residual);
        return ImmutableArray.Create(result);
    }

    private static ImmutableArray<PaymentPlanStage>? BuildFromAmounts(
        decimal finalAmount, PaymentPlanStageInput?[] stages, string[] methods, List<PaymentPlanIssue> found)
    {
        var result = new PaymentPlanStage[stages.Length];
        var sum = 0m;
        for (var i = 0; i < stages.Length; i++)
        {
            var amount = stages[i]!.Amount!.Value;
            sum += amount;
            result[i] = new(i + 1, methods[i], null, amount);
        }

        if (sum != finalAmount)
        {
            found.Add(new(PaymentPlanIssueCode.AmountSumMismatch, null));
            return null;
        }

        return ImmutableArray.Create(result);
    }

    private static string ResolveMethod(string? code, HashSet<string>? allowed, int stageNumber, List<PaymentPlanIssue> found)
    {
        if (!TradeReferenceData.TryFindPaymentMethod(code, out var method))
        {
            found.Add(new(PaymentPlanIssueCode.MethodInvalid, stageNumber));
            return string.Empty;
        }

        if (allowed is not null && !allowed.Contains(method.Code))
        {
            found.Add(new(PaymentPlanIssueCode.MethodNotAllowed, stageNumber));
        }

        return method.Code;
    }

    private static bool HasMoreDecimals(decimal value, int scale) => decimal.Round(value, scale) != value;
}
