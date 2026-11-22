# تحویل وظیفه — ISSUE-UNNUMBERED-vendor-sf-sh-decision-packages

- **Issue:** `UNNUMBERED`
- **Agent/مسئول:** GLM — Vendor/Licensing
- **تاریخ:** 2026-11-22
- **وضعیت:** انجام‌شده (فقط مستندات؛ کد/رفتار تغییر نکرد)

## هدف و معیارهای پذیرش
- [x] S-F: بستهٔ تصمیم‌گیری D-100/OD-26 با وضعیت فعلی کد (نشست‌های server-side #504، نبود state مشترک)، گزینه‌ها با مزایا/معایب، اثر امنیتی/کاربری/نگهداری، پیشنهاد مرحله‌ای G-1 و یک سؤال کوتاه مالک
- [x] S-H: بستهٔ تصمیم‌گیری CustomerNumber با شواهد کد واقعی (binding ordinal در `VendorProvisioningIssuanceService.cs:78`، بازنویسی بی‌قید در `VendorBusinessEndpoints.cs:296`، وابستگی VendorTenantView/D-103)، سه گزینه، پیشنهاد B و سؤال دقیق مالک دربارهٔ تعریف «اولین وابستگی»
- [x] هیچ رفتار جدیدی برای OD-26، سقف نشست یا سیاست Update/تغییر شناسه فعال نشد؛ هر دو سند `Proposed`

## خلاصه تغییرات
- دو سند جدید: `docs/DECISION_PACKAGE_D100_SESSION_CAP.md` و `docs/DECISION_PACKAGE_CUSTOMERNUMBER_EDITABILITY.md`. سیاست Update (D-101) رفتار Accepted دارد و در این بسته به آن اکتفای ارجاع شد؛ تصمیم بازِ آن همان اجرای دوبخشی است که در ACهای خود ADR ثبت شده است.

## فایل‌های مهم تغییرکرده
- `docs/DECISION_PACKAGE_D100_SESSION_CAP.md` — جدید
- `docs/DECISION_PACKAGE_CUSTOMERNUMBER_EDITABILITY.md` — جدید
- `docs/handoffs/ISSUE-UNNUMBERED-vendor-sf-sh-decision-packages.md` — جدید

## تصمیم‌های فنی
- هیچ تصمیم معماری گرفته نشد؛ هر دو بسته با سؤال مشخص برای مالک و گزینهٔ پیشنهادیِ `Proposed` تمام می‌شوند.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| تطبیق ادعاها با کد (endpoint PUT مشتری، issuance binding، نشست‌ها) | انجام شد | همهٔ file:lineها از `main@58a1ccd4` |
| `dotnet build/test` | اجرا نشده | تغییر کد صفر؛ CI PR گزارش خودش را می‌دهد |
| `git diff --check` | موفق | صفر خطا |

## امنیت، داده و سازگاری
- هیچ تغییر امنیتی/داده/Schema؛ هر دو سند صراحتاً گزینه‌های ردشدهٔ امنیتی (client-side counting، تبدیل maxUsers، قفل نشست موجود، تغییر شناسهٔ بدون re-bind) را ثبت می‌کنند تا مبنا برای مالک شفاف باشد.
