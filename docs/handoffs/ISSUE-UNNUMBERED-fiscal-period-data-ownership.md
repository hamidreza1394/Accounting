# تحویل معماری — ISSUE-UNNUMBERED-fiscal-period-data-ownership

- **Issue:** UNNUMBERED (مأموریت مستندسازی معماری؛ Issue اجرایی هنوز ایجاد نشده)
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-11-23
- **وضعیت:** مستندسازی انجام شد؛ پیاده‌سازی مسدود تا Issue و Gateهای لازم

## هدف و معیارهای پذیرش
- [x] ثبت تصمیم‌های مالک درباره مالکیت داده، توپولوژی، OriginFiscalPeriodId، ادامه پرونده، شماره‌گذاری و افتتاحیه.
- [x] تفکیک مالکیت Company-level، داده period-scoped، Snapshot و Participation.
- [x] ارائه مدل منطقی جداول و فیلدهای پیشنهادی بدون ساخت Schema.
- [x] ثبت مرزهای امنیتی، idempotency، concurrency، cross-database و گیت‌های پیش از DDL.
- [x] حفظ وضعیت Pending توپولوژی Tenant + FiscalPeriod و گیت‌های S3-0.

## خلاصه تغییرات
- D-117 به‌عنوان تصمیم مالک برای سیاست مالکیت داده بین توپولوژی‌ها و ادامه پرونده در سال جدید ثبت شد.
- FISCAL_PERIOD_DATA_MODEL.md به‌عنوان مدل منطقی پیشنهادی همراه، جداول و فیلدهای مفهومی، قیود، ایندکس‌ها، عملیات rollover، Snapshot، شماره‌گذاری و افتتاحیه را تعریف می‌کند.
- هیچ کد، Entity، DbSet، API، Permission، Schema، Migration، Backfill یا داده‌ای تغییر نکرد.

## فایل‌های مهم تغییرکرده
- docs/DECISIONS.md — افزودن D-117.
- docs/FISCAL_PERIOD_DATA_MODEL.md — مدل منطقی جداول و قراردادهای داده.
- docs/DATABASE.md — ارجاع به مدل هدف جدید.
- docs/INDEX.md — ثبت سند جدید در فهرست منابع.
- docs/WORK_LOG.md — ثبت append-only این تحویل.
- docs/handoffs/ISSUE-UNNUMBERED-fiscal-period-data-ownership.md — این گزارش.

## تصمیم‌های فنی
- Company DB پایدار برای داده‌های بلندمدت و مرجع در Company + FiscalPeriod DB؛ DB دوره‌ای برای اسناد مالی و Snapshotهای موردنیاز.
- پرونده‌ها OriginFiscalPeriodId اجباری و immutable دارند؛ این فیلد از FiscalPeriodId عملیاتی جداست و استقلال Trade از دوره طبق D-086 را نقض نمی‌کند.
- جدول مشارکت تخصصی هر زیرسیستم؛ Read Model/Query تجمیعی برای فهرست یکپارچه.
- فقط Open به‌صورت پیش‌فرض carry-forward؛ OnHold تا تصمیم جداگانه خارج از انتقال خودکار.
- مدیر مجاز همان Company با Permission مستقل و Audit؛ مدیر سراسری نصب به‌صورت ضمنی مجاز نیست.
- شماره‌گذاری پیوسته پیش‌فرض؛ ریست سالانه قابل تنظیم.
- افتتاحیه موقت و قابل بازسازی؛ ایجاد سال جدید منتظر بستن قطعی سال قبل نیست.
- هیچ Cross-DB FK، distributed transaction یا dual-write دائمی.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| بررسی ساختار مستندات GitHub | انجام شد | AGENTS.md، INDEX، DECISIONS، DATABASE، WORK_LOG، D-111..D-116 و اسناد مرتبط مرور شدند |
| Build/Test | اجرا نشده | فقط مستندات تغییر کرده‌اند؛ runtime تغییری نکرد |
| git diff --check | اجرا نشده | تغییرات از طریق GitHub API ثبت شده‌اند؛ PR diff باید بررسی شود |
| Schema/Migration validation | اجرا نشده | عمداً هیچ DDL/Migration ساخته نشده |

## امنیت، داده و سازگاری
- D-064 و کنترل CompanyAccessService حفظ می‌شوند.
- D-087: TenantId و CompanyId مستقل باقی می‌مانند.
- D-115: Registry سمت سرور تنها مرجع Routing است.
- D-086: هیچ TradeOperationalPeriod ساخته نمی‌شود.
- D-063/D-074: تغییر Context مستقل از دوره Blocked تا Security Review و تست منفی.
- گیت‌های S3-0، Migration/Backfill و تأیید مالک دور زده نشده‌اند.

## ریسک‌ها و کارهای باقی‌مانده
- ممیزی واقعی Entityها/جداول Party، Product، Trade case و Accounting opening balance برای تطبیق فیلدها.
- هم‌ترازی دقیق D-105/D-111 با Snapshot دوره‌ای و Company owner.
- Security Review برای N-5 و Permission Catalog.
- استخراج mapping وضعیت پرونده‌های هر زیرسیستم.
- تعیین retention/archive برای Company owner.
- ساخت Issue اجرایی مستقل پس از بازبینی مالک و عبور از گیت‌ها.

## شناسه‌های Git
- Branch: docs/fiscal-period-data-ownership-carryover
- Commit: در این PR ثبت می‌شود.
- Pull Request: پس از ثبت فایل‌ها.
