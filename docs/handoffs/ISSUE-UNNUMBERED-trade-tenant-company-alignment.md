# تحویل وظیفه — ISSUE-UNNUMBERED-trade-tenant-company-alignment

- **Issue:** UNNUMBERED (مرتبط با #397)
- **Agent/مسئول:** Claude Code
- **تاریخ:** 2026-11-18
- **وضعیت:** انجام‌شده (فقط مستندات)

## هدف و معیارهای پذیرش
- [x] ممیزی مالکیت Tenant/Company برای Trade با برچسب شواهد
- [x] تطبیق تصمیم‌ها و ثبت D-104 (یک‌به‌یک Proforma/OrderRegistration)
- [x] نقشهٔ آمادگی S3 و ERD مفهومی
- [x] بدون Migration، Schema، Backfill، API/UI و تغییر Vendor

## خلاصه تغییرات
- سه سند جدید: ممیزی، تطبیق تصمیم‌ها، آمادگی S3 (ERD در سند ممیزی §۱۱).
- D-104 به انتهای `DECISIONS.md` افزوده شد (Accepted؛ NotImplemented). یادداشت‌های تاریخ‌دار در D-086، V3.2 §1، OD-007، S3_ARCHITECTURE §۶، REQUIREMENTS_TRACEABILITY و PROJECT_STATUS؛ هیچ متن تاریخی حذف نشد.
- هیچ Proposed (D-090..D-096) به Accepted نرفت.

## فایل‌های مهم تغییرکرده
- `docs/TRADE_TENANT_COMPANY_AUDIT.md`، `docs/TRADE_DECISION_RECONCILIATION.md`، `docs/TRADE_S3_READINESS.md` — جدید
- `docs/DECISIONS.md`، `docs/TRADE_DESIGN_V3_2.md`، `docs/TRADE_OPEN_DECISIONS.md`، `docs/TRADE_S3_ARCHITECTURE.md`، `docs/REQUIREMENTS_TRACEABILITY.md`، `docs/PROJECT_STATUS.md` — یادداشت/ردیف

## تصمیم‌های فنی
- D-104 ثبت شد؛ دامنهٔ آن عمداً محدود است: سمت Proforma («دقیقاً یک» یا «حداکثر یک»)، ثبت مجدد، Revision، ForexCommitment، P-4/P-5 تعیین نشده‌اند.
- خطر تصادم شمارهٔ D-104 با ADR هم‌زمان شاخه‌های دیگر وجود دارد.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet build Template.sln -c Release` | موفق | 0 Error، روی کد بدون تغییر |
| `dotnet test --solution Template.sln --no-build -c Release` با SQL Server آزمایشی | اجرا: 1564؛ موفق: 1554؛ ناموفق: 0؛ ردشده: 10 | 10 ردشده همه Windows CNG یا chmod-root هستند؛ اجرای اول به‌علت Login failed برای sa (محیط) خراب بود و پس از ساخت دوبارهٔ کانتینر تکرار شد |
| `git diff --check` | موفق | |
| `python3 scripts/check_static_assets.py` | اجرا نشد | تغییر فقط مستندات |
| تست React (`ClientApp`) | اجرا نشد | بدون تغییر کلاینت |
| Migration جدید | اجرا نشد (ممنوع) | |

بازبینی مستقل: `architect` (فقط‌خواندنی). 3 یافتهٔ مهم و 10 جزئی؛ همه اصلاح شد (ابهام D-104، دامنهٔ منسوخ‌سازی D-086، ارجاع handoff؛ برچسب‌ها و ارجاع‌های جزئی). بازبینی دوبارهٔ D-104 اجرا نشد.

## امنیت، داده و سازگاری
- بدون تغییر کد/Schema/Migration. ریسک‌های ایزولاسیون در سند ممیزی §۶ فهرست شده‌اند.

## ریسک‌ها و کارهای باقی‌مانده
- تأیید مالک: D-090..D-096، مالکیت Party/Product، P-4/P-5، فرمول مبلغ/تلورانس، روش‌های پرداخت مجاز هر Company، سمت Proforma در D-104.
- S3-0 بسته نشده است (بخش آمادگی).

## شناسه‌های Git
- Branch: `docs/trade-tenant-company-alignment`
- Commit/PR: در گزارش نهایی
