# تحویل وظیفه — ISSUE-143-vendor-antiforgery-cookie-variable

- **Issue:** [#143](https://github.com/hamidreza1394/Accounting/issues/143)
- **PR:** [#144](https://github.com/hamidreza1394/Accounting/pull/144)
- **Branch:** `fix/vendor-antiforgery-cookie-variable`
- **تاریخ:** 2026-10-26
- **وضعیت:** پیاده‌سازی شده؛ منتظر CI تازه و بازبینی/ادغام Maintainer

## هدف و علت
Vendor CI run [40349534341](https://github.com/hamidreza1394/Accounting/actions/runs/40349534341) بعد از PR #142 در مرحله Build شکست خورد. متغیر `antiforgeryCookie` در درخواست‌های TOTP و Application استفاده شده بود، اما هنگام دریافت توکن اولیه مقدار دوم tuple با `var (token, _)` دور انداخته شده بود. خطای اصلی CS0103 و خطاهای CS8130 ناشی از آن بودند.

## تغییرات
- `tests/Vendor.Host.Tests/VendorAuthenticationSecurityTests.cs`: کوکی antiforgery اولیه در متغیر `antiforgeryCookie` نگه داشته می‌شود و برای درخواست توکن جدید در مرزهای تغییر هویت قابل استفاده است.
- `docs/WORK_LOG.md`: نتیجه واقعی CI و پیگیری Issue #143 ثبت شد.

## امنیت و دامنه
- فقط آزمون و مستندات تغییر کرده‌اند.
- منطق احراز هویت Production، تنظیمات antiforgery، دیتابیس مشتری و Migrationها تغییر نکرده‌اند.
- هیچ کنترل امنیتی یا assertion حذف/تضعیف نشده است.

## اعتبارسنجی
- CI run [40349534341](https://github.com/hamidreza1394/Accounting/actions/runs/40349534341) علت کامپایل را ثابت می‌کند.
- اصلاح روی شاخه ثبت شده، اما هنگام تهیه این handoff برای SHA `851f797de789b5fff5adbe9b7b38a41698feb3b8` هنوز run/status تازه‌ای از ابزار GitHub در دسترس نبود.
- **اجرا نشده/تأیید نشده:** Build، آزمون Integration روی SQL Server، تمام Vendor tests، strict EF model snapshot gate و Publish پس از اصلاح.
- Merge فقط پس از CI واقعی و بازبینی diff انجام شود.

## شناسه‌ها
- Commit اصلاح آزمون: `9ede9808fe6dbec546f196649a270597383f1d6e`
- آخرین SHA شاخه هنگام مستندسازی: `851f797de789b5fff5adbe9b7b38a41698feb3b8`
