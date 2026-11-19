# مقایسهٔ کتابخانه‌های خواندن Excel برای Excel Import مرحله‌ای

> **وضعیت سند:** `Proposed` — مقایسه، شواهد و پیشنهاد؛ **انتخاب نهایی کتابخانه = `NeedsApproval` (EXC-17)** و مشروط به Spike و تأیید نهایی مالک.
> **تاریخ بررسی:** 2026-11-19 (بازنگری پس از Spike 2) | **TFM مقصد مخزن:** `net10.0` | **Issue:** #715 | **ADR:** D-106
> **سند اصلی:** [`EXCEL_IMPORT_DESIGN.md`](EXCEL_IMPORT_DESIGN.md)
> **هیچ Package به مخزن اضافه نشده است؛ این PR فقط مستندات است.** طبق AGENTS.md بخش ۲، افزودن وابستگی بدون توجیه، تأیید مالک و ADR ممنوع است و نسخه‌ها فقط در `Directory.Packages.props` مدیریت می‌شوند.
> **وضعیت انتخاب:** ClosedXML 0.105.1 «گزینهٔ اول فعلی مالک» (مأموریت 2026-11-19) است؛ پیشنهاد قبلی Lead (Open XML SDK اول) بر پایهٔ Spike 1 بود و با تصمیم مالک و Spike 2 جایگزین شد؛ هیچ‌کدام `Accepted` نیست.
>
> **اصلاح ادعای قبلی Lead (2026-11-19؛ مهم):** ادعای «ClosedXML هنگام خواندن `Value` فرمول را اجرا نمی‌کند و `Value` هم Blank می‌دهد» فقط برای فرمولی درست بود که عنصر `<v></v>` خالی دارد. روی فایل‌هایی که عنصر `<v>` ندارند (`formulas_nov.xlsx`، `formulas_heavy_nov.xlsx`) خواندن `Value` فرمول را **اجرا می‌کند**؛ جزئیات و قواعد: بخش ۲ (محور ۸) و ۳.۲. جمله‌های قبلی همین سند اصلاح شدند.

## برچسب‌های شواهد

| برچسب | معنی |
|---|---|
| راستی‌آزمایی‌شده توسط Lead | Lead مستقیماً از NuGet API، nuspec، LICENSE داخل بسته یا clone مخزن دید؛ این Agent دوباره اجرا نکرده و از `SPIKE2-SUMMARY.md` نقل کرده است |
| اجرای Spike 2 | نتیجهٔ اجرای Spike 2 توسط Lead (2026-11-19) روی فایل‌های ساختگی؛ این Agent دوباره اجرا نکرد |
| اجرای Spike 1 | اجرای قبلی (200k ردیف)؛ شرایط متفاوت، تک‌اجرایی |
| مشاهدهٔ مستندساز از کد | از خواندن کد Spike (پیوست ب) به‌دست آمده؛ اجرا یا آزمایش نشده |
| آزمون مستقل بازبین امنیتی | آزمونی که بازبین امنیتی مستقل از Lead اجرا و نتیجه را گزارش کرد؛ **خروجی خام آن در پیوست پ نیست** |
| منبع ثانویه | ادعای مقاله/ویکی/Issue؛ مستقل راستی‌آزمایی نشده |
| بررسی‌نشده / NotRun | بررسی یا اجرا نشده؛ نتیجه‌اش معلوم نیست |

### ردپای شواهد (فایل‌های خام Spike 2؛ همه در پیوست پ)

| فایل خام | محتوا | جدول‌های وابسته |
|---|---|---|
| `results.jsonl` | اجرای ماتریس با **نسخهٔ قبلی Guard** (مثلاً `bomb_ratio ⇒ ratio_exceeded`؛ `styles_many`/`sheets_many ⇒ ok`) | جدول ۳.۱؛ ردیف‌های ۳.۲ به‌جز موارد زیر؛ نوشتن گزارش خطا |
| `results_extra.jsonl` | **Guard Spike v2** (اجراشده؛ نه «Guard پیشنهادی» NotRun) روی ۱۷ فایل + ClosedXML خام روی `cells_3m`/`styles_5k`/`spoof_header` | جدول Guard (۳.۳)؛ ردیف‌های `cells_3m`، `styles_5k`، `spoof_header` |
| `results_formula_eval.jsonl` | آزمون فرمول با/بدون عنصر `<v>` | ردیف‌های `formulas_heavy`، `formulas_nov`، `formulas_heavy_nov` و محور ۸ |
| `results_dump.jsonl` | خروجی `dump` (نوع، فرمول، cached، فرمت عددی) روی `ids.xlsx` و `formulas.xlsx` (اجرای بعدی برای ثبت ردپای خام) | «رفتار سلول» (محورهای ۱۰ و ۱۱)، `ids.xlsx`/`formulas.xlsx` |
| `results_api_eval.txt` | خروجی `apiprobe` (روی `formulas_nov.xlsx`، سلول‌های B1 و B4؛ ارزیابی هر API با تغییر `CachedValue`) | محور ۸ (Allow-list)؛ کد در پیوست ب |
| `results_contaminated.jsonl` | اجرای مخدوش (هم‌زمانی دو `run.py`) | **استفاده نشد و در پیوست نیامده** |

**اعداد بدون ردپای خام:** (الف) در `SPIKE2-SUMMARY.md` برای `cells_3m` عدد 6947ms و برای `styles_5k` بازهٔ 2072–2178ms (۳ تکرار) آمده بود که در هیچ‌یک از فایل‌های خام نیست؛ جایگزین شد با 6774ms (`results_extra.jsonl`) و 2562ms (یک اجرا؛ بازبین امنیتی مستقل 2035ms). (ب) خروجی `dump` در `results_dump.jsonl` هست (محورهای ۱۰ و ۱۱)؛ ولی نتایج «آزمون مستقل بازبین؛ خروجی خام در پیوست پ نیست» (تغییر نام part: 2249ms/184MB؛ DTD ⇒ `XmlException`؛ `GetFormattedString` روی `A50` ⇒ cached برابر 2؛ `styles_5k` ⇒ 2035ms؛ `spoof_header` ⇒ 337ms؛ فهرست API گسترده‌تر قبل از probe) در پنج فایل خام (چهار JSONL و یک TXT) نیست و با همین برچسب آمده‌اند. probe API اجراشدهٔ جدید (`results_api_eval.txt`) خام در پیوست پ است.

## ۱. جدول مقایسه

ستون‌های نسخه/انتشار/مجوز/TFM/وابستگی: **راستی‌آزمایی‌شده توسط Lead** (NuGet API، 2026-11-19) مگر خلاف آن نوشته شود. ClosedXML ردیف اول است (گزینهٔ اول فعلی مالک).

| کتابخانه | نسخهٔ پایدار | انتشار | مجوز | TFMها | وابستگی‌های مستقیم | یادداشت |
|---|---|---|---|---|---|---|
| ClosedXML | 0.105.1 | 2026-07-25 | MIT (nuspec 0.105.1 و LICENSE مخزن) | netstandard2.0/2.1 (Spike روی net10.0 ساخته و اجرا شد، بدون هشدار ساخت) | ClosedXML.Parser 2.0.0 (MIT)، DocumentFormat.OpenXml [3.1.1, 4.0.0) (در Spike 3.5.1، MIT)، ExcelNumberFormat 1.1.0 (MIT)، RBush.Signed 4.0.0 (MIT)، **SixLabors.Fonts [1.0.0, 3.0.0)**؛ برای netstandard2.0 علاوه بر آن‌ها Microsoft.Bcl.HashCode 1.1.1، System.Buffers 4.5.1 و System.Memory 4.5.5 (nuspec؛ گزارش بازبین امنیتی، این Agent خود nuspec را نخواند) | مبتنی بر DOM و بر پایهٔ Open XML SDK؛ فقط xlsx/xlsm (منبع ثانویه)؛ ریسک مجوز SixLabors.Fonts و لغو‌نداشتن در نسخهٔ منتشرشده: بخش ۲ |
| ExcelDataReader | 3.9.0 | 2026-06-16 | MIT | net462، netstandard2.0/2.1، net8.0 | System.ValueTuple (فقط NetFx) | .xls، .xlsx و CSV؛ نیاز به ثبت `CodePagesEncodingProvider` در .NET Core/10 و Stream قابل‌Seek |
| DocumentFormat.OpenXml (Open XML SDK، Microsoft) | 3.5.1 | 2026-03-18 | MIT | net10.0، net8.0، netstandard2.0، ... | DocumentFormat.OpenXml.Framework | سطح پایین؛ `OpenXmlReader` جریانی |
| MiniExcel | 1.47.1 | 2026-11-14 | Apache-2.0 | net8/9/10/11، netstandard2.0 | Microsoft.Bcl.AsyncInterfaces، System.IO.Compression | نسخهٔ 2.0 هنوز preview؛ عمیق بررسی نشد |
| Sylvan.Data.Excel | 0.5.8 (pre-1.0) | 2026-08-10 | فایل `license.txt`؛ **متن مجوز در این بررسی راستی‌آزمایی نشد** | netstandard2.0/2.1، net6/8 | ندارد | pre-1.0؛ عمیق بررسی نشد |
| NPOI | 2.8.1 | 2026-09-22 | nuspec: `OSMFEULA.txt`؛ منبع ثانویه: از 2.8.0 در باینری‌ها EULA با هزینهٔ نگهداری وابسته به درآمد برای سازمان‌های تجاری؛ **پیش از هر تصمیم متن `OSMFEULA.txt` باید خوانده شود** | net10.0، net8.0، netstandard2.0/2.1، net472 | زیاد: BouncyCastle، SharpZipLib 1.4.2، SkiaSharp (+Native)، MathNet | `.xls` (HSSF)؛ محدودیت ۶۵٬۵۳۶ ردیف HSSF (منبع ثانویه) |
| EPPlus | 8.7.2 | — (راستی‌آزمایی نشد) | از v5 دوگانه: PolyForm Noncommercial + مجوز تجاری پولی؛ ادعای «هر استفادهٔ داخل شرکت تجاری مجوز تجاری per developer می‌خواهد»: **منبع ثانویه/راستی‌آزمایی‌شده توسط Lead از صفحهٔ ویکی GitHub (License-change)**؛ از nuspec نیست و متن مجوز خوانده نشد | net8/9/10 و ... | Microsoft.Extensions.Configuration.Json و ... | مبتنی بر DOM (منبع ثانویه) |

**آسیب‌پذیری سایر بسته‌ها (گزارش Lead، فید NuGet `v3/vulnerabilities`، 2026-11-19):** فید فقط Advisoryهای بازبینی‌شدهٔ GitHub را برای هفت بستهٔ مستقیم (ClosedXML، ExcelDataReader، DocumentFormat.OpenXml، MiniExcel، Sylvan.Data.Excel، NPOI، EPPlus) و SharpZipLib پوشش داد؛ برای آن هفت بسته ردیفی نبود و فقط برای SharpZipLib چهار هشدار قدیمی (محدوده‌های پایین‌تر از 1.3.3) بود (NPOI 2.8.1 نسخهٔ 1.4.2 را می‌کشد). **وابستگی‌های گذرای دیگر (BouncyCastle، SkiaSharp Native، DocumentFormat.OpenXml.Framework و ...) بررسی نشدند.** «نبود هشدار» به‌معنای امن بودن نیست. برای ClosedXML: محور ۶ در بخش ۲.

## ۲. ارزیابی ClosedXML 0.105.1 (۱۲ محور)

هر محور: نتیجه + برچسب منبع. «NotRun» یعنی بررسی نشد و نتیجه‌اش معلوم نیست.

| # | محور | نتیجه | برچسب منبع |
|---|---|---|---|
| ۱ | نسخه و تاریخ انتشار | 0.105.1 در 2026-07-25 (NuGet API)؛ 0.105.0 در 2025-05-14؛ 0.104.2 در 2024-11-15؛ فاصلهٔ حدود 14 ماه بین 0.105.0 و 0.105.1 | راستی‌آزمایی‌شده توسط Lead |
| ۲ | نگهداری مخزن | clone سطحی (depth=50): آخرین Commit در 2026-10-22؛ از 0.105.1 (2026-07-25) تا 2026-10-22 در همان 50 Commit: 31 Commit که 29 تای آن از یک نویسنده (`jahav`)؛ یعنی **تمرکز نگهداری روی یک نفر (ریسک bus-factor)**. Commitهای اخیر شامل اصلاح محدودیت حافظهٔ موتور محاسبه (`fix(calc)`) و حد 32767 کاراکتر متن؛ تغییرات پس از 0.105.1 هنوز منتشر نشده است | راستی‌آزمایی‌شده توسط Lead |
| ۳ | مجوز | ClosedXML: `MIT` (nuspec 0.105.1 و فایل LICENSE مخزن). وابستگی‌های مستقیم دیگر MIT. **SixLabors.Fonts:** 1.0.0 = Apache-2.0 (nuspec)؛ نسخه‌های 2.x (2.0.0..2.2.0) = «Six Labors Split License v1.0» (متن LICENSE در nupkg 2.1.3 خوانده شد): Apache-2.0 برای نرم‌افزار متن‌باز، یا «وابستگی گذرا» (Transitive Package Dependency)، یا شرکت انتفاعی با درآمد کمتر از 1M USD؛ **در سایر موارد مجوز تجاری پولی**. معیار دیگر Split License: نهادهای غیرانتفاعی هم Apache می‌گیرند (گزارش Lead از متن LICENSE). استفادهٔ گذرا از طریق ClosedXML مجاز است؛ **ارجاع مستقیم مخزن به SixLabors.Fonts ممکن است مجوز تجاری بخواهد.** مخزن `CentralPackageTransitivePinningEnabled=true` دارد (`Directory.Packages.props` خط 4): **قاعده: هیچ `PackageVersion`/Pin برای SixLabors.Fonts ≥ 2.0 اضافه نشود و ارجاع مستقیم ممنوع.** این تحلیل حقوقی نیست و تأیید مالک/مشاور لازم است | راستی‌آزمایی‌شده توسط Lead |
| ۴ | وابستگی‌ها و .NET 10 | فقط `netstandard2.0` و `netstandard2.1` (روی .NET 10 قابل استفاده؛ Spike روی net10.0 ساخته و اجرا شد، بدون هشدار ساخت). وابستگی‌ها: ClosedXML.Parser 2.0.0، DocumentFormat.OpenXml [3.1.1, 4.0.0) (در Spike → 3.5.1)، ExcelNumberFormat 1.1.0، RBush.Signed 4.0.0، SixLabors.Fonts [1.0.0, 3.0.0)؛ برای netstandard2.0 علاوه بر آن‌ها Microsoft.Bcl.HashCode 1.1.1، System.Buffers 4.5.1 و System.Memory 4.5.5 (گزارش بازبین امنیتی از nuspec؛ این Agent nuspec را نخواند). در Spike، restore نسخهٔ SixLabors.Fonts 1.0.0 را حل کرد | راستی‌آزمایی‌شده توسط Lead؛ اجرای Spike 2 |
| ۵ | Issueهای باز | **NotRun.** دسترسی به GitHub API/Issues ممکن نشد (`gh` برای مخزن بیرونی بدون `push` مجاز نبود؛ صفحهٔ GitHub خارج از allowlist WebFetch بود). فقط یک صفحهٔ ثانویهٔ ecosyste.ms (فهرست ناقص 39 Issue: 21 باز، 18 بسته؛ تاریخ‌ها و برچسب‌ها ناقص) خوانده شد | منبع ثانویه؛ بررسی کامل NotRun |
| ۶ | آسیب‌پذیری | فید NuGet (در بررسی قبلی) برای ClosedXML هیچ ردیفی نداشت. منابع ثانویهٔ امروز: secure.software («No known vulnerabilities detected» برای 0.105.1؛ یافته‌های غیرآسیب‌پذیری مانند امضای ضعیف و یک «removal incident» دربارهٔ 0.104.0 بدون توضیح) و OSS Index («No vulnerabilities detected»؛ جزئیات پشت ثبت‌نام). **GitHub Security Advisory مخزن قابل دسترسی نبود (NotRun).** CVE-2026-40902 مربوط به phpoffice/phpspreadsheet است، نه ClosedXML. «نبود هشدار» به‌معنای امن بودن نیست | منبع ثانویه؛ GitHub Advisory NotRun |
| ۷ | خواندن xlsx | کد خواندن در Spike: `new XLWorkbook(path, new LoadOptions{RecalculateAllFormulas=false})`، سپس `RowsUsed()` و `CellsUsed(XLCellsUsedOptions.All)` و فقط `DataType`، `HasFormula`، `CachedValue`. روی فایل واقع‌نما (20,001 ردیف × 14 ستون، 253,347 سلول، 2.2MB): 1841/1995/1818ms، Peak کل 304–307MB (افزوده 270–273MB)، تخصیص GC ≈441MB؛ با Guard پیش از بازکردن 1933/2005/2084ms (Guard ≈ 5–30ms). فایل خراب: `corrupt_truncated`/`corrupt_random`/`not_xlsx_text` ⇒ `FileFormatException`؛ ZIP بدون `workbook.xml` ⇒ **`NullReferenceException`** (خطای نامرتب). ClosedXML بر پایهٔ Open XML SDK (وابستگی مستقیم) است | اجرای Spike 2 |
| ۸ | فرمول، خالی و cached | با `RecalculateAllFormulas=false` فرمول هنگام بازکردن اجرا نمی‌شود. `HasFormula=true` با `CachedValue` خالی (Blank) = فرمول بدون مقدار ذخیره‌شده؛ خطای cached (`#DIV/0!`) ⇒ `DataType=Error`؛ فرمول با cached معتبر خوانده شد؛ تابع ناشناخته ⇒ بدون استثنا، Blank. در `formulas.xlsx`: 5 فرمول (3 بدون cached، 1 خطای cached، 1 با cached). **اصلاح ادعای قبلی Lead:** (۱) روی `formulas_heavy.xlsx` که عنصر `<v></v>` خالی دارد، خواندن `Value` Blank برگشت (413ms، Peak 80MB). (۲) روی `formulas_nov.xlsx` و `formulas_heavy_nov.xlsx` (عنصر `<v>` حذف‌شده) خواندن `c.Value` فرمول را **اجرا کرد**: `B1 ⇒ Number:10`، `B4 ⇒ Text:ab`، `B5 ⇒ Error:#NAME?` (596ms)؛ روی 199 فرمول سنگین فقط ۵ سلول اول خوانده شد و 4067ms و Peak 746MB (افزوده 712MB، تخصیص GC 2011MB) شد؛ `A1` با `ArgumentOutOfRangeException` شکست و `A3 ⇒ Number:333338333350003`. (۳) خواندن فقط `HasFormula` + `CachedValue` روی همان `formulas_heavy_nov.xlsx`: 452ms، Peak 81MB (افزوده 47MB)، هیچ محاسبه‌ای نشد. (۴) `GetFormattedString()` طبق کد منبع ClosedXML (گزارش Lead) از `Value` استفاده می‌کند و بازبین امنیتی نشان داد مقدار `CachedValue` را هم تغییر می‌دهد (آزمون مستقل بازبین؛ خروجی خام در پیوست پ نیست؛ اما در probe اجراشدهٔ `results_api_eval.txt` نیز `GetFormattedString` فرمول را اجرا کرد): سلول `A50` با `<f>1+1</f>` بدون `<v>`: cached از blank به 2 شد. **پیامد:** خواندن `Value` فرمول هم ریسک DoS است (اجرای فرمول سنگین، بدون لغو و با حافظهٔ زیاد) و هم EXC-6 را دور می‌زند. **قواعد (پیشنهادی؛ NeedsApproval):** (الف) **Allow-list، نه Deny-list:** برای سلولی که `HasFormula` دارد فقط `HasFormula`، `FormulaA1`، `CachedValue`، `DataType`، `Style.NumberFormat` و `Address` مجازند و هر API دیگر ممنوع است؛ فهرست ممنوعِ قبلی (`Value`، `GetFormattedString`، `GetString`، `GetText`، `TryGetValue`) ناکافی بود. **شاهد probe (`apiprobe`؛ `results_api_eval.txt`؛ کد در پیوست ب؛ `formulas_nov.xlsx`، سلول‌های B1 و B4؛ معیار: تغییر `CachedValue` از blank):** 11 API فرمول را اجرا کردند (`GetString`، `GetText`، `TryGetValue<string>`، `GetFormattedString`، `GetBoolean`، `GetDateTime`، `GetError`، `GetValue<string>`، `GetDouble`، `Value`، `ws.Search`)؛ 5 مورد cached را blank نگه داشتند (خواندن `HasFormula`+`FormulaA1`+`CachedValue`+`DataType`+`Style.NumberFormat.Format`، `IsEmpty()`، `CopyTo`، `AsRange().FirstCellUsed`، `Range.CellsUsed(pred)`). **محدودیت:** دو سلول از یک فایل ساختگی؛ نبود تغییر `CachedValue` اثبات نبود اجرا نیست؛ `Address` در probe نبود (NotRun؛ در تست قرارداد Parser پوشش داده شود)؛ فقط 16 API آزموده شد. (ب) متن نمایشی فقط برای سلول بدون فرمول ساخته شود؛ (پ) `Evaluate` هرگز و `Search` یا هر API دامنه‌ای روی سلول‌های فرمول‌دار ممنوع؛ (ت) تست قرارداد Parser با «فرمول بدون عنصر `<v>`» در PR Parser الزامی است | اجرای Spike 2 (`results_formula_eval.jsonl`، `results_api_eval.txt`)؛ مورد ۴: کد منبع + آزمون مستقل بازبین؛ خروجی خام در پیوست پ نیست |
| ۹ | Styleها و Workbook غیرعادی | 5,000 Style متمایز + 20k ردیف: 2562ms و افزوده 180MB (یک اجرا، `results_extra.jsonl`؛ بازبین امنیتی مستقل 2035ms). **30,000 Style (فایل 0.7MB): 32358/33386/36479ms** و افزوده 402–407MB (`results.jsonl`؛ رشد سریع‌تر از خطی). 120 Worksheet/24,000 ردیف: 1339–1391ms. `dimension=A1:XFD1048576` با سلول XFD1048576: ابعاد اعلام‌شده مبنا نشد؛ 2 سلول؛ 370ms. 3.3M سلول (3,276,800 سلول، فایل 8.7MB، بازشده 79MB): **6774ms** و افزوده 389MB (`results_extra.jsonl`). 6.55M سلول (فایل 17.4MB، بازشده 160MB): 15613ms و افزوده 698MB؛ ClosedXML خودش نایستاد. 300MB padding فشرده (فایل 0.3MB): **`OutOfMemoryException`** زیر سقف heap 1.5GB (1.6s). فایل ساختگی شامل Entry `xl/vbaProject.bin` (ZIP ساختگی با ۱۰۰ بایت صفر، نه ماکروی واقعی): **ClosedXML بی‌خطا باز کرد**؛ رد ماکرو فقط با Guard ما ممکن است. **تغییر نام part (آزمون مستقل بازبین امنیتی؛ خروجی خام در پیوست پ نیست):** `styles_5k` با part تغییرنام‌داده (و اصلاح rels و Content_Types) از Guard Spike `ok` گرفت و ClosedXML آن را در 2249ms/184MB پذیرفت؛ فایل 30k Style با همین روش از Guard رد نمی‌شود و ≈32–36s CPU غیرقابل‌لغو می‌گیرد (استنتاج از اعداد بالا، نه آزمون مستقیم). **Guard Spike راه‌حل نهایی نیست** (بخش ۳.۳). همهٔ این فایل‌ها ساختگی‌اند | اجرای Spike 2؛ تغییر نام part: آزمون مستقل بازبین امنیتی |
| ۱۰ | نوع سلول و فرمت عددی | `DataType` (`Text`/`Number`/`Error`)، `Style.NumberFormat.Format` و `NumberFormat.NumberFormatId` در دسترس‌اند. عدد با فرمت `00000` ⇒ `Number=123` و `Format="00000"`؛ عدد بدون فرمت ⇒ `Number` بدون اثر صفر ابتدایی. عدد 16 رقمی ⇒ `Number=1234567890123456` (double). برای تاریخ: در Spike 1 (فایل کوچک) `DateTime` برگشت؛ در Spike 2 مورد تاریخ آزموده نشد | اجرای Spike 2 (`results_dump.jsonl`)؛ تاریخ: اجرای Spike 1 |
| ۱۱ | شناسه‌ها و صفر ابتدایی | متن «00123»، «۰۰۱۲۳» (ارقام فارسی)، «٠٠١٢٣» (ارقام عربی) و سلول با `quotePrefix` ⇒ `DataType=Text` با مقدار دست‌نخورده. 123 با فرمت `00000` ⇒ `Number` + `Format="00000"`؛ 12345678905 با فرمت ۱۳ صفر ⇒ `Number`، `Format="0000000000000"`؛ 1012100 با فرمت ۸ صفر ⇒ `Number`، `Format="00000000"`. بازسازی «0012345678905» فقط با قاعدهٔ صریح طول ثابت (EXC-5) ممکن است. دقت Excel معمولاً 15 رقم است؛ سیاست «بیش از 15 رقم ⇒ رد» همچنان لازم است | اجرای Spike 2 (`results_dump.jsonl`) |
| ۱۲ | تولید فایل گزارش خطا | نوشتن 20,000 ردیف × 3 ستون: 1212/1325ms، 284KB، Peak 159MB (افزوده 125MB). مقدار رشتهٔ `=cmd\|' /C calc'!A0` که با `Value = "..."` نوشته شد، پس از خواندن دوباره `DataType=Text` و `HasFormula=false` بود (به‌عنوان متن ذخیره می‌شود). خنثی‌سازی (پیشوند) برای دفاع در عمق و خروجی‌های غیر xlsx همچنان در EXC-13 باقی بماند | اجرای Spike 2 |

**یادداشت XML و لینک خارجی (آزمون مستقل بازبین امنیتی؛ خروجی خام در پیوست پ نیست):** DTD داخلی بی‌خطر در `sheet1.xml` و `workbook.xml` ⇒ `XmlException: For security reasons DTD is prohibited` (با Guard و بدون آن)؛ یعنی ClosedXML 0.105.1 + OpenXml 3.5.1 در برابر DTD ایمن بودند؛ پیام استثنا باید به پیام عمومی نگاشت شود. Entity خارجی و سایر حالات XXE، و externalLink/DDE **آزموده نشد (NotRun)**؛ «نادیده گرفته می‌شود» را قطعی ننویسید؛ پیشنهاد: Guard بخش‌های externalLink را رد کند.

**نبود لغو (Cancellation):** `LoadOptions.CancellationToken` در **کد main مخزن** (2026-10) هست، اما **در نسخهٔ منتشرشدهٔ 0.105.1 وجود ندارد** (خطای کامپایل در Spike). پس توقف کنترل‌شدهٔ بارگذاری درون‌فرایندی ممکن نیست؛ تنها کنترل‌ها Guard پیش از بازکردن و سقف‌های Host (Request Timeout، محدودکنندهٔ هم‌زمانی) هستند. **چون ClosedXML لغو ندارد، Request Timeout پاسخ HTTP را قطع می‌کند ولی Parse ادامه می‌یابد؛ جایگاه محدودکنندهٔ هم‌زمانی باید تا پایان واقعی Parse نگه داشته شود؛ برای توقف قطعی، Parse در فرایند جدا با سقف حافظه و kill گزینهٔ NeedsApproval است.** رفتار لغو در نسخهٔ بعدی: NotRun. (اجرای Spike 2 و clone سطحی Lead)

## ۳. Spike 2 (2026-11-19؛ اجرای Lead؛ کد در پیوست ب)

**محیط:** .NET SDK 10.0.403، Linux، ۲ هسته، ۸GB RAM، Release. هر آزمایش در فرایند جدا؛ `DOTNET_GCHeapHardLimit=0x60000000` (1.5GB heap مدیریت‌شده) و `timeout -s KILL` (90 تا 120 ثانیه) برای فایل‌های مخرب. «پایه» ≈ 33MB (Working Set در شروع Main)؛ «افزوده» = PeakWorkingSet − پایه؛ «Peak» = کل فرایند. ۳ تکرار برای فایل‌های اصلی؛ ۱ تکرار برای بقیه. **نه Benchmark رسمی.** یک اجرای اول به‌علت هم‌زمانی دو `run.py` کنار گذاشته شد (نتایج مخدوش)؛ اجرای تمیز جایگزین شد و فایل مخدوش نگه داشته شد ولی استفاده نمی‌شود. همهٔ فایل‌ها ساختگی‌اند؛ هیچ داده واقعی مشتری نیست.

**نحوهٔ خواندن در harness:** ClosedXML: `new XLWorkbook(path, new LoadOptions{RecalculateAllFormulas=false})`، `RowsUsed()`، `CellsUsed(XLCellsUsedOptions.All)` و فقط `DataType`/`HasFormula`/`CachedValue`. ExcelDataReader: `GetValue` هر سلول. Open XML SDK: `OpenXmlReader` فقط شمارش عنصرهای Row/Cell (**بدون مادی‌سازی مقدار؛ پس برای Open XML SDK عدد کم‌برآورد است و مقایسهٔ عادلانهٔ کامل نیست**).

**ردپای هر جدول:** بخش «ردپای شواهد» بالا؛ نتایج خام کامل: پیوست پ. جدول ۳.۱ و بیشتر ردیف‌های ۳.۲ از `results.jsonl` (اجرای قبلی Guard)، جدول Guard (۳.۳) از `results_extra.jsonl` (Guard Spike v2) و ردیف‌های فرمول از `results_formula_eval.jsonl` (و probe API از `results_api_eval.txt`) هستند.

### ۳.۱ جدول ۱ — فایل واقع‌نما: 20,001 ردیف × 14 ستون، 253,347 سلول غیرخالی، 2.2MB (بازشده 18,865,100 بایت ≈ 18MiB)

| کتابخانه | زمان (ms) سه تکرار | Peak کل (MB) | افزوده (MB) | تخصیص GC (MB) |
|---|---|---|---|---|
| ClosedXML 0.105.1 | 1841 / 1995 / 1818 | 304–307 | 270–273 | ≈441 |
| ExcelDataReader 3.9.0 | 581 / 624 / 635 | 142 | 108 | 87 |
| Open XML SDK 3.5.1 (فقط شمارش، جریانی) | 733 / 735 / 767 | 217 | 183 | 155 |

ClosedXML با Guard پیش از بازکردن: 1933 / 2005 / 2084ms (Guard ≈ 5–30ms).

### ۳.۲ جدول ۲ — ClosedXML روی بقیهٔ فایل‌ها (بدون Guard، با سقف heap)

| فایل | ویژگی | نتیجه | زمان | Peak/افزوده MB |
|---|---|---|---|---|
| ids.xlsx | شناسه‌ها | ok | 371ms | 79/45 |
| formulas.xlsx | 5 فرمول | ok؛ 3 بدون cached، 1 خطای cached، 1 با cached | 406ms | 79/46 |
| formulas_heavy.xlsx | 199 فرمول سنگین، با عنصر `<v></v>` خالی | ok؛ هیچ محاسبه‌ای انجام نشد؛ `Value` Blank برمی‌گرداند (**فقط برای این حالت**؛ بدون `<v>` رفتار متفاوت است: دو ردیف بعد) | 366–383ms (خواندن Value: 371–374ms؛ 413ms در `results_formula_eval.jsonl`) | 81/47 (Value: 80/46) |
| formulas_nov.xlsx | همان 5 فرمول `formulas.xlsx` با عنصر `<v>` حذف‌شده (برای سلول‌های بدون cached) | `Value` فرمول را **اجرا کرد**: `B1 ⇒ Number:10`، `B2 ⇒ Error:#DIV/0!` (cached موجود)، `B3 ⇒ Number:6` (cached موجود)، `B4 ⇒ Text:ab`، `B5 ⇒ Error:#NAME?` | 596ms | 92/57 |
| formulas_heavy_nov.xlsx | 199 فرمول سنگین با عنصر `<v>` حذف‌شده | خواندن `Value` ۵ سلول اول: `A1 ⇒ ArgumentOutOfRangeException`، `A2 ⇒ Blank`، `A3..A5 ⇒ Number:333338333350003..005` (فرمول اجرا شد)؛ خواندن فقط `HasFormula`+`CachedValue` روی همین فایل: 199 فرمول بدون cached، هیچ محاسبه‌ای نشد | `Value`: **4067ms**؛ `HasFormula`+`CachedValue`: 452ms | `Value`: **746/712** (GC 2011MB)؛ `HasFormula`+`CachedValue`: 81/47 |
| styles_5k.xlsx | 5,000 Style متمایز + 20k ردیف | ok | 2562ms (یک اجرا، `results_extra.jsonl`؛ بازبین امنیتی مستقل 2035ms) | 214/180 |
| styles_many.xlsx | 30,000 Style متمایز (فایل 0.7MB) | ok | **32358 / 33386 / 36479ms** | 436–441/402–407 |
| sheets_many.xlsx | 120 Worksheet، 24,000 ردیف | ok | 1339–1391ms | 275–276/241–242 |
| dims_fake.xlsx | `dimension=A1:XFD1048576` + سلول XFD1048576 | ok؛ ابعاد اعلام‌شده مبنا نشد؛ 2 سلول | 370ms | 76/42 |
| cells_3m.xlsx | 3,276,800 سلول، فایل 8.7MB، بازشده 79MB | ok | **6774ms** | 423/389 |
| bomb_cells.xlsx | 6.55M سلول، فایل 17.4MB، بازشده 160MB | ok (ClosedXML خودش نایستاد) | **15613ms** | 732/698 |
| bomb_ratio.xlsx | 300MB padding فشرده (فایل 0.3MB) | **OutOfMemoryException** زیر سقف 1.5GB | 1570ms | 1299/1265 |
| corrupt_truncated.xlsx | 60% فایل | FileFormatException | 119ms | 53 |
| corrupt_random.xlsx | بایت تصادفی | FileFormatException | 104ms | 53 |
| corrupt_nowb.xlsx | ZIP بدون workbook.xml | **NullReferenceException** (خطای نامرتب) | 181ms | 60 |
| not_xlsx_text.xlsx | متن | FileFormatException | 89ms | 53 |
| macro_like.xlsx | شامل `xl/vbaProject.bin` (ساختگی) | **ClosedXML بی‌خطا باز می‌کند** (بدون کنترل ما رد نمی‌شود) | 374ms | 79/45 |
| spoof_header.xlsx | Entry واقعاً 314,573,013 بایت (300.0MB؛ رمزگشایی deflate توسط مستندساز) با اندازهٔ اعلامی 2,000 بایت در Central Directory و Local Header؛ فشرده 305,947 بایت | `XmlException` («Unexpected end of file while parsing Comment… position 2001»)؛ با Guard: Guard `ok` سپس همان استثنا | بدون Guard 490ms؛ با Guard 406ms؛ Guard تنها 26ms؛ بازبین امنیتی مستقل 337ms | 76/42 |

**منبع ردیف‌های بالا:** همه از `results.jsonl` مگر `cells_3m`، `styles_5k`، `spoof_header` (`results_extra.jsonl`) و `formulas_nov`/`formulas_heavy_nov` (`results_formula_eval.jsonl`). **ساخت فایل‌ها:** `formulas_nov`/`formulas_heavy_nov` و `spoof_header` در اسکریپت‌های `gen.py`/`gen2.py` نیستند؛ روش ساخت `*_nov` در پیوست ب (بازسازی توصیفی) آمده است و برای `spoof_header` اسکریپت تولید ذخیره نشده است.

### ۳.۳ Guard پیش از بازکردن (Guard Spike v2 اجراشده؛ `results_extra.jsonl`؛ مقادیر Proposed)

قواعد پیاده‌شده در harness: فایل ≤ 10MB؛ امضای ZIP (`PK`)؛ جمع بازشدهٔ Entryها ≤ 40MB؛ هر Entry ≤ 30MB؛ نسبت فشرده‌سازی Entryهای بزرگ‌تر از 1MB ≤ 100:1؛ Entryها ≤ 200؛ Worksheetها ≤ 50 (شمارش نام `xl/worksheets/sheet*.xml`)؛ `cellXfs` در `xl/styles.xml` ≤ 5,000 (با `XmlReader`، DTD ممنوع)؛ وجود `xl/workbook.xml`؛ رد `xl/vbaProject*`.

| فایل | نتیجهٔ Guard | زمان |
|---|---|---|
| realistic_20k, ids, formulas, formulas_heavy, dims_fake, spoof_header | ok | 19–33ms (realistic 22، ids 25، formulas 33، formulas_heavy 19، dims_fake 23، spoof_header 26) |
| bomb_cells | file_too_large | 5ms |
| bomb_ratio | entry_too_large | 6ms |
| cells_3m | entry_too_large | 6ms |
| styles_many | too_many_styles | 50ms |
| styles_5k | too_many_styles (مرز دقیقاً 5000 Style + پیش‌فرض‌ها؛ نشانهٔ مرز بودن سقف) | 34ms |
| sheets_many (120 Sheet) | too_many_sheets | 24ms |
| corrupt_truncated | bad_zip | 34ms |
| corrupt_random, not_xlsx_text | not_zip_signature | 5ms |
| corrupt_nowb | no_workbook | 6ms |
| macro_like | macro_part | 21ms |

**تفاوت با نسخهٔ قبلی Guard (`results.jsonl`):** در آن اجرا `bomb_ratio ⇒ ratio_exceeded` (6ms)، و `styles_many`/`sheets_many ⇒ ok` بود. در Guard Spike v2 `bomb_ratio` زودتر با `entry_too_large` رد می‌شود و **قاعدهٔ نسبت و قاعدهٔ تعداد Entry روی هیچ فایل Spike تنها فعال نشدند**؛ اثر مستقل آن دو در نسخهٔ نهایی اندازه‌گیری نشد. زمان‌های جدول بالا جایگزین زمان‌های قبلی این سند (نقل از `SPIKE2-SUMMARY.md`) شدند؛ ClosedXML + Guard روی فایل عادی: 1933 / 2005 / 2084ms (`results.jsonl`).

برای فایل‌هایی که Guard Spike v2 رد کرد (`bomb_cells`، `bomb_ratio`، `cells_3m`، `styles_many`، `styles_5k`، `sheets_many`) در harness ClosedXML فراخوانی نمی‌شود (اجرای Guard-only؛ اجرای ClosedXML+Guard فقط روی `bomb_cells`/`bomb_ratio`/`realistic_20k` با نسخهٔ قبلی Guard در `results.jsonl` و روی `spoof_header` در `results_extra.jsonl` هست).

**«Guard Spike v2» (اجراشده) راه‌حل نهایی نیست؛ «Guard پیشنهادی» هنوز NotRun است.**
1. **تکیه بر نام Entry (امنیت؛ High):** شمارش Style/Sheet/ماکرو/Workbook به نام ثابت partها تکیه دارد و با تغییر نام part (+ اصلاح rels و Content_Types) دور زده می‌شود. آزمون مستقل بازبین امنیتی (خروجی خام در پیوست پ نیست): `styles_5k` تغییرنام‌داده ⇒ Guard `ok` و ClosedXML 2249ms/184MB پذیرفت؛ فایل 30k Style با همین روش از Guard رد نمی‌شود و ≈32–36s CPU غیرقابل‌لغو می‌گیرد (استنتاج).
2. **«Guard پیشنهادی» (NotRun؛ در Spike پیاده/اجرا نشده؛ کار PR Parser؛ «نهایی» بودنش فقط پس از پیاده‌سازی و آزمون):** partها را از `_rels/.rels`، `xl/_rels/workbook.xml.rels` و `[Content_Types].xml` (`XmlReader` با DTD ممنوع) resolve کند؛ Sheetها از عناصر `<sheet>` شمرده شوند؛ `cellXfs` در part با Relationship نوع styles؛ ماکرو با Content Type/نوع Relationship؛ Entry ناشناخته/نام تکراری ⇒ رد؛ نام‌های OPC نرمال‌سازی شوند (حساسیت به حروف، percent-encoding، Targetهای نسبی/مطلق) و ورودی تکراری پس از نرمال‌سازی رد شود؛ Relationship با `TargetMode="External"` دنبال نشود؛ علاوه بر `cellXfs` سقف `fonts/fills/cellStyleXfs/dxfs` هم بررسی شود؛ رد part از نوع externalLink؛ تست رگرسیون «تغییر نام part» ([`EXCEL_IMPORT_DESIGN.md`](EXCEL_IMPORT_DESIGN.md) بخش ۱۶).
3. **اندازهٔ اعلامی Entry (آزموده شد؛ فقط حالت «کوچک‌تر از واقعی»):** Guard بر `ZipArchiveEntry.Length` (اندازهٔ اعلام‌شده) استوار است. `spoof_header.xlsx`: Entry واقعاً 314,573,013 بایت (300.0MB؛ رمزگشایی deflate توسط مستندساز) با اندازهٔ اعلامی 2,000 بایت در Central Directory و Local Header و فشردهٔ 305,947 بایت. Guard آن را پذیرفت؛ ClosedXML با `XmlException` شکست (بدون Guard 490ms؛ با Guard 406ms؛ Peak 76MB، افزوده 42MB؛ بازبین امنیتی مستقل 337ms). پیام خطا (position 2001) نشان می‌دهد خواندن در این runtime حدود اندازهٔ اعلامی قطع شد (استنتاج؛ وابسته به runtime). **اصلاح پیشنهاد Guard:** صرفِ `CompressedLength > Length` معیار رد نیست؛ محتوای از قبل فشرده یا غیرقابل‌فشرده‌سازی در فایل معتبر ممکن است چنین وضعیتی داشته باشد. برای مقابله با `spoof_header`، Guard باید هنگام خواندن هر Entry بایت‌های واقعاً بازشده را با Stream شمارنده اندازه بگیرد و سقف هر Entry/مجموع را بر بایت واقعی اعمال کند؛ نسبت فشرده‌سازی نیز بر اساس بایت واقعی بازشده نسبت به `CompressedLength` محاسبه شود. `Length` فقط راهنمای غیرقابل‌اعتماد است. حالت‌های دیگر دست‌کاری هدر و Entryهای تودرتو آزموده نشد. **شرط اعتبار سقف‌های Entry/جمع بازشده (پیشنهادی):** این سقف‌ها فقط وقتی کران واقعی‌اند که runtime خواندن Entry را در اندازهٔ اعلامی قطع کند؛ این قطع فقط در یک runtime و برای یک فایل مشاهده شد؛ اگر در نسخه‌ای دیگر برقرار نبود، باید شمارش بایت‌های واقعاً خوانده‌شده (Stream شمارنده) جایگزین اتکا به اندازهٔ اعلامی شود (NeedsApproval).

### ۳.۴ کالیبراسیون سقف‌ها (استنتاج، نه اندازه‌گیری مستقیم)

- فایل واقع‌نما 18,865,100 بایت (≈18MiB؛ مجموع اندازهٔ Entryها، محاسبهٔ مستندساز از `realistic_20k.xlsx`) بازشده ⇒ ≈2s و ≈270MB افزوده. سقف مؤثر که بدترین حالت را کران می‌زند «**جمع بازشده ≤ 40MB**» است (نه 30MB هر Entry). **برآورد برون‌یابی خطی، فقط برای محتوای واقع‌نما، در 40MB** (×40/18: 270–273MB و 1.82–1.995s): افزوده ≈600MB و ≈4.1–4.4s؛ با دو Import هم‌زمان ≈1.2GB؛ اندازه‌گیری نشده؛ نسبت حافظه به حجم بازشده به نوع محتوا بستگی دارد و محتوای پر از shared strings اندازه‌گیری نشد (NotRun). بدترین حالت ترکیبی (40MB + حداکثر Style + حداکثر Sheet) **NotRun**.
- هزینهٔ Style سریع‌تر از خطی رشد می‌کند: 5k ≈ 2.0–2.6s (2562ms یک اجرا؛ بازبین مستقل 2035ms)، 30k ≈ 33s (32358–36479ms) ⇒ سقف 5,000.
- سقف هم‌زمانی ≤ 2 **پیشنهادی است و پیش از تثبیت باید اندازه‌گیری شود** (مبنا: Peak اندازه‌گیری‌شدهٔ 304–307MB روی ≈18MiB بازشده و برآورد بالا).
- سقف تعداد سلول و ستون: اندازه‌گیری مستقیم نشد (NeedsApproval؛ بدون عدد پیشنهادی؛ [`EXCEL_IMPORT_DESIGN.md`](EXCEL_IMPORT_DESIGN.md) بخش ۱۲).

### ۳.۵ لغو و NotRun در Spike 2

- نبود لغو در ClosedXML 0.105.1: بخش ۲ (و پیامد Timeout/Parse ادامه‌یابنده).
- یک شکست گذرا و تکرارنشدنی در حلقهٔ Guard برای `not_xlsx_text.xlsx` (KeyError در اسکریپت جمع‌بندی، نه در Guard)؛ سه اجرای بعدی Guard همگی `not_zip_signature` دادند.
- ارقام یک‌ماشینهٔ ۲ هسته‌ای؛ نه Benchmark رسمی.


## ۴. Spike 1 قدیمی (200k ردیف) — اجرای قبلی

> **برچسب:** اجرای قبلی، شرایط متفاوت، تک‌اجرایی؛ با Spike 2 قابل‌مقایسهٔ مستقیم نیست (فایل ۲۰۰٬۰۰۰ ردیف × ۸ ستون و ۸٫۹MB در برابر 20,001 × 14 در Spike 2؛ ۱ اجرا؛ کد پیوست الف: ClosedXML بدون `LoadOptions` و بدون Guard؛ وجود یا نبود سقف heap در آن اجرا ثبت نشده). دلیل نگه‌داری: ثبت تاریخچهٔ تصمیم و رفتار سلول‌ها (تاریخ، فرمول).

**محدودیت:** پروژهٔ موقت بیرون از مخزن؛ فایل ساختگی ۲۰۰٬۰۰۰ ردیف × ۸ ستون (۸٫۹ MB)؛ **۱ اجرا** روی ماشین ۲ هسته‌ای؛ عدد حافظه Peak Working Set **کل فرایند** است (شامل پایهٔ runtime حدود 47-57MB در فایل کوچک) و فایل در فرایند جدا تولید شده؛ .NET 10.0.403؛ **نه Benchmark رسمی**. کد: پیوست الف.

| کتابخانه | حافظه | زمان |
|---|---|---|
| ExcelDataReader | 294MB (کل فرایند) | 2.2s |
| Open XML SDK (`OpenXmlReader` جریانی روی Sheet) | 302MB (کل فرایند) | 2.5s |
| ClosedXML (DOM) | 590MB | 7.6s |

| مورد (فایل کوچک ساختگی) | ClosedXML | ExcelDataReader | Open XML SDK |
|---|---|---|---|
| سلول متنی «00123» | حفظ شد | حفظ شد | پشتیبانی‌نشده با این artifact (Spike ۱ رشتهٔ مشترک/inline را حل نمی‌کند؛ `NotRun`) |
| عددی با فرمت `00000` و مقدار ذخیره‌شدهٔ 123 | Number=123 و رشتهٔ فرمت‌شده «00123» | Double=123 و رشتهٔ فرمت «00000» (متن نمایشی را خودمان باید بسازیم) | مقدار خام 123 و StyleIndex (جست‌وجوی فرمت از `styles.xml` بر عهدهٔ ما) |
| تاریخ | DateTime | DateTime | عدد سریال خام |
| فرمول بدون مقدار cached | Blank با `HasFormula=true` | مقدار null؛ تفاوت «فرمول بدون مقدار» با «خالی» نشان داده نمی‌شود (**شکاف برای EXC-6**) | عنصر فرمول دیده می‌شود |

> **یادداشت:** ستون Open XML برای ردیف «00123» اجرای تأییدشده ندارد؛ با این artifact رشتهٔ مشترک (shared string)/inline در Spike ۱ حل نشده و `NotRun` است. سایر ستون‌ها بدون تغییر.

عدد ۱۶رقمی: مقدار ذخیره‌شده `double` است (ClosedXML متن نمایشی را علمی `8.90123456789012E+15` می‌دهد). عدد ۱۹رقمی در فایل به‌صورت `double` دقت را از دست داده بود؛ یعنی دقت پیش از رسیدن به هر کتابخانه‌ای از بین رفته و فقط با سیاست خطا (EXC-5) قابل‌مدیریت است.

**نکات تکمیلی:**
- ExcelDataReader در .NET Core/10 بدون `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` هنگام ساخت Reader خطا داد و Stream قابل‌Seek می‌خواهد. **منبع:** Issue رسمی پروژه #17 (منبع ثانویه) و اجرای Spike 1 (راستی‌آزمایی‌شده توسط Lead).
- ExcelDataReader از `.xls`، `.xlsx` و CSV پشتیبانی می‌کند؛ ClosedXML فقط xlsx/xlsm — **منبع ثانویه** (مقالهٔ مقایسهٔ pistack، 2026-09).
- منابع ثانویه (ادعا؛ بدون عدد قابل‌اتکا): مقالهٔ pistack می‌گوید «فقط ClosedXML MIT ساده مانده»؛ ClosedXML/EPPlus مبتنی بر DOM‌اند؛ NPOI دارای SXSSF جریانی برای **نوشتن** است.

## ۵. مقایسهٔ نهایی: ClosedXML، Open XML SDK و ExcelDataReader (`Proposed`؛ نهایی‌کردن = `NeedsApproval` مالک)

معیارها: سادگی توسعه، قابلیت اطمینان، نگهداری بلندمدت و مصرف منابع **کنترل‌شده** (قابلیت مهار با Guard و سقف‌ها)، نه Benchmark خام. فقط شواهد این سند؛ ادعای بدون شاهد نیامده است.

| معیار | ClosedXML 0.105.1 (گزینهٔ اول فعلی مالک) | Open XML SDK 3.5.1 (جایگزین اول) | ExcelDataReader 3.9.0 (جایگزین دوم) |
|---|---|---|---|
| سادگی توسعه | ساده‌ترین: `DataType`، `HasFormula`، `CachedValue` و رشتهٔ فرمت عدد آماده (Spike 2) | **کد Parser بیشتر:** SharedStrings، InlineString، Styles و تاریخ؛ مقدار خام + `StyleIndex` و **رشتهٔ فرمت عدد را باید خودمان از `styles.xml` بسازیم**؛ تاریخ عدد سریال خام (Spike 1) | ساده؛ `GetValue` و رشتهٔ فرمت؛ نیاز به ثبت Encoding Provider و Stream قابل‌Seek (Spike 1)؛ متن نمایشی فرمت را خودمان باید بسازیم |
| قابلیت اطمینان | فرمول بدون cached از خالی جدا قابل‌تشخیص است (`HasFormula`)؛ ولی **خواندن `Value` فرمولِ بدون `<v>` آن را اجرا می‌کند** (DoS و دور زدن EXC-6؛ محور ۸) و باید ممنوع شود؛ بدون Guard: ماکرو بی‌خطا باز می‌شود، ZIP بدون workbook ⇒ `NullReferenceException`، `bomb_ratio` ⇒ OOM؛ Guard Spike v2 با تغییر نام part دور زده می‌شود؛ لغو ندارد (منتشرشده) | دسترسی کامل به عنصر فرمول؛ **روی فایل مخرب/Style/سلول اجرا نشد (NotRun)** | **فرمول بدون cached را از «خالی» جدا نمی‌کند** (شکاف EXC-6؛ Spike 1)؛ روی فایل مخرب NotRun |
| نگهداری بلندمدت | MIT؛ تمرکز نگهداری روی یک نفر (29 از 31 Commit)؛ فاصلهٔ انتشار 14 ماه؛ ریسک مجوز SixLabors.Fonts؛ وابستگی مستقیم به Open XML SDK (ریسک آن را هم دارد) | MIT؛ Microsoft؛ TFM `net10.0`؛ انتشار 2026-03-18 | MIT؛ انتشار 2026-06-16؛ TFMها بدون `net10.0` صریح (netstandard2.x کار می‌کند؛ در Spike روی net10.0 اجرا شد) |
| مصرف منابع کنترل‌شده | DOM: 1.8–2.0s و افزوده ≈270MB روی 253k سلول؛ رشد سریع Style (30k ⇒ 32–36s) و سلول؛ **با Guard قابل‌مهار** (Guard Spike v2: 5–50ms؛ «Guard پیشنهادی» با resolve رابطه‌ها پیاده/اجرا نشده: NotRun؛ Guard Spike v2 دور زده می‌شود)؛ بدون لغو ⇒ Request Timeout پاسخ را قطع می‌کند ولی Parse ادامه می‌یابد؛ برآورد بدترین حالت 40MB (برون‌یابی خطی برای محتوای واقع‌نما؛ اندازه‌گیری نشده): ≈600MB و ≈4.1–4.4s برای هر Import | 733–767ms و افزوده 183MB (فقط شمارش، کم‌برآورد)؛ SharedStringTable/styles در حافظه بار می‌شوند (تأییدنشده برای مقادیر بزرگ؛ NotRun) | 581–635ms و افزوده 108MB؛ روی فایل مخرب NotRun |

**نتیجهٔ پیشنهادی (`Proposed`؛ تأیید نهایی مالک لازم):** با توجه به سادگی توسعه و رفتار سلول مورد نیاز EXC-5/EXC-6، ClosedXML به‌شرط ایجاد کنترل‌های EXC-13/EXC-17 (Guard پیشنهادی پیش از بازکردن که partها را از رابطه‌ها resolve کند؛ برای سلول فرمول‌دار فقط Allow-list (فقط `HasFormula`، `FormulaA1`، `CachedValue`، `DataType`، `Style.NumberFormat` و `Address`) و هر API دیگر ممنوع؛ منع ارجاع مستقیم/Pin SixLabors.Fonts ≥ 2.0؛ تأیید مالک/مشاور برای مجوز؛ پذیرش نبود لغو و پیامد آن) گزینهٔ اول فعلی است. اگر ClosedXML سقف‌ها را رعایت نکند: Open XML SDK؛ سپس ExcelDataReader (با جبران شکاف EXC-6). EPPlus رد؛ NPOI فعلاً کنار (متن `OSMFEULA.txt` خوانده نشد)؛ MiniExcel و Sylvan.Data.Excel بررسی‌نشده/Deferred.

| رتبه | کتابخانه | دلیل (خلاصه) |
|---|---|---|
| گزینهٔ اول فعلی مالک | ClosedXML 0.105.1 | بالا؛ مشروط |
| جایگزین اول | Open XML SDK | اگر ClosedXML سقف‌ها را رعایت نکند؛ کد Parser بیشتر |
| جایگزین دوم | ExcelDataReader | ساده؛ شکاف EXC-6 |
| رد | EPPlus | مجوز غیرتجاری/پولی با محصول تجاری ناسازگار مگر خرید (و ADR/تأیید مالک) |
| فعلاً کنار | NPOI | EULA با هزینهٔ درآمدمحور باید بررسی شود؛ وابستگی‌های سنگین و Native |
| بررسی‌نشده / Deferred | MiniExcel، Sylvan.Data.Excel | 2.0 هنوز preview / pre-1.0 و متن مجوز تأییدنشده |

**هیچ Package به مخزن اضافه نمی‌شود؛ این مرحله فقط مقایسه و پیشنهاد است.**

**شرط‌های پیش از انتخاب نهایی:**
1. تأیید صریح مالک و ثبت ADR (AGENTS.md بخش ۲؛ D-106 بند EXC-17).
2. Spike/تست قرارداد روی فایل‌های **ساختگی** برای: صفر ابتدایی با فرمت عددی (EXC-5)، فرمول بدون cached و **بدون عنصر `<v>` (EXC-6؛ فقط Allow-list خوانده شود)**، تاریخ سریال/`date1904`، «Guard پیشنهادی» با رگرسیون «تغییر نام part»، اندازهٔ دروغین هدر ZIP (حالت‌های بیش از «کوچک‌تر از واقعی»)، externalLink، و در صورت لزوم سقف تعداد سلول/ستون.
3. خواندن متن مجوز هر کتابخانهٔ انتخابی و ثبت نسخه؛ افزودن فقط در `Directory.Packages.props`؛ **بدون Pin SixLabors.Fonts ≥ 2.0 و بدون ارجاع مستقیم به آن**.
4. اسکن NuGet CI برای آسیب‌پذیری وابستگی‌های گذرا؛ بررسی Issueهای باز و GitHub Advisory ClosedXML (NotRun).
5. بازبینی `security-reviewer` برای سیاست XML/zip (EXC-13).

## ۶. محدودیت‌ها و NotRun

- روی ClosedXML NotRun: لغو در نسخهٔ منتشرشده؛ هم‌زمانی چند Import؛ Windows؛ فایل واقعی مشتری؛ Issueهای باز؛ GitHub Advisory؛ externalLink/DDE و Entity خارجی XXE (فقط DTD داخلی را بازبین امنیتی آزمود).
- Guard پیشنهادی (resolve رابطه‌ها، نرمال‌سازی نام‌های OPC)، رد externalLink و شمارش بایت واقعی بازشده با Stream محدودکننده پیشنهاد هستند و اجرا نشدند؛ صرفِ `CompressedLength > Length` موجب رد فایل معتبر نمی‌شود. «تغییر نام part» و `GetFormattedString` و DTD را فقط بازبین مستقل آزمود (به‌جز `GetFormattedString` که probe اجراشده هم دارد) و خروجی خام آن‌ها در دسترس این Agent و در پیوست پ نیست.
- Guard: اندازهٔ دروغین هدر فقط در حالت «کوچک‌تر از واقعی» آزموده شد؛ قاعدهٔ نسبت و تعداد Entry در Guard Spike v2 هیچ فایل Spike تنها را رد نکردند؛ سقف تعداد سلول/ستون اندازه‌گیری مستقیم ندارد.
- بدترین حالت (≈600MB و ≈4.1–4.4s برای 40MB؛ ≈1.2GB برای دو Import) برون‌یابی خطی فقط برای محتوای واقع‌نما است؛ محتوای پر از shared strings (SharedStringTable بزرگ) و بدترین حالت ترکیبی NotRun؛ سقف هم‌زمانی ≤ 2 پیش از تثبیت باید اندازه‌گیری شود.
- Open XML SDK و ExcelDataReader فقط روی فایل 20k اجرا شدند، نه روی فایل‌های مخرب/Style/سلول؛ مقایسهٔ تاب‌آوری آن‌ها NotRun. Open XML SDK در Spike 2 فقط شمارش بود (بدون مادی‌سازی مقدار)؛ مقایسهٔ منابع آن عادلانه نیست؛ بار SharedStringTable/styles اندازه‌گیری نشد.
- تاریخ در Spike 2 آزموده نشد (فقط Spike 1). خروجی `dump` در `results_dump.jsonl` هست.
- تاریخ انتشار EPPlus 8.7.2 در داده‌ها نیامده است؛ متن مجوز Sylvan.Data.Excel و `OSMFEULA.txt` خوانده نشده است؛ MiniExcel و Sylvan.Data.Excel در Spike نبودند.
- وابستگی‌های گذرای بیرون از فید (بخش ۱) بررسی نشدند؛ nuspec ClosedXML را این Agent نخواند (گزارش Lead و بازبین امنیتی).
- Spike 2 را این Agent دوباره اجرا نکرد؛ اعداد از `SPIKE2-SUMMARY.md` و فایل‌های خام (پیوست پ) نقل شده‌اند. ارقام یک ماشین ۲ هسته‌ای؛ نه Benchmark رسمی.
- ادعاهای «منبع ثانویه» مستقل بررسی نشده‌اند.


## پیوست الف — کد Spike 1 (اجرای قبلی؛ برای بازتولید و مستندسازی)

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
import os; os.makedirs('data', exist_ok=True)
wb.save('data/small.xlsx')
wb = Workbook(write_only=True); ws = wb.create_sheet('Big')
ws.append(['Code','Name','Qty','Price','Country','Hs','Gtin','Note'])
for i in range(200000):
    ws.append([f'{i:08d}', f'Product name {i} with some text', i % 97, i * 1.25, 'IR', '0' + str(10000000 + i % 999999), f'0{i:012d}', 'note ' * 3])
wb.save('data/big.xlsx')
```

## پیوست ب — کد Spike 2 (2026-11-19)

**کد آزمایشی، Production نیست؛ فایل‌ها ساختگی‌اند.** کد زیر عیناً از artifactهای Spike 2 (مسیر موقت بیرون از مخزن: `scratchpad/xlspike2/`) نقل شده، **به مخزن افزوده نشده** (نه فایل کد، نه `csproj`) و این Agent آن را اجرا نکرد. نتایج خام: پیوست پ. این Spike نسخهٔ بسته‌ها را در خود `csproj` تکرار می‌کند، که برای کد مخزن مجاز نیست (AGENTS.md بخش ۲). **Guard این Spike الگوی نهایی Parser نیست** (بخش ۳.۳: تکیه بر نام part و اندازهٔ اعلامی). ترتیب اجرا: `gen/gen.py <dir>` و `gen/gen2.py <dir>` برای تولید فایل‌ها در `data/`، سپس `run.py`، و برای اجرای تکمیلی `run_extra.py`.

**فایل‌هایی که در اسکریپت‌های این پیوست تولید نمی‌شوند:** `formulas_nov.xlsx` و `formulas_heavy_nov.xlsx` (روش ساخت در پایین)، `spoof_header.xlsx` (اسکریپت تولید ذخیره نشده؛ مشخصات اندازه‌گیری‌شده در بخش ۳.۳). دلیل: این سه فایل پس از اسکریپت‌های اصلی ساخته شده‌اند.

### h.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><ServerGarbageCollector>false</ServerGarbageCollector><ConcurrentGarbageCollection>false</ConcurrentGarbageCollection></PropertyGroup>
<ItemGroup><PackageReference Include="ExcelDataReader" Version="3.9.0"/><PackageReference Include="ClosedXML" Version="0.105.1"/><PackageReference Include="DocumentFormat.OpenXml" Version="3.5.1"/></ItemGroup></Project>
```

### Program.cs

```csharp
using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using ClosedXML.Excel;
using ExcelDataReader;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Spreadsheet;
System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

// args: <lib cx|edr|ox|guard|dump|cancel|write> <path> [guard=on|off]
var lib = args[0]; var path = args[1]; bool useGuard = args.Length > 2 && args[2] == "guard";
var proc = Process.GetCurrentProcess(); proc.Refresh();
long baseWs = proc.WorkingSet64; var sw = Stopwatch.StartNew();
var o = new Dictionary<string, object?> { ["lib"] = lib, ["file"] = Path.GetFileName(path), ["guard"] = useGuard };

// محدودیت‌های پیشنهادی (Proposed) برای Guard
const long MaxFile = 10L << 20, MaxUncompressed = 40L << 20, MaxEntry = 30L << 20, MaxEntries = 200, MaxSheets = 50, MaxStyles = 5000; const double MaxRatio = 100;
(bool ok, string reason) Guard(string p) {
  var fi = new FileInfo(p); if (fi.Length > MaxFile) return (false, "file_too_large");
  using var fs = File.OpenRead(p); Span<byte> sig = stackalloc byte[4]; if (fs.Read(sig) < 4 || sig[0] != 0x50 || sig[1] != 0x4B) return (false, "not_zip_signature");
  fs.Position = 0;
  try {
    using var z = new ZipArchive(fs, ZipArchiveMode.Read); long total = 0; int n = 0; bool hasWb = false;
    int sheetsN = 0; int xfs = 0; foreach (var e in z.Entries) { n++; total += e.Length; if (e.Length > MaxEntry) return (false, "entry_too_large"); if (e.FullName.StartsWith("xl/worksheets/sheet") && e.FullName.EndsWith(".xml")) sheetsN++; if (e.FullName == "xl/styles.xml") { using var xr = System.Xml.XmlReader.Create(e.Open(), new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit, MaxCharactersInDocument = 20_000_000 }); bool inX = false; while (xr.Read()) { if (xr.NodeType == System.Xml.XmlNodeType.Element && xr.LocalName == "cellXfs") inX = true; else if (xr.NodeType == System.Xml.XmlNodeType.EndElement && xr.LocalName == "cellXfs") break; else if (inX && xr.NodeType == System.Xml.XmlNodeType.Element && xr.LocalName == "xf" && ++xfs > MaxStyles) return (false, "too_many_styles"); } } if (e.FullName == "xl/workbook.xml") hasWb = true;
      if (e.FullName.StartsWith("xl/vbaProject")) return (false, "macro_part");
      if (e.CompressedLength > 0 && (double)e.Length / e.CompressedLength > MaxRatio && e.Length > (1 << 20)) return (false, "ratio_exceeded"); }
    if (sheetsN > MaxSheets) return (false, "too_many_sheets"); if (n > MaxEntries) return (false, "too_many_entries"); if (total > MaxUncompressed) return (false, "uncompressed_too_large"); if (!hasWb) return (false, "no_workbook");
    return (true, "ok");
  } catch (InvalidDataException) { return (false, "bad_zip"); }
}

try {
  if (useGuard) { var g = Guard(path); o["guardResult"] = g.reason; if (!g.ok) { Finish(o, sw, baseWs, "rejected_by_guard"); return; } }
  long rows = 0, cells = 0, formulas = 0, formulasNoCache = 0, numeric = 0, textc = 0, errs = 0; int sheets = 0;
  if (lib == "guard") { Finish(o, sw, baseWs, "guard_only"); return; }
  if (lib == "cx") {
    var lo = new LoadOptions { RecalculateAllFormulas = false };
    using var wb = new XLWorkbook(path, lo);
    foreach (var ws in wb.Worksheets) { sheets++;
      foreach (var row in ws.RowsUsed()) { rows++;
        foreach (var c in row.CellsUsed(XLCellsUsedOptions.All)) { cells++;
          if (c.HasFormula) { formulas++; var cv = c.CachedValue; if (cv.IsBlank) formulasNoCache++; else if (cv.IsError) errs++; }
          else { var t = c.DataType; if (t == XLDataType.Number) numeric++; else if (t == XLDataType.Text) textc++; } } } }
  } else if (lib == "edr") {
    using var st = File.OpenRead(path); using var r = ExcelReaderFactory.CreateReader(st);
    do { sheets++; while (r.Read()) { rows++; for (int i = 0; i < r.FieldCount; i++) { var v = r.GetValue(i); if (v != null) cells++; } } } while (r.NextResult());
  } else if (lib == "ox") {
    using var doc = SpreadsheetDocument.Open(path, false); var wbp = doc.WorkbookPart!;
    foreach (var wsp in wbp.WorksheetParts) { sheets++; using var rd = OpenXmlReader.Create(wsp);
      while (rd.Read()) { if (rd.ElementType == typeof(Row) && rd.IsStartElement) rows++; if (rd.ElementType == typeof(Cell) && rd.IsStartElement) cells++; } }
  } else if (lib == "dump") {
    using var wb = new XLWorkbook(path); var ws = wb.Worksheets.First(); var lines = new List<string>();
    foreach (var c in ws.CellsUsed(XLCellsUsedOptions.All)) {
      var cv = c.CachedValue; string cvs = cv.IsBlank ? "blank" : cv.IsError ? "error:" + cv.GetError() : cv.Type + ":" + cv;
      lines.Add($"{c.Address} dt={c.DataType} hasF={c.HasFormula} cached=[{cvs}] numFmt=[{c.Style.NumberFormat.Format}] id={c.Style.NumberFormat.NumberFormatId} quote={c.Style.IncludeQuotePrefix} raw=[{(c.HasFormula ? c.FormulaA1 : c.Value.ToString())}]");
    }
    o["cells"] = lines;
  } else if (lib == "dumpval") {   // خواندن Value سلول‌های فرمول (ممکن است فرمول را محاسبه کند)
    using var wb = new XLWorkbook(path); var ws = wb.Worksheets.First(); var lines = new List<string>(); int n = 0;
    foreach (var c in ws.CellsUsed(XLCellsUsedOptions.All)) { if (!c.HasFormula) continue; if (++n > 5) break; string v; try { var vv = c.Value; v = vv.Type + ":" + (vv.ToString().Length > 40 ? vv.ToString().Substring(0, 40) + "…" : vv.ToString()); } catch (Exception ex) { v = "EXC:" + ex.GetType().Name; } lines.Add($"{c.Address} Value => {v}"); }
    o["cells"] = lines;
  } else if (lib == "write") {   // گزارش خطا + خنثی‌سازی فرمول
    using var wb = new XLWorkbook(); var ws = wb.AddWorksheet("errors");
    ws.Cell(1,1).Value="row"; ws.Cell(1,2).Value="field"; ws.Cell(1,3).Value="message";
    for (int i = 0; i < 20000; i++) { ws.Cell(i+2,1).Value = i+2; ws.Cell(i+2,2).Value = "HS Code"; ws.Cell(i+2,3).Value = (i % 2 == 0 ? "=cmd|' /C calc'!A0" : "مقدار نامعتبر"); }
    var outp = Path.Combine(Path.GetDirectoryName(path)!, $"errors_out_{Environment.ProcessId}.xlsx"); wb.SaveAs(outp);
    using var back = new XLWorkbook(outp); var c3 = back.Worksheet(1).Cell(2,3);
    o["writeBackDataType"] = c3.DataType.ToString(); o["writeBackHasFormula"] = c3.HasFormula; o["writeBackValue"] = c3.Value.ToString(); o["outBytes"] = new FileInfo(outp).Length; File.Delete(outp);
  }
  o["sheets"] = sheets; o["rows"] = rows; if(!o.ContainsKey("cells")) o["cells"] = cells; o["formulas"] = formulas; o["formulasNoCache"] = formulasNoCache; o["errCached"] = errs; o["numericCells"] = numeric; o["textCells"] = textc;
  Finish(o, sw, baseWs, "ok");
} catch (OutOfMemoryException) { Finish(o, sw, baseWs, "OutOfMemoryException"); }
catch (Exception ex) { o["error"] = ex.Message.Length > 120 ? ex.Message.Substring(0, 120) : ex.Message; Finish(o, sw, baseWs, "exception:" + ex.GetType().Name); }

static void Finish(Dictionary<string, object?> o, Stopwatch sw, long baseWs, string outcome) {
  var p = Process.GetCurrentProcess(); p.Refresh();
  o["outcome"] = outcome; o["ms"] = sw.ElapsedMilliseconds; o["baseMB"] = baseWs >> 20; o["peakMB"] = p.PeakWorkingSet64 >> 20; o["deltaMB"] = (p.PeakWorkingSet64 - baseWs) >> 20;
  o["gcAllocMB"] = GC.GetTotalAllocatedBytes() >> 20;
  Console.WriteLine(JsonSerializer.Serialize(o, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
}
```

### gen.py

```python
# تولید فایل‌های آزمایشی ساختگی (هیچ داده واقعی مشتری در کار نیست)
import random, zipfile, io, os, sys
from openpyxl import Workbook
from openpyxl.styles import PatternFill, Font
random.seed(7)
D = sys.argv[1]
os.makedirs(D, exist_ok=True)
fa = ["شرکت بازرگانی", "پتروشیمی", "فولاد", "کاشی", "صنعت", "گستر", "پارس", "آریا", "نوین", "کیمیا"]
en = ["Alpha Trading", "Beta Steel", "Gamma Chem", "Delta Foods", "Omega Ltd", "Sigma Co"]
fadig = str.maketrans("0123456789", "۰۱۲۳۴۵۶۷۸۹")
ardig = str.maketrans("0123456789", "٠١٢٣٤٥٦٧٨٩")

def realistic(path, rows):
    wb = Workbook(); ws = wb.active; ws.title = "Products"
    ws.append(["نام کالا", "Product Name", "HS Code", "GTIN", "IRC", "واحد", "وزن", "قیمت", "کشور", "تاریخ", "توضیحات", "کد داخلی", "ارقام فارسی", "ستون خالی"])
    for i in range(rows):
        n = random.randint(0, 10**8)
        ws.append([
            f"{random.choice(fa)} {random.choice(fa)} {i}", f"{random.choice(en)} {i}",
            f"{random.randint(0, 99999999):08d}", f"{random.randint(0, 10**13-1):013d}",
            f"{random.randint(0, 10**16-1):016d}", random.choice(["kg", "ton", "عدد"]),
            round(random.random() * 1000, 3), random.randint(1, 10**7),
            random.choice(["IR", "CN", "DE", "AE", "TR"]), "2026-01-%02d" % (1 + i % 28),
            ("توضیح مفصل " * random.randint(1, 4)) if i % 3 else None, f"P-{i:06d}",
            str(n).translate(fadig) if i % 2 else str(n).translate(ardig), None])
    wb.save(path)

realistic(f"{D}/realistic_20k.xlsx", 20000)

# شناسه‌های عددی و متنی
wb = Workbook(); ws = wb.active; ws.title = "ids"
ws.append(["num_fmt5", "gtin13_num_fmt", "text_lead0", "fa_digits_text", "ar_digits_text", "num_16digits", "hs8_num_fmt", "plain_num_no_fmt", "quote_prefix"])
ws.append([123, 12345678905, "00123", "۰۰۱۲۳", "٠٠١٢٣", 1234567890123456, 1012100, 123, "00789"])
c = ws["A2"]; c.number_format = "00000"
ws["B2"].number_format = "0000000000000"
ws["F2"].number_format = "0"
ws["G2"].number_format = "00000000"
ws["I2"].quotePrefix = True
wb.save(f"{D}/ids.xlsx")

# فرمول‌ها
wb = Workbook(); ws = wb.active; ws.title = "f"
ws["A1"] = 5; ws["A2"] = None; ws["A3"] = "text"
ws["B1"] = "=A1*2"          # بدون cached value (openpyxl مقدار نمی‌نویسد)
ws["B2"] = "=1/0"            # خطا (پس از patch مقدار cached خطا)
ws["B3"] = "=A1+1"           # پس از patch مقدار cached = 6
ws["B4"] = '=CONCATENATE("a","b")'
ws["B5"] = "=UNKNOWNFUNC(1)" # تابع ناشناخته
wb.save(f"{D}/_f0.xlsx")
zin = zipfile.ZipFile(f"{D}/_f0.xlsx"); out = zipfile.ZipFile(f"{D}/formulas.xlsx", "w", zipfile.ZIP_DEFLATED)
for it in zin.infolist():
    data = zin.read(it.filename)
    if it.filename == "xl/worksheets/sheet1.xml":
        s = data.decode()
        s = s.replace('<c r="B2"><f>1/0</f><v></v></c>', '<c r="B2" t="e"><f>1/0</f><v>#DIV/0!</v></c>')
        s = s.replace('<c r="B3"><f>A1+1</f><v></v></c>', '<c r="B3"><f>A1+1</f><v>6</v></c>')
        data = s.encode()
    out.writestr(it.filename, data)
out.close(); zin.close(); os.remove(f"{D}/_f0.xlsx")

# فرمول سنگین بدون cached value (سنجش اجرای فرمول هنگام خواندن Value)
wb = Workbook(); ws = wb.active
ws["A1"] = '=SUM(ROW(A1:A1048576)*COLUMN(A1:Z1))'
ws["A2"] = '=REPT("x",32767)&REPT("y",32767)'
for i in range(3, 200):
    ws[f"A{i}"] = f'=SUMPRODUCT(ROW(A1:A100000)*ROW(A1:A100000))+{i}'
wb.save(f"{D}/formulas_heavy.xlsx")

# Style زیاد
wb = Workbook(); ws = wb.active
for i in range(1, 30001):
    c = ws.cell(row=i, column=1, value=i)
    c.fill = PatternFill("solid", fgColor="%06X" % (i * 37 % 0xFFFFFF))
    c.font = Font(size=8 + (i % 40) / 4, color="%06X" % (i * 91 % 0xFFFFFF))
wb.save(f"{D}/styles_many.xlsx")

# Worksheet زیاد
wb = Workbook(); wb.remove(wb.active)
for s in range(120):
    ws = wb.create_sheet(f"S{s}")
    for r in range(1, 201):
        ws.append([r, f"v{s}-{r}", random.random()])
wb.save(f"{D}/sheets_many.xlsx")

# خراب
raw = open(f"{D}/realistic_20k.xlsx", "rb").read()
open(f"{D}/corrupt_truncated.xlsx", "wb").write(raw[: int(len(raw) * 0.6)])
open(f"{D}/corrupt_random.xlsx", "wb").write(os.urandom(200000))
open(f"{D}/not_xlsx_text.xlsx", "wb").write(b"hello,world\n1,2\n" * 1000)
z = zipfile.ZipFile(f"{D}/corrupt_nowb.xlsx", "w"); z.writestr("a.txt", "x"); z.close()
z = zipfile.ZipFile(f"{D}/macro_like.xlsx", "w", zipfile.ZIP_DEFLATED)
src = zipfile.ZipFile(f"{D}/ids.xlsx")
for it in src.infolist(): z.writestr(it.filename, src.read(it.filename))
z.writestr("xl/vbaProject.bin", b"\x00" * 100); z.close()

# فایل با سقف‌های مصنوعی
def xlsx_with_sheet_xml(path, writer):
    src = zipfile.ZipFile(f"{D}/ids.xlsx")
    z = zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED, compresslevel=9)
    for it in src.infolist():
        if it.filename == "xl/worksheets/sheet1.xml":
            with z.open("xl/worksheets/sheet1.xml", "w", force_zip64=True) as f: writer(f)
        else:
            z.writestr(it.filename, src.read(it.filename))
    z.close()
HEAD = b'<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">'
def col(n):
    s = ""
    while n: n, r = divmod(n - 1, 26); s = chr(65 + r) + s
    return s
def fake_dims(f):
    f.write(HEAD + b'<dimension ref="A1:XFD1048576"/><sheetData><row r="1"><c r="A1"><v>1</v></c></row><row r="1048576"><c r="XFD1048576"><v>2</v></c></row></sheetData></worksheet>')
def bomb_cells(f):  # 400 ردیف × 16384 ستون
    f.write(HEAD + b"<sheetData>")
    cols = [col(i).encode() for i in range(1, 16385)]
    for r in range(1, 401):
        f.write(b'<row r="%d">' % r + b"".join(b'<c r="%s%d"><v>1</v></c>' % (c, r) for c in cols) + b"</row>")
    f.write(b"</sheetData></worksheet>")
def bomb_ratio(f):  # padding فشرده‌پذیر ~300MB
    f.write(HEAD + b"<sheetData><row r=\"1\"><c r=\"A1\"><v>1</v></c></row></sheetData><!--")
    chunk = b"A" * (1 << 20)
    for _ in range(300): f.write(chunk)
    f.write(b"--></worksheet>")
xlsx_with_sheet_xml(f"{D}/dims_fake.xlsx", fake_dims)
xlsx_with_sheet_xml(f"{D}/bomb_cells.xlsx", bomb_cells)
xlsx_with_sheet_xml(f"{D}/bomb_ratio.xlsx", bomb_ratio)
for f in sorted(os.listdir(D)): print(f, os.path.getsize(f"{D}/{f}"))
```

### gen2.py

```python
import zipfile, sys, random
from openpyxl import Workbook
from openpyxl.styles import PatternFill, Font
D=sys.argv[1]
import os; os.makedirs(D, exist_ok=True)
# ۵۰۰۰ Style متمایز در یک فایل ۲۰هزار ردیفی
wb=Workbook(); ws=wb.active
for i in range(1,20001):
    c=ws.cell(row=i,column=1,value=i); ws.cell(row=i,column=2,value=f"v{i}")
    if i<=5000:
        c.fill=PatternFill("solid",fgColor="%06X"%(i*37%0xFFFFFF)); c.font=Font(size=8+(i%40)/4,color="%06X"%(i*91%0xFFFFFF))
wb.save(f"{D}/styles_5k.xlsx")
# بمب سلول در حد سقف ۱۰۰MB بازشده: 200 ردیف × 16384 ستون
def col(n):
    s=""
    while n: n,r=divmod(n-1,26); s=chr(65+r)+s
    return s
HEAD=b'<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">'
src=zipfile.ZipFile(f"{D}/ids.xlsx"); z=zipfile.ZipFile(f"{D}/cells_3m.xlsx","w",zipfile.ZIP_DEFLATED,compresslevel=9)
for it in src.infolist():
    if it.filename=="xl/worksheets/sheet1.xml":
        with z.open(it.filename,"w",force_zip64=True) as f:
            f.write(HEAD+b"<sheetData>"); cols=[col(i).encode() for i in range(1,16385)]
            for r in range(1,201): f.write(b'<row r="%d">'%r+b"".join(b'<c r="%s%d"><v>1</v></c>'%(c,r) for c in cols)+b"</row>")
            f.write(b"</sheetData></worksheet>")
    else: z.writestr(it.filename,src.read(it.filename))
z.close()
```

### Build and run order

از پوشه‌ای که `h.csproj` در آن قرار دارد اجرا شود تا مسیر خروجی با هر دو runner (`bin/h.dll`) یکسان باشد:

```bash
dotnet build h.csproj -c Release -o bin
python gen.py data
python gen2.py data
python run.py
python run_extra.py
```

گزینهٔ `-o bin` عمداً خروجی را در `bin/h.dll` قرار می‌دهد؛ Build معمولی Release بدون `-o bin` خروجی را در `bin/Release/net10.0/h.dll` می‌گذارد و با مسیر hard-coded runnerها سازگار نیست.

### run.py

```python
import subprocess, json, os, sys, time
env = dict(os.environ, DOTNET_GCHeapHardLimit="0x60000000", DOTNET_gcServer="0", DOTNET_TieredPGO="0")
OUT = open("results.jsonl", "w")
sys.stdout.reconfigure(line_buffering=True)
def run(lib, f, guard=False, reps=1, timeout=120):
    for i in range(reps):
        cmd = ["timeout", "-s", "KILL", str(timeout), "dotnet", "bin/h.dll", lib, f"data/{f}"] + (["guard"] if guard else [])
        t = time.time(); p = subprocess.run(cmd, capture_output=True, text=True, env=env)
        wall = int((time.time() - t) * 1000)
        try: d = json.loads(p.stdout.strip().splitlines()[-1])
        except Exception: d = {"lib": lib, "file": f, "guard": guard, "outcome": "killed_or_crashed", "rc": p.returncode, "stderr": p.stderr[-150:]}
        d["rep"] = i + 1; d["wallMs"] = wall; d["rc"] = p.returncode
        OUT.write(json.dumps(d, ensure_ascii=False) + "\n"); OUT.flush()
# مقایسهٔ سه کتابخانه روی فایل ۲۰هزار ردیفی (۳ تکرار)
for lib in ["cx", "edr", "ox"]: run(lib, "realistic_20k.xlsx", reps=3)
# ClosedXML روی بقیهٔ فایل‌های واقع‌نما (بدون Guard)
for f in ["ids.xlsx", "formulas.xlsx", "styles_many.xlsx", "sheets_many.xlsx", "dims_fake.xlsx"]: run("cx", f, reps=3 if f in ("styles_many.xlsx", "sheets_many.xlsx") else 1)
# فایل‌های خراب
for f in ["corrupt_truncated.xlsx", "corrupt_random.xlsx", "corrupt_nowb.xlsx", "not_xlsx_text.xlsx", "macro_like.xlsx"]: run("cx", f)
# فرمول سنگین: خواندن CachedValue، سپس خواندن Value
run("cx", "formulas_heavy.xlsx", reps=2); run("dumpval", "formulas_heavy.xlsx", reps=2, timeout=60)
# بمب‌های کنترل‌شده: خام (با سقف heap و timeout) و با Guard
for f in ["bomb_cells.xlsx", "bomb_ratio.xlsx"]:
    run("cx", f, timeout=90); run("cx", f, guard=True)
# Guard روی همهٔ فایل‌ها (فقط Guard) و Guard + ClosedXML روی فایل عادی
for f in sorted(os.listdir("data")):
    if f.endswith(".xlsx"): run("guard", f, guard=True)
run("cx", "realistic_20k.xlsx", guard=True, reps=3)
run("write", "realistic_20k.xlsx", reps=2)
print("done")
```

### run_extra.py

```python
# اجرای تکمیلی: Guard نهایی روی همهٔ فایل‌ها + ClosedXML خام روی cells_3m/styles_5k + spoof_header
import subprocess, json, os, time
env = dict(os.environ, DOTNET_GCHeapHardLimit="0x60000000")
out = open("results_extra.jsonl", "w")
def run(lib, f, guard, timeout=120):
    cmd = ["timeout", "-s", "KILL", str(timeout), "dotnet", "bin/h.dll", lib, f"data/{f}"] + (["guard"] if guard else [])
    p = subprocess.run(cmd, capture_output=True, text=True, env=env)
    try: d = json.loads(p.stdout.strip().splitlines()[-1])
    except Exception: d = {"lib": lib, "file": f, "outcome": "killed_or_crashed", "rc": p.returncode}
    out.write(json.dumps(d, ensure_ascii=False) + "\n"); out.flush()
for f in sorted(x for x in os.listdir("data") if x.endswith(".xlsx")): run("guard", f, True)
for f in ["cells_3m.xlsx", "styles_5k.xlsx", "spoof_header.xlsx"]: run("cx", f, False)
run("cx", "spoof_header.xlsx", True)
print("done")
```

### ساخت `formulas_nov.xlsx` و `formulas_heavy_nov.xlsx` (بازسازی توصیفی)

اسکریپت اصلی Spike برای این دو فایل ذخیره نشده است. روش (طبق گزارش Lead): حذف `<v></v>` از `xl/worksheets/sheet1.xml` فایل‌های `formulas.xlsx` و `formulas_heavy.xlsx`. کد زیر **بازسازی مستندساز** است، نه کد اصلی Spike؛ مستندساز آن را اجرا کرد و **همهٔ partهای** خروجی (نام و بایت‌ها) با دو فایل artifact یکسان بود.

```python
import zipfile, sys
src, dst = sys.argv[1], sys.argv[2]
zin = zipfile.ZipFile(src); zout = zipfile.ZipFile(dst, "w", zipfile.ZIP_DEFLATED)
for it in zin.infolist():
    data = zin.read(it.filename)
    if it.filename == "xl/worksheets/sheet1.xml":
        data = data.replace(b"<v></v>", b"")
    zout.writestr(it.filename, data)
zout.close(); zin.close()
```

### apiprobe (probe Allow-list فرمول؛ `apiprobe/apiprobe.csproj` و `apiprobe/Program.cs`)

کد عیناً از `scratchpad/xlspike2/apiprobe/`. ورودی `args[0]`؛ طبق کامنت کد و خروجی (`B1 ⇒ 10`، `B4 ⇒ ab`) فایل `formulas_nov.xlsx` بود؛ فرمان دقیق اجرا جداگانه ثبت نشده است. معیار: تغییر `CachedValue` سلول‌های B1/B4 پس از هر API.

```xml
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><Nullable>disable</Nullable><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>
<ItemGroup><PackageReference Include="ClosedXML" Version="0.105.1"/><PackageReference Include="DocumentFormat.OpenXml" Version="3.5.1"/></ItemGroup></Project>
```

```csharp
using ClosedXML.Excel;
var path = args[0];
// cell B1 in formulas_nov has <f> and no <v>; detect evaluation by CachedValue change
var probes = new (string name, Action<IXLWorksheet, IXLCell> act)[] {
  ("GetString", (ws,c)=>{ try{_=c.GetString();}catch{} }),
  ("GetText", (ws,c)=>{ try{_=c.GetText();}catch{} }),
  ("TryGetValue<string>", (ws,c)=>{ c.TryGetValue<string>(out _); }),
  ("GetFormattedString", (ws,c)=>{ _=c.GetFormattedString(); }),
  ("GetBoolean", (ws,c)=>{ try{_=c.GetBoolean();}catch{} }),
  ("GetDateTime", (ws,c)=>{ try{_=c.GetDateTime();}catch{} }),
  ("GetError", (ws,c)=>{ try{_=c.GetError();}catch{} }),
  ("GetValue<string>", (ws,c)=>{ try{_=c.GetValue<string>();}catch{} }),
  ("GetDouble", (ws,c)=>{ try{_=c.GetDouble();}catch{} }),
  ("Value", (ws,c)=>{ _=c.Value; }),
  ("ws.Search", (ws,c)=>{ _=ws.Search("a").Count(); }),
  ("HasFormula+FormulaA1+CachedValue only", (ws,c)=>{ _=c.HasFormula; _=c.FormulaA1; _=c.CachedValue; _=c.DataType; _=c.Style.NumberFormat.Format; }),
  ("IsEmpty()", (ws,c)=>{ _=c.IsEmpty(); }),
  ("CopyTo", (ws,c)=>{ try{ c.CopyTo(ws.Cell("Z99")); }catch{} }),
  ("AsRange().FirstCellUsed", (ws,c)=>{ _=ws.Range("A1:C5").FirstCellUsed(); }),
  ("Range.CellsUsed(pred)", (ws,c)=>{ _=ws.Range("A1:C5").CellsUsed(x=>x.HasFormula).Count(); }),
};
foreach (var (name, act) in probes) {
  string res;
  try {
    using var wb = new XLWorkbook(path, new LoadOptions{RecalculateAllFormulas=false});
    var ws = wb.Worksheets.First(); var c = ws.Cell("B1"); var c4 = ws.Cell("B4");
    act(ws, c); act(ws, c4);
    res = $"B1 cached=[{(c.CachedValue.IsBlank?"blank":c.CachedValue.ToString())}] B4 cached=[{(c4.CachedValue.IsBlank?"blank":c4.CachedValue.ToString())}]";
  } catch (Exception ex) { res = "EXC " + ex.GetType().Name; }
  Console.WriteLine($"{name,-24} {res}");
}
```

## پیوست پ — نتایج خام Spike 2 (JSONL و TXT)

نتایج خام عیناً از `scratchpad/xlspike2/` نقل شده‌اند (هر خط یک اجرا؛ مقادیر ms، MB). `results_contaminated.jsonl` (اجرای مخدوش) استفاده نشد و نیامده است. فیلد `cells` در خروجی `dumpval` فهرست متنی سلول‌هاست. این نتایج دربارهٔ فایل‌های ساختگی‌اند. **در این فایل‌ها نیستند:** آزمون‌های مستقل بازبین امنیتی (تغییر نام part، DTD، `GetFormattedString`، اجرای مستقل `styles_5k`/`spoof_header`) و خروجی `dump` فایل `ids.xlsx`.

### results.jsonl (اجرای ماتریس با نسخهٔ قبلی Guard)

```jsonl
{"lib": "cx", "file": "realistic_20k.xlsx", "guard": false, "sheets": 1, "rows": 20001, "cells": 253347, "formulas": 0, "formulasNoCache": 0, "errCached": 0, "numericCells": 40000, "textCells": 213347, "outcome": "ok", "ms": 1841, "baseMB": 33, "peakMB": 304, "deltaMB": 270, "gcAllocMB": 440, "rep": 1, "wallMs": 1912, "rc": 0}
{"lib": "cx", "file": "realistic_20k.xlsx", "guard": false, "sheets": 1, "rows": 20001, "cells": 253347, "formulas": 0, "formulasNoCache": 0, "errCached": 0, "numericCells": 40000, "textCells": 213347, "outcome": "ok", "ms": 1995, "baseMB": 33, "peakMB": 307, "deltaMB": 273, "gcAllocMB": 443, "rep": 2, "wallMs": 2065, "rc": 0}
{"lib": "cx", "file": "realistic_20k.xlsx", "guard": false, "sheets": 1, "rows": 20001, "cells": 253347, "formulas": 0, "formulasNoCache": 0, "errCached": 0, "numericCells": 40000, "textCells": 213347, "outcome": "ok", "ms": 1818, "baseMB": 33, "peakMB": 306, "deltaMB": 272, "gcAllocMB": 441, "rep": 3, "wallMs": 1888, "rc": 0}
{"lib": "edr", "file": "realistic_20k.xlsx", "guard": false, "sheets": 1, "rows": 20001, "cells": 253347, "formulas": 0, "formulasNoCache": 0, "errCached": 0, "numericCells": 0, "textCells": 0, "outcome": "ok", "ms": 581, "baseMB": 33, "peakMB": 142, "deltaMB": 108, "gcAllocMB": 87, "rep": 1, "wallMs": 645, "rc": 0}
{"lib": "edr", "file": "realistic_20k.xlsx", "guard": false, "sheets": 1, "rows": 20001, "cells": 253347, "formulas": 0, "formulasNoCache": 0, "errCached": 0, "numericCells": 0, "textCells": 0, "outcome": "ok", "ms": 624, "baseMB": 33, "peakMB": 142, "deltaMB": 108, "gcAllocMB": 87, "rep": 2, "wallMs": 690, "rc": 0}
{"lib": "edr", "file": "realistic_20k.xlsx", "guard": false, "sheets": 1, "rows": 20001, "cells": 253347, "formulas": 0, "formulasNoCache": 0, "errCached": 0, "numericCells": 0, "textCells": 0, "outcome": "ok", "ms": 635, "baseMB": 33, "peakMB": 142, "deltaMB": 108, "gcAllocMB": 87, "rep": 3, "wallMs": 709, "rc": 0}
{"lib": "ox", "file": "realistic_20k.xlsx", "guard": false, "sheets": 1, "rows": 20001, "cells": 253347, "formulas": 0, "formulasNoCache": 0, "errCached": 0, "numericCells": 0, "textCells": 0, "outcome": "ok", "ms": 733, "baseMB": 33, "peakMB": 217, "deltaMB": 183, "gcAllocMB": 155, "rep": 1, "wallMs": 801, "rc": 0}
{"lib": "ox", "file": "realistic_20k.xlsx", "guard": false, "sheets": 1, "rows": 20001, "cells": 253347, "formulas": 0, "formulasNoCache": 0, "errCached": 0, "numericCells": 0, "textCells": 0, "outcome": "ok", "ms": 735, "baseMB": 33, "peakMB": 217, "deltaMB": 183, "gcAllocMB": 155, "rep": 2, "wallMs": 796, "rc": 0}
{"lib": "ox", "file": "realistic_20k.xlsx", "guard": false, "sheets": 1, "rows": 20001, "cells": 253347, "formulas": 0, "formulasNoCache": 0, "errCached": 0, "numericCells": 0, "textCells": 0, "outcome": "ok", "ms": 767, "baseMB": 33, "peakMB": 217, "deltaMB": 183, "gcAllocMB": 155, "rep": 3, "wallMs": 834, "rc": 0}
{"lib": "cx", "file": "ids.xlsx", "guard": false, "sheets": 1, "rows": 2, "cells": 18, "formulas": 0, "formulasNoCache": 0, "errCached": 0, "numericCells": 5, "textCells": 13, "outcome": "ok", "ms": 371, "baseMB": 33, "peakMB": 79, "deltaMB": 45, "gcAllocMB": 8, "rep": 1, "wallMs": 429, "rc": 0}
{"lib": "cx", "file": "formulas.xlsx", "guard": false, "sheets": 1, "rows": 5, "cells": 7, "formulas": 5, "formulasNoCache": 3, "errCached": 1, "numericCells": 1, "textCells": 1, "outcome": "ok", "ms": 406, "baseMB": 33, "peakMB": 79, "deltaMB": 46, "gcAllocMB": 8, "rep": 1, "wallMs": 466, "rc": 0}
{"lib": "cx", "file": "styles_many.xlsx", "guard": false, "sheets": 1, "rows": 30000, "cells": 30000, "formulas": 0, "formulasNoCache": 0, "errCached": 0, "numericCells": 30000, "textCells": 0, "outcome": "ok", "ms": 32358, "baseMB": 34, "peakMB": 436, "deltaMB": 402, "gcAllocMB": 558, "rep": 1, "wallMs": 32427, "rc": 0}
{"lib": "cx", "file": "styles_many.xlsx", "guard": false, "sheets": 1, "rows": 30000, "cells": 30000, "formulas": 0, "formulasNoCache": 0, "errCached": 0, "numericCells": 30000, "textCells": 0, "outcome": "ok", "ms": 33386, "baseMB": 34, "peakMB": 439, "deltaMB": 405, "gcAllocMB": 564, "rep": 2, "wallMs": 33475, "rc": 0}
{"lib": "cx", "file": "styles_many.xlsx", "guard": false, "sheets": 1, "rows": 30000, "cells": 30000, "formulas": 0, "formulasNoCache": 0, "errCached": 0, "numericCells": 30000, "textCells": 0, "outcome": "ok", "ms": 36479, "baseMB": 33, "peakMB": 441, "deltaMB": 407, "gcAllocMB": 562, "rep": 3, "wallMs": 36579, "rc": 0}
{"lib": "cx", "file": "sheets_many.xlsx", "guard": false, "sheets": 120, "rows": 24000, "cells": 72000, "formulas": 0, "formulasNoCache": 0, "errCached": 0, "numericCells": 48000, "textCells": 24000, "outcome": "ok", "ms": 1391, "baseMB": 33, "peakMB": 276, "deltaMB": 242, "gcAllocMB": 266, "rep": 1, "wallMs": 1465, "rc": 0}
{"lib": "cx", "file": "sheets_many.xlsx", "guard": false, "sheets": 120, "rows": 24000, "cells": 72000, "formulas": 0, "formulasNoCache": 0, "errCached": 0, "numericCells": 48000, "textCells": 24000, "outcome": "ok", "ms": 1367, "baseMB": 33, "peakMB": 275, "deltaMB": 241, "gcAllocMB": 266, "rep": 2, "wallMs": 1437, "rc": 0}
{"lib": "cx", "file": "sheets_many.xlsx", "guard": false, "sheets": 120, "rows": 24000, "cells": 72000, "formulas": 0, "formulasNoCache": 0, "errCached": 0, "numericCells": 48000, "textCells": 24000, "outcome": "ok", "ms": 1339, "baseMB": 33, "peakMB": 275, "deltaMB": 241, "gcAllocMB": 261, "rep": 3, "wallMs": 1413, "rc": 0}
{"lib": "cx", "file": "dims_fake.xlsx", "guard": false, "sheets": 1, "rows": 2, "cells": 2, "formulas": 0, "formulasNoCache": 0, "errCached": 0, "numericCells": 2, "textCells": 0, "outcome": "ok", "ms": 370, "baseMB": 33, "peakMB": 76, "deltaMB": 42, "gcAllocMB": 8, "rep": 1, "wallMs": 438, "rc": 0}
{"lib": "cx", "file": "corrupt_truncated.xlsx", "guard": false, "error": "File contains corrupted data.", "outcome": "exception:FileFormatException", "ms": 119, "baseMB": 33, "peakMB": 53, "deltaMB": 19, "gcAllocMB": 1, "rep": 1, "wallMs": 202, "rc": 0}
{"lib": "cx", "file": "corrupt_random.xlsx", "guard": false, "error": "File contains corrupted data.", "outcome": "exception:FileFormatException", "ms": 104, "baseMB": 33, "peakMB": 53, "deltaMB": 19, "gcAllocMB": 1, "rep": 1, "wallMs": 182, "rc": 0}
{"lib": "cx", "file": "corrupt_nowb.xlsx", "guard": false, "error": "Object reference not set to an instance of an object.", "outcome": "exception:NullReferenceException", "ms": 181, "baseMB": 33, "peakMB": 60, "deltaMB": 26, "gcAllocMB": 1, "rep": 1, "wallMs": 248, "rc": 0}
{"lib": "cx", "file": "not_xlsx_text.xlsx", "guard": false, "error": "File contains corrupted data.", "outcome": "exception:FileFormatException", "ms": 89, "baseMB": 33, "peakMB": 53, "deltaMB": 19, "gcAllocMB": 1, "rep": 1, "wallMs": 147, "rc": 0}
{"lib": "cx", "file": "macro_like.xlsx", "guard": false, "sheets": 1, "rows": 2, "cells": 18, "formulas": 0, "formulasNoCache": 0, "errCached": 0, "numericCells": 5, "textCells": 13, "outcome": "ok", "ms": 374, "baseMB": 33, "peakMB": 79, "deltaMB": 45, "gcAllocMB": 8, "rep": 1, "wallMs": 432, "rc": 0}
{"lib": "cx", "file": "formulas_heavy.xlsx", "guard": false, "sheets": 1, "rows": 199, "cells": 199, "formulas": 199, "formulasNoCache": 199, "errCached": 0, "numericCells": 0, "textCells": 0, "outcome": "ok", "ms": 366, "baseMB": 33, "peakMB": 81, "deltaMB": 47, "gcAllocMB": 9, "rep": 1, "wallMs": 423, "rc": 0}
{"lib": "cx", "file": "formulas_heavy.xlsx", "guard": false, "sheets": 1, "rows": 199, "cells": 199, "formulas": 199, "formulasNoCache": 199, "errCached": 0, "numericCells": 0, "textCells": 0, "outcome": "ok", "ms": 383, "baseMB": 33, "peakMB": 81, "deltaMB": 47, "gcAllocMB": 9, "rep": 2, "wallMs": 440, "rc": 0}
{"lib": "dumpval", "file": "formulas_heavy.xlsx", "guard": false, "cells": ["A1 Value => Blank:", "A2 Value => Blank:", "A3 Value => Blank:", "A4 Value => Blank:", "A5 Value => Blank:"], "sheets": 0, "rows": 0, "formulas": 0, "formulasNoCache": 0, "errCached": 0, "numericCells": 0, "textCells": 0, "outcome": "ok", "ms": 374, "baseMB": 33, "peakMB": 80, "deltaMB": 46, "gcAllocMB": 8, "rep": 1, "wallMs": 434, "rc": 0}
{"lib": "dumpval", "file": "formulas_heavy.xlsx", "guard": false, "cells": ["A1 Value => Blank:", "A2 Value => Blank:", "A3 Value => Blank:", "A4 Value => Blank:", "A5 Value => Blank:"], "sheets": 0, "rows": 0, "formulas": 0, "formulasNoCache": 0, "errCached": 0, "numericCells": 0, "textCells": 0, "outcome": "ok", "ms": 371, "baseMB": 33, "peakMB": 80, "deltaMB": 46, "gcAllocMB": 8, "rep": 2, "wallMs": 434, "rc": 0}
{"lib": "cx", "file": "bomb_cells.xlsx", "guard": false, "sheets": 1, "rows": 400, "cells": 6553600, "formulas": 0, "formulasNoCache": 0, "errCached": 0, "numericCells": 6553600, "textCells": 0, "outcome": "ok", "ms": 15613, "baseMB": 34, "peakMB": 732, "deltaMB": 698, "gcAllocMB": 4794, "rep": 1, "wallMs": 15694, "rc": 0}
{"lib": "cx", "file": "bomb_cells.xlsx", "guard": true, "guardResult": "file_too_large", "outcome": "rejected_by_guard", "ms": 4, "baseMB": 33, "peakMB": 41, "deltaMB": 7, "gcAllocMB": 0, "rep": 1, "wallMs": 86, "rc": 0}
{"lib": "cx", "file": "bomb_ratio.xlsx", "guard": false, "outcome": "OutOfMemoryException", "ms": 1570, "baseMB": 33, "peakMB": 1299, "deltaMB": 1265, "gcAllocMB": 1210, "rep": 1, "wallMs": 1670, "rc": 0}
{"lib": "cx", "file": "bomb_ratio.xlsx", "guard": true, "guardResult": "ratio_exceeded", "outcome": "rejected_by_guard", "ms": 6, "baseMB": 33, "peakMB": 42, "deltaMB": 9, "gcAllocMB": 0, "rep": 1, "wallMs": 92, "rc": 0}
{"lib": "guard", "file": "bomb_cells.xlsx", "guard": true, "guardResult": "file_too_large", "outcome": "rejected_by_guard", "ms": 6, "baseMB": 34, "peakMB": 41, "deltaMB": 7, "gcAllocMB": 0, "rep": 1, "wallMs": 93, "rc": 0}
{"lib": "guard", "file": "bomb_ratio.xlsx", "guard": true, "guardResult": "ratio_exceeded", "outcome": "rejected_by_guard", "ms": 6, "baseMB": 33, "peakMB": 42, "deltaMB": 9, "gcAllocMB": 0, "rep": 1, "wallMs": 116, "rc": 0}
{"lib": "guard", "file": "corrupt_nowb.xlsx", "guard": true, "guardResult": "no_workbook", "outcome": "rejected_by_guard", "ms": 5, "baseMB": 33, "peakMB": 42, "deltaMB": 9, "gcAllocMB": 0, "rep": 1, "wallMs": 85, "rc": 0}
{"lib": "guard", "file": "corrupt_random.xlsx", "guard": true, "guardResult": "not_zip_signature", "outcome": "rejected_by_guard", "ms": 4, "baseMB": 33, "peakMB": 41, "deltaMB": 7, "gcAllocMB": 0, "rep": 1, "wallMs": 91, "rc": 0}
{"lib": "guard", "file": "corrupt_truncated.xlsx", "guard": true, "guardResult": "bad_zip", "outcome": "rejected_by_guard", "ms": 6, "baseMB": 33, "peakMB": 42, "deltaMB": 8, "gcAllocMB": 0, "rep": 1, "wallMs": 91, "rc": 0}
{"lib": "guard", "file": "dims_fake.xlsx", "guard": true, "guardResult": "ok", "outcome": "guard_only", "ms": 6, "baseMB": 33, "peakMB": 42, "deltaMB": 9, "gcAllocMB": 0, "rep": 1, "wallMs": 95, "rc": 0}
{"lib": "guard", "file": "formulas.xlsx", "guard": true, "guardResult": "ok", "outcome": "guard_only", "ms": 5, "baseMB": 33, "peakMB": 42, "deltaMB": 9, "gcAllocMB": 0, "rep": 1, "wallMs": 83, "rc": 0}
{"lib": "guard", "file": "formulas_heavy.xlsx", "guard": true, "guardResult": "ok", "outcome": "guard_only", "ms": 5, "baseMB": 33, "peakMB": 42, "deltaMB": 9, "gcAllocMB": 0, "rep": 1, "wallMs": 80, "rc": 0}
{"lib": "guard", "file": "ids.xlsx", "guard": true, "guardResult": "ok", "outcome": "guard_only", "ms": 8, "baseMB": 33, "peakMB": 42, "deltaMB": 9, "gcAllocMB": 0, "rep": 1, "wallMs": 94, "rc": 0}
{"lib": "guard", "file": "macro_like.xlsx", "guard": true, "guardResult": "macro_part", "outcome": "rejected_by_guard", "ms": 5, "baseMB": 33, "peakMB": 42, "deltaMB": 9, "gcAllocMB": 0, "rep": 1, "wallMs": 84, "rc": 0}
{"lib": "guard", "file": "not_xlsx_text.xlsx", "guard": true, "guardResult": "not_zip_signature", "outcome": "rejected_by_guard", "ms": 4, "baseMB": 34, "peakMB": 41, "deltaMB": 7, "gcAllocMB": 0, "rep": 1, "wallMs": 118, "rc": 0}
{"lib": "guard", "file": "realistic_20k.xlsx", "guard": true, "guardResult": "ok", "outcome": "guard_only", "ms": 5, "baseMB": 33, "peakMB": 43, "deltaMB": 9, "gcAllocMB": 0, "rep": 1, "wallMs": 99, "rc": 0}
{"lib": "guard", "file": "sheets_many.xlsx", "guard": true, "guardResult": "ok", "outcome": "guard_only", "ms": 5, "baseMB": 33, "peakMB": 43, "deltaMB": 9, "gcAllocMB": 0, "rep": 1, "wallMs": 94, "rc": 0}
{"lib": "guard", "file": "styles_many.xlsx", "guard": true, "guardResult": "ok", "outcome": "guard_only", "ms": 5, "baseMB": 33, "peakMB": 43, "deltaMB": 9, "gcAllocMB": 0, "rep": 1, "wallMs": 84, "rc": 0}
{"lib": "cx", "file": "realistic_20k.xlsx", "guard": true, "guardResult": "ok", "sheets": 1, "rows": 20001, "cells": 253347, "formulas": 0, "formulasNoCache": 0, "errCached": 0, "numericCells": 40000, "textCells": 213347, "outcome": "ok", "ms": 1933, "baseMB": 33, "peakMB": 304, "deltaMB": 270, "gcAllocMB": 439, "rep": 1, "wallMs": 2011, "rc": 0}
{"lib": "cx", "file": "realistic_20k.xlsx", "guard": true, "guardResult": "ok", "sheets": 1, "rows": 20001, "cells": 253347, "formulas": 0, "formulasNoCache": 0, "errCached": 0, "numericCells": 40000, "textCells": 213347, "outcome": "ok", "ms": 2005, "baseMB": 33, "peakMB": 311, "deltaMB": 277, "gcAllocMB": 446, "rep": 2, "wallMs": 2073, "rc": 0}
{"lib": "cx", "file": "realistic_20k.xlsx", "guard": true, "guardResult": "ok", "sheets": 1, "rows": 20001, "cells": 253347, "formulas": 0, "formulasNoCache": 0, "errCached": 0, "numericCells": 40000, "textCells": 213347, "outcome": "ok", "ms": 2084, "baseMB": 33, "peakMB": 307, "deltaMB": 273, "gcAllocMB": 443, "rep": 3, "wallMs": 2171, "rc": 0}
{"lib": "write", "file": "realistic_20k.xlsx", "guard": false, "writeBackDataType": "Text", "writeBackHasFormula": false, "writeBackValue": "=cmd|' /C calc'!A0", "outBytes": 284198, "sheets": 0, "rows": 0, "cells": 0, "formulas": 0, "formulasNoCache": 0, "errCached": 0, "numericCells": 0, "textCells": 0, "outcome": "ok", "ms": 1212, "baseMB": 33, "peakMB": 159, "deltaMB": 125, "gcAllocMB": 139, "rep": 1, "wallMs": 1281, "rc": 0}
{"lib": "write", "file": "realistic_20k.xlsx", "guard": false, "writeBackDataType": "Text", "writeBackHasFormula": false, "writeBackValue": "=cmd|' /C calc'!A0", "outBytes": 284198, "sheets": 0, "rows": 0, "cells": 0, "formulas": 0, "formulasNoCache": 0, "errCached": 0, "numericCells": 0, "textCells": 0, "outcome": "ok", "ms": 1325, "baseMB": 33, "peakMB": 159, "deltaMB": 125, "gcAllocMB": 139, "rep": 2, "wallMs": 1390, "rc": 0}
```

### results_extra.jsonl (Guard Spike v2 روی ۱۷ فایل + cells_3m/styles_5k/spoof_header)

```jsonl
{"lib": "guard", "file": "bomb_cells.xlsx", "guard": true, "guardResult": "file_too_large", "outcome": "rejected_by_guard", "ms": 5, "baseMB": 34, "peakMB": 43, "deltaMB": 9, "gcAllocMB": 0}
{"lib": "guard", "file": "bomb_ratio.xlsx", "guard": true, "guardResult": "entry_too_large", "outcome": "rejected_by_guard", "ms": 6, "baseMB": 34, "peakMB": 45, "deltaMB": 11, "gcAllocMB": 0}
{"lib": "guard", "file": "cells_3m.xlsx", "guard": true, "guardResult": "entry_too_large", "outcome": "rejected_by_guard", "ms": 6, "baseMB": 34, "peakMB": 45, "deltaMB": 11, "gcAllocMB": 0}
{"lib": "guard", "file": "corrupt_nowb.xlsx", "guard": true, "guardResult": "no_workbook", "outcome": "rejected_by_guard", "ms": 6, "baseMB": 34, "peakMB": 45, "deltaMB": 11, "gcAllocMB": 0}
{"lib": "guard", "file": "corrupt_random.xlsx", "guard": true, "guardResult": "not_zip_signature", "outcome": "rejected_by_guard", "ms": 5, "baseMB": 34, "peakMB": 43, "deltaMB": 9, "gcAllocMB": 0}
{"lib": "guard", "file": "corrupt_truncated.xlsx", "guard": true, "guardResult": "bad_zip", "outcome": "rejected_by_guard", "ms": 34, "baseMB": 34, "peakMB": 44, "deltaMB": 10, "gcAllocMB": 0}
{"lib": "guard", "file": "dims_fake.xlsx", "guard": true, "guardResult": "ok", "outcome": "guard_only", "ms": 23, "baseMB": 34, "peakMB": 47, "deltaMB": 12, "gcAllocMB": 0}
{"lib": "guard", "file": "formulas.xlsx", "guard": true, "guardResult": "ok", "outcome": "guard_only", "ms": 33, "baseMB": 34, "peakMB": 47, "deltaMB": 12, "gcAllocMB": 0}
{"lib": "guard", "file": "formulas_heavy.xlsx", "guard": true, "guardResult": "ok", "outcome": "guard_only", "ms": 19, "baseMB": 34, "peakMB": 46, "deltaMB": 12, "gcAllocMB": 0}
{"lib": "guard", "file": "ids.xlsx", "guard": true, "guardResult": "ok", "outcome": "guard_only", "ms": 25, "baseMB": 34, "peakMB": 47, "deltaMB": 12, "gcAllocMB": 0}
{"lib": "guard", "file": "macro_like.xlsx", "guard": true, "guardResult": "macro_part", "outcome": "rejected_by_guard", "ms": 21, "baseMB": 34, "peakMB": 47, "deltaMB": 12, "gcAllocMB": 0}
{"lib": "guard", "file": "not_xlsx_text.xlsx", "guard": true, "guardResult": "not_zip_signature", "outcome": "rejected_by_guard", "ms": 5, "baseMB": 34, "peakMB": 43, "deltaMB": 9, "gcAllocMB": 0}
{"lib": "guard", "file": "realistic_20k.xlsx", "guard": true, "guardResult": "ok", "outcome": "guard_only", "ms": 22, "baseMB": 34, "peakMB": 46, "deltaMB": 12, "gcAllocMB": 0}
{"lib": "guard", "file": "sheets_many.xlsx", "guard": true, "guardResult": "too_many_sheets", "outcome": "rejected_by_guard", "ms": 24, "baseMB": 34, "peakMB": 46, "deltaMB": 12, "gcAllocMB": 0}
{"lib": "guard", "file": "spoof_header.xlsx", "guard": true, "guardResult": "ok", "outcome": "guard_only", "ms": 26, "baseMB": 34, "peakMB": 47, "deltaMB": 12, "gcAllocMB": 0}
{"lib": "guard", "file": "styles_5k.xlsx", "guard": true, "guardResult": "too_many_styles", "outcome": "rejected_by_guard", "ms": 34, "baseMB": 34, "peakMB": 47, "deltaMB": 13, "gcAllocMB": 0}
{"lib": "guard", "file": "styles_many.xlsx", "guard": true, "guardResult": "too_many_styles", "outcome": "rejected_by_guard", "ms": 50, "baseMB": 34, "peakMB": 47, "deltaMB": 13, "gcAllocMB": 0}
{"lib": "cx", "file": "cells_3m.xlsx", "guard": false, "sheets": 1, "rows": 200, "cells": 3276800, "formulas": 0, "formulasNoCache": 0, "errCached": 0, "numericCells": 3276800, "textCells": 0, "outcome": "ok", "ms": 6774, "baseMB": 34, "peakMB": 423, "deltaMB": 389, "gcAllocMB": 2323}
{"lib": "cx", "file": "styles_5k.xlsx", "guard": false, "sheets": 1, "rows": 20000, "cells": 40000, "formulas": 0, "formulasNoCache": 0, "errCached": 0, "numericCells": 20000, "textCells": 20000, "outcome": "ok", "ms": 2562, "baseMB": 34, "peakMB": 214, "deltaMB": 180, "gcAllocMB": 274}
{"lib": "cx", "file": "spoof_header.xlsx", "guard": false, "error": "Unexpected end of file while parsing Comment has occurred. Line 1, position 2001.", "outcome": "exception:XmlException", "ms": 490, "baseMB": 34, "peakMB": 76, "deltaMB": 42, "gcAllocMB": 7}
{"lib": "cx", "file": "spoof_header.xlsx", "guard": true, "guardResult": "ok", "error": "Unexpected end of file while parsing Comment has occurred. Line 1, position 2001.", "outcome": "exception:XmlException", "ms": 406, "baseMB": 34, "peakMB": 76, "deltaMB": 42, "gcAllocMB": 7}
```

### results_formula_eval.jsonl (آزمون فرمول بدون عنصر `<v>`)

```jsonl
{"lib":"dumpval","file":"formulas_nov.xlsx","guard":false,"cells":["B1 Value => Number:10","B2 Value => Error:#DIV/0!","B3 Value => Number:6","B4 Value => Text:ab","B5 Value => Error:#NAME?"],"sheets":0,"rows":0,"formulas":0,"formulasNoCache":0,"errCached":0,"numericCells":0,"textCells":0,"outcome":"ok","ms":596,"baseMB":34,"peakMB":92,"deltaMB":57,"gcAllocMB":8}
{"lib":"dumpval","file":"formulas_heavy_nov.xlsx","guard":false,"cells":["A1 Value => EXC:ArgumentOutOfRangeException","A2 Value => Blank:","A3 Value => Number:333338333350003","A4 Value => Number:333338333350004","A5 Value => Number:333338333350005"],"sheets":0,"rows":0,"formulas":0,"formulasNoCache":0,"errCached":0,"numericCells":0,"textCells":0,"outcome":"ok","ms":4067,"baseMB":34,"peakMB":746,"deltaMB":712,"gcAllocMB":2011}
{"lib":"cx","file":"formulas_heavy_nov.xlsx","guard":false,"sheets":1,"rows":199,"cells":199,"formulas":199,"formulasNoCache":199,"errCached":0,"numericCells":0,"textCells":0,"outcome":"ok","ms":452,"baseMB":34,"peakMB":81,"deltaMB":47,"gcAllocMB":9}
{"lib":"dumpval","file":"formulas_heavy.xlsx","guard":false,"cells":["A1 Value => Blank:","A2 Value => Blank:","A3 Value => Blank:","A4 Value => Blank:","A5 Value => Blank:"],"sheets":0,"rows":0,"formulas":0,"formulasNoCache":0,"errCached":0,"numericCells":0,"textCells":0,"outcome":"ok","ms":413,"baseMB":34,"peakMB":80,"deltaMB":45,"gcAllocMB":8}
{"lib":"cx","file":"formulas_heavy.xlsx","guard":false,"sheets":1,"rows":199,"cells":199,"formulas":199,"formulasNoCache":199,"errCached":0,"numericCells":0,"textCells":0,"outcome":"ok","ms":490,"baseMB":33,"peakMB":81,"deltaMB":47,"gcAllocMB":9}
```

### results_dump.jsonl (خروجی `dump` روی `ids.xlsx` و `formulas.xlsx`؛ شاهد محورهای ۱۰ و ۱۱ و جدول رفتار سلول)

هر خط یک اجرای `dotnet bin/h.dll dump data/<file>.xlsx` است (یک اجرا، بدون سقف زمانی خاص؛ خروجی خام عیناً).

```jsonl
{"lib":"dump","file":"ids.xlsx","guard":false,"cells":["A1 dt=Text hasF=False cached=[Text:num_fmt5] numFmt=[] id=0 quote=False raw=[num_fmt5]","B1 dt=Text hasF=False cached=[Text:gtin13_num_fmt] numFmt=[] id=0 quote=False raw=[gtin13_num_fmt]","C1 dt=Text hasF=False cached=[Text:text_lead0] numFmt=[] id=0 quote=False raw=[text_lead0]","D1 dt=Text hasF=False cached=[Text:fa_digits_text] numFmt=[] id=0 quote=False raw=[fa_digits_text]","E1 dt=Text hasF=False cached=[Text:ar_digits_text] numFmt=[] id=0 quote=False raw=[ar_digits_text]","F1 dt=Text hasF=False cached=[Text:num_16digits] numFmt=[] id=0 quote=False raw=[num_16digits]","G1 dt=Text hasF=False cached=[Text:hs8_num_fmt] numFmt=[] id=0 quote=False raw=[hs8_num_fmt]","H1 dt=Text hasF=False cached=[Text:plain_num_no_fmt] numFmt=[] id=0 quote=False raw=[plain_num_no_fmt]","I1 dt=Text hasF=False cached=[Text:quote_prefix] numFmt=[] id=0 quote=False raw=[quote_prefix]","A2 dt=Number hasF=False cached=[Number:123] numFmt=[00000] id=-1 quote=False raw=[123]","B2 dt=Number hasF=False cached=[Number:12345678905] numFmt=[0000000000000] id=-1 quote=False raw=[12345678905]","C2 dt=Text hasF=False cached=[Text:00123] numFmt=[] id=0 quote=False raw=[00123]","D2 dt=Text hasF=False cached=[Text:۰۰۱۲۳] numFmt=[] id=0 quote=False raw=[۰۰۱۲۳]","E2 dt=Text hasF=False cached=[Text:٠٠١٢٣] numFmt=[] id=0 quote=False raw=[٠٠١٢٣]","F2 dt=Number hasF=False cached=[Number:1234567890123456] numFmt=[] id=1 quote=False raw=[1234567890123456]","G2 dt=Number hasF=False cached=[Number:1012100] numFmt=[00000000] id=-1 quote=False raw=[1012100]","H2 dt=Number hasF=False cached=[Number:123] numFmt=[] id=0 quote=False raw=[123]","I2 dt=Text hasF=False cached=[Text:00789] numFmt=[] id=0 quote=True raw=[00789]"],"sheets":0,"rows":0,"formulas":0,"formulasNoCache":0,"errCached":0,"numericCells":0,"textCells":0,"outcome":"ok","ms":419,"baseMB":33,"peakMB":79,"deltaMB":45,"gcAllocMB":8}
{"lib":"dump","file":"formulas.xlsx","guard":false,"cells":["A1 dt=Number hasF=False cached=[Number:5] numFmt=[] id=0 quote=False raw=[5]","B1 dt=Blank hasF=True cached=[blank] numFmt=[] id=0 quote=False raw=[A1*2]","B2 dt=Error hasF=True cached=[error:DivisionByZero] numFmt=[] id=0 quote=False raw=[1/0]","A3 dt=Text hasF=False cached=[Text:text] numFmt=[] id=0 quote=False raw=[text]","B3 dt=Number hasF=True cached=[Number:6] numFmt=[] id=0 quote=False raw=[A1+1]","B4 dt=Blank hasF=True cached=[blank] numFmt=[] id=0 quote=False raw=[CONCATENATE(\"a\",\"b\")]","B5 dt=Blank hasF=True cached=[blank] numFmt=[] id=0 quote=False raw=[UNKNOWNFUNC(1)]"],"sheets":0,"rows":0,"formulas":0,"formulasNoCache":0,"errCached":0,"numericCells":0,"textCells":0,"outcome":"ok","ms":426,"baseMB":33,"peakMB":79,"deltaMB":45,"gcAllocMB":8}
```

### results_api_eval.txt (خروجی `apiprobe` روی `formulas_nov.xlsx`؛ شاهد Allow-list محور ۸)

خروجی خام عیناً؛ `cached=[blank]` فقط نشان می‌دهد در لحظهٔ مشاهده مقدار cached خالی بوده است و به‌تنهایی اثبات نمی‌کند فرمول اجرا نشده؛ ممکن است ارزیابی بدون تغییر cache باقی مانده باشد. مقادیر غیرخالی مانند `cached=[10]`/`[ab]` نشان می‌دهند cache به آن مقدار تغییر کرده است.

```text
GetString                B1 cached=[10] B4 cached=[ab]
GetText                  B1 cached=[10] B4 cached=[ab]
TryGetValue<string>      B1 cached=[10] B4 cached=[ab]
GetFormattedString       B1 cached=[10] B4 cached=[ab]
GetBoolean               B1 cached=[10] B4 cached=[ab]
GetDateTime              B1 cached=[10] B4 cached=[ab]
GetError                 B1 cached=[10] B4 cached=[ab]
GetValue<string>         B1 cached=[10] B4 cached=[ab]
GetDouble                B1 cached=[10] B4 cached=[ab]
Value                    B1 cached=[10] B4 cached=[ab]
ws.Search                B1 cached=[10] B4 cached=[ab]
HasFormula+FormulaA1+CachedValue only B1 cached=[blank] B4 cached=[blank]
IsEmpty()                B1 cached=[blank] B4 cached=[blank]
CopyTo                   B1 cached=[blank] B4 cached=[blank]
AsRange().FirstCellUsed  B1 cached=[blank] B4 cached=[blank]
Range.CellsUsed(pred)    B1 cached=[blank] B4 cached=[blank]
```
