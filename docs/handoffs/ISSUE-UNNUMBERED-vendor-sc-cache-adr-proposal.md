# تحویل وظیفه — ISSUE-UNNUMBERED-vendor-sc-cache-adr-proposal

- **Issue:** `UNNUMBERED`
- **Agent/مسئول:** GLM — Licensing/Customer Consumer
- **تاریخ:** 2026-11-22
- **وضعیت:** انجام‌شده (فقط مستندات؛ کد و رفتار تغییر نکرد)

## هدف و معیارهای پذیرش
- [x] بازبینی رفتار فعلی رزولوشن و invariant ثبت‌شدهٔ «بدون cache» در کد
- [x] شناسایی نقاط بهبود کارایی و ثبت نکتهٔ کلیدی: مفاد D-058/D-059 برای حالت Cloud با authority مرکزی است و پیاده‌سازی فعلی هیچ round-trip شبکه‌ای ندارد
- [x] مقایسهٔ چهار گزینه از نظر پیچیدگی/هزینه/امنیت/سازگاری با تحلیل کامل موضوعات الزامی (version، invalidation، revocation، قطع ارتباط، منقضی/downgrade، جداسازی Tenant، آلودگی cache)
- [x] پیش‌نویس ADR `Proposed` + طرح تست با پیش‌فرض خاموش + ثبت OD-28 در LICENSING_OPEN_DECISIONS.md
- [x] هیچ تغییر مسیر revocation یا authority؛ هیچ کدی نوشته نشد

## خلاصه تغییرات
- سند جدید `docs/LICENSE_RESOLUTION_CACHE_ADR_PROPOSAL.md` (پیش‌نویس ADR با گزینهٔ O2 fingerprint-keyed memo به‌عنوان پیشنهاد بررسی) و ورودی `OD-28` در `docs/LICENSING_OPEN_DECISIONS.md`.

## فایل‌های مهم تغییرکرده
- `docs/LICENSE_RESOLUTION_CACHE_ADR_PROPOSAL.md` — جدید
- `docs/LICENSING_OPEN_DECISIONS.md` — افزودن OD-28 (فقط append)
- `docs/handoffs/ISSUE-UNNUMBERED-vendor-sc-cache-adr-proposal.md` — جدید

## تصمیم‌های فنی
- هیچ تصمیم معماری گرفته نشد؛ OD-28 صراحتاً `NeedsApproval` است و پیاده‌سازی فقط پس از پذیرش ADR توسط مالک شروع می‌شود.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| تطبیق ادعاها با کد (state service، evaluator، composition DI، D-058/D-059) | انجام شد | از کد `main@58a1ccd4` و متن ADRها |
| `dotnet build/test` | اجرا نشده | تغییر کد صفر؛ CI PR گزارش خودش را می‌دهد |
| `git diff --check` | موفق | صفر خطا |

## امنیت، داده و سازگاری
- هیچ تغییر امنیتی/داده؛ سند تأکید می‌کند نتایج رد هرگز cache نمی‌شوند و هر فعال‌سازی به تصمیم مالک و اندازه‌گیری مزیت واقعی وابسته است.
