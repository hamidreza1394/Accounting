# تحویل وظیفه — ISSUE-UNNUMBERED-vendor-sd-enforcement-inventory

- **Issue:** `UNNUMBERED`
- **Agent/مسئول:** GLM — Vendor/Licensing/Provisioning/Customer Consumer
- **تاریخ:** 2026-11-22
- **وضعیت:** انجام‌شده (فقط مستندات و تحلیل؛ کد تولیدی تغییر نکرد)

## هدف و معیارهای پذیرش
- [x] شناسایی همهٔ call-siteهایی که entitlement را بررسی می‌کنند یا باید بررسی کنند، با file:line دقیق
- [x] ثبت محل، عملیات محافظت‌شده، اطلاعات Company/Tenant، رفتار فعلی، ریسک bypass، رفتار fail-closed مورد انتظار و تست‌های لازم برای هر مورد
- [x] تفکیک نقاط enforcement مرکزی کم‌هزینه از نقاط پرهزینه + طرح اجرای حداقلی (E1..E4) بدون abstraction اضافه
- [x] عدم فعال‌سازی هیچ سیاست جدید؛ عدم دست‌زدن به Trade/`DenyAllTradeModuleEntitlement`؛ همهٔ پیشنهادها `Proposed`

## خلاصه تغییرات
- سند جدید `docs/ENTITLEMENT_ENFORCEMENT_CALLSITE_INVENTORY.md`: sweep کد روی `main@58a1ccd4` با دو لایهٔ فعلی (گیت سراسری opt-in + کنترل‌های provisioning-time)، جدول ۱۱ ردیفی نقاط، گروه‌بندی A/B و طرح E1..E4.

## فایل‌های مهم تغییرکرده
- `docs/ENTITLEMENT_ENFORCEMENT_CALLSITE_INVENTORY.md` — جدید
- `docs/handoffs/ISSUE-UNNUMBERED-vendor-sd-enforcement-inventory.md` — جدید

## تصمیم‌های فنی
- تصمیم معماری جدیدی گرفته نشد؛ همهٔ یافته‌ها از کد واقعی و تصمیم‌های ثبت‌شده (D-025/D-041/D-054/D-059/D-098) استخراج شد. اجرای E1..E4 نیازمند تأیید مالک است و این سند مجوز پیاده‌سازی نیست.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| Sweep اکتشافی کد (خواندن Program.cs/middleware/apply-service/endpoints) | انجام شد | file:lineها مستقیماً از کد `main@58a1ccd4` استخراج شد |
| `dotnet build/test` | اجرا نشده | تغییر کد صفر است؛ CI PR اجرای خودش را گزارش می‌کند |
| `git diff --check` | موفق | صفر خطای whitespace |

## امنیت، داده و سازگاری
- هیچ تغییر امنیتی/داده/Schema؛ سند فقط مسیر آینده را مستند می‌کند و تصریح می‌کند enforcement جایگزین RBAC نمی‌شود و `DenyAllTradeModuleEntitlement` fail-closed می‌ماند.
