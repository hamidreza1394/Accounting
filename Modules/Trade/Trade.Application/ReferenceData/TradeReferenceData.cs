using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Faranou.Modules.Trade.Application.ReferenceData;

/// <summary>
/// Local, read-only, in-code reference data (no database, no network). Lists are immutable arrays
/// (every mutating member throws and no cast exposes writable storage; the lookup indexes are built from the same lists); lookups are ordinal and case-insensitive, and trim surrounding whitespace.
/// </summary>
public static class TradeReferenceData
{
    public static IReadOnlyList<CountryRef> Countries { get; } = Build(CountryData.Rows, ParseCountry);

    public static IReadOnlyList<CurrencyRef> Currencies { get; } = Build(CurrencyData.Rows, ParseCurrency);

    public static IReadOnlyList<UnitRef> Units { get; } = Build(UnitData.Rows, ParseUnit);

    public static IReadOnlyList<IncotermRef> Incoterms { get; } = Build(IncotermData.Rows, ParseIncoterm);

    public static IReadOnlyList<PaymentMethodRef> PaymentMethods { get; } = Build(PaymentMethodData.Rows, ParsePaymentMethod);

    private static readonly Dictionary<string, CountryRef> CountryIndex = Index(Countries, x => x.Code);
    private static readonly Dictionary<string, CurrencyRef> CurrencyIndex = Index(Currencies, x => x.Code);
    private static readonly Dictionary<string, UnitRef> UnitIndex = Index(Units, x => x.Code);
    private static readonly Dictionary<string, IncotermRef> IncotermIndex = Index(Incoterms, x => x.Code);

    private static readonly Dictionary<string, PaymentMethodRef> PaymentMethodIndex = Index(PaymentMethods, x => x.Code);

    public static bool TryFindCountry(string? code, [MaybeNullWhen(false)] out CountryRef country) => Find(CountryIndex, code, out country);

    public static bool TryFindCurrency(string? code, [MaybeNullWhen(false)] out CurrencyRef currency) => Find(CurrencyIndex, code, out currency);

    public static bool TryFindUnit(string? code, [MaybeNullWhen(false)] out UnitRef unit) => Find(UnitIndex, code, out unit);

    public static bool TryFindIncoterm(string? code, [MaybeNullWhen(false)] out IncotermRef incoterm) => Find(IncotermIndex, code, out incoterm);

    public static bool TryFindPaymentMethod(string? code, [MaybeNullWhen(false)] out PaymentMethodRef method) => Find(PaymentMethodIndex, code, out method);

    private static bool Find<T>(Dictionary<string, T> index, string? code, [MaybeNullWhen(false)] out T value)
    {
        if (!string.IsNullOrWhiteSpace(code) && index.TryGetValue(code.Trim(), out var found))
        {
            value = found;
            return true;
        }

        value = default!;
        return false;
    }

    private static Dictionary<string, T> Index<T>(IReadOnlyList<T> items, Func<T, string> code)
    {
        var map = new Dictionary<string, T>(items.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            map.Add(code(item), item);
        }

        return map;
    }

    private static ImmutableArray<T> Build<T>(string[] rows, Func<string[], T> parse)
    {
        var items = new T[rows.Length];
        for (var i = 0; i < rows.Length; i++)
        {
            items[i] = parse(rows[i].Split('|'));
        }

        return ImmutableArray.Create(items);
    }

    private static CountryRef ParseCountry(string[] p) => new(p[0], p[2], p[1]);

    private static CurrencyRef ParseCurrency(string[] p) => new(p[0], p[2], p[1], int.Parse(p[3], CultureInfo.InvariantCulture));

    private static UnitRef ParseUnit(string[] p) => new(p[0], p[2], p[1]);

    private static PaymentMethodRef ParsePaymentMethod(string[] p) => new(p[0], p[2], p[1]);

    private static IncotermRef ParseIncoterm(string[] p) => new(p[0], p[2], p[1], p[3] == "1");
}
