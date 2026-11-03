# تحویل وظیفه — ISSUE-485-security-audit-report-api

- **Issue:** #485 (زیرمجموعه #444)
- **مسئول:** ChatGPT — Backend/API و تست
- **تاریخ:** 2026-11-03
- **وضعیت:** پیاده‌سازی و تست‌ها در PR؛ Verified منوط به CI سبز

## قرارداد API
- GET `/api/v1/admin/security/audit-events`
- فیلترهای exact: `eventType`, `userId`, `companyId`, `fromUtc`, `toUtc`.
- صفحه‌بندی محدود: `page` و `pageSize`؛ مرتب‌سازی قطعی بر اساس زمان نزولی و سپس شناسه نزولی.
- احراز مجدد مدیر نصب از جدول Users و Role فعال با helper موجود.
- پاسخ فقط metadata ضروری رویداد، نام کاربر و نام شرکت را برمی‌گرداند؛ `IpAddress` و `DetailsJson` حذف شده‌اند.

## امنیت و محدودیت‌ها
- هیچ ادعایی درباره مدت نشست، زمان انقضا یا خروج ضمنی از نبود رویداد نمی‌شود.
- API installation-wide و فقط برای Installation Administrator است؛ سیاست Company Manager در این برش فعال نشده است.
- داده‌های موجود، بدون جدول یا migration جدید خوانده می‌شوند.
- retention، export و پاک‌سازی داده خارج از این PR هستند و نیازمند تصمیم/سیاست جداگانه‌اند.

## آزمون
- Unit: مجوز مدیر/عدم مجوز، فیلترهای ترکیبی، صفحه‌بندی، ترتیب، ورودی نامعتبر و نبود IP/DetailsJson در DTO.
- SQL Server Integration: query واقعی و projection نام کاربر/شرکت روی SQL Server.
- اجرای محلی در این محیط ممکن نیست (GitHub DNS و .NET SDK در دسترس نیست)؛ CI باید نتیجه نهایی را ثابت کند.
