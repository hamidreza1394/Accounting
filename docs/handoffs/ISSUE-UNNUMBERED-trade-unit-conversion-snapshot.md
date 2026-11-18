# تحویل وظیفه — ISSUE-UNNUMBERED-trade-unit-conversion-snapshot

- **Issue:** `UNNUMBERED` (Epic مرتبط: #397 و S3 Trade؛ شماره جعلی ساخته نشد)
- **Agent/مسئول:** Claude (Trade؛ مستقل از کار GLM روی Vendor)
- **تاریخ:** 2026-11-18
- **وضعیت:** انجام‌شده برای این برش (فقط دامنه و تست، بدون Schema)؛ CI در PR گزارش می‌شود

## هدف و معیارهای پذیرش
- [x] واحد اصلی کالا (`BaseUnitCode`) اجباری و جدا از واحد معامله (`TradeUnitCode`) باشد.
- [x] هر کالا بتواند چند واحد تجاری مجاز با ضریب تبدیل مختص همان کالا داشته باشد؛ واحد بدون ورودی در مجموعه «غیرمجاز» باشد.
- [x] تبدیل دقیق (نمونه: TON به KG) با `decimal`؛ دقت اعشار و قاعدهٔ گردکردن صریح و قابل کنترل.
- [x] واحد انتخاب‌شده، ضریب، قاعدهٔ گردکردن و مقادیر محاسبه‌شده در یک Snapshot تغییرناپذیر ثبت شود؛ تغییر بعدی ضریب Snapshot قبلی را عوض نکند.
- [x] بدون سامانهٔ عمومی تبدیل واحد، بدون Schema/Migration/API/UI، بدون وابستگی به نگاشت Tenant/Company.

## خلاصه تغییرات
- `TradeUnitConversion.TryCreate(...)`: ضریب (مقدار واحد اصلی در یک واحد معامله) و `TradeRoundingPolicy` صریح. ضریب مثبت، حداکثر ۶ رقم اعشار و حداکثر `1_000_000`؛ اگر دو واحد یکی‌اند ضریب باید دقیقاً ۱ باشد. سیاست گردکردن پیش‌فرض ندارد (`default` رد می‌شود)؛ تعداد اعشار ۰..۶ و حالت یکی از `HalfUp`، `HalfEven`، `Truncate`.
- `TradeUnitConversion.TryConvert(quantity)`: مقدار مثبت، حداکثر ۶ رقم اعشار و حداکثر `1_000_000_000`؛ ضرب با `decimal`؛ یک بار گردکردن؛ مقدار مثبتی که در واحد اصلی صفر می‌شود رد می‌شود (`ResultRoundsToZero`).
- `TradeQuantitySnapshot`: رکورد تغییرناپذیر شامل واحد و مقدار معامله، واحد اصلی، ضریب، مقدار واحد اصلی، تعداد اعشار و حالت گردکردن. هر ورودی در خود Snapshot کپی می‌شود.
- `ProductUnitConversionSet`: مجموعهٔ واحدهای مجاز یک کالا؛ واحد اصلی خودش فقط با ورودی صریح (ضریب ۱) مجاز است؛ رد واحد تکراری، ورودی با واحد اصلی دیگر و مجموعهٔ خالی.
- خطاها فقط یک مقدار `TradeUnitConversionError` هستند و هرگز مقدار ورودی را بازتاب نمی‌دهند (همان الگوی `TradeDocumentNumber`).
- سقف‌های عددی صرفاً برای دقیق‌ماندن ضرب `decimal` هستند (حداکثر ۱۶ و ۱۳ رقم بدون مقیاس؛ حاصل ≤ ۲۸ رقم)، نه قاعدهٔ کسب‌وکار.

## فایل‌های مهم تغییرکرده
- `Modules/Trade/Trade.Domain/Units/TradeUnitConversion.cs`، `TradeRoundingPolicy.cs`، `TradeQuantitySnapshot.cs`، `ProductUnitConversionSet.cs`، `TradeUnitConversionError.cs` — نوع‌های دامنه (جدید)
- `tests/Template.Tests/TradeUnitConversionTests.cs` — ۵۷ تست (جدید؛ فقط داده ساختگی)
- `docs/handoffs/ISSUE-UNNUMBERED-trade-unit-conversion-snapshot.md` — این گزارش (جدید)

## تصمیم‌های فنی
- تصمیم معماری جدیدی گرفته نشد. این برش فقط منطق خالص دامنه است؛ ذخیرهٔ واحدهای تجاری هر کالا و ستون‌های Snapshot روی ردیف Proforma جدول و ستون جدید می‌خواهد و تا تأیید نقشهٔ Tenant/Company (D-087، D-090) Blocked است.
- بررسی «کد واحد در فهرست مرجع هست» عمداً در دامنه نیست (دامنه به Application وابسته نیست)؛ لایهٔ Application هنگام ساخت مجموعه با `TradeReferenceData.TryFindUnit` اعتبارسنجی می‌کند. تست توافق: همهٔ کدهای `TradeReferenceData.Units` کد معتبر تبدیل‌اند.
- `Product.BaseUnitCode` موجود بدون تغییر ماند؛ ارتباط `ProductUnitConversionSet.BaseUnitCode` با آن هنگام ذخیره‌سازی (برش بعدی) برقرار می‌شود.
- «واحد پیش‌فرض Proforma» ساخته نشد: مرجع آن (سطح Proforma، شرکت یا کالا) در مأموریت صریح نیست و حدس زده نشد.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet restore Template.sln` | موفق | |
| `dotnet build tests/Template.Tests/Template.Tests.csproj -c Release --no-restore` | موفق | ۰ خطا |
| `dotnet test --project tests/Template.Tests/Template.Tests.csproj -c Release --no-build --filter-class Template.Tests.TradeUnitConversionTests` | موفق | ۵۷ موفق، ۰ شکست، ۰ Skip |
| همهٔ کلاس‌های `*Trade*` بدون SQL Server (`--filter-class "*Trade*"`) | ۳۱۰ موفق، ۰ شکست، ۲۶ Skip | ۲۶ Skip تست SQL Server بودند (متغیر `FARANOU_TEST_SQLSERVER` آن لحظه تنظیم نبود)؛ در اجرای کامل پایین‌تر با SQL Server واقعی اجرا شدند |
| آزمون جهش | ۳ جهش، هر سه شکست ایجاد کردند | جایگزینی `HalfEven` با `AwayFromZero`؛ حذف رد «گرد به صفر»؛ حذف قاعدهٔ ضریب ۱ برای واحد یکسان؛ پس از هر جهش کد برگردانده شد |
| `dotnet build Template.sln --no-restore -c Release` | موفق | ۰ خطا، ۳۶۷۳ هشدار (هشدارهای تحلیل‌گر موجود در Server/Trade؛ هیچ‌کدام از فایل‌های جدید نیست) |
| `dotnet test --solution Template.sln --no-build -c Release` با SQL Server 2022 واقعی (کانتینر موقت `scripts/dev/test-sqlserver.sh up`، پس از اجرا حذف می‌شود) | موفق | ۱۶۲۱ تست: ۱۶۱۱ موفق، ۰ شکست، ۱۰ Skip (همگی Windows CNG؛ فقط روی Windows اجرا می‌شوند) |
| `python3 scripts/check_static_assets.py` | موفق | |
| `git diff --check` | موفق | |
| ClientApp (`npm ci`، `typecheck`، `test`، `build`، `check:offline`) | اجرا نشد | کلاینت و API تغییر نکرده است |

## امنیت، داده و سازگاری
- هیچ تغییری در احراز هویت، مجوز، Tenant/Company، دیتابیس یا Migration نیست؛ هیچ `TenantId`/`CompanyId` اضافه یا تغییر نکرده است.
- هیچ فایل Vendor، Core یا فایل مشترک (`DECISIONS`، `PROJECT_STATUS`، `WORK_LOG`، Solution) تغییر نکرد.
- در فایل‌ها داده واقعی مشتری یا Excel نیست.

## ریسک‌ها و کارهای باقی‌مانده
- هنوز Entity مصرف‌کننده‌ای ندارد؛ مصرف در S3 (ردیف Proforma و ذخیرهٔ واحدهای کالا) پس از بسته‌شدن گیت Tenant/Company.
- ذخیرهٔ `ProductUnitConversionSet` (جدول واحدهای تجاری کالا) و ستون‌های Snapshot: `Blocked` تا تأیید Schema.
- پرسش باز برای مالک: واحد پیش‌فرض Proforma در کدام سطح تنظیم شود؛ و آیا سقف‌های `MaxFactor`/`MaxQuantity` برای کالاهای واقعی کافی‌اند.
- ردیف `docs/WORK_LOG.md` طبق قاعدهٔ «عدم تغییر فایل مشترک هم‌زمان» (و برای جلوگیری از تعارض بین دو PR مستقل این مأموریت) به یک PR هماهنگ‌شده موکول شد (مانند `trade-s3-document-number`).

## شناسه‌های Git
- Branch: `feat/trade-unit-conversion-snapshot`
- Commit: در PR ثبت می‌شود
- Pull Request: در PR ثبت می‌شود
