# تحویل وظیفه — ISSUE-233-provisioning-dedicated-signer

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/233
- **تاریخ:** 2026-10-28
- **وضعیت:** مرز provider جداگانه پیاده‌سازی شده؛ منتظر CI و بازبینی

## هدف و معیارهای پذیرش
- جداسازی کامل مرز امضای provisioning از امضای لایسنس.
- پیش‌فرض امن که بدون provider مورد تأیید هیچ امضایی تولید نکند.
- حفظ امضای دقیق بایت‌های canonical و عدم افزودن کلید خصوصی یا fallback نرم‌افزاری.
- عدم انتخاب provider تولیدی بدون تصمیم/پیکربندی مورد تأیید.

## خلاصه تغییرات
- افزودن `IProvisioningSigningProvider` با قرارداد ورودی exact canonical UTF-8 bytes و الزام استفاده از کلید/credential مجزا از امضای لایسنس.
- افزودن `UnavailableProvisioningSigningProvider` و ثبت مستقل آن در DI؛ هر دو signer پیش‌فرض fail-closed هستند.
- تغییر `VendorProvisioningIssuanceService` به استفاده از `IProvisioningSigningProvider`.
- اصلاح تست Integration تا فقط provider provisioning را جایگزین کند؛ امضای لایسنس همچنان با provider پیش‌فرض unavailable می‌ماند. retry و امضای موفق تست provisioning حفظ شده‌اند.
- افزودن تست قرارداد برای نتیجه unavailable و لغو درخواست.
- به‌روزرسانی README، threat model، work log و این handoff.

## تصمیم‌های فنی
- Provider واقعی انتخاب یا پیاده‌سازی نشده است؛ این PR فقط مرز مستقل را enforce می‌کند. KMS/HSM یا سرویس امضای حفاظت‌شده و provisioning key اختصاصی نیازمند تصمیم و پیکربندی عملیاتی جداگانه‌اند.
- از `LicenseSigningResult` مشترک برای قالب نتیجه استفاده شده؛ این کار به معنی اشتراک provider یا کلید نیست.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| Vendor CI | در انتظار | باید روی آخرین SHA اجرا شود |
| .NET CI | در انتظار | شامل تست SQL Server، E2E و publish |
| تست محلی | اجرا نشده | محیط GitHub-only؛ CI منبع تأیید است |

## امنیت، داده و سازگاری
- بدون Migration، تغییر دیتابیس مشتری، تغییر `Server/**` یا تغییر قرارداد package.
- هیچ کلید خصوصی یا provider تولیدی اضافه نشده است.
- Provisioning در Production تا پیکربندی signer مورد تأیید همچنان unavailable باقی می‌ماند.

## ریسک‌ها و کارهای باقی‌مانده
- پیاده‌سازی provider متصل به KMS/HSM یا سرویس امضای مورد تأیید، با کلید provisioning اختصاصی.
- اعتبارسنجی کلید ID از منبع trusted سمت سرور، verify کردن امضای تولیدشده پیش از ذخیره، rotation/retirement، recovery و آزمون‌های عملیاتی.
- این PR به‌تنهایی Issue #233 را کامل نمی‌کند.

## شناسه‌های Git
- **Branch:** `fix/vendor-provisioning-dedicated-signer`
- **Commit:** پس از CI نهایی ثبت شود
- **Pull Request:** پس از بازبینی و CI ایجاد می‌شود
