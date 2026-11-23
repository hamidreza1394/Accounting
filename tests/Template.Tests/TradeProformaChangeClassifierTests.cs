using Faranou.Modules.Trade.Application.Proformas;
using Faranou.Modules.Trade.Domain.Units;
using Xunit;

namespace Template.Tests;

public sealed class TradeProformaChangeClassifierTests
{
    private static readonly DateOnly Valid = new(2027, 1, 31);

    private static TradeQuantitySnapshot Qty(decimal tons)
    {
        Assert.True(TradeUnitConversion.TryCreate("TON", "KG", 1000m, new(3, TradeRoundingMode.HalfUp), out var c, out _));
        Assert.True(c.TryConvert(tons, out var s, out _));
        return s;
    }

    private static PaymentPlan Plan(decimal total, params (string Method, decimal Percent)[] stages)
    {
        Assert.True(PaymentPlanCalculator.TryBuild(
            "USD", total, stages.Select(x => new PaymentPlanStageInput(x.Method, x.Percent, null)).ToArray(), null, out var plan, out _));
        return plan!;
    }

    private static ProformaCommercialTerms Terms(
        string currency = "USD", string? incoterm = "FOB", decimal tons = 10m, decimal price = 500m, string hs = "100119", decimal total = 5000m, decimal firstPercent = 30m)
        => new(
            currency,
            incoterm,
            [new ProformaLineTerms(hs, "Wheat", Qty(tons), price)],
            Plan(total, ("TT", firstPercent), ("LC", 100m - firstPercent)));

    [Fact]
    public void IdenticalTermsAndDate_IsNoChange()
    {
        var r = ProformaChangeClassifier.Classify(Terms(), Valid, Terms(), Valid);
        Assert.True(r.IsNoChange);
    }

    [Fact]
    public void OnlyExtendingValidity_NeedsNoNewRevision()
    {
        var r = ProformaChangeClassifier.Classify(Terms(), Valid, Terms(), Valid.AddDays(30));
        Assert.False(r.RequiresNewRevision);
        Assert.Equal(ProformaValidityChange.Extended, r.Validity);
    }

    [Fact]
    public void ShorteningValidity_IsReportedSeparatelyAndNotDecided()
    {
        var r = ProformaChangeClassifier.Classify(Terms(), Valid, Terms(), Valid.AddDays(-1));
        Assert.False(r.RequiresNewRevision);
        Assert.Equal(ProformaValidityChange.Shortened, r.Validity);
    }

    [Theory]
    [InlineData("currency")]
    [InlineData("incoterm")]
    [InlineData("quantity")]
    [InlineData("price")]
    [InlineData("hs")]
    [InlineData("payment")]
    public void AnyCommercialTermChange_RequiresNewRevision(string field)
    {
        var changed = field switch
        {
            "currency" => Terms(currency: "EUR"),
            "incoterm" => Terms(incoterm: "CIF"),
            "quantity" => Terms(tons: 11m, total: 5000m),
            "price" => Terms(price: 510m),
            "hs" => Terms(hs: "100590"),
            _ => Terms(firstPercent: 40m),
        };

        var r = ProformaChangeClassifier.Classify(Terms(), Valid, changed, Valid);
        Assert.True(r.RequiresNewRevision);
        Assert.Equal(ProformaValidityChange.None, r.Validity);
    }

    [Fact]
    public void TermsChangeWithExtension_ReportsBoth()
    {
        var r = ProformaChangeClassifier.Classify(Terms(), Valid, Terms(price: 510m), Valid.AddDays(10));
        Assert.True(r.RequiresNewRevision);
        Assert.Equal(ProformaValidityChange.Extended, r.Validity);
    }

    [Fact]
    public void MultiLineWithDifferentHsCodes_ComparesEveryLineAndOrder()
    {
        var plan = Plan(5000m, ("TT", 100m));
        var a = new ProformaLineTerms("100119", "Wheat", Qty(5m), 500m);
        var b = new ProformaLineTerms("100590", "Maize", Qty(5m), 500m);
        var same = ProformaChangeClassifier.Classify(new("USD", "FOB", [a, b], plan), Valid, new("USD", "FOB", [a, b], plan), Valid);
        var swapped = ProformaChangeClassifier.Classify(new("USD", "FOB", [a, b], plan), Valid, new("USD", "FOB", [b, a], plan), Valid);
        var dropped = ProformaChangeClassifier.Classify(new("USD", "FOB", [a, b], plan), Valid, new("USD", "FOB", [a], plan), Valid);

        Assert.True(same.IsNoChange);
        Assert.True(swapped.RequiresNewRevision);
        Assert.True(dropped.RequiresNewRevision);
    }

    [Fact]
    public void QuantityUnitChange_WithSameBaseAmount_IsStillAChange()
    {
        Assert.True(TradeUnitConversion.TryCreate("KG", "KG", 1m, new(3, TradeRoundingMode.HalfUp), out var kg, out _));
        Assert.True(kg.TryConvert(10000m, out var inKg, out _));
        var plan = Plan(5000m, ("TT", 100m));
        var tons = new ProformaCommercialTerms("USD", "FOB", [new("100119", "Wheat", Qty(10m), 500m)], plan);
        var kilos = new ProformaCommercialTerms("USD", "FOB", [new("100119", "Wheat", inKg, 500m)], plan);

        Assert.True(ProformaChangeClassifier.Classify(tons, Valid, kilos, Valid).RequiresNewRevision);
    }

    [Fact]
    public void NullTerms_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => ProformaChangeClassifier.Classify(null!, Valid, Terms(), Valid));
        Assert.Throws<ArgumentNullException>(() => ProformaChangeClassifier.Classify(Terms(), Valid, null!, Valid));
    }
}
