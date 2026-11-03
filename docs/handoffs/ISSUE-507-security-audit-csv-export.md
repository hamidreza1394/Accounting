# تحویل وظیفه — ISSUE-507-security-audit-csv-export

- Issue: #507 (زیرمجموعه #444 / S7)
- Branch: agent/507-security-audit-csv-export
- Status: API و تست‌ها اضافه شده‌اند؛ CI نهایی در انتظار است.

## قرارداد خروجی
- GET /api/v1/admin/security/audit-events/export
- همان مجوز Installation Administrator و فیلترهای گزارش موجود.
- fromUtc و toUtc اجباری؛ بازه معکوس یا بزرگ‌تر از ۳۱ روز رد می‌شود.
- حداکثر ۱۰٬۰۰۰ ردیف؛ بیش از آن 413 و درخواست بازه/فیلتر محدودتر.
- ترتیب OccurredAtUtc DESC سپس Id DESC.
- CSV با UTF-8 BOM، quoting مطابق RFC 4180 و CRLF برای Excel و متن فارسی.
- سلول‌های متنی با شروع فرمولی (=، +، -، @) پس از whitespace با apostrophe خنثی می‌شوند.
- projection شامل شناسه رویداد، شناسه/نام/نام کاربری عامل، شناسه/نام شرکت، EventType، EntityType، EntityId، OccurredAtUtc و CorrelationId است.
- IpAddress و DetailsJson هرگز صادر نمی‌شوند؛ پاسخ no-store/no-cache است.
- هر خروجی موفق یک SecurityAuditExported event با actor و فیلترهای غیرحساس ثبت می‌کند؛ اگر ثبت audit شکست بخورد فایل تحویل داده نمی‌شود.

## محدوده خارج از PR
- هیچ retention، purge، archive یا حذف رکورد انجام نمی‌شود.
- دوره نگهداری و پاک‌سازی audit نیازمند تصمیم مستقل مالک است؛ هیچ cleanup job مجاز نشده است.

## آزمون‌ها
- Unit: مجوز مدیر نصب، بازه محدود، CSV BOM، escaping، formula injection، حذف IP/DetailsJson، ثبت رویداد export و سقف ۱۰٬۰۰۰.
- SQL Server integration: query/filter translation و ثبت audit export در دیتابیس واقعی.
- CI روی SHA نهایی باید build/test، SQL Server integration، Semgrep و E2E را تأیید کند.
