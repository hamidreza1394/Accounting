# تحویل وظیفه — ISSUE-228-vendor-provisioning-time-provider

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/228
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-11-01
- **وضعیت:** Merged؛ PR checks موفق؛ post-merge .NET CI در زمان ثبت هنوز در حال اجرا

## هدف و معیارهای پذیرش
- [x] timestampهای مسیرهای Vendor Provisioning و Approval از منبع زمان تزریق‌شده و مشترک استفاده کنند.
- [x] تست Integration مسیر ایجاد Provisioning، درخواست Approval و تصمیم Approval را با ساعت کنترل‌شده بررسی کند.
- [x] Vendor CI و .NET CI روی SHA نهایی PR موفق شوند.
- [x] Semgrep و CodeQL روی SHA نهایی PR موفق شوند.
- [x] تمام jobهای .NET CI پس از merge روی main تکمیل و موفق شدند.

## خلاصه تغییرات
- استفاده مستقیم از `DateTimeOffset.UtcNow` در endpointهای Approval و Provisioning با `TimeProvider.GetUtcNow().ToUniversalTime()` جایگزین شد.
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
| SQL Server Integration regression | موفق در CI | Vendor CI run `41284398065`؛ log job نشان می‌دهد SQL Server test connection تنظیم شده، Test run summary برابر Passed و failed=0 است. |
| .NET CI روی head PR | موفق | run `41284398083`؛ ۷ job از جمله E2E و Linux/Windows publish موفق. |
| Semgrep / CodeQL روی head PR | موفق | runs `41284398117` و `41284394919`. |
| CI پس از merge روی main | موفق | Vendor CI `41285195060`، Semgrep `41285195043`، Push on main/CodeQL `41285194925` و .NET CI `41285195040` همگی موفق؛ هر ۷ job .NET CI شامل E2E و Linux/Windows publish تکمیل شدند. |
| `git diff --check` محلی | اجرا نشده | این نشست checkout محلی ندارد؛ نتیجهٔ CI جایگزین اجرای محلی این فرمان نیست. |

## امنیت، داده و سازگاری
- هیچ مجوز یا resource authorization جابه‌جا نشده؛ تغییر فقط منبع زمان timestampها است.
- هیچ کلید خصوصی، رمز، hash رمز، connection string یا secret اضافه نشده است.
- هیچ customer database، `Server/**` یا migration مشتری تغییر نکرده است.

## ریسک‌ها و کارهای باقی‌مانده
- consumer سمت مشتری #235 مستقل می‌ماند.
- signer Production در #236 و sink مستقل Production در #107 به انتخاب و دسترسی عملیاتی انسانی وابسته‌اند.
- post-merge .NET CI روی main با run `41285195040` تکمیل و موفق شد.

## شناسه‌های Git
- Branch: `fix/vendor-provisioning-time-provider`
- Commit پیاده‌سازی: `875f3090098020e964fed257e83108122fb2e6b4`
- Pull Request: [#410](https://github.com/hamidreza1394/Accounting/pull/410)
- Merge commit / main: `e26ecc4f387b46da2b703cabbda8e4105bc1d2f7`
