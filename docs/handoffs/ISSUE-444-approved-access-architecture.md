# تحویل — ISSUE-444-approved-access-architecture

- Issue: #444
- Agent: ChatGPT GitHub connector
- تاریخ: 2026-11-03
- وضعیت: مستندات معماری ثبت‌شده؛ پیاده‌سازی runtime انجام نشده است.

## معیارها
- [x] تخصیص سال و دوره مالی
- [x] directory محدود فقط هم‌گروه
- [x] هر دو دامنه Company و Group
- [x] تأیید تمام عضویت‌های بین‌شرکتی
- [x] threat model، API intent، مرز ماژول و مسیر extraction
- [ ] reconcile و اجرای PRهای runtime و تست SQL Server

## تغییرات
D-065 و ACCESS_CONTROL_ARCHITECTURE.md افزوده شدند. INDEX، ARCHITECTURE، SECURITY، TESTING، USER_ACCESS_MANAGEMENT و WORK_LOG به‌روزرسانی شدند. Issue #444 با تصمیمات مصوب comment شد. هیچ runtime code یا migration تغییر نکرد.

## تصمیم فنی
چهار تصمیم D-065 Accepted هستند. مدل دقیق grant سال/دوره و Domain mapping باید با مدل واقعی و SQL Server constraints تطبیق داده شود؛ نام‌های مفهومی سند schema نهایی نیستند.

## آزمون
| فرمان | نتیجه |
|---|---|
| Markdown lint | اجرا نشده |
| dotnet build/test | اجرا نشده؛ تغییر صرفاً مستندات |
| SQL Server migration tests | اجرا نشده؛ migration ساخته نشده |
| CI | پس از PR باید بررسی شود |

## ریسک و ادامه
مدل واقعی FiscalPeriod باید پیش از طراحی grant بررسی شود. Host resolution و approval flow باید با API و session فعلی تطبیق یابند. PRهای #445..#456 نیازمند reconcile هستند.

## Git
- Branch: docs/issue-444-approved-access-architecture
- آخرین Commit شاخه مستندات: dd5615f17b34ab7c7bacca6cd8422ee9a97de212
- Pull Request: #457 — https://github.com/hamidreza1394/Accounting/pull/457
