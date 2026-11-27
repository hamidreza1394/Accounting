# تحویل وظیفه — ISSUE-802-vendor-db-ownership-safety

- **Issue:** [#802](https://github.com/hamidreza1394/Accounting/issues/802) — بخش PR-1 از مجموعهٔ ۵ PR
- **Agent/مسئول:** GLM (Autonomous Engineering)
- **تاریخ:** 2026-11-27
- **وضعیت:** انجام‌شده (پایهٔ کد + تست‌ها)؛ وضعیت کارکرد واقعی روی SQL Server مالک: Not yet Verified

## هدف و معیارهای پذیرش (این PR)
- [x] اعتبارسنجی نام دیتابیس با قواعد مسیر مشتری (نویسه‌ها، طول، NFKC، نام‌های سیستمی/هم‌ریخت)
- [x] Quote امن شناسهٔ SQL با escape سهوی کروشه
- [x] Allow-list مقصد SQL: پیش‌فرض فقط محلی، Remote فقط با Allow-list دقیق، بدون wildcard، بدون Hardcode نام ماشین
- [x] نشانهٔ مالکیت `FaranouVendorOwnership` با اعتبارسنجی مقدار (نشانهٔ جعلی ⇒ Fail Closed)
- [x] تشخیص دیتابیس Foreign/Customer و دیتابیس خالی ناشناس به‌صورت Fail Closed؛ استثنای واحد، Recovery کنترل‌شده فقط با رکورد قصدِ تازهٔ هم‌نام + اتصال create_date، و بدون هیچ DROP
- [x] پنجرهٔ شکست CREATE DATABASE ⇒ دیتابیس حذف نمی‌شود؛ گزارش «ناشناس» و Recovery کنترل‌شده با رکورد قصد
- [x] تست واحد مربوط + تست SQL Integration گیت‌دار
- [ ] اجرای واقعی روی SQL Server مالک (پس از تحویل، با مجوز عملیاتی جداگانه)

## خلاصه تغییرات
- فایل جدید `VendorDevelopmentDatabaseSafety.cs` با پنج بلوک خالص: `VendorSqlIdentifier`، `VendorDatabaseName`، `VendorRuntimeLoginName`، `VendorSqlServerTarget`، `VendorOwnershipMarker` و `VendorProvisioningIntent`.
- گارد مقصد IP غیر-loopback فقط از مسیر Allow-list دقیق عبور می‌کند و wildcard در خود Allow-list هم اثر ندارد (تقویت نسبت به پیش‌نویس شاخهٔ agent)؛ جفت‌بودن کروشه‌ها و نام‌ساز بودن نمونه نیز پیش از تطبیق میزبان اعتبارسنجی می‌شود.
- نشانهٔ مالکیت سه‌حالته شد (`Absent/Valid/InvalidValue`) تا «Marker جعلی یا ناسازگار، شامل مقدار SQL NULL، ⇒ Fail Closed» تضمین شود.
- رکورد قصد راه‌اندازی (Extended Property روی `master`) برای Recovery کنترل‌شدهٔ پنجرهٔ شکست بین CREATE و ثبت نشانه اضافه شد؛ دیتابیس در هیچ حالتی Drop نمی‌شود. Recovery فقط با سه شرط هم‌زمان مجاز است: رکورد تازهٔ هم‌نام (مقایسهٔ حساس به حروف)، دیتابیس خالی، و اتصال زمانی — `create_date` ثبت‌شدهٔ سرور نباید قدیمی‌تر از زمان ثبت رکورد باشد. رکورد آینده‌نگر، رکورد خراب یا دیتابیس قدیمی‌تر از رکورد، Fail Closed می‌شود؛ رکورد به‌تنهایی اثبات مالکیت نیست.
- `InternalsVisibleTo "Vendor.Host.Tests"` برای تست‌پذیری این primitives اضافه شد.
- تصمیم معماری با شناسهٔ D-122 در `docs/DECISIONS.md` به‌صورت `Proposed` ثبت شد (تأیید مالک لازم است؛ Accepted جعل نشده است).

## فایل‌های مهم تغییرکرده
- `Vendor/Vendor.Host/Development/VendorDevelopmentDatabaseSafety.cs` — جدید؛ primitives مالکیت/مقصد/Recovery
- `Vendor/Vendor.Host/Vendor.Host.csproj` — فقط `InternalsVisibleTo`
- `tests/Vendor.Host.Tests/VendorDevelopmentDatabaseSafetyTests.cs` — جدید؛ تست واحد خالص
- `tests/Vendor.Host.Tests/VendorDevelopmentDatabaseSafetySqlServerTests.cs` — جدید؛ تست SQL گیت‌دار
- `docs/superpowers/specs/2026-11-27-vendor-db-provisioning-ownership-design.md` — جدید؛ سند طراحی
- `docs/DECISIONS.md` — پیوست D-122 (Proposed)
- `docs/WORK_LOG.md` — یادداشت Append-only
- `docs/INDEX.md` — ارجاع به سند طراحی و handoff

## تصمیم‌های فنی
- رکورد قصد روی `master` (Extended Property) انتخاب شد چون روی خود سرور ماندگار است، به هویت کاربر یا فایل محلی وابسته نیست و با یک دستور حذف می‌شود؛ گزینه‌های فایل محلی و Credential Manager در سند طراحی رد شده‌اند.
- مقدار نشانهٔ مالکیت دقیقاً اعتبارسنجی می‌شود؛ صرفِ وجود Extended Property هم‌نام اثبات مالکیت نیست.
- هیچ Endpoint، سرویس، Migration یا تغییر Customer در این PR وجود ندارد؛ primitives فعلاً فقط توسط تست‌ها مصرف می‌شوند تا در PR-2/3/4 به pipeline وصل شوند.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet restore Vendor.sln` | موفق | SDK 10.0.404 مطابق global.json (latestFeature) |
| `dotnet build Vendor.sln --no-restore --configuration Release` | موفق | بدون error جدید؛ warningهای از قبل موجود |
| `dotnet test tests/Vendor.Host.Tests/Vendor.Host.Tests.csproj -c Release` | total: 567، succeeded: 470، failed: 12، skipped: 85 | ۱۲ failed دقیقاً همان baselineِ main خالص در همین محیط است (وابسته به build ClientApp/محیط؛ روی main خالص با stash هم ۱۲ failed)؛ همهٔ تست‌های جدید این PR سبز |
| تست‌های SQL گیت‌دار | Skip محلی | Docker/SQL Server در محیط اجرا نیست؛ اجرای واقعی در Vendor CI روی SQL Server 2022 Disposable (`FARANO_VENDOR_TEST_SQLSERVER`) |
| `git diff --check` | موفق | بدون whitespace error |

## امنیت، داده و سازگاری
- هیچ اسرار، رمز، توکن یا Connection String در کد/تست/مستندات وجود ندارد.
- هیچ تغییر در Customer، `Server/**`، Migrationها، Schema یا پیکربندی Production.
- رفتار Fail Closed حفظ شده: دیتابیس خالی/بی‌نشان، نشانهٔ جعلی، مقصد غیرمجاز — همه رد می‌شوند.
- سازگاری با .NET 10 / C# 14 و سبک موجود؛ وابستگی جدید اضافه نشده است.

## ریسک‌ها و کارهای باقی‌مانده
- اجرای واقعی تست‌های SQL در CI باید تأیید شود (گیت‌دار).
- مصرف‌کنندهٔ واقعی primitives در PR-2 (pipeline) و PR-3 (هویت Runtime) می‌آید؛ تا آن زمان کد فقط با تست پوشش داده می‌شود.
- D-122 تا تأیید مالک `Proposed` است.

## شناسه‌های Git
- Branch: `feat/vendor-db-provisioning-ownership`
- Commit: (پس از Commit ثبت می‌شود)
- Pull Request: (پس از باز شدن ثبت می‌شود)
