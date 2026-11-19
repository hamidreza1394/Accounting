# مقایسهٔ کتابخانه‌های خواندن Excel برای Excel Import مرحله‌ای

> **وضعیت سند:** `Proposed` — مقایسه و پیشنهاد؛ **انتخاب نهایی کتابخانه = `NeedsApproval` (EXC-17)**.
> **تاریخ بررسی:** 2026-11-19 | **TFM مقصد مخزن:** `net10.0` | **Issue:** #715 | **ADR:** D-106
> **سند اصلی:** [`EXCEL_IMPORT_DESIGN.md`](EXCEL_IMPORT_DESIGN.md)
> **هیچ Package به مخزن اضافه نشده است.** طبق AGENTS.md بخش ۲، افزودن وابستگی بدون توجیه، تأیید مالک و ADR ممنوع است و نسخه‌ها فقط در `Directory.Packages.props` مدیریت می‌شوند.

## برچسب‌های شواهد

| برچسب | معنی |
|---|---|
| راستی‌آزمایی‌شده توسط Lead | Lead مستقیماً از NuGet API یا اجرای Spike دید؛ این Agent دوباره اجرا نکرده و داده را از گزارش Lead نقل کرده است |
| منبع ثانویه | ادعای مقاله/ویکی/Issue؛ مستقل راستی‌آزمایی نشده |
| بررسی‌نشده | بررسی نشده یا نتیجه‌اش قطعی نیست |

## ۱. جدول مقایسه

همهٔ ستون‌های نسخه/انتشار/مجوز/TFM/وابستگی: **راستی‌آزمایی‌شده توسط Lead** (NuGet API، 2026-11-19) مگر خلاف آن نوشته شود.

| کتابخانه | نسخهٔ پایدار | انتشار | مجوز | TFMها | وابستگی‌های مستقیم | یادداشت |
|---|---|---|---|---|---|---|
| ClosedXML | 0.105.1 | 2026-07-25 | MIT (nuspec) | netstandard2.0/2.1 | ClosedXML.Parser، DocumentFormat.OpenXml [3.1.1,4.0.0)، ExcelNumberFormat، RBush.Signed، SixLabors.Fonts، ... | مبتنی بر DOM؛ فقط xlsx/xlsm (منبع ثانویه) |
| ExcelDataReader | 3.9.0 | 2026-06-16 | MIT | net462، netstandard2.0/2.1، net8.0 | System.ValueTuple (فقط NetFx) | .xls، .xlsx و CSV؛ نیاز به ثبت `CodePagesEncodingProvider` در .NET Core/10 و Stream قابل‌Seek |
| DocumentFormat.OpenXml (Open XML SDK، Microsoft) | 3.5.1 | 2026-03-18 | MIT | net10.0، net8.0، netstandard2.0، ... | DocumentFormat.OpenXml.Framework | سطح پایین؛ `OpenXmlReader` جریانی |
| MiniExcel | 1.47.1 | 2026-11-14 | Apache-2.0 | net8/9/10/11، netstandard2.0 | Microsoft.Bcl.AsyncInterfaces، System.IO.Compression | نسخهٔ 2.0 هنوز preview؛ عمیق بررسی نشد |
| Sylvan.Data.Excel | 0.5.8 (pre-1.0) | 2026-08-10 | فایل `license.txt`؛ **متن مجوز در این بررسی راستی‌آزمایی نشد** | netstandard2.0/2.1، net6/8 | ندارد | pre-1.0؛ عمیق بررسی نشد |
| NPOI | 2.8.1 | 2026-09-22 | nuspec: `OSMFEULA.txt`؛ منبع ثانویه: از 2.8.0 در باینری‌ها EULA با هزینهٔ نگهداری وابسته به درآمد برای سازمان‌های تجاری؛ **پیش از هر تصمیم متن `OSMFEULA.txt` باید خوانده شود** | net10.0، net8.0، netstandard2.0/2.1، net472 | زیاد: BouncyCastle، SharpZipLib 1.4.2، SkiaSharp (+Native)، MathNet | `.xls` (HSSF)؛ محدودیت ۶۵٬۵۳۶ ردیف HSSF (منبع ثانویه) |
| EPPlus | 8.7.2 | — (راستی‌آزمایی نشد) | از v5 دوگانه: PolyForm Noncommercial + مجوز تجاری پولی؛ ادعای «هر استفادهٔ داخل شرکت تجاری مجوز تجاری per developer می‌خواهد»: **منبع ثانویه/راستی‌آزمایی‌شده توسط Lead از صفحهٔ ویکی GitHub (License-change)**؛ از nuspec نیست و متن مجوز خوانده نشد | net8/9/10 و ... | Microsoft.Extensions.Configuration.Json و ... | مبتنی بر DOM (منبع ثانویه) |

## ۲. آسیب‌پذیری‌ها

**گزارش Lead:** فید رسمی آسیب‌پذیری NuGet (`v3/vulnerabilities`، 2026-11-19) فقط Advisoryهای بازبینی‌شدهٔ GitHub را برای **هفت بستهٔ مستقیم (ClosedXML، ExcelDataReader، DocumentFormat.OpenXml، MiniExcel، Sylvan.Data.Excel، NPOI، EPPlus) و SharpZipLib** پوشش داده است؛ برای آن هفت بسته هیچ ردیفی نبود و فقط برای SharpZipLib چهار هشدار قدیمی (محدوده‌های پایین‌تر از 1.3.3) بود (NPOI 2.8.1 نسخهٔ 1.4.2 را می‌کشد). **وابستگی‌های گذرای دیگر (SixLabors.Fonts، BouncyCastle، SkiaSharp Native، DocumentFormat.OpenXml.Framework و ...) در این بررسی بررسی نشده‌اند.** «نبود هشدار» به‌معنای امن بودن نیست.

## ۳. نتایج Spike

**گزارش Lead؛ artifact قابل بازتولید (پیوست الف).** محدودیت: پروژهٔ موقت بیرون از مخزن؛ فایل ساختگی ۲۰۰٬۰۰۰ ردیف × ۸ ستون (۸٫۹ MB)؛ **۱ اجرا** روی ماشین ۲ هسته‌ای؛ عدد حافظه Peak Working Set **کل فرایند** است (شامل پایهٔ runtime حدود 47-57MB در فایل کوچک) و فایل در فرایند جدا تولید شده؛ .NET 10.0.403؛ اعداد تقریبی و تک‌اجرایی‌اند، **نه Benchmark رسمی**. این Agent Spike را دوباره اجرا نکرد.

### ۳.۱ حافظه و زمان (Peak Working Set)

| کتابخانه | حافظه | زمان |
|---|---|---|
| ExcelDataReader | 294MB (کل فرایند) | 2.2s |
| Open XML SDK (`OpenXmlReader` جریانی روی Sheet) | 302MB (کل فرایند) | 2.5s |
| ClosedXML (DOM) | 590MB | 7.6s |

### ۳.۲ رفتار سلول‌ها (فایل کوچک ساختگی)

| مورد | ClosedXML | ExcelDataReader | Open XML SDK |
|---|---|---|---|
| سلول متنی «00123» | حفظ شد | حفظ شد | حفظ شد |
| عددی با فرمت `00000` و مقدار ذخیره‌شدهٔ 123 | Number=123 و رشتهٔ فرمت‌شده «00123» | Double=123 و رشتهٔ فرمت «00000» (متن نمایشی را خودمان باید بسازیم) | مقدار خام 123 و StyleIndex (جست‌وجوی فرمت از `styles.xml` بر عهدهٔ ما) |
| تاریخ | DateTime | DateTime | عدد سریال خام |
| فرمول بدون مقدار cached | Blank با `HasFormula=true` | مقدار null؛ تفاوت «فرمول بدون مقدار» با «خالی» نشان داده نمی‌شود (**شکاف برای EXC-6**) | عنصر فرمول دیده می‌شود |

عدد ۱۶رقمی: مقدار ذخیره‌شده `double` است (ClosedXML متن نمایشی را علمی `8.90123456789012E+15` می‌دهد). عدد ۱۹رقمی در فایل به‌صورت `double` دقت را از دست داده است؛ یعنی دقت پیش از رسیدن به هر کتابخانه‌ای از بین رفته و فقط با سیاست خطا (EXC-5) قابل‌مدیریت است.

### ۳.۳ نکات تکمیلی

- ExcelDataReader در .NET Core/10 بدون `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` هنگام ساخت Reader خطا داد و Stream قابل‌Seek می‌خواهد. **منبع:** Issue رسمی پروژه #17 (منبع ثانویه برای ادعا) و اجرای Spike (راستی‌آزمایی‌شده توسط Lead).
- ExcelDataReader از `.xls`، `.xlsx` و CSV پشتیبانی می‌کند؛ ClosedXML فقط xlsx/xlsm — **منبع ثانویه** (مقالهٔ مقایسهٔ pistack، 2026-09).
- منابع ثانویه (ادعا؛ بدون عدد قابل‌اتکا): مقالهٔ pistack می‌گوید «فقط ClosedXML MIT ساده مانده»؛ ClosedXML/EPPlus مبتنی بر DOM‌اند؛ NPOI دارای SXSSF جریانی برای **نوشتن** است.

## ۴. نتیجه‌گیری پیشنهادی (`Proposed`؛ نهایی‌کردن = `NeedsApproval` مالک)

برای Parser ورودی نسخهٔ اول (`.xlsx`):

| رتبه | کتابخانه | دلیل | هزینه/ریسک |
|---|---|---|---|
| گزینهٔ اول | Open XML SDK | MIT، مایکروسافت، TFM `net10.0`، خواندن رو‌به‌جلو با `OpenXmlReader` (حافظهٔ Spike نزدیک ExcelDataReader؛ «جریانی = حافظهٔ کم» تأیید نشده چون SharedStringTable/styles در حافظه بار می‌شوند)، دسترسی کامل به فرمول/نوع/استایل (برای EXC-5/EXC-6 لازم) | کد بیشتر برای SharedStrings، InlineString، Styles و تاریخ؛ **نیازمند Spike/تست قرارداد پیش از قطعی‌شدن** |
| جایگزین | ExcelDataReader | MIT، ساده‌تر، `.xls` هم می‌خواند، حافظه/زمان مناسب | شکاف تشخیص فرمول بدون مقدار (EXC-6)؛ نیاز به ثبت Encoding Provider و Stream قابل‌Seek؛ متن نمایشی فرمت عدد را باید خودمان بسازیم |
| برای خواندن فایل بزرگ مناسب نیست | ClosedXML | DOM، حدود ۲× حافظه و ۳× زمان در Spike | برای **تولید گزارش خطای Excel در آینده** کاندید است |
| رد | EPPlus | مجوز غیرتجاری/پولی با محصول تجاری ناسازگار مگر خرید (و ADR/تأیید مالک) | — |
| فعلاً کنار | NPOI | EULA با هزینهٔ درآمدمحور باید بررسی شود؛ وابستگی‌های سنگین و Native | ریسک مجوز و سطح حمله |
| بررسی‌نشده / Deferred | MiniExcel | 2.0 هنوز preview | عمیق بررسی نشد |
| بررسی‌نشده / Deferred | Sylvan.Data.Excel | pre-1.0؛ متن مجوز تأییدنشده | عمیق بررسی نشد |

**هیچ Package به مخزن اضافه نمی‌شود؛ این مرحله فقط مقایسه و پیشنهاد است.**

## ۵. شرط‌های پیش از انتخاب نهایی

1. تأیید صریح مالک و ثبت ADR (AGENTS.md بخش ۲؛ D-106 بند EXC-17).
2. Spike/تست قرارداد Open XML SDK روی فایل‌های **ساختگی** برای سه چالش: صفر ابتدایی با فرمت عددی (EXC-5)، فرمول بدون cached (EXC-6)، تاریخ سریال.
3. خواندن متن مجوز هر کتابخانهٔ انتخابی (و در صورت NPOI، `OSMFEULA.txt`) و ثبت نسخه؛ افزودن فقط در `Directory.Packages.props`.
4. اسکن NuGet CI برای آسیب‌پذیری وابستگی‌های گذرا.
5. بازبینی `security-reviewer` برای سیاست XML/zip (EXC-13).

## ۶. آنچه راستی‌آزمایی نشد

- تاریخ انتشار EPPlus 8.7.2 در داده‌ها نیامده است.
- متن مجوز Sylvan.Data.Excel و `OSMFEULA.txt` خوانده نشده است.
- MiniExcel و Sylvan.Data.Excel در Spike نبودند.
- Spike دوباره توسط این Agent اجرا نشد؛ اعداد از گزارش Lead نقل شده‌اند.
- ادعاهای «منبع ثانویه» مستقل بررسی نشده‌اند.
- بار حافظهٔ SharedStringTable/styles در Open XML SDK و ExcelDataReader اندازه‌گیری نشد؛ **پیش از قطعی‌شدن EXC-17 Spike باید دوباره (و روی چند اجرا/فایل) اجرا شود.**
- وابستگی‌های گذرای بیرون از فید (بخش ۲) بررسی نشدند.

## پیوست الف — کد Spike برای بازتولید (فقط برای مستندسازی)

کد زیر **ساختگی** است، هیچ دادهٔ واقعی ندارد و **به مخزن افزوده نشده** (نه فایل کد، نه `csproj`). از گزارش Lead نقل شده (مسیر موقت بیرون از مخزن: `scratchpad/xlspike/`) و این Agent آن را اجرا نکرد. نسخهٔ بسته‌ها در `spike.csproj` همان نسخه‌های جدول بخش ۱ است. **این کد Spike هیچ‌یک از کنترل‌های EXC-13 را ندارد (`CreateReader` با تشخیص خودکار نوع، `SpreadsheetDocument.Open` بدون `MaxCharactersInPart`) و الگوی Parser نیست؛ برای Parser واقعی از `CreateOpenXmlReader` و `OpenSettings.MaxCharactersInPart` استفاده شود.** ضدالگو برای مخزن: این Spike نسخه را در خود `csproj` تکرار می‌کند، که برای کد مخزن مجاز نیست (AGENTS.md بخش ۲).

### Program.cs

```csharp
using System.Diagnostics;
using ExcelDataReader;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Spreadsheet;
System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
var lib=args[0]; var path=args[1]; var mode=args.Length>2?args[2]:"dump";
var sw=Stopwatch.StartNew(); long rows=0;
void Show(string s)=>Console.WriteLine(s);
if(lib=="edr"){
  using var st=File.OpenRead(path); using var r=ExcelReaderFactory.CreateReader(st);
  do{ while(r.Read()){ rows++;
     if(mode=="dump"&&rows<=3){ for(int i=0;i<r.FieldCount;i++){ var v=r.GetValue(i); Show($"r{rows} c{i} type={(v?.GetType().Name??"null")} value=[{v}] fmt=[{r.GetNumberFormatString(i)}]"); } } } }while(r.NextResult());
}
else if(lib=="cx"){
  using var wb=new XLWorkbook(path); var ws=wb.Worksheets.First();
  foreach(var row in ws.RowsUsed()){ rows++;
     if(mode=="dump"&&rows<=3){ foreach(var c in row.CellsUsed()) Show($"r{rows} {c.Address.ColumnLetter} type={c.DataType} value=[{c.Value}] str=[{c.GetFormattedString()}] fmt=[{c.Style.NumberFormat.Format}] hasFormula={c.HasFormula}"); } }
}
else if(lib=="ox"){
  using var doc=SpreadsheetDocument.Open(path,false); var wbp=doc.WorkbookPart!; var sst=wbp.SharedStringTablePart?.SharedStringTable;
  var wsp=wbp.WorksheetParts.First();
  using var rd=OpenXmlReader.Create(wsp);
  while(rd.Read()){ if(rd.ElementType==typeof(Row)&&rd.IsStartElement){ rows++; }
    if(mode=="dump"&&rows<=3&&rd.ElementType==typeof(Cell)&&rd.IsStartElement){ var c=(Cell)rd.LoadCurrentElement()!; var raw=c.CellValue?.Text; var t=c.DataType?.Value.ToString(); Show($"r{rows} {c.CellReference} t={t} raw=[{raw}] formula=[{c.CellFormula?.Text}] style={c.StyleIndex?.Value}"); } }
}
sw.Stop(); var p=Process.GetCurrentProcess();
Show($"{lib} rows={rows} time={sw.ElapsedMilliseconds}ms peakWS={p.PeakWorkingSet64/1024/1024}MB");
```

### spike.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>
<ItemGroup><PackageReference Include="ExcelDataReader" Version="3.9.0"/><PackageReference Include="ClosedXML" Version="0.105.1"/><PackageReference Include="DocumentFormat.OpenXml" Version="3.5.1"/></ItemGroup></Project>
```

### make_data.py

```python
# Synthetic workbooks for the Excel library spike (no real customer data).
import datetime
from openpyxl import Workbook
wb = Workbook(); ws = wb.active; ws.title = 'Sheet1'
ws.append(['TextCode','NumFmtCode','GeneralNum','BigId','Formula','Date','Shamsi','Empty'])
ws.append(['00123', 123, 123, 8901234567890123, '=1/0', datetime.date(2026,11,19), '1405/08/28', None])
ws['B2'].number_format = '00000'
ws.append(['0012345678901', 123, '1,234.50', 1234567890123456789, '=SUM(C2:C3)', datetime.datetime(2026,11,19,10,30), '۱۴۰۵/۰۸/۲۸', ''])
wb.save('data/small.xlsx')
wb = Workbook(write_only=True); ws = wb.create_sheet('Big')
ws.append(['Code','Name','Qty','Price','Country','Hs','Gtin','Note'])
for i in range(200000):
    ws.append([f'{i:08d}', f'Product name {i} with some text', i % 97, i * 1.25, 'IR', '0' + str(10000000 + i % 999999), f'0{i:012d}', 'note ' * 3])
wb.save('data/big.xlsx')
```
