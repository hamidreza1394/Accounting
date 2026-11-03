# تحویل وظیفه — ISSUE-500-utc-kind-value-converter

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/500
- **Agent/مسئول:** ChatGPT — Backend / EF Core
- **تاریخ:** 2026-11-03
- **وضعیت:** پیاده‌سازی اولیه و تست SQL Server اضافه شده؛ CI و بررسی نهایی مدل EF در انتظار است.

## دامنه
- فقط ویژگی‌های EF از نوع DateTime یا DateTime? که نامشان به Utc ختم می‌شود، هنگام خواندن و نوشتن با Kind=Utc مشخص می‌شوند.
- مقدار Ticks تغییر نمی‌کند؛ هیچ تبدیل منطقهٔ زمانی یا افزودن/کسر offset انجام نمی‌شود.
- DateTimeOffset دست‌نخورده است.
- فیلدهای قدیمی InsertDateTime، UpdateDateTime و LastLoginDateTime عمداً از مبدل خارج‌اند؛ آن‌ها هنوز ساعت دیواری محلی سرور هستند.
- هیچ Migration یا تغییر داده‌ای در این برش وجود ندارد.

## پیاده‌سازی
- DateTimeConventions شامل converterهای غیرnullable و nullable شده است.
- DatabaseContext فقط propertyهایی را انتخاب می‌کند که نامشان با Utc تمام می‌شود و نوع CLR آن‌ها DateTime/DateTime? است.
- تست SQL Server، JSON خروجی برای OccurredAtUtc را با Z و Kind=Utc بررسی می‌کند؛ RequestedAtUtc و DecidedAtUtc nullable نیز بررسی می‌شوند.
- همان تست ثابت می‌کند InsertDateTime قدیمی بدون Z و با Kind=Unspecified باقی می‌ماند.

## آزمون و ریسک
- UtcDateTimeKindSqlServerTests باید در CI با SQL Server واقعی اجرا شود.
- بررسی pending model changes ضروری است؛ converter نباید باعث Migration ساختاری شود. اگر EF snapshot آن را ثبت کند، snapshot باید بدون Migration ساختاری به‌روز شود.
- تست characterization موجود UtcWireFormatSqlServerTests در PR #501 متعلق به شاخهٔ Frontend است و انتظار قدیمی Unspecified/no-Z دارد؛ پس از این تغییر باید در همان PR به Utc/Z تغییر کند. برای جلوگیری از تداخل با Claude، آن فایل در این شاخه ویرایش نشده است.
- تبدیل فیلدهای قدیمی به UTC و Migration داده همچنان NeedsApproval است و در این PR انجام نمی‌شود.

## شناسه Git
- Branch: agent/500-utc-kind-value-converter
- Pull Request: در حال ایجاد
