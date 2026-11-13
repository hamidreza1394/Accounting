namespace Faranou.Modules.Trade.Application.ReferenceData;

// Row format: CODE|English name|Persian name|extra (minor units / sea-only flag)
internal static class CurrencyData
{
    internal static readonly string[] Rows =
    [
        "IRR|Iranian Rial|ریال ایران|2",
        "USD|US Dollar|دلار آمریکا|2",
        "EUR|Euro|یورو|2",
        "AED|UAE Dirham|درهم امارات|2",
        "CNY|Chinese Yuan|یوان چین|2",
        "TRY|Turkish Lira|لیر ترکیه|2",
        "GBP|Pound Sterling|پوند استرلینگ|2",
        "INR|Indian Rupee|روپیه هند|2",
        "RUB|Russian Ruble|روبل روسیه|2",
        "JPY|Japanese Yen|ین ژاپن|0",
        "CHF|Swiss Franc|فرانک سوئیس|2",
        "KRW|South Korean Won|وون کره جنوبی|0",
        "CAD|Canadian Dollar|دلار کانادا|2",
        "AUD|Australian Dollar|دلار استرالیا|2",
        "SAR|Saudi Riyal|ریال عربستان|2",
        "QAR|Qatari Riyal|ریال قطر|2",
        "OMR|Omani Rial|ریال عمان|3",
        "KWD|Kuwaiti Dinar|دینار کویت|3",
        "BHD|Bahraini Dinar|دینار بحرین|3",
        "IQD|Iraqi Dinar|دینار عراق|3",
        "AFN|Afghan Afghani|افغانی افغانستان|2",
        "PKR|Pakistani Rupee|روپیه پاکستان|2",
        "AMD|Armenian Dram|درام ارمنستان|2",
        "AZN|Azerbaijani Manat|منات آذربایجان|2",
        "GEL|Georgian Lari|لاری گرجستان|2",
        "KZT|Kazakhstani Tenge|تنگه قزاقستان|2",
        "UZS|Uzbekistani Som|سوم ازبکستان|2",
        "SEK|Swedish Krona|کرون سوئد|2",
        "NOK|Norwegian Krone|کرون نروژ|2",
        "DKK|Danish Krone|کرون دانمارک|2",
        "PLN|Polish Zloty|زلوتی لهستان|2",
        "HKD|Hong Kong Dollar|دلار هنگ‌کنگ|2",
        "SGD|Singapore Dollar|دلار سنگاپور|2",
        "MYR|Malaysian Ringgit|رینگیت مالزی|2",
        "THB|Thai Baht|بات تایلند|2",
        "EGP|Egyptian Pound|پوند مصر|2",
        "ZAR|South African Rand|راند آفریقای جنوبی|2",
        "BRL|Brazilian Real|رئال برزیل|2",
        "MXN|Mexican Peso|پزو مکزیک|2",
    ];
}

internal static class UnitData
{
    internal static readonly string[] Rows =
    [
        "KG|Kilogram|کیلوگرم",
        "TON|Metric ton|تن",
        "G|Gram|گرم",
        "M|Meter|متر",
        "M2|Square meter|متر مربع",
        "M3|Cubic meter|متر مکعب",
        "L|Liter|لیتر",
        "PCS|Piece|عدد",
        "SET|Set|دست",
        "CARTON|Carton|کارتن",
        "PALLET|Pallet|پالت",
        "CNT20|20-foot container|کانتینر ۲۰ فوت",
        "CNT40|40-foot container|کانتینر ۴۰ فوت",
    ];
}

internal static class IncotermData
{
    internal static readonly string[] Rows =
    [
        "EXW|Ex Works|تحویل در محل فروشنده|0",
        "FCA|Free Carrier|تحویل به حمل‌کننده|0",
        "CPT|Carriage Paid To|حمل پرداخت‌شده تا|0",
        "CIP|Carriage and Insurance Paid To|حمل و بیمه پرداخت‌شده تا|0",
        "DAP|Delivered at Place|تحویل در مقصد|0",
        "DPU|Delivered at Place Unloaded|تحویل در مقصد با تخلیه|0",
        "DDP|Delivered Duty Paid|تحویل با پرداخت حقوق گمرکی|0",
        "FAS|Free Alongside Ship|تحویل کنار کشتی|1",
        "FOB|Free on Board|تحویل روی کشتی|1",
        "CFR|Cost and Freight|هزینه و کرایه حمل|1",
        "CIF|Cost, Insurance and Freight|هزینه، بیمه و کرایه حمل|1",
    ];
}
