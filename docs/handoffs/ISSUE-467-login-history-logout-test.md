# تحویل وظیفه — ISSUE-467-login-history-logout-test

- **Issue:** [#467](https://github.com/hamidreza1394/Accounting/issues/467)
- **Parent:** #444
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-11-03
- **وضعیت:** پیاده‌سازی تست و مستندات؛ تأیید CI در انتظار

## هدف و معیارهای پذیرش
- [x] پوشش صریح رویداد موفق `LogoutSucceeded` در تاریخچهٔ کاربر جاری.
- [x] حفظ محدودیت فقط‌خود، allowlist رویدادها و ترتیب نزولی.
- [x] عدم تغییر API، Schema، Migration یا Frontend.

## خلاصه تغییرات
- تست تاریخچه اکنون LoginSucceeded، LoginFailed و LogoutSucceeded را ثبت و انتظار دارد.
- رویداد خروج جدیدترین زمان را دارد و باید اولین آیتم پاسخ باشد.
- رویداد کاربر دیگر و رویداد غیرمرتبط RoleChanged همچنان نباید بازگردند.

## فایل‌های مهم تغییرکرده
- `tests/Template.Tests/UserSelfServiceEndpointTests.cs` — regression coverage برای LogoutSucceeded.
- `docs/handoffs/ISSUE-467-login-history-logout-test.md` — این گزارش.
- `docs/WORK_LOG.md` و `docs/INDEX.md` — ثبت افزایشی تحویل.

## تصمیم‌های فنی
- ممیزی کد موجود نشان داد API از قبل LogoutSucceeded را در allowlist قرار می‌دهد؛ فقط تست ناقص بود. هیچ رفتار تولیدی تغییر نکرد.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet test tests/Template.Tests/Template.Tests.csproj --filter FullyQualifiedName~UserSelfServiceEndpointTests` | اجرا نشده در محیط ChatGPT | CI این PR باید اجرا و بررسی شود. |
| `dotnet build Template.sln --configuration Release` | اجرا نشده در محیط ChatGPT | اجرای محلی در این محیط در دسترس نیست. |
| `git diff --check` | اجرا نشده در محیط ChatGPT | تغییرات از طریق GitHub API ثبت می‌شوند؛ CI/بازبینی diff لازم است. |

## امنیت، داده و سازگاری
- فقط داده تست تغییر کرده است. API همچنان فقط رخدادهای احراز هویت کاربر جاری را بازمی‌گرداند؛ هیچ فیلد حساس یا رخداد audit دیگری افشا نمی‌شود.
- بدون Migration و بدون تغییر رفتار runtime.

## ریسک‌ها و کارهای باقی‌مانده
- نتیجهٔ .NET CI و بررسی diff پس از ایجاد PR باید تأیید شود.

## شناسه‌های Git
- Branch: `agent/467-login-history-logout-test`
- Commit: در PR ثبت می‌شود.
- Pull Request: پس از ایجاد ثبت می‌شود.
