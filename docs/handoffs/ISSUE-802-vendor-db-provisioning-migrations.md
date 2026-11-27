# تحویل وظیفه — ISSUE-802-vendor-db-provisioning-migrations

- **Issue:** [#802](https://github.com/hamidreza1394/Accounting/issues/802) — بخش PR-2 از مجموعهٔ ۵ PR (ادامهٔ PR-1: #803)
- **Agent/مسئول:** GLM (Autonomous Engineering)
- **تاریخ:** 2026-11-27
- **وضعیت:** انجام‌شده (pipeline + تست‌ها)؛ وضعیت کارکرد واقعی روی SQL Server مالک: Not yet Verified

## هدف و معیارهای پذیرش (این PR)
- [x] اجرای Migrationهای موجود Vendor از طریق `VendorDbContext.Database.MigrateAsync` و Migration Assembly موجود — بدون Migration جدید و بدون EnsureCreated و بدون Drop/Recreate
- [x] جلوگیری از اجرای Migration روی دیتابیس ناشناس (گارد مالکیت در زمان اجرا + رد سریع در مرحلهٔ createDatabase)
- [x] کنترل Migration History و رد Migrationهای ناشناس (نسخهٔ جدیدتر از برنامه) با پیام قابل اقدام
- [x] Idempotency (اجرای مجدد بدون تغییر) و Retry امن
- [x] قفل هم‌زمانی `SemaphoreSlim` یک‌زمان روی کل pipeline (شامل ثبت/حذف رکورد قصد)
- [x] Timeout و Cancellation: CREATE DATABASE با CommandTimeout=120s؛ دکمهٔ بستن مرورگر (`RequestAborted`) مراحل ۱–۳ را لغو می‌کند اما CREATE/Migration/hویت/Verify با `CancellationToken.None` تا نیمه‌کاره نماند
- [x] **اصلاح ۳.۱ بازبینی مأموریت:** گذرواژهٔ SQL Login موجود هرگز Reset نمی‌شود؛ Login غیرفعال خودکار فعال نمی‌شود؛ استفادهٔ مجدد از Login موجود فقط با ارائهٔ گذرواژهٔ خودِ آن Login و آزمون واقعی اتصال؛ در نبود اثبات، Fail Closed و بازگشت mapping ساخته‌شده
- [x] Recovery کنترل‌شدهٔ پنجرهٔ شکست CREATE DATABASE/Marker با رکورد قصد + اتصال create_date (تست انتها-به-انتها روی SQL Server)
- [x] تست واحد + تست SQL Integration گیت‌دار شامل تست Regression حفظ گذرواژه
- [ ] اجرای واقعی روی SQL Server مالک (پس از تحویل، با مجوز عملیاتی جداگانه)

## خلاصه تغییرات
- فایل جدید `VendorDevelopmentDatabaseProvisioning.cs`: pipeline هفت‌مرحله‌ای (connection/test/inspect/createDatabase/migrate/runtimeIdentity/verify) با قرارداد `StageResult` ثابت و پیام‌های فارسی؛ مصرف primitives `VendorDevelopmentDatabaseSafety` از PR-1؛ بدون هرگونه Endpoint (اتصال در PR-4).
- مالکیت پیش از Migration تصمیم‌گیری می‌شود: دیتابیس موجود بدون اثبات مالکیت در مرحلهٔ `createDatabase` رد می‌شود (Fail-fast)؛ دیتابیس قدیمی فارنو با تاریخچهٔ Migration پذیرفته و نشانهٔ آن Backfill می‌شود.
- رکورد قصد پیش از CREATE ثبت، پس از نوشتن نشانه حذف و در مسیر 1801/Recovery نیز مدیریت می‌شود؛ شکست ثبت رکورد غیرمرگ‌بار است (فقط Recovery غیرفعال می‌شود).
- هویت Runtime: برای Login جدید — `CREATE LOGIN` با `CHECK_POLICY=ON`، `DEFAULT_DATABASE` هدف و `DENY VIEW ANY DATABASE`؛ برای Login موجود — ممیزی نقش/مجوز سرور، رد Login پراختیار، رد غیرفعال، و اثبات مالکیت با آزمون اتصال؛ اگر اثبات شکست بخورد mapping ساخته‌شده در همین اجرا `DROP USER` می‌شود تا Login دست‌نخورده بماند.
- قرارداد `ProvisionRequest` public برای endpointهای PR-4؛ ذخیرهٔ Credential Manager عمداً به PR-3 موکول شده است.
- بازسازی شاخهٔ موجود به‌صورت کنترل‌شده: منطق معتبر شاخهٔ `fix/vendor-provisioning-security` حفظ و اشکالات گزارش‌شدهٔ بازبینی (Reset گذرواژه، فعال‌سازی خودکار، نبود Recovery) اصلاح شد.

## فایل‌های مهم تغییرکرده
- `Vendor/Vendor.Host/Development/VendorDevelopmentDatabaseProvisioning.cs` — جدید؛ pipeline هفت‌مرحله‌ای
- `tests/Vendor.Host.Tests/VendorDevelopmentDatabaseProvisioningTests.cs` — جدید؛ قرارداد مراحل، اعتبارسنجی ورودی، شکل گذرواژهٔ ساخته‌شده و اتصال Runtime
- `tests/Vendor.Host.Tests/VendorDevelopmentDatabaseProvisioningSqlServerTests.cs` — جدید؛ چرخهٔ کامل واقعی + Regression گذرواژه + Recovery + دیتابیس خارجی/سیستمی
- `docs/handoffs/ISSUE-802-vendor-db-provisioning-migrations.md`، `docs/WORK_LOG.md`، `docs/INDEX.md` — مستندات

## تصمیم‌های فنی
- Fail-fast در مرحلهٔ createDatabase به‌جای شکست دیرهنگام در migrate: پیام مالکیت به‌موقع و وضعیت مراحل شفاف‌تر است.
- اثبات مالکیت Login موجود با آزمون اتصال واقعی به دیتابیس هدف انجام می‌شود؛ چون Login بدون user در دیتابیس نمی‌تواند وارد شود، mapping قبل از آزمون ساخته و در شکست آزمون بازگردانده می‌شود (Login دست‌نخورده می‌ماند).
- هیچ تغییر Customer/Server؛ هیچ Endpoint جدید؛ هیچ Migration جدید.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet build Vendor.sln -c Release` | موفق | بدون error جدید |
| `dotnet test tests/Vendor.Host.Tests -c Release` | total 597 / succeeded 491 / failed 12 / skipped 94 | ۱۲ failed همان baseline محیطی main است؛ همهٔ تست‌های جدید سبز |
| تست‌های SQL گیت‌دار | Skip محلی | اجرای واقعی در Vendor CI روی SQL Server 2022 Disposable |

## امنیت، داده و سازگاری
- گذرواژهٔ Login موجود تغییر نمی‌کند (تست Regression روی SQL Server)؛ Login غیرفعال فعال نمی‌شود؛ دیتابیس یا Login موجود هرگز حذف یا بازسازی نمی‌شود. تنها حذفِ مجاز، پاک‌سازی mapping کاربری است که خودِ همین اجرا برای اثبات گذرواژه ساخته و اثبات شکست خورده است (و در مسیر شکست پس از ساخت Login تازه، حذف همان Login تازه‌ساخت).
- لاگ‌ها فقط نام مراحل/شمارش Migration؛ connection string و گذرواژه در هیچ لاگ/پیامی نیست؛ `PersistSecurityInfo=false`.
- همهٔ lookupها پارامتری؛ شناسه‌ها Quote امن؛ مقصد فقط از Allow-list.

## ریسک‌ها و کارهای باقی‌مانده
- اجرای واقعی روی SQL Server مالک و چرخهٔ Restart کامل در مأموریت عملیاتی جداگانه.
- ذخیرهٔ اتصال Runtime در Credential Manager، ممیزی سراسری DENY/دیتابیس‌های دیگر و لاگ پاک‌سازی‌شده در PR-3؛ Endpointها و Bootstrap اپراتور در PR-4؛ UI در PR-5.

## شناسه‌های Git
- Branch: `feat/vendor-db-provisioning-migrations`
- Commit: پس از Commit ثبت می‌شود
- Pull Request: پس از باز شدن ثبت می‌شود
