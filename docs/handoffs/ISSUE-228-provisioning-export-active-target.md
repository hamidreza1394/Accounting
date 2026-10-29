# تحویل وظیفه — ISSUE-228-provisioning-export-active-target

- **Issue:** [#228 — Vendor onboarding: secure customer provisioning and purchased subsystem setup](https://github.com/hamidreza1394/Accounting/issues/228)
- **Agent/مسئول:** ChatGPT GitHub automation
- **تاریخ:** 2026-10-29
- **وضعیت:** پیاده‌سازی پیشنهادی؛ منتظر CI و بازبینی

## هدف و معیارهای پذیرش
- [x] هنگام export، وضعیت فعال مشتری و نصب مجدداً بررسی شود؛ امضای قبلی به‌تنهایی مجوز تحویل پس از تعلیق نیست.
- [x] وضعیت هدف تا پایان تراکنش export و ثبت audit در سطح تراکنش پایدار بماند.
- [x] تست Integration برای تعلیق پس از امضا و پیش از export اضافه شود.
- [x] محدوده Vendor حفظ شود؛ بدون تغییر schema یا دیتابیس مشتری.
- [ ] اجرای واقعی تست‌های SQL Server و CI روی commit نهایی موفق شود.
- [ ] بازبینی diff و ادغام پس از موفقیت تمام بررسی‌های الزامی.

## خلاصه تغییرات
- VendorProvisioningEndpoints پیش از تحویل بسته، داخل تراکنش Serializable مالکیت رابطهٔ مشتری/نصب و وضعیت فعال هر دو را دوباره بررسی می‌کند.
- هدف مفقود یا ناسازگار با 404 و هدف غیرفعال با 409 (provisioning_target_inactive) رد می‌شود.
- Integration test مسیر «امضا → تعلیق → تلاش برای export» را پوشش می‌دهد و سپس مسیر export موفق را با امضای معتبر بررسی می‌کند.

## فایل‌های مهم تغییرکرده
- Vendor/Vendor.Host/Authorization/VendorProvisioningEndpoints.cs — کنترل وضعیت و قفل‌گذاری تراکنش هنگام export.
- tests/Vendor.Host.Tests/VendorCustomerMutationHttpIntegrationTests.cs — تست Regression واقعی endpoint با SQL Server.
- docs/WORK_LOG.md — ثبت یافته و محدودیت اعتبارسنجی.

## تصمیم‌های فنی
- استفاده از IsolationLevel.Serializable فقط در عملیات export، چون وضعیت مشتری/نصب نباید بین بررسی و ثبت تحویل تغییر کند.
- بدون تغییر API success payload، schema، migration یا مرز دیتابیس مشتری.
- هیچ کلید خصوصی یا راز عملیاتی اضافه نشده است.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| dotnet build Template.sln --no-restore --configuration Release | اجرا نشده | محیط این اجرا checkout محلی ندارد. |
| dotnet test Template.sln --no-build --configuration Release | اجرا نشده | تست Integration به SQL Server نیاز دارد. |
| GitHub Vendor CI / .NET CI | در انتظار | پس از ایجاد PR باید نتیجهٔ واقعی بررسی شود. |
| بازبینی ایستا | انجام شد | کد issuance از قبل فعال‌بودن هدف را هنگام امضا بررسی می‌کرد؛ شکاف، فاصلهٔ امضا تا export بود. |

## امنیت، داده و سازگاری
- این تغییر از تحویل بستهٔ از قبل امضاشده به نصب تعلیق‌شده جلوگیری می‌کند.
- تراکنش Serializable ممکن است هم‌زمانی تغییر وضعیت هدف را تا پایان export کوتاه‌مدت مسدود کند؛ دامنه آن فقط عملیات export است.
- هیچ migration یا تغییر دیتابیس مشتری وجود ندارد.

## ریسک‌ها و کارهای باقی‌مانده
- CI باید رفتار EF Core/SQL Server و تست integration را تأیید کند.
- تأمین‌کنندهٔ امضای Production همچنان عمداً fail-closed و نیازمند انتخاب/پیکربندی KMS/HSM است (Issues #236/#214).
- مصرف‌کنندهٔ سمت مشتری و acknowledgement معتبر همچنان کار مستقل Issue #235 است.

## شناسه‌های Git
- Branch: fix/228-provisioning-export-active-target
- Commit پیاده‌سازی: fd390cb4af7e6af519305277cd0a73f45a2e6d2e (تغییر تکمیلی assertion در commit بعدی همین شاخه است).
- Pull Request: [#283](https://github.com/hamidreza1394/Accounting/pull/283) — باز، منتظر CI.
