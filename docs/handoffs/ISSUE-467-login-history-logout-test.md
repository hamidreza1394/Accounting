# تحویل وظیفه — ISSUE-467-login-history-logout-test

- **Issue:** [#467](https://github.com/hamidreza1394/Accounting/issues/467)
- **Parent:** #444
- **مسئول:** ChatGPT
- **تاریخ:** 2026-11-03

## نتیجه ممیزی
رفتار API از قبل درست بود: `GetMyLoginHistoryAsync` رویداد `LogoutSucceeded` را در allowlist دارد. مشکل فقط نبود regression test صریح برای خروج موفق بود؛ بنابراین کد API تغییر نکرد.

## تغییر
- یک رویداد LogoutSucceeded متعلق به کاربر جاری به تست self-service اضافه شد.
- تست اکنون بازگشت سه رویداد LoginSucceeded، LoginFailed و LogoutSucceeded را بررسی می‌کند.
- ترتیب نزولی زمان نیز با قرار دادن LogoutSucceeded در اولین نتیجه بررسی می‌شود.
- رویداد کاربر دیگر و رویداد نامرتبط RoleChanged همچنان نباید در history برگردند.

## فایل‌ها
- `tests/Template.Tests/UserSelfServiceEndpointTests.cs`
- `docs/handoffs/ISSUE-467-login-history-logout-test.md`
- `docs/WORK_LOG.md`
- `docs/INDEX.md`

## آزمون
- تست regression به‌روزرسانی شد؛ اجرای dotnet test در محیط فعلی ممکن نیست چون .NET SDK در دسترس نیست.
- CI باید نتیجه تست را تأیید کند.

## محدودیت
- هیچ API، schema، Migration یا Frontend تغییر نکرد.
