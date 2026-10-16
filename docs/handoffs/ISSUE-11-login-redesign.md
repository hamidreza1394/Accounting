# تحویل وظیفه — ISSUE-11-login-redesign

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/11
- **Agent/مسئول:** AshnaAI
- **تاریخ:** 2026-10-16
- **وضعیت:** ناقص تا تکمیل CI و بازبینی بصری

## هدف و معیارهای پذیرش
- [x] صفحه ورود فارسی، RTL، اختصاصی و واکنش‌گرا.
- [x] حفظ جریان Login، Remember Me، ReturnUrl، Anti-forgery و Validation.
- [x] استفاده از دارایی‌های محلی و مستندسازی.
- [ ] Build/Test واقعی و بازبینی بصری در محیط اجرا.

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

## تصمیم‌های فنی
- Layout عمومی تغییر نکرد تا سایر صفحات تحت تأثیر CSS صفحه ورود قرار نگیرند.
- داده‌های داشبورد تزئینی‌اند و داده واقعی معرفی نمی‌کنند.
- وابستگی خارجی یا پکیج جدید اضافه نشد.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| `dotnet build Template.sln --configuration Release` | اجرا نشده | محیط Agent ابزار dotnet/اجرای محلی ارائه نکرده است. |
| `dotnet test Template.sln --configuration Release` | اجرا نشده | نیازمند SDK و وابستگی‌های پروژه است. |
| `python3 scripts/check_static_assets.py` | اجرا نشده | باید در CI یا محیط توسعه اجرا شود. |
| GitHub Actions | در حال اجرا/نیازمند بررسی | Workflow برای Commitهای شاخه فعال شده است. |
| Browser visual review | انجام نشده | Browser Control در محیط حاضر در دسترس نبود. |

## امنیت، داده و سازگاری
- هیچ Entity، Migration، PageModel یا منطق Sign-in تغییر نکرد.
- Token ضدجعل، ReturnUrl محلی و Remember Me در Markup/رفتار موجود حفظ شدند.
- هش SHA-256 بدون salt همچنان ریسک قبلی پروژه است و در این Issue تغییر داده نشد.

## ریسک‌ها و کارهای باقی‌مانده
- نتیجه نهایی CI و هشدارهای Build بررسی شود.
- صفحه در مرورگر واقعی در عرض‌های دسکتاپ، تبلت و موبایل بررسی شود.
- اجرای برنامه نیازمند Connection String و Secretهای معتبر است.

## شناسه‌های Git
- Branch: `feature/login-redesign`
- Commit: آخرین Commit شاخه هنگام تحویل
- Pull Request: پس از تأیید نتیجه CI ایجاد می‌شود.
