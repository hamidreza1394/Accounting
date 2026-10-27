# تحویل وظیفه — ISSUE-202-vendor-audit-chain-indexes

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/202
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-10-27
- **وضعیت:** Verified و ادغام‌شده در PR #203

## هدف و معیارهای پذیرش
- [x] افزودن ایندکس برای lookup با PreviousHash.
- [x] افزودن ایندکس یکتا برای EventHash.
- [x] Migration و Snapshot مستقل Vendor.
- [x] تست مدل و تست SQL Server برای ایندکس‌ها.
- [x] Build، تست‌ها، SQL Server اجباری و CI تأیید شدند.

## خلاصه تغییرات
- VendorDbContext ایندکس غیر یکتای PreviousHash و ایندکس یکتای EventHash را تعریف می‌کند.
- Migration 20261027190000_VendorAuditChainIndexes فقط دو ایندکس جدول VendorAuditEvents را اضافه/حذف می‌کند.
- Snapshot و migration target model به‌روزرسانی شدند.
- تست مدل و تست مهاجرت SQL Server وجود ایندکس‌ها را بررسی می‌کنند.

## فایل‌های مهم تغییرکرده
- Vendor/Vendor.Persistence/VendorDbContext.cs
- Vendor/Vendor.Persistence/Migrations/20261027190000_VendorAuditChainIndexes.cs
- Vendor/Vendor.Persistence/Migrations/20261027190000_VendorAuditChainIndexes.Designer.cs
- Vendor/Vendor.Persistence/Migrations/VendorDbContextModelSnapshot.cs
- tests/Vendor.Host.Tests/VendorPersistenceTests.cs
- Vendor/README.md, docs/PROJECT_STATUS.md, docs/WORK_LOG.md

## تصمیم‌های فنی
- EventHash یکتا است؛ چون hash شامل شناسه GUID مستقل رخداد است، برخورد معتبر نباید رخ دهد. دادهٔ تکراری موجود باعث شکست صریح Migration می‌شود، نه حذف/اصلاح خودکار داده.
- Migration فقط دیتابیس Vendor را تغییر می‌دهد.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| dotnet build Template.sln -c Release | موفق در CI | .NET CI run 40563642237 |
| dotnet test Template.sln -c Release | موفق در CI | .NET CI run 40563642237 |
| SQL Server migration integration | موفق در CI | Vendor CI run 40563642222 |
| dotnet ef migrations has-pending-model-changes | موفق در CI | Vendor CI run 40563642222 |
| git diff --check | موفق در CI | بررسی انتشار/CI روی head نهایی |

## امنیت، داده و سازگاری
- هیچ داده‌ای تغییر یا حذف نمی‌شود؛ فقط دو ایندکس افزوده می‌شوند.
- اگر جدول موجود دارای EventHash تکراری باشد، Migration به‌درستی شکست می‌خورد و نیاز به بررسی داده خواهد داشت.
- این بهینه‌سازی جایگزین مجوزهای محدود SQL و checkpoint مستقل Audit نیست.

## ریسک‌ها و کارهای باقی‌مانده
- CI واقعی باید Migration، Snapshot، build و تست SQL Server را تأیید کند.
- checkpoint مستقل Audit و maker-checker همچنان Production blockers هستند.

## شناسه‌های Git
- **Branch:** fix/vendor-audit-chain-indexes-main
- **PR:** #203 (merged)
- **Merge commit:** dfd5d941c6249570f8010176d16187e88f6d6217
- **Vendor CI:** 40563642222 — success
- **.NET CI:** 40563642237 — success
- **Commit:** چند commit اتمیک روی شاخه؛ SHA نهایی پس از بازبینی ثبت می‌شود.
- **Pull Request:** در انتظار ساخت پس از بازبینی تغییرات.