# تحویل وظیفه — ISSUE-UNNUMBERED-localhost-windows-guide

- **Issue:** UNNUMBERED (پوشش سناریوی A مأموریت مالک 2026-11-19: «Windows Localhost — سیستم شخصی») — مرجع‌ها: D-107، D-060، D-098، #396، #236
- **Agent/مسئول:** GLM
- **تاریخ:** 2026-11-19
- **وضعیت:** سند راهنما آماده است؛ اجرای واقعی روی Windows توسط مالک (طبق بندهای صادقانهٔ سند)

## هدف و معیارهای پذیرش
- [x] مسیر تکرارپذیر ۱۲ مرحله‌ای مأموریت (پیش‌نیازها → دیتابیس محلی → Vendor محلی → Customer/Installation → صدور License امضاشده → اجرای فرانو → ورود → بررسی وضعیت/Entitlement → تغییر کنترل‌شدهٔ اعتبار → تمدید → آفلاین → Restricted/Unlicensed).
- [x] همهٔ دستورها PowerShell آمادهٔ Copy/Paste.
- [x] localhost صرفاً آدرس است؛ هیچ فرضی از مالکیت/Tenant از آن ساخته نمی‌شود.
- [x] روش معتبر صدور License آزمایشی با امضای واقعی (CNG SoftwareProtected طبق D-107) — بدون bypass و بدون کلید آزمایشی در Runtime.
- [x] محدودیت‌های واقعی صادقانه ثبت شده‌اند (Wizard React، reactivation D-099، Gate v5، D-100/101/102).

## خلاصه تغییرات
- `docs/LOCALHOST_WINDOWS_TEST_GUIDE.md` اضافه شد: معماری سه‌نقشی محلی، دو دیتابیس، Key Ceremony CNG با `New-SelfSignedCertificate -KeyExportPolicy NonExportable`، پیکربندی `Signing:CertificateStore` + Trust Store با فرمان فقط‌عمومی `--signing-certificate export-public`، bootstrap-admin، Maker-Checker صدور، اجرای فرانو با `Licensing:RuntimeGate:Enabled` و `Licensing:DeploymentMode`، import فایل Envelope، سناریوهای انقضا/تمدید/آفلاین/Restricted/Unlicensed با رفتارهای دقیق.

## فایل‌های مهم تغییرکرده
- `docs/LOCALHOST_WINDOWS_TEST_GUIDE.md` — سند جدید
- `docs/PROJECT_STATUS.md`، `docs/WORK_LOG.md`، این handoff — هم‌ترازی

## تصمیم‌های فنی
- مسیر فعال‌سازی فعلی کد (import پایدار + provisioning v4) مبنا قرار گرفت؛ هیچ قابلیت حدسی به مستندات افزوده نشد.
- تصمیم معماری جدیدی گرفته نشد.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| Build/Test در PRهای #720/#721/#722 | ثبت‌شده در همان PRها | این PR فقط مستندات است |
| اجرای راهنما روی Windows واقعی | اجرا نشده | به دست مالک؛ خروجی‌ها در Issue/WORK_LOG ثبت شود |

## امنیت، داده و سازگاری
- هیچ secret/کلیدی در مخزن نیست؛ متغیرهای محیطی موقت با مقادیر placeholder.
- ریسک D-107 (مدیر محلی) در سند تکرار شده؛ میزبان اختصاصی الزامی است.

## ریسک‌ها و کارهای باقی‌مانده
- پس از اجرای واقعی مالک، بازخورد به این سند اضافه شود؛ Wizard React و D-099 کارهای بعدی‌اند.

## شناسه‌های Git
- Branch: `agent/unnumbered-localhost-windows-guide`
- Commit: (پس از commit)
- Pull Request: (پس از باز شدن)
