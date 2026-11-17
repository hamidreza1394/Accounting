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
/// Whitespace is only trimmed and collapsed; separators such as '-', '/' and ' ' are never removed or unified,
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

        if (display.Length == 0)
        {
            error = TradeDocumentNumberError.Missing;
            return false;
        }

        if (display.Length > MaxLength)
        {
            error = TradeDocumentNumberError.TooLong;
            return false;
        }

        var normalized = Normalize(display);
        if (normalized.Length == 0 || normalized.Length > MaxLength)
        {
            // NFKC can expand some characters; the stored form must still fit the column.
            error = normalized.Length == 0 ? TradeDocumentNumberError.InvalidCharacter : TradeDocumentNumberError.TooLong;
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

    /// <summary>Rejects invisible/control characters and collapses space runs; tabs and newlines are rejected.</summary>
    private static bool TryCollapse(string raw, out string collapsed)
    {
        var builder = new StringBuilder(raw.Length);
        var pendingSpace = false;

        foreach (var ch in raw)
        {
            switch (CharUnicodeInfo.GetUnicodeCategory(ch))
            {
                case UnicodeCategory.SpaceSeparator:
                    pendingSpace = builder.Length > 0;
                    continue;
                case UnicodeCategory.Control:
                case UnicodeCategory.Format:
                case UnicodeCategory.LineSeparator:
                case UnicodeCategory.ParagraphSeparator:
                case UnicodeCategory.PrivateUse:
                case UnicodeCategory.Surrogate:
                case UnicodeCategory.OtherNotAssigned:
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

    private static string Normalize(string display)
    {
        var text = display.Normalize(NormalizationForm.FormKC);
        var builder = new StringBuilder(text.Length);

        foreach (var ch in text)
        {
            builder.Append(ch switch
            {
                >= '\u06F0' and <= '\u06F9' => (char)('0' + (ch - '\u06F0')),
                >= '\u0660' and <= '\u0669' => (char)('0' + (ch - '\u0660')),
                '\u064A' or '\u0649' => '\u06CC',
                '\u0643' => '\u06A9',
                _ => ch,
            });
        }

        return builder.ToString().ToUpperInvariant();
    }
}
