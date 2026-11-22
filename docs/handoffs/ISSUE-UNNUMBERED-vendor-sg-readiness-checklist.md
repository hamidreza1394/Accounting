# تحویل وظیفه — ISSUE-UNNUMBERED-vendor-sg-readiness-checklist

- **Issue:** `UNNUMBERED`
- **Agent/مسئول:** GLM — Vendor/Licensing/Provisioning
- **تاریخ:** 2026-11-22
- **وضعیت:** انجام‌شده (فقط مستندات؛ هیچ گیت/فلگی باز نشد)

## هدف و معیارهای پذیرش
- [x] چک‌لیست عملیاتی برای گاردهای جداسازی D-041 (G1..G6) با وضعیت واقعی ثبت‌شده (G1/G3/G6 انجام، G2 ناقص، G4/G5 به مالک وابسته)
- [x] Gate دوکلیدی v5: سازوکار دو کلید (`Provisioning:CompanyReferenceIssuance:Enabled` + `CustomerConsumerReady=false` در `VendorProvisioningCompanyReferenceGate.cs:22`)، پیش‌نیاز باز شدن و ممنوعیت باز کردن توسط عامل
- [x] چک‌لیست Customer Migration (#392)، Key Ceremony (#236)، #107، Trust #235، OD-27 و #391/#401 با ستون‌های پیش‌نیاز/مسئول/شواهد/تست/مانع/قابلیت انجام در محیط فعلی
- [x] قاعدهٔ صریح: هیچ گیت با تکمیل مستندات باز نمی‌شود؛ هیچ وضعیت Accepted/Verified جدیدی ایجاد نشد
- [x] تکمیل در اصلاحیهٔ همین PR: بخش ۰ — دسته‌بندی مستقل هر مورد مأموریت (Gate دوکلیدی، Customer Migration، Key Ceremony، Audit Sink، Trust Distribution، OD-27، Enforcement) در چهار ستون Implemented/Tested/Operationally Verified/وضعیت نهایی با واژگان D-030؛ تفاوت «وجود پیاده‌سازی» با «اثبات آمادگی عملیاتی» صریح شد و هیچ موردی Operationally Verified اعلام نشد

## خلاصه تغییرات
- سند جدید `docs/VENDOR_OPERATIONAL_READINESS_CHECKLIST.md` — تفکیک شفاف «قابل انجام در محیط فعلی» (G2 و بخش کدی #235 و #391 پس از تأیید) از «فقط مالک/زیرساخت» (G4/G5، migration واقعی، Key Ceremony، sink #107، OD-27).

## فایل‌های مهم تغییرکرده
- `docs/VENDOR_OPERATIONAL_READINESS_CHECKLIST.md` — جدید
- `docs/handoffs/ISSUE-UNNUMBERED-vendor-sg-readiness-checklist.md` — جدید

## تصمیم‌های فنی
- تصمیم معماری جدیدی گرفته نشد؛ همهٔ وضعیت‌ها از مستندات و کد موجود استخراج شد و فقط ساختار چک‌لیست جدید است.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| تطبیق ادعاها با D-041 (پیگیری گاردها)، LICENSING.md هدر وضعیت و کد Gate | انجام شد | مبنای واقعیت ثبت شد |
| `dotnet build/test` | اجرا نشده | تغییر کد صفر؛ CI PR گزارش خودش را می‌دهد |
| `git diff --check` | موفق | صفر خطا |

## امنیت، داده و سازگاری
- هیچ تغییر امنیتی/داده/گیت؛ سند صراحتاً باز کردن gateها را به تصمیم مالک + شواهد + تست محدود می‌کند و وضعیت fail-closed همهٔ مسیرها را حفظ‌شده ثبت می‌کند.
