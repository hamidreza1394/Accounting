# اصلاح قرارداد HTTP نتیجهٔ Provisioning دیتابیس Vendor

## دامنه

اصلاح Vendor-only برای endpoint زیر:

`POST /api/development/database-setup/provision`

بدون تغییر Customer، `Server/**`، Schema یا Migration.

## علت ریشه‌ای

Endpoint همیشه پاسخ HTTP 200 می‌داد، حتی وقتی `VendorDevelopmentDatabaseProvisioning.RunAsync` یک `ProvisionOutcome.Success=false` برمی‌گرداند. در نتیجه Clientهایی که `response.ok` را معیار موفقیت می‌گیرند می‌توانستند اجرای ناموفق را موفق نمایش دهند.

همچنین فیلدهای Runtime identity و گذرواژهٔ تولیدی نباید در پاسخ شکست منتشر شوند؛ حتی اگر یک مسیر آینده خروجی جزئی یا غیرمنتظره داشته باشد.

## اصلاح

- موفقیت Provisioning فقط با HTTP 200 برگردانده می‌شود.
- نتیجهٔ ناموفق Pipeline با HTTP 422 (Unprocessable Entity) برمی‌گردد؛ جزئیات Stageها و پیام عمومی برای تشخیص وضعیت حفظ می‌شوند.
- `runtimeUsername`، `generatedPassword` و `savedToCredentialManager` فقط در نتیجهٔ موفق قابل انتشارند؛ در شکست مقدار حساس/موفقیت‌نما برگردانده نمی‌شود.
- UI باید هم `response.ok` و هم `body.success === true` را بررسی کند و پس از قطع Transport، نتیجهٔ Provisioning را «نامشخص» اعلام کند؛ Retry خودکار مجاز نیست.

## Regression coverage

ماتریس HTTP واقعی `VendorDevelopmentSetupHttpMatrixTests` روی مسیر Provisioning با SQL مقصد Loopback و پورت بسته، انتظار دارد:

- پاسخ شکست HTTP 422 باشد؛
- `success=false` باشد؛
- Runtime username و generated password تهی باشند؛
- ذخیرهٔ Credential به‌عنوان موفق گزارش نشود؛
- Rate limit همچنان پس از سه اجرای Provisioning اعمال شود.

تست در CI ویندوزی اجرا می‌شود؛ این محیط به‌تنهایی اجرای Windows Credential Manager را اثبات نمی‌کند.

## وضعیت تأیید

- پیاده‌سازی و Regression test در شاخهٔ اصلاحی ثبت شده‌اند.
- Build/Test/HTTP matrix و سایر Quality Gateها تا تکمیل GitHub Actions روی HEAD نهایی **Pending Verification** هستند.
- هیچ SQL Server عملیاتی، Customer DB یا Credential Manager واقعی سیستم مالک تغییر نکرده است.
