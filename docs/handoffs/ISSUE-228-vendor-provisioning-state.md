# تحویل وظیفه — ISSUE-228-vendor-provisioning-state

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/228
- **وضعیت:** پیاده‌سازی backend/state در شاخه؛ منتظر CI و بازبینی

## خلاصه تغییرات
- افزودن جدول مستقل VendorProvisioningRequests در دیتابیس Vendor؛ هیچ تغییری در Server/** یا دیتابیس حسابداری مشتری انجام نشده است.
- ذخیرهٔ intent نسخه‌دار به‌صورت canonical JSON، hash، requestId idempotency، nonce یکتا، status، metadata امضا/تحویل/تأیید، failure code و rowversion.
- وضعیت اولیه AwaitingSigner است؛ API ادعا نمی‌کند شرکت یا مدیر مشتری ساخته شده یا بسته‌ای به مشتری تحویل داده شده است.
- افزودن API برای ثبت درخواست provisioning با antiforgery، مجوز سمت سرور، بررسی رابطه مشتری/نصب، ممیزی تراکنشی و رفتار idempotent برای RequestId تکراری.
- افزودن تست‌های واحد برای تنظیمات مجاز/نامعتبر و idempotency.

## فایل‌های تغییرکرده
- Vendor/Vendor.Persistence/Domain/VendorProvisioningRequest.cs
- Vendor/Vendor.Persistence/VendorDbContext.cs
- Vendor/Vendor.Persistence/Migrations/20261028163000_VendorProvisioningRequests.cs
- Vendor/Vendor.Persistence/Migrations/20261028163000_VendorProvisioningRequests.Designer.cs
- Vendor/Vendor.Persistence/Migrations/VendorDbContextModelSnapshot.cs
- Vendor/Vendor.Host/Authorization/VendorProvisioningRequestRules.cs
- Vendor/Vendor.Host/Authorization/VendorProvisioningEndpoints.cs
- Vendor/Vendor.Host/Program.cs
- tests/Vendor.Host.Tests/VendorProvisioningRequestRulesTests.cs
- این handoff و docs/WORK_LOG.md

## مرزهای امنیتی و محدودیت‌ها
- هیچ رمز خام، هش رمز مشتری یا راز bootstrap ذخیره نمی‌شود.
- امضاکنندهٔ عملیاتی/KMS هنوز متصل نشده؛ به همین دلیل وضعیت AwaitingSigner باقی می‌ماند و endpoint فعلی بستهٔ امضاشده را به مشتری ارسال نمی‌کند.
- مصرف‌کننده سمت مشتری، claim اتمی nonce، پاسخ امضاشدهٔ تأیید، retry سمت مشتری و UI وضعیت هنوز باید تکمیل شوند. این‌ها به‌عنوان تکمیل‌شده ادعا نمی‌شوند.
- Migration جدید برای دیتابیس Vendor است؛ دیتابیس مشتری و Server/** بدون تغییرند. Migration از طریق GitHub نوشته شده و باید با guardهای EF/CI روی SHA نهایی تأیید شود؛ اجرای محلی dotnet ef در این نشست در دسترس نبود.

## آزمون‌ها
| مورد | وضعیت |
|---|---|
| Build/Test .NET | در انتظار CI برای PR |
| Vendor UI/build/test | در انتظار CI برای PR |
| EF pending-model-changes / migration guard | در انتظار CI برای PR |
| SQL Server integration test migration | باید CI تأیید کند |

## ادامه لازم برای Issue #228
- افزودن UI فارسی RTL برای company setup، نام مدیر اولیه و زیرسیستم‌های خریداری‌شده با پیش‌فرض‌های امن.
- نمایش وضعیت‌های metadata saved / awaiting signer / pending delivery / confirmed / failed بدون ادعای provisioning قبل از تأیید مشتری.
- افزودن API/سرویس امضا فقط پس از پیکربندی کلید مورد اعتماد خارج از Git و طراحی جریان تحویل یک‌باره.
- طرح مصرف‌کننده سمت مشتری در Issue/PR جداگانه طبق محدوده Issue #228؛ بدون تغییر Server/** در این Issue.

- **Branch:** feat/issue-228-vendor-provisioning-state
- **PR:** در حال ایجاد
