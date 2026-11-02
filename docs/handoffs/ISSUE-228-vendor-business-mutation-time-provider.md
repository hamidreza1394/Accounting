# تحویل وظیفه — ISSUE-228-vendor-business-mutation-time-provider

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/228
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-11-02
- **وضعیت:** پیاده‌سازی در PR؛ منتظر CI و بازبینی

## هدف و معیارهای پذیرش
- [x] timestampهای ایجاد Customer، Installation، Contract و License در APIهای Vendor از `TimeProvider` تزریق‌شده استفاده کنند.
- [x] timestamp به‌روزرسانی Customer از همان منبع زمان استفاده کند.
- [x] برای هر عملیات، یک مقدار UTC واحد محاسبه و در تمام فیلدهای زمانی همان write استفاده شود.
- [x] تست Integration با ساعت کنترل‌شده، مقادیر ذخیره‌شده و پاسخ HTTP را بررسی کند.
- [ ] تمام بررسی‌های الزامی PR روی SHA نهایی موفق شوند و پس از merge، CI روی main تأیید شود.

## خلاصه تغییرات
- پنج استفاده مستقیم از `DateTimeOffset.UtcNow` در `VendorBusinessEndpoints` حذف شد.
- برای ساخت Customer، مقدار واحد `now` هم در `CreatedAtUtc` و هم در `UpdatedAtUtc` ذخیره می‌شود.
- APIهای ایجاد Installation، Contract و License و ویرایش Customer اکنون `TimeProvider` را از DI دریافت می‌کنند.
- تست Integration موجود اکنون timestamp ویرایش Customer، ایجاد Customer در رقابت دو درخواست، و ایجاد سه موجودیت تجاری دیگر را با ساعت کنترل‌شده بررسی می‌کند.

## فایل‌های مهم تغییرکرده
- `Vendor/Vendor.Host/Authorization/VendorBusinessEndpoints.cs` — تزریق TimeProvider و timestampهای یکنواخت.
- `tests/Vendor.Host.Tests/VendorCustomerMutationHttpIntegrationTests.cs` — regression روی SQL Server واقعی.
- `docs/handoffs/ISSUE-228-vendor-business-mutation-time-provider.md` — این گزارش.
- `docs/WORK_LOG.md` — ثبت یافته و وضعیت.

## تصمیم‌های فنی
- تصمیم معماری جدیدی گرفته نشد؛ از singleton `TimeProvider.System` ثبت‌شده در DI موجود استفاده می‌شود و تست با provider قابل‌کنترل آن را جایگزین می‌کند.
- هیچ تغییری در مجوزدهی، antiforgery، RowVersion، تراکنش، audit، قرارداد API یا مدل داده انجام نشده است.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| تست محلی .NET | اجرا نشد | این نشست checkout محلی/SDK ندارد؛ نتیجهٔ محلی ادعا نمی‌شود. |
| اجرای نخست Vendor CI روی SHA `3e2818b` | ناموفق — 204 موفق، 1 ناموفق | تست Integration در POST نصب، `Unauthorized` گرفت؛ ساعت fake دو ساعت جلو رفته و cookie احراز هویت ۳۰ دقیقه‌ای را منقضی کرده بود. |
| اصلاح Regression | انجام شد | ساعت تست اکنون یک دقیقه پس از زمان تأیید قبلی جلو می‌رود و در محدودهٔ عمر cookie می‌ماند. |
| Vendor CI / .NET CI / Semgrep / CodeQL روی SHA جدید | در انتظار | Merge تا سبزشدن تمام بررسی‌های الزامی ممنوع است. |
| `git diff --check` | اجرا نشد | تغییر از Git object API ثبت می‌شود؛ بررسی CI و diff نهایی PR لازم است. |

## امنیت، داده و سازگاری
- فقط Vendor Host و تست Vendor تغییر می‌کنند؛ هیچ فایل `Server/**`، Customer DB یا migration تغییر نکرده است.
- هیچ کلید، رمز، hash رمز، connection string یا secret اضافه نشده است.
- مقدار زمان از DI گرفته می‌شود؛ ورودی کاربر نمی‌تواند ساعت سرور را تعیین کند.

## ریسک‌ها و کارهای باقی‌مانده
- مسیر command-mode بوت‌استرپ مدیر Vendor در `VendorBootstrapCommand` هنوز یک timestamp مستقیم `DateTimeOffset.UtcNow` دارد؛ خارج از endpointهای تجاری این Issue است و در این PR تغییر داده نمی‌شود.
- signer Production در #236، consumer مستقل مشتری در #235 و sink مستقل checkpoint در #107 همچنان به تصمیم/زیرساخت جداگانه وابسته‌اند.
- تست فیزیکی دو میزبان/انتقال فایل در محیط عملیاتی انجام نشده است.

## شناسه‌های Git
- Branch: fix/vendor-business-mutation-time-provider
- Commit اول: `3e2818b50ee12ec942f0e44c9ec9e12ca66ab8b5`
- Commit اصلاح regression: پس از ساخت
- Pull Request: #424
