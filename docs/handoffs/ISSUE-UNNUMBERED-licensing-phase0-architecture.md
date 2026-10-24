# تحویل وظیفه — ISSUE-UNNUMBERED-licensing-phase0-architecture

- **Issue:** `UNNUMBERED` (مأموریت «Farano — Phase 0: معماری سامانه مرکزی مشتریان و لایسنس»)
- **Agent/مسئول:** Claude (Lead؛ ابزار Agent برای بازبینی مستقل در انتها استفاده شد، نه برای نگارش)
- **تاریخ:** 2026-10-24
- **وضعیت:** انجام‌شده (فقط مستندات)؛ در انتظار بازبینی و تأیید مالک محصول؛ Merge انجام نشده

## هدف و معیارهای پذیرش
- [x] ممیزی مجدد مخزن و وضعیت‌دهی به هر یافتهٔ گزارش قبلی با شاهد
- [x] ثبت تصمیم‌های معماری (D-041..D-044) بدون بازنویسی ADRهای پیشین
- [x] ثبت ابهام‌ها و تصمیم‌های باز بدون حدس (`LICENSING_OPEN_DECISIONS.md`)
- [x] گسترش مدل داده، گردش‌کار و مدل تهدید
- [x] نقشهٔ راه مرحله‌ای L0..L4
- [x] هیچ کد اجرایی، Schema، Migration، API، UI یا وابستگی جدید ساخته نشد

## خلاصه تغییرات
- ADRهای D-041 (`Accepted` مشروط)، D-042..D-044 (`Proposed`)؛ جدول فهرست ADR و راهنمای وضعیت (`Deferred`، `Rejected`) به‌روز شد.
- بخش «ممیزی مجدد فاز صفر» در `REPOSITORY_AUDIT.md` با ۲۷ ردیف وضعیت و AUD-2026-10-24-01..09.
- تصمیم‌های باز OD-01..OD-17 با گزینه‌ها، ریسک و پیشنهاد؛ نتیجهٔ آزمایش واقعی امضا (ECDSA/RSA-PSS).
- شاخص‌ها و راهنماها: `INDEX.md`، `PROJECT_STATUS.md`، `ROADMAP.md`، `ARCHITECTURE.md`، `LICENSING.md`؛ اصلاح شمارهٔ ADR بعدی در `AGENTS.md` و `CLAUDE.md` (فقط همان یک عبارت؛ دستورالعمل‌های دیگر دست‌نخورده).

## فایل‌های مهم تغییرکرده
- `docs/DECISIONS.md` — D-041..D-044، یادداشت‌های «توضیح 2026-10-24» روی ردیف D-031 و D-036، راهنمای وضعیت
- `docs/REPOSITORY_AUDIT.md` — ممیزی مجدد و AUD جدید
- `docs/LICENSING_OPEN_DECISIONS.md` — **فایل جدید**
- `docs/LICENSING_DATA_MODEL.md`، `docs/LICENSING_WORKFLOWS.md`، `docs/LICENSING_THREAT_MODEL.md`، `docs/LICENSING.md` — تکمیل
- `docs/ROADMAP.md`، `docs/PROJECT_STATUS.md`، `docs/INDEX.md`، `docs/ARCHITECTURE.md`، `docs/WORK_LOG.md` و این handoff
- `AGENTS.md`، `CLAUDE.md` — فقط شمارهٔ ADR بعدی (و ذکر `Deferred`/`Rejected` در `AGENTS.md`)

## تصمیم‌های فنی
- D-041: همین مخزن با Host، دیتابیس، هویت، انتشار و اسرار مستقل؛ مخزن جدا `Deferred`؛ گاردهای G1–G6 پیش‌شرط اولین کد فروشنده.
- D-042..D-044: `Proposed` و در انتظار تأیید؛ هیچ‌کدام مبنای کد نیستند.
- تصمیم‌های `Accepted` قبلی (D-020..D-029، D-038) تغییر نکردند.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet restore Template.sln` | موفق | SDK 10.0.402 نصب‌شده با `scripts/dev/install-dotnet.sh` |
| `dotnet build Template.sln --no-restore -c Release` | موفق، ۰ خطا، ۶۶۵ هشدار | تلاش اول با قفل فایل `Server/obj` (Build هم‌زمان پس‌زمینهٔ خودم) شکست خورد؛ تلاش دوم موفق |
| `dotnet test tests/Template.Tests ... --filter "FullyQualifiedName!~Integration"` | ۴۲۳ تست: ۴۰۷ موفق، ۰ ناموفق، ۱۶ ردشده | Integration/SQL Server اجرا نشد (Docker daemon در دسترس نیست) |
| `cd ClientApp && npm ci && npm run typecheck && npm test` | موفق؛ ۹۴ از ۹۴ | `npm run build` و `check:offline` اجرا نشد |
| `python3 scripts/check_static_assets.py` | PASS | |
| آزمایش امضا در پوشهٔ موقت خارج از مخزن | ECDSA P-256 و RSA-PSS 3072 موفق؛ Ed25519 در BCL یافت نشد | فقط Linux؛ Windows/ARM/FIPS آزمون نشد |
| CI، E2E، SQL Server، Branch protection، PRها | اجرا/بررسی نشد | توکن `gh` نامعتبر؛ Docker نیست |

## امنیت، داده و سازگاری
- هیچ Secret، کلید خصوصی یا داده مشتری وارد مخزن نشد؛ کلید آزمایشی فقط در حافظهٔ برنامهٔ موقت خارج از مخزن بود و ذخیره نشد.
- بدون Migration و بدون اثر بر داده یا احراز هویت.

## ریسک‌ها و کارهای باقی‌مانده
- پاسخ مالک به پرسش‌های P1 در `LICENSING_OPEN_DECISIONS.md` (OD-01، OD-02، OD-12) پیش‌شرط L1 است؛ پرسش پایه: آیا نصب Productionی نزد مشتری هست؟
- گاردهای G1–G3 هنوز وجود ندارند (AUD-2026-10-24-06).
- ارتقای D-031/D-036 به `Verified` به شاهد CI نیاز دارد.
- تست‌های SQL Server/E2E و CI باید در محیط دارای Docker/GitHub Actions اجرا شوند.
- **انحراف از `CLAUDE.md`:** قاعدهٔ تقسیم کار بین Subagentها برای نگارش به‌کار نرفت (Lead زمینهٔ کامل را داشت و فایل‌ها هم‌پوشان بودند)؛ بازبینی مستقل معماری و امنیت در انتها با Agentهای مخزن انجام شد (نتیجه در گزارش نهایی/PR).

## شناسه‌های Git
- Branch: `docs/licensing-central-system-phase0` (مبنا `main`@`8c1e7a3`)
- Commit / Pull Request: در صفحهٔ PR و گزارش نهایی ثبت می‌شود (شناسه پیش از Commit مشخص نیست)
