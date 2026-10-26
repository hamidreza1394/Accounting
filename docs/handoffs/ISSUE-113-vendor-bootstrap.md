# تحویل وظیفه — ISSUE-113-vendor-bootstrap

- **Issue:** [#113](https://github.com/hamidreza1394/Accounting/issues/113)
- **Branch:** `feat/issue-113-vendor-bootstrap`
- **وابستگی:** [PR #146](https://github.com/hamidreza1394/Accounting/pull/146) (شاخهٔ مبنا؛ ابتدا باید ادغام شود)
- **تاریخ:** 2026-10-26
- **وضعیت:** پیاده‌سازی ثبت شده؛ CI این شاخه هنوز لازم است

## هدف
پیاده‌سازی command-mode یک‌باره برای ایجاد اولین مدیر Vendor بدون public HTTP endpoint یا مدیر seed‌شده.

## تغییرات
- `Vendor/Vendor.Host/Bootstrap/VendorBootstrapCommand.cs`: راز تصادفی از configuration/environment، مقایسهٔ ثابت‌زمان، ورودی مخفی راز/گذرواژه، نمایش URI TOTP فقط در کنسول غیر redirect، ایجاد حساب غیرفعال، enrollment و تأیید TOTP، SQL Server transaction-scoped application lock، بازیابی حساب pending و audit events.
- `Vendor/Vendor.Host/Program.cs`: اجرای فرمان پیش از پیکربندی/اجرای HTTP host؛ هیچ راز یا گذرواژه‌ای از آرگومان CLI دریافت نمی‌شود.
- `Vendor/Vendor.Persistence/DependencyInjection.cs`: ثبت `IVendorAuditWriter` و `TimeProvider` برای استفادهٔ عملیاتی.
- `tests/Vendor.Host.Tests/VendorBootstrapIntegrationTests.cs`: آزمون SQL Server واقعی برای دو تلاش هم‌زمان، فعال‌سازی فقط یک مدیر با MFA و صحت زنجیرهٔ ممیزی؛ تست واحد مقایسهٔ راز.
- `Vendor/README.md`, `docs/VENDOR_BOOTSTRAP.md`, `docs/INDEX.md`, `docs/WORK_LOG.md`: دستورالعمل فارسی و ثبت شواهد.

## تصمیم‌های فنی
- دیتابیس و Migrationهای مشتری تغییر نمی‌کنند؛ از جداول Identity موجود Vendor استفاده شده و migration جدیدی لازم نشده است.
- حساب pending پیش از تعامل TOTP در DB commit می‌شود تا قطع فرایند قابل بازیابی باشد؛ transaction دوم application lock را تا تأیید TOTP نگه می‌دارد.
- ایمیل حساب اولیه تأییدشده علامت نمی‌خورد (`EmailConfirmed=false`)؛ bootstrap نباید تأیید ایمیل را جعل کند.
- در خطای TOTP، رخداد رد در زنجیرهٔ ممیزی ثبت می‌شود و حساب فعال نمی‌شود.

## آزمون و اعتبارسنجی
- CI پایهٔ .NET قبلاً `build-and-test` را در حال اجرا نشان می‌داد؛ Vendor CI مربوط به PR #146 موفق شده است.
- **اجرا نشده توسط Agent:** Build/Test محلی.
- **نیازمند CI این شاخه:** Build، تست Integration روی SQL Server، strict EF snapshot gate، Publish و بررسی artifact.
- وضعیت تا CI کامل **Not Verified** است.

## ریسک‌ها و باقی‌مانده‌ها
- Issue #113 باید تا تأیید همهٔ معیارها و CI باز بماند.
- RBAC و maker-checker در Issue #114 جداگانه تکمیل می‌شوند؛ پیش از آن پنل تجاری Production-ready نیست.
- audit hash-chain به‌تنهایی در برابر DBA دارای دسترسی بازنویسی کافی نیست؛ SQL permissions محدود و checkpoint مستقل همچنان لازم‌اند.

## شناسه‌ها
- Branch: `feat/issue-113-vendor-bootstrap`
- PR: پس از بررسی Build و CI ایجاد/به‌روزرسانی می‌شود.