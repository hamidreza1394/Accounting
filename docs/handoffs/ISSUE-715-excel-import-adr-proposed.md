# تحویل وظیفه — ISSUE-715-excel-import-adr-proposed

- **Issue:** #715
- **Agent/مسئول:** documentation-engineer (Claude Code)
- **تاریخ:** 2026-11-19 (بازنگری پس از Spike 2 و تصمیم‌های جدید مالک؛ دور دوم و دور سوم (نهایی) اصلاح پس از بازبینی‌های مستقل معماری و امنیت)
- **وضعیت:** انجام‌شده برای مستندات؛ در انتظار بازبینی دور جدید (فقط مستندات؛ همهٔ تصمیم‌ها Proposed/NeedsApproval/Deferred/Blocked)

## هدف و معیارهای پذیرش
- [x] ثبت ۱۵ توافق مالک در ADR D-106 به‌صورت Proposed (نه Accepted).
- [x] سند طراحی با شناسه و وضعیت برای هر تصمیم (EXC-1..EXC-17)، هماهنگ با تصمیم‌های جدید مالک (EXC-2، 5، 12، 13، 14، 16، 17).
- [x] سند مقایسهٔ کتابخانه با ارزیابی ClosedXML در ۱۲ محور، شواهد Spike 2 و پیوست کد.
- [x] «اصلاح برداشت قبلی Lead» دربارهٔ توافق ۱۵ و محل موتور صریح ثبت شد.
- [x] هیچ Accepted جدیدی ایجاد نشده؛ ارجاع به Acceptedهای موجود (D-074، D-084، D-105 بندهای ۱، ۳، ۴، ۸، ۹، D-012، D-082، D-103) فقط استناد است. `git diff origin/main -- docs/DECISIONS.md` (فقط‌خواندنی): فقط دو خط سرآیند (۳ و ۴) و بلوک D-106 (۹۶ خط افزوده) تغییر کرده است.
- [x] بدون هیچ تغییر کد/Schema/Migration/Package/csproj/props/ClientApp؛ `docs/ARCHITECTURE.md` تغییر نکرد.
- [x] WORK_LOG فقط افزوده شد (پنج بخش؛ 38 خط افزوده؛ 0 خط حذف‌شده).

## خلاصه تغییرات
- **محل موتور و Parser (EXC-2، بازنویسی کامل):** کل Excel Import و Mapping متعلق به Trade است. موتور Mapping/Validation و قرارداد مدل سلول در `Trade.Application` (پوشهٔ `Import`، بدون Reference/Package جدید)، Parser و هر کد وابسته به کتابخانهٔ Excel در `Trade.Infrastructure`؛ پروژهٔ مستقل و Plugin ساخته نشود (YAGNI)؛ تست معماری سبک فقط پیشنهاد شد و افزوده نشد. نمودار متنی لایه‌ها و یادداشت مسیر استخراج آینده افزوده شد.
- **اصلاح برداشت قبلی Lead:** برداشت «پروژهٔ خنثی تنها گزینهٔ سازگار با توافق ۱۵» کنار گذاشته شد؛ مالک روشن کرد «فقط Party/Product نباشد» یعنی برای موجودیت‌های مختلف Trade قابل استفاده باشد. همهٔ جاهایی که پروژهٔ مستقل را «لازم» می‌دانستند هماهنگ شد.
- **کتابخانه (EXC-17):** ClosedXML 0.105.1 گزینهٔ اول فعلی مالک (مشروط به Spike و تأیید نهایی)؛ جایگزین Open XML SDK سپس ExcelDataReader؛ شرایط ClosedXML (Guard اجباری، Allow-list برای سلول فرمول‌دار، نبود لغو در نسخهٔ منتشرشده، منع ارجاع مستقیم و Pin SixLabors.Fonts ≥ 2.0، تمرکز نگهداری روی یک نفر) و NotRun بررسی Issueهای باز و GitHub Advisory.
- **سیاست ثبت (EXC-12):** موتور فقط خطاهای ردیفی و دسته‌ای را گزارش می‌کند و دربارهٔ Commit تصمیم نمی‌گیرد؛ پیشنهاد v1 All-or-Nothing در سطح دسته (همچنان تفسیر Lead)؛ ثبت ردیفی در v1 نیست؛ تک‌مصرف‌بودن Preview/Idempotency سطح Import به state ماندگار نیاز دارد و Blocked است.
- **شناسه‌ها (EXC-5/16):** رشته؛ صفر ابتدایی حفظ؛ بازسازی فقط با طول ثابت/قالب صریح در Target Schema وگرنه خطا؛ نرمال‌سازی ارقام فارسی/عربی فقط برای مقایسه و Duplicate Detection با حفظ مقدار اصلی.
- **مجوز (EXC-14):** `trade.form.excelimport.*` در حوزهٔ Trade؛ موتور Authentication/Authorization ندارد؛ `TRADE_IMPORT` هویت تجاری کل ماژول Trade است (نه Entitlement ویژهٔ Excel)؛ بدون AllowAll/bypass؛ نام `data-import` ردشده (فقط یادداشت).
- **سقف‌های نسخهٔ اول (EXC-13):** جدول سقف‌ها با دلیل از Spike 2 (همه Proposed)؛ سقف سلول/ستون NeedsApproval و بدون عدد پیشنهادی؛ بدترین حالت بر مبنای «جمع بازشده ≤ 40MB» و صریحاً «برآوردی/برون‌یابی» (دور سوم: ≈600MB و ≈4.1–4.4s؛ اعداد قبلی ≈635MB/≈4.5–5s و نسخهٔ اول ≈3–4s/≈450MB بر مبنای 30MB حذف شدند).
- **سند مقایسه:** بازساختار به: جدول مقایسه، ارزیابی ClosedXML (۱۲ محور)، Spike 2، Spike 1 قدیمی با برچسب «شرایط متفاوت، تک‌اجرایی، غیرقابل‌مقایسهٔ مستقیم»، مقایسهٔ نهایی با چهار معیار، NotRun، پیوست الف (Spike 1) و پیوست ب (کد Spike 2).

## دور دوم اصلاح (پس از دو بازبینی مستقل معماری و امنیت)
- **اصلاح ادعای قبلی Lead:** خواندن `Value` فرمولِ بدون عنصر `<v>` در ClosedXML فرمول را اجرا می‌کند (شاهد `results_formula_eval.jsonl`: 199 فرمول سنگین، ۵ سلول ⇒ 4067ms و Peak 746MB؛ همان فایل با `HasFormula`+`CachedValue` ⇒ 452ms و 81MB). ادعای «Blank» فقط برای `<v></v>` خالی درست بود. قواعد Parser (در دور سوم به Allow-list تبدیل شد؛ پایین؛ تست قرارداد با فرمول بدون `<v>`) در EXC-4، EXC-6، EXC-13، EXC-17، طراحی بخش‌های ۹ و ۱۲ و محور ۸ سند مقایسه آمد.
- **Guard Spike (v2) نهایی نیست:** با تغییر نام part دور زده می‌شود (آزمون مستقل بازبین؛ خروجی خام در پیوست پ نیست)؛ «Guard پیشنهادی» با resolve رابطه‌ها، رد externalLink و شمارش بایت واقعی بازشده با Stream محدودکننده پیشنهاد شد (NotRun). صرفِ `CompressedLength > Length` معیار رد نیست، چون ممکن است فایل معتبر دارای محتوای از قبل فشرده را رد کند.
- **ردپای شواهد:** `results.jsonl` = نسخهٔ قبلی Guard؛ `results_extra.jsonl` = Guard Spike v2؛ `results_formula_eval.jsonl` = آزمون فرمول؛ هر سه در پیوست پ سند مقایسه آمدند (در دور سوم `results_dump.jsonl` و `results_api_eval.txt` هم افزوده شد: پنج فایل خام). اعداد بدون ردپای خام (6947ms برای `cells_3m`؛ 2072–2178ms برای `styles_5k`) با 6774ms و 2562ms جایگزین شدند.
- **اندازهٔ اعلامی Entry:** فقط حالت «کوچک‌تر از واقعی» آزموده شد؛ Entry واقعاً 314,573,013 بایت (رمزگشایی deflate توسط مستندساز) با اندازهٔ اعلامی 2,000 در هر دو هدر؛ ClosedXML با `XmlException` شکست: 490ms بدون Guard، 406ms با Guard (فایل خام)، 337ms (بازبین مستقل). عدد 360–388ms که در ابلاغ آمده بود در فایل خام نیست و ننوشته شد.
- **بدترین حالت:** مبنا از 30MB هر Entry به سقف مؤثر «جمع بازشده ≤ 40MB» تغییر کرد: (دور دوم؛ در دور سوم اصلاح شد: ≈600MB و ≈4.1–4.4s برای یک Import، ≈1.2GB برای دو Import؛ برون‌یابی؛ بدترین حالت ترکیبی NotRun)؛ سقف هم‌زمانی ≤ 2 پیشنهادی و نیازمند اندازه‌گیری.
- **Preview/Commit:** تناقض داخلی رفع شد: Commit تا رفع مسدودی کامل Blocked؛ گزینه‌های آینده (توکن Preview با Data Protection؛ آپلود دوباره + hash با محدودیت؛ Blocked ماندن) فقط Proposed/NeedsApproval.
- **«جریانی»** فقط برای API موتور؛ Parser مبتنی بر ClosedXML جریانی نیست. **Timeout/لغو:** Request Timeout پاسخ را قطع می‌کند ولی Parse ادامه می‌یابد؛ Parse در فرایند جدا با kill گزینهٔ NeedsApproval. **XXE:** DTD داخلی ⇒ `XmlException` (آزمون بازبین امنیتی)؛ externalLink/DDE NotRun.
- **Store/تراکنش و ایندکس‌ها (با خواندن کد):** `PartyStore.AddAsync` (خطوط 76–89) و `ProductStore.AddAsync` (از خط 65) هرکدام `SaveChangesAsync` می‌زنند؛ Commit همه‌یاهیچ به مسیر جدید Store و تراکنش صریح نیاز دارد؛ ایندکس‌های یکتای `(TenantId, Code)` و `(TenantId, Gtin)`/`(TenantId, Irc)` از قبل هست (TenantId تاریخی)؛ تشخیص تکراری نسبت به DB: Use Case کلیدهای موجود را به موتور می‌دهد (به‌جای «پورت» داخل موتور).
- **جزئیات Low:** `TRADE_IMPORT` هویت تجاری کل ماژول Trade (`TradeModuleIdentity.cs`) است نه Entitlement Excel؛ پسوند مجوز باید در `IsAccessIntentCompatible` طبقه‌بندی شود؛ ترتیب D-074 بند ۳ شامل FiscalPeriod/DataScope؛ `TradeAccessContext.TenantId` تا S3-0 legacy؛ «رد، نه Upsert» تفسیر Lead؛ جهت FK در خطوط 996 و 1068 دقیق نقل شد؛ لحن‌های قطعی در بخش‌های Proposed نرم شد؛ وابستگی‌های netstandard2.0 ClosedXML (`Microsoft.Bcl.HashCode`، `System.Buffers`، `System.Memory`) و معیار غیرانتفاعی Split License افزوده شد.

## دور سوم (نهایی) اصلاح
- **Allow-list (S1):** برای سلول با `HasFormula` فقط `HasFormula`، `FormulaA1`، `CachedValue`، `DataType`، `Style.NumberFormat` و `Address` مجازند؛ هر API دیگر ممنوع. شاهد `apiprobe` (`results_api_eval.txt`، خام در پیوست پ؛ کد در پیوست ب؛ `formulas_nov.xlsx` سلول‌های B1/B4؛ معیار تغییر `CachedValue`): 11 API فرمول را اجرا کردند (`GetString`، `GetText`، `TryGetValue<string>`، `GetFormattedString`، `GetBoolean`، `GetDateTime`، `GetError`، `GetValue<string>`، `GetDouble`، `Value`، `ws.Search`) و 5 مورد نه. محدودیت: دو سلول، یک فایل؛ `Address` در probe نبود (NotRun).
- **Guard (S2، S7):** «Guard Spike v2» (اجراشده) و «Guard پیشنهادی» (NotRun) جدا نام‌گذاری شدند؛ شکاف‌های پیشنهاد (نرمال‌سازی نام‌های OPC، `TargetMode="External"`، رد externalLink) ثبت شد.
- **اعداد (S3، S4):** مجموع بازشدهٔ `realistic_20k.xlsx` از جمع اندازهٔ Entryها (محاسبهٔ مستندساز): 18,865,100 بایت (≈18MiB) و نه 17MB؛ برآورد 40MB: ≈600MB و ≈4.1–4.4s (برون‌یابی خطی ×40/18 فقط برای محتوای واقع‌نما؛ اندازه‌گیری نشده)، دو Import ≈1.2GB؛ shared-strings-heavy NotRun.
- **شرط اعتبار سقف‌ها (S5):** قطع خواندن Entry در اندازهٔ اعلامی فقط در یک runtime و یک فایل دیده شد؛ در صورت نبود آن، شمارش بایت واقعی لازم است (NeedsApproval).
- **لغو (S6):** `CancellationToken` فقط بین مراحل؛ لغو Host حین بارگذاری CPU را آزاد نمی‌کند (استنتاج)؛ توقف قطعی فقط با فرایند جدا و kill (NeedsApproval).
- **Commit/توکن (A3، A4، S8):** شرط رفع مسدودی: S3-0 و D-105 و (state سمت سرور یا تأیید صریح گزینهٔ توکن)؛ انقضا داخل توکن بی‌state ممکن است؛ فقط تک‌مصرف‌بودن و Idempotency سطح Import Blocked‌اند. پیش‌شرط‌ها: Host فراخوانی صریح `AddDataProtection`/`PersistKeysTo*` ندارد (جست‌وجو در `Server`: فقط `DatabaseConnectionSettingsStore.cs`)؛ `WorkContextCookie` از `IDataProtectionProvider` تزریقی استفاده می‌کند؛ توکن فقط در حافظهٔ کلاینت (D-012)؛ Tenant/Company فقط برای تطبیق برابری با Context سرور (D-013).
- **سایر (A1، A2، A5، A6):** «لازم دانسته نمی‌شود (NeedsApproval)» به‌جای «لازم نیست»؛ `TRADE_IMPORT` هویت کل ماژول؛ EXC-15 «برای خود موتور»؛ هر دو خط سرآیند DECISIONS ذکر شد؛ شمارش WORK_LOG اصلاح شد.
- **تناقض/ابهام حل‌نشده:** نیاز به تأیید مالک برای انتخاب بین توکن و state سمت سرور؛ سقف سلول/ستون بدون شاهد نوشته نشد. در صورت انتخاب توکن، binding باید ConsumerOperation و TargetSchemaId پایدار را نیز مقید کند.

## فایل‌های مهم تغییرکرده
- `docs/DECISIONS.md` — فقط بلوک D-106 (ردیف‌های EXC-1، 2، 4، 5، 6، 12، 13، 14، 15، 16، 17؛ توافق ۱۵؛ زمینه؛ بخش اصلاح برداشت؛ گزینه‌ها؛ پیامدها؛ خط تاریخ بازنگری) و **هر دو خط سرآیند 3 و 4**. متن هیچ ADR دیگری تغییر نکرد.
- `docs/EXCEL_IMPORT_DESIGN.md` — بخش‌های محل موتور/Parser و نمودار، تعارض‌ها (۱۳ تا ۱۵ جدید و بازنویسی ۱ و ۱۰)، پرسش‌های باز، فایل‌های بزرگ/حافظه، امنیت فایل و جدول سقف‌ها، شناسه‌ها، مجوز، تصمیم‌های باز، جدول EXC، مراحل آینده، راستی‌آزمایی‌نشده.
- `docs/EXCEL_IMPORT_LIBRARY_COMPARISON.md` — بازنویسی ساختار؛ پیوست ب (کد Spike 2 + `run_extra.py` + روش ساخت `*_nov`) و پیوست ب (شامل `apiprobe`) و پیوست پ (نتایج خام پنج فایل: چهار JSONL و `results_api_eval.txt`).
- `docs/INDEX.md` — توضیح ردیف‌های Excel Import هماهنگ شد.
- `docs/WORK_LOG.md` — پنج بخش جدید در انتها، 38 خط افزوده و 0 خط حذف‌شده (چیزی حذف یا بازنویسی نشد).
- این handoff.

## تصمیم‌های فنی
- تصمیم معماری `Accepted` جدیدی گرفته نشد. D-106 و EXC-1..EXC-17 `Proposed`/`NeedsApproval`/`Deferred` (و برخی Blocked) هستند؛ وضعیت‌های D-082، D-103، D-105 و سایر ADRها تغییر نکرد.
- شمارهٔ D-106 از انتهای `docs/DECISIONS.md` خوانده شده بود (آخرین ADR قبلی D-105).
- هیچ Schema، Migration، Entity، API، UI، Sidebar یا محل ذخیرهٔ Mapping/Preview پیشنهاد نشد؛ TenantId/CompanyId هرگز از فایل/کلاینت نمی‌آید (D-103 بند ۹، D-105 بند ۸).

## تعارض‌ها و ابهام‌ها (حل‌نشده؛ تفصیل در `EXCEL_IMPORT_DESIGN.md` بخش ۱۹)
- `docs/ARCHITECTURE.md` خط 173 (Trade به حسابداری/Server/Infrastructure حسابداری Reference نمی‌دهد): **فقط گزارش؛ آن سند تغییر نکرد.** با خواندن `.csproj`ها (`Trade.Application` فقط ProjectReference به `Trade.Domain`، بدون PackageReference) تناقضی با جای‌گذاری موتور در `Trade.Application` و Parser در `Trade.Infrastructure` دیده نشد؛ نکتهٔ باز: PackageReference کتابخانهٔ Excel در `Trade.Infrastructure` به تأیید EXC-17 وابسته است.
- نبود لغو (CancellationToken) در ClosedXML 0.105.1 منتشرشده؛ ریسک مجوز SixLabors.Fonts 2.x (Split License)؛ هر دو در جدول تعارض‌ها.
- `docs/TRADE_DESIGN_V3_2.md` §12 (V3.2 موجودیت Mapping ندارد)، `docs/TRADE_S3_READINESS.md` («بعد از S3»)، وضعیت Combobox در D-082/`UI_COMPONENTS.md` در برابر `SearchableCombobox.tsx`، D-085/D-090، D-105 (مالکیت)؛ بدون تغییر نسبت به نسخهٔ قبلی.
- Guard Spike v2 به نام part و اندازهٔ اعلامی Entry تکیه دارد: نام part دور زده می‌شود (آزمون بازبین)، اندازهٔ اعلامی فقط در حالت «کوچک‌تر از واقعی» آزموده شد؛ قاعدهٔ نسبت و تعداد Entry در Guard Spike v2 هیچ فایل Spike تنها را رد نکردند.
- متن اصلی مأموریت مالک (۱۵ توافق، وضوح‌بخشی دربارهٔ توافق ۱۵، «الزام دائمی Sidebar») در مخزن نیست و از ابلاغ Lead نقل شده؛ مستقل راستی‌آزمایی نشد.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet build ...` / `dotnet test ...` | اجرا نشد | فقط مستندات؛ Build/Test کد اجرا نشد |
| `git diff --check` | موفق | کد خروج 0 (دور اول یک فاصلهٔ انتهای خط داشت و رفع شد؛ دور دوم خروجی پاک) |
| `python3 scripts/check_static_assets.py` | موفق | `PASS: no remote CDN asset references found in application-owned pages, CSS, or JS.`؛ کد خروج 0 |
| `git diff origin/main -- docs/DECISIONS.md` (فقط‌خواندنی) | موفق | فقط دو خط سرآیند و بلوک D-106 تغییر کرده است |
| بازتولید `formulas_nov`/`formulas_heavy_nov` | موفق (مستندساز، خارج از مخزن) | همهٔ partهای خروجی با فایل‌های artifact یکسان؛ برای پیوست ب |
| رمزگشایی Entry فایل `spoof_header.xlsx` | موفق (مستندساز، فقط‌خواندنی) | اندازهٔ واقعی 314,573,013 بایت؛ اعلامی 2,000 در Central Directory و Local Header |
| محاسبهٔ مجموع اندازهٔ Entryها (`zipfile`، فقط‌خواندنی؛ مستندساز، دور سوم) | موفق | `realistic_20k`: 18,865,100 بایت؛ `cells_3m`: 83,302,216؛ `bomb_cells`: 168,357,216؛ `styles_5k`: 3,406,631 |
| `git diff --check`، `check_static_assets.py` (دور سوم، پس از همهٔ ویرایش‌ها) | موفق | کد خروج 0 و 0؛ `PASS: no remote CDN asset references found in application-owned pages, CSS, or JS.` |
| `git diff origin/main -U0 -- docs/DECISIONS.md \| grep '^@@'` (دور سوم) | موفق | فقط `@@ -3,2 +3,2 @@` و `@@ -2800,0 +2801,96 @@` |
| `git diff origin/main -- docs/WORK_LOG.md \| grep -c '^-[^-]'` (دور سوم) | موفق | 0 خط حذف‌شده |
| Spike 2 (کتابخانه‌ها، Guard، فایل‌های ساختگی) | اجرا توسط Lead، نه این Agent | شواهد از `SPIKE2-SUMMARY.md` نقل شده و دوباره اجرا یا راستی‌آزمایی نشد؛ ۳ تکرار روی فایل اصلی، ۱ تکرار بقیه؛ یک ماشین ۲ هسته‌ای؛ نه Benchmark رسمی |

**اجرا نشد (NotRun):** Build/Test .NET و ClientApp؛ تست معماری؛ رفتار لغو ClosedXML در نسخهٔ بعدی؛ هم‌زمانی چند Import؛ Windows؛ فایل واقعی مشتری؛ Issueهای باز و GitHub Advisory ClosedXML؛ Open XML SDK و ExcelDataReader روی فایل مخرب/Style/سلول؛ «Guard پیشنهادی» (resolve رابطه‌ها، نرمال‌سازی نام‌های OPC)؛ پذیرش نسخهٔ ClosedXML با لغو؛ `Address` و API های خارج از 16 مورد probe روی سلول فرمول‌دار؛ محتوای پر از shared strings؛ حالت‌های دیگر دست‌کاری هدر ZIP؛ externalLink/DDE و Entity خارجی؛ بدترین حالت ترکیبی (40MB + حداکثر Style + حداکثر Sheet)؛ اندازه‌گیری مستقیم سقف سلول/ستون؛ تاریخ در Spike 2؛ آزمون‌های «آزمون مستقل بازبین؛ خروجی خام در پیوست پ نیست»: تغییر نام part (2249ms/184MB)، DTD ⇒ `XmlException`، `GetFormattedString`/`A50` ⇒ cached برابر 2، `styles_5k` (2035ms)، `spoof_header` (337ms)، فهرست API گسترده‌تر قبل از probe؛ این Agent آن‌ها را ندید. (probe اجراشدهٔ `results_api_eval.txt` خام در پیوست پ هست.)

## بازبینی
- نتیجهٔ دور اول (نقل از ابلاغ Lead؛ این Agent گزارش کامل بازبین‌ها را ندید): بازبینی معماری و امنیت (opus): هیچ Critical؛ High: دور زدن Guard با تغییر نام part، اجرای فرمول بدون `<v>` با `Value`، ناسازگاری ردپای شواهد؛ همه در این نسخه اصلاح/ثبت شد. بخش «تأییدشده‌ها»ی دو بازبینی به این Agent داده نشد و خلاصه نشد؛ از آزمون‌های مستقل فقط DTD ایمن، `spoof_header` (337ms) و `styles_5k` (2035ms) در ابلاغ آمده بود (همه با برچسب «آزمون مستقل بازبین؛ خروجی خام در پیوست پ نیست»).
- دور دوم: یافته‌های دو بازبینی مستقل (نقل از ابلاغ Lead؛ گزارش کامل به این Agent داده نشد) در دور سوم اعمال شد؛ تفصیل در بخش «دور سوم».
- **در انتظار بازبینی دور جدید (معماری و امنیت).**

## امنیت، داده و سازگاری
- هیچ اثر روی کد، امنیت، احراز هویت، مجوز، داده یا Migration. هیچ دادهٔ/فایل واقعی مشتری در مستندات نیست؛ مثال‌ها و فایل‌های Spike ساختگی‌اند.
- طراحی بر D-103 بند ۹ و D-105 بند ۸ (Scope از Context سرور، نه فایل) استوار است.
- سیاست مجوز: بدون AllowAll/Development bypass و بدون تغییر `DenyAllTradeModuleEntitlement` (فقط مستندات).

## ریسک‌ها و کارهای باقی‌مانده
- تأیید مالک: EXC-2، 5، 12، 13 (اعداد)، 14، 17 (ClosedXML مشروط به Spike).
- ریسک‌های ClosedXML: اجرای فرمول با 11 API از 16 API آزمایش‌شده (DoS؛ شاهد probe)؛ Guard Spike v2 دورزدنی؛ نبود لغو در نسخهٔ منتشرشده؛ مجوز SixLabors.Fonts (تحلیل حقوقی نیست؛ تأیید مالک/مشاور)؛ تمرکز نگهداری روی یک نفر؛ فاصلهٔ انتشار؛ بررسی Issueهای باز/Advisory NotRun.
- Persistence Mapping/Preview و تک‌مصرف‌بودن/Idempotency سطح Import Blocked تا S3-0 و تصمیم مالکیت (D-105)؛ Commit تا S3-0 و D-105 و (state سمت سرور یا تأیید گزینهٔ توکن Data Protection) Blocked است؛ تست دستی UI تا #401/تصمیم امنیتی Blocked.
- سقف سلول/ستون و مقدار Timeout هنوز تعیین‌نشده‌اند؛ سقف هم‌زمانی ≤ 2 پیش از تثبیت باید اندازه‌گیری شود.
- «Guard پیشنهادی» و تست‌های رگرسیون (تغییر نام part، فرمول بدون `<v>`، اندازهٔ دروغین هدر) کار PR Parser است.
- اسکریپت تولید `spoof_header.xlsx` و اسکریپت اصلی `*_nov` ذخیره نشده‌اند.
- پیشنهاد تست معماری سبک برای جهت وابستگی در PR پیاده‌سازی موتور (در این PR افزوده نشد).
- مصرف توکن Agentها: نامشخص (ابزار گزارش نداد).

## شناسه‌های Git
- Branch: docs/excel-import-adr-proposed (مبنا main@6f539679؛ Commit فعلی شاخه 9cd7c794؛ تغییرات این بازنگری هنوز Commit نشده)
- Commit: ایجاد نشده (Lead انجام می‌دهد)
- Pull Request: #716 (طبق ابلاغ Lead)
