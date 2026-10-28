# تحویل وظیفه — ISSUE-107-vendor-audit-hash-version

- **Issue:** [#107 — L2.4 hardening: enforce audit-chain uniqueness and independent checkpoints](https://github.com/hamidreza1394/Accounting/issues/107)
- **Agent/مسئول:** ChatGPT + GitHub integration
- **تاریخ:** 2026-10-28
- **وضعیت:** بخش کد ادغام‌شده و CI تأییدشده؛ Issue #107 همچنان باز برای provider مستقل و کنترل‌های عملیاتی

## هدف و معیارهای پذیرش
- افزودن نسخهٔ الگوریتم هش به هر رویداد و رد fail-closed نسخهٔ ناشناخته.
- جلوگیری از fork در سطح SQL Server با unique index روی `PreviousHash` و حفظ unique index موجود `EventHash`.
- جلوگیری از اعمال Migration روی تاریخچهٔ ممیزی ناسازگار، بدون اصلاح/حذف خودکار داده.
- تست واحد و SQL Server integration برای نسخه ناشناخته، تکرار هش، fork و رد Migration روی دو root.
- تغییر فقط در Vendor؛ هیچ تغییری در `Server/**` یا schema دیتابیس مشتری.

## خلاصه تغییرات
- `VendorAuditEvent.HashVersion` با مقدار پیش‌فرض 1 افزوده شد؛ Migration مقدار 1 را برای رکوردهای قبلی backfill می‌کند.
- `VendorAuditChainVerifier` اکنون بر اساس نسخه dispatch می‌کند و نسخهٔ ناشناخته را با `unsupported-hash-version` رد می‌کند. فقط نسخهٔ 1 پشتیبانی می‌شود.
- Migration جدید پیش از ایجاد unique index، duplicate parent hashes، تعداد root، parent مفقود و گسستگی زنجیره را بررسی و در صورت ناسازگاری با THROW متوقف می‌شود؛ داده‌ها را repair یا حذف نمی‌کند.
- تست SQL Server بررسی می‌کند unique constraintهای `PreviousHash` و `EventHash` fork/تکرار را رد کنند و Migration با دو root rollback شود.
- Snapshot و migration designer با مدل فعلی شامل VendorApprovalRequests و مدل HashVersion همگام شدند.

## فایل‌های مهم تغییرکرده
- `Vendor/Vendor.Persistence/Auditing/VendorAuditEvent.cs`
- `Vendor/Vendor.Persistence/Auditing/VendorAuditChainVerifier.cs`
- `Vendor/Vendor.Persistence/VendorDbContext.cs`
- `Vendor/Vendor.Persistence/Migrations/20261028110000_VendorAuditHashVersionAndLinearChain.cs`
- `Vendor/Vendor.Persistence/Migrations/20261028110000_VendorAuditHashVersionAndLinearChain.Designer.cs`
- `Vendor/Vendor.Persistence/Migrations/VendorDbContextModelSnapshot.cs`
- `tests/Vendor.Host.Tests/VendorAuditChainVerifierTests.cs`
- `tests/Vendor.Host.Tests/VendorAuditChainIntegrationTests.cs`

## تصمیم‌های فنی
- نسخهٔ هش ذخیره‌شده در هر رخداد منبع dispatch است؛ نسخهٔ ناشناخته fail-closed می‌شود.
- unique index روی `PreviousHash` حداکثر یک فرزند برای هر parent و حداکثر یک Genesis root را تضمین می‌کند.
- Migration برای داده ناسازگار fail-closed است و تاریخچه را خودکار دست‌کاری نمی‌کند.
- Migration و تست‌ها فقط دیتابیس مستقل Vendor را هدف می‌گیرند؛ دیتابیس مشتری خارج از محدوده است.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet build` | موفق در Vendor CI و .NET CI PR | CI واقعی GitHub |
| `dotnet test` | موفق در Vendor CI: 77/77؛ .NET CI نیز موفق | SQL Server Integration روی Vendor CI |
| `VendorModelSnapshotTests` | موفق در Vendor CI | Snapshot با مدل جاری هم‌خوان است |
| `git diff --check` | اجرا نشده در این محیط | از GitHub API برای تغییر فایل استفاده شده و shell محلی متصل نیست |
| Vendor CI PR #212 | موفق: 77/77، 0 شکست، 0 Skip | [Run 40650985169](https://github.com/hamidreza1394/Accounting/actions/runs/40650985169) |
| .NET CI PR #212 | موفق | [Run 40650985057](https://github.com/hamidreza1394/Accounting/actions/runs/40650985057) |
| Vendor CI پس از merge | موفق | [Run 40651677915](https://github.com/hamidreza1394/Accounting/actions/runs/40651677915) |

## امنیت، داده و سازگاری
- Migration روی تاریخچهٔ ناسازگار THROW می‌کند و داده را حذف/اصلاح نمی‌کند.
- `HashVersion` برای رکوردهای موجود 1 می‌شود؛ الگوریتم canonical نسخهٔ 1 بدون تغییر باقی می‌ماند.
- تست‌های Migration فقط روی دیتابیس آزمون تصادفی ساخته‌شده اجرا می‌شوند.
- checkpoint مستقل واقعی، provider، مجوزهای SQL و عملیات Production هنوز خارج از این تغییر باقی‌اند.

## ریسک‌ها و کارهای باقی‌مانده
- نتیجهٔ اجرای واقعی تست SQL Server و EF pending-model guard باید از CI تأیید شود.
- این تغییر provider مستقل checkpoint را پیاده‌سازی نمی‌کند و به‌تنهایی Production readiness نیست.
- migration source مطابق الگوی EF و snapshot به‌روزشده است؛ در محیط حاضر CLI محلی EF اجرا نشده است. نتیجهٔ guard مدل/مهاجرت در CI معیار نهایی است.

## شناسه‌های Git
- **Branch:** `fix/vendor-audit-hash-version-unique-parent`
- **Commit:** `4985908d395bb3856aec43d1a22d23555ae7d897`
- **Pull Request:** [#212](https://github.com/hamidreza1394/Accounting/pull/212)
