# تحویل وظیفه — ISSUE-UNNUMBERED-secure-login-page

- **Issue:** [#7](https://github.com/hamidreza1394/Accounting/issues/7)
- **Agent/مسئول:** Ashna-X1
- **تاریخ:** 2026-10-16
- **وضعیت:** ناقص — CI اصلاح شده و اجرای نهایی در حال انجام است؛ بازبینی Maintainer لازم است

## تحلیل علت CI
- پروژه تست روی `net9.0` از `xunit.v3` نسخه `4.0.2` استفاده می‌کرد و هم‌زمان `Microsoft.NET.Test.Sdk` و `xunit.runner.visualstudio` را داشت.
- CI از SDK شناور `9.0.x` استفاده می‌کرد و `global.json` نداشت؛ در نتیجه انتخاب مسیر VSTest/MTP صریح و قابل تکرار نبود.
- مستندات رسمی xUnit v3 می‌گوید xUnit v3 نسخه 4 به‌صورت پیش‌فرض MTP v2 دارد؛ برای `dotnet test` روی SDKهای 8/9 باید `TestingPlatformDotnetTestSupport=true` در پروژه تست فعال شود. همچنین در مسیر VSTest، Adapter/ Test SDK استفاده می‌شوند.
- تغییر کم‌ریسک اعمال‌شده: حذف `Microsoft.NET.Test.Sdk` و `xunit.runner.visualstudio` از پروژه تست، افزودن `TestingPlatformDotnetTestSupport=true` و تثبیت SDK با `global.json` روی `9.0.100`. Target Framework، نسخه `xunit.v3` و معماری برنامه تغییر نکردند.
- لاگ اجرای جدید پس از Commit `38c69b71e8e5e4e78f1ab5f177c49d970d38a0e2` در زمان ثبت این Handoff هنوز نهایی نشده است؛ بنابراین رفع CI قطعی اعلام نمی‌شود.

## تغییرات UI و JavaScript
- عنوان تکراری از بخش برند حذف شد؛ چون نام تجاری واقعی در تنظیمات/منابع موجود نبود، نام جدیدی اضافه نشد.
- JavaScript فقط روی `.login-form` نصب می‌شود؛ فرم‌های دیگر برنامه handler ورود، Loading یا disable شدن دکمه‌های ورود را دریافت نمی‌کنند.
- رویداد submit اعتبارسنجی موجود را اجرا می‌کند؛ در خطای اعتبارسنجی ارسال متوقف می‌شود و Loading فعال نمی‌شود. در حالت معتبر، کنترل‌های فرم ورود غیرفعال، Loading فعال و ارسال native فرم انجام می‌شود؛ Enter همان مسیر را طی می‌کند.

## بررسی و آزمون
| مرحله | نتیجه واقعی | توضیح |
|---|---|---|
| بررسی `Template.Tests.csproj` و `Directory.Packages.props` | موفق | xUnit `4.0.2`، SDK پروژه `net9.0` و وابستگی‌های قبلی ثبت شد |
| بررسی لاگ/Runهای CI قبلی | شکست قبلی تأیید شد | Build موفق و Test در مسیر VSTest/MTP شکست خورد؛ اجرای دقیق جدید هنوز در حال انجام است |
| `dotnet restore Template.sln` | CI جدید در حال اجرا | نتیجه نهایی هنوز منتشر نشده |
| `dotnet build Template.sln --no-restore --configuration Release` | CI جدید در حال اجرا | نتیجه نهایی هنوز منتشر نشده |
| `dotnet test Template.sln --no-build --configuration Release` | CI جدید در حال اجرا | نتیجه نهایی هنوز منتشر نشده |
| `python3 scripts/check_static_assets.py` | CI جدید در حال اجرا | نتیجه نهایی هنوز منتشر نشده |
| `git diff --check` | اجرا نشده | Shell محلی در این محیط در دسترس نیست |
| مرورگر/RTL/LTR/کوکی | اجرا نشده | محیط مرورگر در اختیار نیست |

## امنیت، داده و سازگاری
- احراز هویت Legacy با SHA-256 بدون salt و کاربر مرکزی همچنان باقی است و Production-ready نیست.
- Rate Limiting و Account Lockout اضافه نشده‌اند.
- Integration Test واقعی برای Authentication، Cookie، Remember Me، Logout و ReturnUrl وجود ندارد.
- Cookie فعلی HttpOnly، SameSite=Lax و در Production Secure است؛ عمر و Sliding Expiration حفظ شده‌اند.
- هیچ Schema یا Migration تغییر نکرد.
- هیچ رمز عبور، Secret یا حساب ثابت جدیدی اضافه نشد.

## کارهای باقی‌مانده
1. تأیید نتیجه نهایی CI روی Commit `38c69b71e8e5e4e78f1ab5f177c49d970d38a0e2`.
2. اجرای مرورگر و تست Integration واقعی.
3. طراحی مهاجرت تدریجی از SHA-256 خام به PasswordHasher استاندارد بدون قفل‌کردن کاربران موجود.
4. تعیین و پیاده‌سازی سیاست Lockout/Rate Limiting در Issue جداگانه.

## شناسه‌های Git
- **Branch:** `agent/UNNUMBERED-secure-login-page`
- **Base:** `main`
- **Latest code commit:** [`38c69b71e8e5e4e78f1ab5f177c49d970d38a0e2`](https://github.com/hamidreza1394/Accounting/commit/38c69b71e8e5e4e78f1ab5f177c49d970d38a0e2)
- **Pull Request:** [#8](https://github.com/hamidreza1394/Accounting/pull/8)
