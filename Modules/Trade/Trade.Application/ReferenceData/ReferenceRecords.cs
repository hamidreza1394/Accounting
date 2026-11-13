namespace Faranou.Modules.Trade.Application.ReferenceData;

/// <summary>Country (ISO 3166-1 alpha-2 code).</summary>
public sealed record CountryRef(string Code, string NameFa, string NameEn);

/// <summary>Currency (ISO 4217 code) with the number of minor-unit decimal digits.</summary>
public sealed record CurrencyRef(string Code, string NameFa, string NameEn, int MinorUnits);

/// <summary>Trade measurement unit. Codes are upper-case ASCII (compatible with Product.BaseUnitCode).</summary>
public sealed record UnitRef(string Code, string NameFa, string NameEn);

/// <summary>Incoterms 2020 rule; <see cref="SeaOnly" /> marks sea and inland-waterway only rules.</summary>
public sealed record IncotermRef(string Code, string NameFa, string NameEn, bool SeaOnly);
