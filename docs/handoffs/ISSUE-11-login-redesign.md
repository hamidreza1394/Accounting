# تحویل وظیفه — ISSUE-11-login-redesign

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/11
- **Agent/مسئول:** AshnaAI
- **تاریخ:** 2026-10-16
- **وضعیت:** آماده بررسی در Draft Pull Request

## هدف و معیارهای پذیرش
- [x] صفحه ورود فارسی، RTL، اختصاصی و واکنش‌گرا.
- [x] حفظ جریان Login، Remember Me، ReturnUrl، Anti-forgery و Validation.
- [x] استفاده از دارایی‌های محلی و مستندسازی.
- [x] Build/Test و static asset check در CI موفق.
- [ ] بازبینی بصری در مرورگر واقعی.

## خلاصه تغییرات
- صفحه Login با ساختار دو بخشی و کارت ورود حرفه‌ای بازطراحی شد.
- لوگوی SVG محلی، نمودار تزئینی SVG، CSS اختصاصی و JavaScript محدود اضافه شد.
- PageModel، مدل فرم و احراز هویت تغییر نکردند.

## فایل‌های مهم تغییرکرده
- `Server/Pages/Account/Login.cshtml`
- `Server/wwwroot/css/rtl/taraz-login.css`
- `Server/wwwroot/js/taraz-login.js`
- `Server/wwwroot/images/taraz-ava-mark.svg`
- `docs/login-redesign.md`
- `docs/WORK_LOG.md`

## تصمیم‌های فنی
- Layout عمومی تغییر نکرد تا سایر صفحات تحت تأثیر CSS صفحه ورود قرار نگیرند.
- داده‌های داشبورد تزئینی‌اند و داده واقعی معرفی نمی‌کنند.
- وابستگی خارجی یا پکیج جدید اضافه نشد.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| GitHub Actions `build-and-test` | موفق | شامل static asset check، restore، build و test؛ Run `38921084636`. |
| `dotnet build Template.sln --configuration Release` محلی | اجرا نشده | محیط Agent ابزار dotnet محلی ارائه نکرده است. |
| `dotnet test Template.sln --configuration Release` محلی | اجرا نشده | نتیجه معتبر CI موجود است. |
| `python3 scripts/check_static_assets.py` محلی | اجرا نشده | همین اسکریپت در CI موفق اجرا شد. |
| Browser visual review | انجام نشده | Browser Control در محیط حاضر در دسترس نبود. |
| اجرای برنامه با دیتابیس | انجام نشده | Connection String و Secret معتبر در محیط موجود نبود. |

## امنیت، داده و سازگاری
- هیچ Entity، Migration، PageModel یا منطق Sign-in تغییر نکرد.
- Token ضدجعل، ReturnUrl محلی و Remember Me در Markup/رفتار موجود حفظ شدند.
- هش SHA-256 بدون salt همچنان ریسک قبلی پروژه است و در این Issue تغییر داده نشد.

## ریسک‌ها و کارهای باقی‌مانده
- بازبینی بصری در مرورگر واقعی در دسکتاپ، تبلت و موبایل.
- اجرای end-to-end با محیط توسعه دارای SQL Server و Secretهای معتبر.
- بررسی Rate Limiting/Account Lockout و مهاجرت PasswordHasher در Issue امنیتی مستقل.

## شناسه‌های Git
- Branch: `feature/login-redesign`
- Latest verified before PR: `6ebaf2f52582f134605a5f59009aa5e63dd46940`
- Work Log update commit: `0ea76b91e733d6fecc878b8d6f794b4ba98de29a`
- Pull Request: Draft بعد از آخرین Commit ایجاد می‌شود.
