using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace Faranou.Modules.Trade.Domain.Common;

/// <summary>Why a raw document number was rejected. Never carries the rejected value.</summary>
public enum TradeDocumentNumberError
{
    None = 0,
    Missing,
    TooLong,
    InvalidCharacter,
}

/// <summary>
/// A user-entered foreign-trade document number (proforma number, national trade system case number,
/// order registration number) with a canonical <see cref="Normalized"/> form for uniqueness checks.
/// </summary>
/// <remarks>
/// <para>
/// The normalized form is built in the application, not by database collation (D-080 section 9, D-086 section 3):
/// Unicode NFKC, Persian/Arabic-Indic digits to ASCII digits, Arabic Yeh/Kaf to Persian Yeh/Kaf, upper-invariant.
/// Whitespace (including spaces produced by NFKC) is only trimmed and collapsed; separators such as '-', '/' and ' ' are never removed or unified,
/// so <c>FM-1008</c> and <c>FM-1008/1</c> stay distinct (the user, not the software, picks a replacement number).
/// </para>
/// <para>
/// This type carries no uniqueness scope and no number format. Scope (Company) and per-kind formats are decided
/// by the entities that use it. Equality is by <see cref="Normalized"/> only.
/// </para>
/// </remarks>
public sealed class TradeDocumentNumber : IEquatable<TradeDocumentNumber>
{
    public const int MaxLength = 64;

    private TradeDocumentNumber(string display, string normalized)
    {
        Display = display;
        Normalized = normalized;
    }

    /// <summary>Trimmed text as the user entered it, with runs of spaces collapsed to one space.</summary>
    public string Display { get; }

    /// <summary>Canonical text to store and compare for uniqueness.</summary>
    public string Normalized { get; }

    public static bool TryCreate(
        string? raw,
        [NotNullWhen(true)] out TradeDocumentNumber? number,
        out TradeDocumentNumberError error)
    {
        number = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            error = TradeDocumentNumberError.Missing;
            return false;
        }

        if (!TryCollapse(raw, out var display))
        {
            error = TradeDocumentNumberError.InvalidCharacter;
            return false;
        }

        if (display.Length > MaxLength)
        {
            error = TradeDocumentNumberError.TooLong;
            return false;
        }

        // NFKC and the letter/digit mapping run first; validation and space collapsing then run again on the
        // result, so characters that NFKC turns into spaces, combining marks or controls cannot slip through.
        if (!TryCollapse(MapLetters(display.Normalize(NormalizationForm.FormKC)), out var collapsed))
        {
            error = TradeDocumentNumberError.InvalidCharacter;
            return false;
        }

        var normalized = collapsed.ToUpperInvariant();
        if (normalized.Length > MaxLength)
        {
            // NFKC can expand some characters; the stored form must still fit the column.
            error = TradeDocumentNumberError.TooLong;
            return false;
        }

        number = new TradeDocumentNumber(display, normalized);
        error = TradeDocumentNumberError.None;
        return true;
    }

    public bool Equals(TradeDocumentNumber? other) =>
        other is not null && string.Equals(Normalized, other.Normalized, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as TradeDocumentNumber);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Normalized);

    public override string ToString() => Display;

    /// <summary>
    /// Rejects invisible, control and combining characters and collapses space runs (tabs and newlines are rejected).
    /// Coverage is deliberate but partial: Unicode has no complete "invisible character" list, so a rejected-set miss
    /// is possible for exotic code points; it is not a substitute for a visual-similarity (homoglyph) check.
    /// </summary>
    private static bool TryCollapse(string text, out string collapsed)
    {
        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;

        foreach (var ch in text)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.SpaceSeparator)
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (IsRejected(ch))
            {
                collapsed = string.Empty;
                return false;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(ch);
        }

        collapsed = builder.ToString();
        return true;
    }

    private static bool IsRejected(char ch)
    {
        switch (CharUnicodeInfo.GetUnicodeCategory(ch))
        {
            case UnicodeCategory.Control:
            case UnicodeCategory.Format:
            case UnicodeCategory.LineSeparator:
            case UnicodeCategory.ParagraphSeparator:
            case UnicodeCategory.PrivateUse:
            case UnicodeCategory.Surrogate:
            case UnicodeCategory.OtherNotAssigned:
            case UnicodeCategory.NonSpacingMark:
            case UnicodeCategory.EnclosingMark:
                return true;
        }

        // Letter/symbol code points that render as blank: Hangul fillers and the Braille blank pattern.
        return (int)ch is 0x115F or 0x1160 or 0x3164 or 0xFFA0 or 0x2800;
    }

    private static string MapLetters(string text)
    {
        var builder = new StringBuilder(text.Length);

        foreach (var ch in text)
        {
            var code = (int)ch;
            builder.Append(code switch
            {
                >= 0x06F0 and <= 0x06F9 => (char)('0' + (code - 0x06F0)),
                >= 0x0660 and <= 0x0669 => (char)('0' + (code - 0x0660)),
                0x064A or 0x0649 => (char)0x06CC,
                0x0643 => (char)0x06A9,
                _ => ch,
            });
        }

        return builder.ToString();
    }
}
