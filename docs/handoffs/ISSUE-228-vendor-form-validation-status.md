# تحویل وظیفه — ISSUE-228-vendor-form-validation-status

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/228
- **تاریخ:** 2026-10-29
- **وضعیت:** اصلاحات اعتبارسنجی فرم‌ها ادغام شده؛ CI پس از آخرین ادغام باید نهایی تأیید شود.

## هدف و محدوده
رفع ناسازگاری بین constraintهای HTML و `noValidate` در فرم‌های Vendor، و ثبت دقیق شواهد/محدودیت‌ها برای ادامهٔ Issue #228.

## تغییرات تأییدشده
- PR #244: حذف `noValidate` از `ProvisioningForm.tsx` و افزودن آزمون رگرسیون. Merge SHA: `96ecc4c49475d04fdaeb889e20f9d6b51354bc8e`.
- PR #245: حذف `noValidate` از `CustomerForm.tsx` و افزودن آزمون رگرسیون. Merge SHA: `381201b478363d5035e2b9d4dd9016ab497d71d8`.
- PR #246: حذف `noValidate` از فرم‌های قرارداد، نصب و metadata لایسنس و افزودن آزمون رگرسیون برای هر فرم. Merge SHA: `ecce57cbb5b8c0b058a1cea2643a27a870245b32`.

## شواهد CI روی headهای PR
- PR #244: .NET CI [40816378102](https://github.com/hamidreza1394/Accounting/actions/runs/40816378102) موفق؛ Build/Test، E2E، انتشار Linux/Windows و secret scan.
- PR #245: .NET CI [40816639908](https://github.com/hamidreza1394/Accounting/actions/runs/40816639908)، Vendor CI [40816639918](https://github.com/hamidreza1394/Accounting/actions/runs/40816639918)، CodeQL [40816647570](https://github.com/hamidreza1394/Accounting/actions/runs/40816647570) موفق.
- PR #246: .NET CI [40816815563](https://github.com/hamidreza1394/Accounting/actions/runs/40816815563)، Vendor CI [40816815620](https://github.com/hamidreza1394/Accounting/actions/runs/40816815620)، CodeQL [40816821910](https://github.com/hamidreza1394/Accounting/actions/runs/40816821910) موفق.
- اجرای CI پس از ادغام روی main در زمان نگارش هنوز نهایی نشده بود؛ runهای [40817730196](https://github.com/hamidreza1394/Accounting/actions/runs/40817730196)، [40817728004](https://github.com/hamidreza1394/Accounting/actions/runs/40817728004) و [40817727539](https://github.com/hamidreza1394/Accounting/actions/runs/40817727539) باید بررسی شوند.
- تست محلی در این محیط اجرا نشده است؛ نتیجه فقط بر اساس اجرای واقعی GitHub Actions گزارش می‌شود.

## مرزهای امنیتی و کارهای باقی‌مانده
- فعال‌سازی اعتبارسنجی مرورگر، جایگزین اعتبارسنجی سمت سرور نیست.
- این تغییرها API، دیتابیس Vendor، schema/migration مشتری، credential یا `Server/**` را تغییر نداده‌اند.
- فرم provisioning هنوز intent را در control plane ثبت می‌کند؛ تا مصرف‌کنندهٔ قابل‌اعتماد سمت مشتری بسته را اعتبارسنجی و acknowledgement معتبر بازنگرداند، ایجاد شرکت/مدیر مشتری نباید «تأییدشده» گزارش شود.
- signer پیش‌فرض عمداً fail-closed است. KMS/HSM واقعی (#236/#214)، مصرف‌کنندهٔ مشتری (#235)، onboarding end-to-end (#228) و sink مستقل audit checkpoint (#107) همچنان باز هستند و به تصمیم/دسترسی عملیاتی نیاز دارند.

## فایل‌های مرتبط
- `Vendor/Vendor.Web/src/ProvisioningForm.tsx`
- `Vendor/Vendor.Web/src/CustomerForm.tsx`
- `Vendor/Vendor.Web/src/ContractForm.tsx`
- `Vendor/Vendor.Web/src/InstallationForm.tsx`
- `Vendor/Vendor.Web/src/LicenseForm.tsx`
- `docs/PROJECT_STATUS.md`
- `docs/WORK_LOG.md`

## معیار ادامه
- [ ] CI پس از ادغام روی آخرین SHA از main موفق شود.
- [x] آزمون‌های رگرسیون برای هر پنج فرم در PRهای ادغام‌شده اضافه شده‌اند.
- [ ] ادامهٔ Issue #228 فقط در مرز Vendor control-plane و قرارداد provisioning؛ هر consumer یا تغییر دیتابیس مشتری در Issue/PR جداگانه و با بازبینی معماری انجام شود.
