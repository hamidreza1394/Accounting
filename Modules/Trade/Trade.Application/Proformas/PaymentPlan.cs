using System.Collections.Immutable;

namespace Faranou.Modules.Trade.Application.Proformas;

/// <summary>Machine-readable reason a payment plan was rejected. Never reflects caller input.</summary>
public enum PaymentPlanIssueCode
{
    NoStages = 1,
    TooManyStages,
    CurrencyInvalid,
    FinalAmountNotPositive,
    FinalAmountTooLarge,
    FinalAmountPrecisionTooHigh,
    MethodInvalid,
    MethodNotAllowed,
    StageValueMissing,
    StageValueAmbiguous,
    PercentNotInRange,
    PercentPrecisionTooHigh,
    AmountNotPositive,
    AmountTooLarge,
    AmountPrecisionTooHigh,
    MixedPercentAndAmount,
    PercentSumNot100,
    AmountSumMismatch,
    ResidualStageNotPositive,
}

/// <summary>One validation problem; <see cref="StageNumber" /> is 1-based, or null for plan-level issues.</summary>
public sealed record PaymentPlanIssue(PaymentPlanIssueCode Code, int? StageNumber);

/// <summary>Caller input for one stage: exactly one of <see cref="Percent" /> or <see cref="Amount" /> must be set.</summary>
public sealed record PaymentPlanStageInput(string? MethodCode, decimal? Percent, decimal? Amount);

/// <summary>
/// Resolved stage. <see cref="Percent" /> is set only for plans defined by percentage. It has no public constructor and
/// no <c>init</c> accessor: a stage exists only inside a plan produced by <see cref="PaymentPlanCalculator.TryBuild" />.
/// </summary>
public sealed record PaymentPlanStage
{
    internal PaymentPlanStage(int sequence, string methodCode, decimal? percent, decimal amount)
    {
        Sequence = sequence;
        MethodCode = methodCode;
        Percent = percent;
        Amount = amount;
    }

    public int Sequence { get; }

    public string MethodCode { get; }

    public decimal? Percent { get; }

    public decimal Amount { get; }
}

/// <summary>
/// Validated, immutable payment plan whose stage amounts sum exactly to <see cref="FinalAmount" />.
/// </summary>
/// <remarks>
/// <see cref="Stages" /> is an <see cref="ImmutableArray{T}" /> built from a private copy: unlike a read-only wrapper around an array, no
/// cast (for example to <c>ICollection</c> and its <c>SyncRoot</c>) gives access to writable storage.
/// There is no public constructor and no <c>init</c> accessor, so an invalid plan cannot be built by hand or changed
/// with a <c>with</c> expression. The only way to obtain a plan is <see cref="PaymentPlanCalculator.TryBuild" />.
/// A stored plan is restored the same way, by calling <see cref="PaymentPlanCalculator.TryBuild" /> again with the stored
/// currency, final amount and stage inputs (percent for a percentage plan, amount for an amount plan); the stored stage
/// amounts must then be compared with the rebuilt ones by the code that reads them. No separate restore type exists yet
/// because nothing is stored yet.
/// </remarks>
public sealed record PaymentPlan
{
    internal PaymentPlan(string currencyCode, decimal finalAmount, ImmutableArray<PaymentPlanStage> stages)
    {
        CurrencyCode = currencyCode;
        FinalAmount = finalAmount;
        Stages = stages;
    }

    public string CurrencyCode { get; }

    public decimal FinalAmount { get; }

    public ImmutableArray<PaymentPlanStage> Stages { get; }
}
