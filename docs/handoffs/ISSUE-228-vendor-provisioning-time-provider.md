# تحویل وظیفه — ISSUE-228-vendor-provisioning-time-provider

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/228
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-11-01
- **وضعیت:** پیاده‌سازی در PR؛ منتظر CI

## هدف و معیارهای پذیرش
- [x] timestampهای مسیرهای Vendor Provisioning و Approval از منبع زمان تزریق‌شده و مشترک استفاده کنند.
- [x] تست Integration مسیر ایجاد Provisioning، درخواست Approval و تصمیم Approval را با ساعت کنترل‌شده بررسی کند.
- [ ] Vendor CI و .NET CI روی SHA نهایی PR موفق شوند.

## خلاصه تغییرات
- استفاده مستقیم از `DateTimeOffset.UtcNow` در endpointهای مربوط به Approval و Provisioning با `TimeProvider.GetUtcNow().ToUniversalTime()` جایگزین شد.
- `TimeProvider` از DI موجود استفاده می‌کند؛ provider جدید یا تنظیم عملیاتی ساخته نشده است.
- تست SQL Server موجود ساعت را پس از MFA تغییر می‌دهد، زمان ایجاد و درخواست Approval را بررسی می‌کند، برای ورود MFA بازبین ساعت جاری را برمی‌گرداند و سپس زمان تصمیم را کنترل می‌کند.

## فایل‌های مهم تغییرکرده
- `Vendor/Vendor.Host/Authorization/VendorApprovalEndpoints.cs` — زمان درخواست و تصمیم Approval.
- `Vendor/Vendor.Host/Authorization/VendorProvisioningEndpoints.cs` — زمان ایجاد intent و درخواست Approval.
- `tests/Vendor.Host.Tests/VendorCustomerMutationHttpIntegrationTests.cs` — Regression روی SQL Server واقعی.
- `docs/PROJECT_STATUS.md` و `docs/WORK_LOG.md` — وضعیت و شواهد جاری.

## تصمیم‌های فنی
- تصمیم معماری جدیدی گرفته نشد؛ از `TimeProvider.System` ثبت‌شده در DI استفاده می‌شود و فقط در تست جایگزین کنترل‌شده دارد.
- ترتیب authorization، transaction isolation، Maker-Checker، entitlement و قرارداد بسته تغییر نکرد.
- این PR فقط control-plane Vendor را تغییر می‌دهد؛ #235 و هر تغییر `Server/**` یا customer migration خارج از محدوده‌اند.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| تست محلی Vendor | اجرا نشده | این نشست checkout/SDK/SQL Server محلی ندارد. |
| SQL Server Integration regression | نوشته شده؛ اجرا نشده | Vendor CI باید با `FARANO_VENDOR_TEST_SQLSERVER` و `FARANO_REQUIRE_VENDOR_SQLSERVER=1` اجرا کند. |
| .NET CI / Vendor CI / Semgrep | در انتظار PR | تا دریافت نتیجه نهایی Verified نیست. |
| `git diff --check` | اجرا نشده محلی | diff از طریق GitHub commit/PR بازبینی می‌شود؛ اجرای CI جای اجرای محلی این فرمان را نمی‌گیرد. |

## امنیت، داده و سازگاری
- هیچ مجوز یا resource authorization جابه‌جا نشده؛ تغییر فقط منبع زمان timestampها است.
- هیچ کلید خصوصی، رمز، hash رمز، connection string یا secret اضافه نشده است.
- هیچ customer database، `Server/**` یا migration مشتری تغییر نکرده است.

## ریسک‌ها و کارهای باقی‌مانده
- CI باید تست SQL Server را اجرا کند؛ بدون آن تغییر Verified نیست.
- آزمون consumer سمت مشتری در #235 مستقل می‌ماند.
- signer Production در #236 و sink مستقل Production در #107 به انتخاب و دسترسی عملیاتی انسانی وابسته‌اند.

## شناسه‌های Git
- Branch: fix/vendor-provisioning-time-provider
- Commit پیاده‌سازی: `a8d8a686fa9faa95f44afb37f727603ab81ddb29`
- Pull Request: [#410](https://github.com/hamidreza1394/Accounting/pull/410)
