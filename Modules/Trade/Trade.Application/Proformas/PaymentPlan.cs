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

/// <summary>Resolved stage. <see cref="Percent" /> is set only for plans defined by percentage.</summary>
public sealed record PaymentPlanStage(int Sequence, string MethodCode, decimal? Percent, decimal Amount);

/// <summary>Validated, immutable payment plan whose stage amounts sum exactly to <see cref="FinalAmount" />.</summary>
public sealed record PaymentPlan(string CurrencyCode, decimal FinalAmount, IReadOnlyList<PaymentPlanStage> Stages);
