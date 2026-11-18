using System;
using Faranou.Modules.Trade.Domain.Common;
using Xunit;

namespace Template.Tests;

public sealed class TradeDocumentNumberTests
{
    private static TradeDocumentNumber Valid(string? raw)
    {
        Assert.True(TradeDocumentNumber.TryCreate(raw, out var number, out var error), $"unexpected error {error}");
        Assert.Equal(TradeDocumentNumberError.None, error);
        return number!;
    }

    private static void Invalid(string? raw, TradeDocumentNumberError expected)
    {
        Assert.False(TradeDocumentNumber.TryCreate(raw, out var number, out var error));
        Assert.Null(number);
        Assert.Equal(expected, error);
    }

    [Theory]
    [InlineData("PI-100", "PI-100")]
    [InlineData("pi-100", "PI-100")]
    [InlineData("  ABC2041  ", "ABC2041")]
    [InlineData("AB-G/P-W-7", "AB-G/P-W-7")]
    [InlineData("FM-77/1", "FM-77/1")]
    public void Normalized_IsTrimmedAndUpperCased(string raw, string expected)
    {
        Assert.Equal(expected, Valid(raw).Normalized);
    }

    [Fact]
    public void Normalized_ConvertsPersianAndArabicIndicDigitsToAscii()
    {
        // Persian (U+06F1..U+06F9) and Arabic-Indic (U+0661..U+0669) digits one to nine.
        var persian = "\u06F1\u06F2\u06F3\u06F4\u06F5\u06F6\u06F7\u06F8\u06F9";
        var arabic = "\u0661\u0662\u0663\u0664\u0665\u0666\u0667\u0668\u0669";

        Assert.Equal("123456789", Valid(persian).Normalized);
        Assert.Equal("123456789", Valid(arabic).Normalized);
        Assert.Equal(Valid("123456789"), Valid(persian));
    }

    [Fact]
    public void Normalized_ConvertsFullWidthDigitsThroughNfkc()
    {
        Assert.Equal("1008", Valid("\uFF11\uFF10\uFF10\uFF18").Normalized);
    }

    [Fact]
    public void Normalized_UnifiesArabicYehAndKafWithPersianForms()
    {
        Assert.Equal("\u06A9\u06CC", Valid("\u0643\u064A").Normalized);
        Assert.Equal("\u06A9\u06CC", Valid("\u0643\u0649").Normalized);
        Assert.Equal("\u06A9\u06CC", Valid("\u06A9\u06CC").Normalized);
    }

    [Fact]
    public void Display_KeepsUserTextWithCollapsedSpaces()
    {
        var number = Valid("  pi   x \u00A0 100 ");

        Assert.Equal("pi x 100", number.Display);
        Assert.Equal("PI X 100", number.Normalized);
        Assert.Equal("pi x 100", number.ToString());
    }

    [Fact]
    public void Separators_AreNeverRemovedOrUnified()
    {
        var hyphen = Valid("FM-77");
        var slashSuffix = Valid("FM-77/1");
        var space = Valid("FM 77");
        var joined = Valid("FM77");

        Assert.NotEqual(hyphen, slashSuffix);
        Assert.NotEqual(hyphen, space);
        Assert.NotEqual(hyphen, joined);
        Assert.NotEqual(space, joined);
    }

    [Fact]
    public void Equality_UsesNormalizedFormOnly()
    {
        var a = Valid("pi-100");
        var b = Valid("  PI-100");

        Assert.Equal(a, b);
        Assert.True(a.Equals((object)b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a.Display, b.Display);
        Assert.False(a.Equals(null));
        Assert.False(a.Equals((object?)"PI-100"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\u00A0\u2003")]
    public void Missing_WhenNullOrOnlyWhitespace(string? raw)
    {
        Invalid(raw, TradeDocumentNumberError.Missing);
    }

    [Fact]
    public void Length_IsLimitedToMaxLengthAfterTrimming()
    {
        var atLimit = new string('9', TradeDocumentNumber.MaxLength);

        Assert.Equal(atLimit, Valid(atLimit).Normalized);
        Assert.Equal(atLimit, Valid("  " + atLimit + "  ").Normalized);
        Invalid(atLimit + "9", TradeDocumentNumberError.TooLong);
    }

    [Theory]
    [InlineData("A\u0000B")]
    [InlineData("A\tB")]
    [InlineData("A\nB")]
    [InlineData("A\u007FB")]
    [InlineData("A\u200CB")]
    [InlineData("A\u200BB")]
    [InlineData("A\u200EB")]
    [InlineData("A\u200FB")]
    [InlineData("A\u202EB")]
    [InlineData("A\u2066B")]
    [InlineData("A\u061CB")]
    [InlineData("A\u2028B")]
    [InlineData("A\u2029B")]
    [InlineData("A\uFEFFB")]
    [InlineData("A\uE000B")]
    [InlineData("A\uD83D\uDE00B")]
    [InlineData("A\u3164B")]
    [InlineData("A\uFFA0B")]
    [InlineData("A\u115FB")]
    [InlineData("A\u2800B")]
    [InlineData("A\u034FB")]
    [InlineData("A\uFE0FB")]
    [InlineData("A\u17B4B")]
    [InlineData("A\uFE70B")]
    [InlineData("A\u093E")]
    [InlineData("A\u093EB")]
    public void InvalidCharacter_RejectsControlFormatAndInvisibleCharacters(string raw)
    {
        Invalid(raw, TradeDocumentNumberError.InvalidCharacter);
    }

    [Fact]
    public void InvisibleCharacters_CannotBeUsedToEvadeUniqueness()
    {
        Invalid("FM\u200E-77", TradeDocumentNumberError.InvalidCharacter);
        Invalid("FM-\u200B77", TradeDocumentNumberError.InvalidCharacter);
    }

    [Fact]
    public void SameNumberDifferentCase_IsEqualAndNeighbouringNumbersAreNot()
    {
        Assert.Equal(Valid("PI-100"), Valid("pi-100 "));
        Assert.NotEqual(Valid("AB-G/P-W-7"), Valid("AB-G/P-W-8"));
        Assert.NotEqual(Valid("AB-G/P-W-7"), Valid("AB-G/P-W-7/1"));
    }

    [Fact]
    public void Normalized_ConvertsZeroDigitsOfBothScripts()
    {
        Assert.Equal("0", Valid("\u06F0").Normalized);
        Assert.Equal("0", Valid("\u0660").Normalized);
        Assert.Equal("90", Valid("\u06F9\u0660").Normalized);
    }

    [Fact]
    public void Normalized_UpperCasesNonLatinLettersWithSimpleMappings()
    {
        Assert.Equal("\u0391\u0392", Valid("\u03B1\u03B2").Normalized);
        Assert.Equal("\u0410\u0411", Valid("\u0430\u0431").Normalized);
    }

    [Fact]
    public void TooLong_IsAlsoDetectedAfterNfkcExpansion()
    {
        // U+FDFA expands to 18 characters under NFKC: four of them fit the raw limit but not the stored form.
        var raw = new string((char)0xFDFA, 4);

        Assert.True(raw.Length <= TradeDocumentNumber.MaxLength);
        Invalid(raw, TradeDocumentNumberError.TooLong);
    }

    [Fact]
    public void SpacesProducedByNfkc_AreNotAllowedToSplitEquivalentInputs()
    {
        // U+FE70 turns into a space plus a combining mark under NFKC; the combining mark is rejected after NFKC.
        Invalid("A\uFE70", TradeDocumentNumberError.InvalidCharacter);
        Invalid("A \uFE70", TradeDocumentNumberError.InvalidCharacter);
    }

    [Theory]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData(" \t ")]
    public void OnlyWhitespaceControlCharacters_AreReportedAsMissing(string raw)
    {
        Invalid(raw, TradeDocumentNumberError.Missing);
    }
}
