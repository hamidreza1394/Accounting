# Handoff — مبنای مستنداتی S3 بازرگانی خارجی

- تاریخ: 2026-11-14
- شاخه: docs/trade-s3-architecture-baseline
- مبنا: main@b5a6caf6217a054643ba9aed04d7c1b7af831abe
- دامنه: فقط مستندات؛ بدون کد، Entity، API، Migration یا تغییر تنظیمات runtime.

## منابعی که باید پیش از شروع کدنویسی خوانده شوند
1. AGENTS.md و CLAUDE.md
2. docs/INDEX.md
3. docs/DECISIONS.md — D-061..D-086؛ به‌ویژه D-080، D-081، D-084..D-086
4. docs/TRADE_S3_ARCHITECTURE.md
5. docs/TRADE_MODULE.md
6. docs/TRADE_DESIGN_V3_2.md
7. docs/TRADE_OPEN_DECISIONS.md
8. docs/ROADMAP.md و docs/PROJECT_STATUS.md

## تصمیم‌های کلیدی
- پروفایل فعالیت نسخه‌دار و محدود به تنظیمات نوع‌دار؛ بدون workflow engine یا فرم‌ساز عمومی.
- شماره پرونده، Proforma و OrderRegistration در محدوده هر Company یکتا هستند؛ نه در کل Tenant.
- تغییر کالا پیش از ثبت سفارش روی همان پرونده با تاریخچه کامل مجاز؛ پس از ثبت سفارش ممنوع.
- تعداد کالا در پرونده و پیش‌فاکتور مستقل و قابل تنظیم است؛ تعداد Proformaهای جاری و OrderRegistrationهای جاری نیز تنظیم‌پذیر است.
- Revision پیش‌فاکتور تاریخچه قبلی را حفظ می‌کند؛ تغییر فروشنده تابع پروفایل است.
- P-1 در D-080 Accepted: پس از ابطال ثبت سفارش، پرونده جدید با ReplacesImportCaseId.
- P-4/P-5 بازند؛ رفتار وابسته به آن‌ها در S3 پیاده‌سازی نشود.
- ImportCase و پیگیری‌ها مستقل از FiscalPeriod.
- فایل‌ها خارج SQL Server، retention زمانی نامحدود، دسترسی تابع مجوز پرونده.
- Maker-Checker پایه از سیاست مرکزی، برای هر عملیات قابل تنظیم.
- یک پرونده می‌تواند چند بارنامه داشته باشد؛ دو بارنامه از همان ثبت سفارش/کشتی می‌توانند در یک اظهارنامه تجمیعی باشند.

## گام بعدی پس از Merge این PR
S3-0 فقط بررسی read-only مدل فعلی Modules/Trade، V3.2، Migrationها و migration host است. خروجی S3-0 باید ماتریس «موجود/قابل‌استفاده/نیازمند تغییر/جدید» و ERD حداقلی باشد. هیچ جدول/Entity/Migration تا پایان تطبیق مدل ساخته نشود. پس از S3-0، هر برش با Issue و PR مستقل پیش برود.

## موارد باز
- D-080 P-4: رفتار دقیق پس از انقضای Proforma.
- D-080 P-5: تغییر/جایگزینی Proforma با OrderRegistration زنده.
- OD-010: سیاست‌های پیشرفته تأیید در صورت نیاز.
- روش اعمال Migrationهای Trade در customer host.
- ظرفیت، backup/restore و بازیابی فضای ذخیره فایل.
