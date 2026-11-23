using Faranou.Modules.Trade.Domain.Units;

namespace Faranou.Modules.Trade.Application.Proformas;

/// <summary>How a Proforma validity date moves between two states.</summary>
public enum ProformaValidityChange
{
    None = 0,
    Extended = 1,

    /// <summary>The new date is earlier than the old one. Whether this is allowed, and as which event, is not decided (owner decision).</summary>
    Shortened = 2,
}

/// <summary>One goods line of the commercial terms. The quantity is the stored snapshot, so unit and factor are part of the comparison.</summary>
public sealed record ProformaLineTerms(string? HsCode, string? ProductName, TradeQuantitySnapshot Quantity, decimal UnitPrice);

/// <summary>The commercial terms of a Proforma. A change in any of these is a new Revision; the validity date is not part of them.</summary>
public sealed record ProformaCommercialTerms(
    string CurrencyCode,
    string? IncotermCode,
    IReadOnlyList<ProformaLineTerms> Lines,
    PaymentPlan PaymentPlan);

/// <summary>Result of <see cref="ProformaChangeClassifier.Classify" />. A terms change and a validity change are reported independently.</summary>
public sealed record ProformaChangeResult(bool RequiresNewRevision, ProformaValidityChange Validity)
{
    public bool IsNoChange => !RequiresNewRevision && Validity == ProformaValidityChange.None;
}

/// <summary>
/// Pure, schema-free rule from the owner's Proforma decisions: a change of commercial terms creates a new Revision,
/// while extending only the validity date is a separate history event and creates none. Where the validity date is
/// stored (Header or Revision) is not decided here and this type does not need it.
/// </summary>
public static class ProformaChangeClassifier
{
    public static ProformaChangeResult Classify(
        ProformaCommercialTerms previous,
        DateOnly previousValidUntil,
        ProformaCommercialTerms proposed,
        DateOnly proposedValidUntil)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(proposed);

        var validity = proposedValidUntil.CompareTo(previousValidUntil) switch
        {
            > 0 => ProformaValidityChange.Extended,
            < 0 => ProformaValidityChange.Shortened,
            _ => ProformaValidityChange.None,
        };

        return new(!TermsEqual(previous, proposed), validity);
    }

    private static bool TermsEqual(ProformaCommercialTerms a, ProformaCommercialTerms b)
    {
        if (!string.Equals(a.CurrencyCode, b.CurrencyCode, StringComparison.Ordinal)
            || !string.Equals(a.IncotermCode, b.IncotermCode, StringComparison.Ordinal)
            || a.Lines.Count != b.Lines.Count
            || !PlanEqual(a.PaymentPlan, b.PaymentPlan))
        {
            return false;
        }

        // Line order is part of the document, so a reordering counts as a change.
        for (var i = 0; i < a.Lines.Count; i++)
        {
            if (a.Lines[i] != b.Lines[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool PlanEqual(PaymentPlan a, PaymentPlan b)
    {
        if (!string.Equals(a.CurrencyCode, b.CurrencyCode, StringComparison.Ordinal)
            || a.FinalAmount != b.FinalAmount
            || a.Stages.Length != b.Stages.Length)
        {
            return false;
        }

        for (var i = 0; i < a.Stages.Length; i++)
        {
            if (a.Stages[i] != b.Stages[i])
            {
                return false;
            }
        }

        return true;
    }
}
