using System.Collections;
using Faranou.Modules.Trade.Application.ReferenceData;
using Xunit;

namespace Template.Tests;

public sealed class TradeReferenceDataTests
{
    private static IEnumerable<(string Code, string[] Texts)> All()
    {
        foreach (var x in TradeReferenceData.Countries)
        {
            yield return (x.Code, [x.NameFa, x.NameEn]);
        }
        foreach (var x in TradeReferenceData.Currencies)
        {
            yield return (x.Code, [x.NameFa, x.NameEn]);
        }
        foreach (var x in TradeReferenceData.Units)
        {
            yield return (x.Code, [x.NameFa, x.NameEn]);
        }
        foreach (var x in TradeReferenceData.Incoterms)
        {
            yield return (x.Code, [x.NameFa, x.NameEn]);
        }
    }

    [Fact]
    public void Codes_AreUniquePerListAndFieldsNonEmpty()
    {
        Assert.Equal(TradeReferenceData.Countries.Count, TradeReferenceData.Countries.Select(x => x.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(TradeReferenceData.Currencies.Count, TradeReferenceData.Currencies.Select(x => x.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(TradeReferenceData.Units.Count, TradeReferenceData.Units.Select(x => x.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(TradeReferenceData.Incoterms.Count, TradeReferenceData.Incoterms.Select(x => x.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        Assert.All(All(), item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Code));
            Assert.Equal(item.Code.Trim(), item.Code);
            Assert.All(item.Code, c => Assert.True(char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c), item.Code));
            Assert.True(item.Code.Length <= 16, item.Code);
            Assert.All(item.Texts, t =>
            {
                Assert.False(string.IsNullOrWhiteSpace(t), item.Code);
                Assert.Equal(t.Trim(), t);
            });
        });
    }

    [Fact]
    public void CodeShapes_FollowTheirStandards()
    {
        Assert.All(TradeReferenceData.Countries, x => Assert.Equal(2, x.Code.Length));
        Assert.All(TradeReferenceData.Currencies, x =>
        {
            Assert.Equal(3, x.Code.Length);
            Assert.InRange(x.MinorUnits, 0, 4);
        });
        Assert.Equal(249, TradeReferenceData.Countries.Count);
    }

    [Fact]
    public void PersianText_UsesPersianLettersNotArabicVariants()
    {
        Assert.All(All(), item => Assert.All(item.Texts, text =>
        {
            Assert.DoesNotContain('ي', text); // Arabic Yeh
            Assert.DoesNotContain('ك', text); // Arabic Kaf
            Assert.DoesNotContain('ى', text); // Alef Maksura
        }));

        Assert.All(TradeReferenceData.Countries, x => Assert.Contains(x.NameFa, c => c is >= '؀' and <= 'ۿ'));
    }

    [Theory]
    [InlineData("ir")]
    [InlineData("IR")]
    [InlineData("Ir")]
    public void CountryLookup_IsCaseInsensitive(string code)
    {
        Assert.True(TradeReferenceData.TryFindCountry(code, out var country));
        Assert.Equal("IR", country.Code);
        Assert.Equal("ایران", country.NameFa);
    }

    [Fact]
    public void Lookups_AreCaseInsensitiveAndRejectUnknownOrEmpty()
    {
        Assert.True(TradeReferenceData.TryFindCurrency("usd", out var usd));
        Assert.Equal("USD", usd.Code);
        Assert.True(TradeReferenceData.TryFindUnit("ton", out var ton));
        Assert.Equal("TON", ton.Code);
        Assert.True(TradeReferenceData.TryFindIncoterm("fob", out var fob));
        Assert.True(fob.SeaOnly);
        Assert.True(TradeReferenceData.TryFindIncoterm("Exw", out var exw));
        Assert.False(exw.SeaOnly);

        Assert.False(TradeReferenceData.TryFindCountry("XX", out _));
        Assert.False(TradeReferenceData.TryFindCountry("", out var none));
        Assert.Null(none);
        Assert.False(TradeReferenceData.TryFindCurrency(null, out _));
        Assert.False(TradeReferenceData.TryFindUnit("  ", out _));
        Assert.False(TradeReferenceData.TryFindIncoterm("DDU", out var noIncoterm));
        Assert.Null(noIncoterm);
        Assert.False(TradeReferenceData.TryFindCurrency("XXX", out var noCurrency));
        Assert.Null(noCurrency);
        Assert.False(TradeReferenceData.TryFindUnit("NOPE", out var noUnit));
        Assert.Null(noUnit);
        Assert.True(TradeReferenceData.TryFindCountry(" ir ", out var trimmed));
        Assert.Equal("IR", trimmed.Code);
    }

    [Fact]
    public void Incoterms_AreExactlyTheElevenOf2020()
    {
        var expected = new[] { "EXW", "FCA", "CPT", "CIP", "DAP", "DPU", "DDP", "FAS", "FOB", "CFR", "CIF" };
        Assert.Equal(expected.Order(StringComparer.Ordinal), TradeReferenceData.Incoterms.Select(x => x.Code).Order(StringComparer.Ordinal));
        Assert.Equal(
            new[] { "CFR", "CIF", "FAS", "FOB" },
            TradeReferenceData.Incoterms.Where(x => x.SeaOnly).Select(x => x.Code).Order(StringComparer.Ordinal));
    }

    private static readonly string[] RequiredCurrencies = ["IRR", "USD", "EUR", "AED", "CNY", "TRY", "GBP", "INR", "RUB", "JPY", "CHF", "KRW", "CAD", "AUD"];

    [Fact]
    public void KeyCountriesCurrenciesAndUnits_ArePresentWithAccuratePersianNames()
    {
        var countries = new Dictionary<string, string>
        {
            ["IR"] = "ایران", ["TR"] = "ترکیه", ["AE"] = "امارات متحده عربی", ["CN"] = "چین", ["DE"] = "آلمان",
            ["IT"] = "ایتالیا", ["IN"] = "هند", ["RU"] = "روسیه", ["IQ"] = "عراق", ["AF"] = "افغانستان",
            ["PK"] = "پاکستان", ["AM"] = "ارمنستان", ["AZ"] = "جمهوری آذربایجان", ["FR"] = "فرانسه",
            ["GB"] = "بریتانیا", ["US"] = "ایالات متحده آمریکا", ["JP"] = "ژاپن", ["KR"] = "کره جنوبی",
        };
        foreach (var (code, fa) in countries)
        {
            Assert.True(TradeReferenceData.TryFindCountry(code, out var c), code);
            Assert.Equal(fa, c.NameFa);
        }

        foreach (var code in RequiredCurrencies)
        {
            Assert.True(TradeReferenceData.TryFindCurrency(code, out _), code);
        }

        Assert.Equal(0, TradeReferenceData.Currencies.Single(x => x.Code == "JPY").MinorUnits);
        Assert.Equal(2, TradeReferenceData.Currencies.Single(x => x.Code == "USD").MinorUnits);

        // Product.BaseUnitCode defaults to "TON" and must stay a valid unit.
        foreach (var code in new[] { "TON", "KG", "G", "M", "M2", "M3", "L", "PCS", "SET", "CARTON", "PALLET", "CNT20", "CNT40" })
        {
            Assert.True(TradeReferenceData.TryFindUnit(code, out _), code);
        }
    }

    [Fact]
    public void Lists_AreImmutable()
    {
        AssertImmutable(TradeReferenceData.Countries, TradeReferenceData.Countries[0]);
        AssertImmutable(TradeReferenceData.Currencies, TradeReferenceData.Currencies[0]);
        AssertImmutable(TradeReferenceData.Units, TradeReferenceData.Units[0]);
        AssertImmutable(TradeReferenceData.Incoterms, TradeReferenceData.Incoterms[0]);
    }

    private static void AssertImmutable<T>(IReadOnlyList<T> list, T sample)
    {
        var before = list.Count;
        Assert.False(list is T[]);
        var asList = Assert.IsAssignableFrom<IList<T>>(list);
        Assert.True(asList.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => asList.Add(sample));
        Assert.Throws<NotSupportedException>(() => asList[0] = sample);
        Assert.Throws<NotSupportedException>(() => asList.Clear());
        Assert.Throws<NotSupportedException>(() => ((IList)list).RemoveAt(0));
        Assert.Equal(before, list.Count);
    }
}
