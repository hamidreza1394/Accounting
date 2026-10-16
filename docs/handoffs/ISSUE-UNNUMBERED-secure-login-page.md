# تحویل وظیفه — ISSUE-UNNUMBERED-secure-login-page

- **Issue:** [#7](https://github.com/hamidreza1394/Accounting/issues/7)
- **Agent/مسئول:** Ashna-X1
- **تاریخ:** 2026-10-16
- **وضعیت:** ناقص — اصلاحات ثبت شده، CI نهایی باید تأیید شود؛ بازبینی Maintainer لازم است

## تغییرات این دور
- علت شکست تست بررسی شد: اجرای `dotnet test` در مسیر VSTest با SDK جدیدتر و Microsoft.Testing.Platform، در حالی که پروژه تست روی `Microsoft.NET.Test.Sdk` و xUnit Visual Studio Adapter است.
- `global.json` با SDK سازگار `9.0.100` اضافه شد؛ Target Framework، پکیج‌ها و معماری تغییر نکردند.
- بخش برند دیگر عنوان صفحه را تکرار نمی‌کند؛ نام تجاری واقعی در منابع/تنظیمات پروژه پیدا نشد و نام جدیدی جعل نشد.
- JavaScript به `.login-form` محدود شد؛ فرم‌های دیگر برنامه تحت تأثیر handler ورود قرار نمی‌گیرند و ارسال با Enter نیز با اعتبارسنجی/Loading اختصاصی فرم انجام می‌شود.

## خلاصه تغییرات اصلی
- صفحه ورود دو ستونه، واکنش‌گرا، RTL/LTR و مبتنی بر Bootstrap/دارایی‌های محلی.
- نمایش/مخفی‌سازی رمز عبور، Loading و پیام‌های اعتبارسنجی.
- حفظ فرم واقعی Razor Pages، Cookie، Remember Me، Logout و کنترل ReturnUrl محلی.
- حذف لینک‌های غیرفعال Google OAuth و بازیابی رمز عبور.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| `dotnet restore Template.sln` | CI قبلی موفق | Restore پیش از Build تکمیل شد |
| `dotnet build Template.sln --no-restore --configuration Release` | CI قبلی موفق | Build با تغییرات صفحه ورود موفق بود |
| `dotnet test Template.sln --no-build --configuration Release` | CI قبلی ناموفق | شکست VSTest/Microsoft.Testing.Platform بررسی و با تثبیت SDK اصلاح شد |
| `python3 scripts/check_static_assets.py` | در CI اجرا می‌شود | نتیجه اجرای جدید باید از لینک CI تأیید شود |
| `git diff --check` | اجرا نشده | Shell محلی در این محیط در دسترس نیست |
| CI پس از اصلاح | Pending | آخرین اجرای شناخته‌شده روی `b54ac4214a6ae54fe297682791445ce465ab0894` است |
| مرورگر/RTL/LTR/کوکی | اجرا نشده | محیط مرورگر در اختیار نیست |

## امنیت، داده و سازگاری
- احراز هویت Legacy با SHA-256 بدون salt و کاربر مرکزی همچنان باقی است و Production-ready نیست.
- Rate Limiting و Account Lockout اضافه نشده‌اند.
- Integration Test واقعی برای Authentication، Cookie، Remember Me، Logout و ReturnUrl وجود ندارد.
- Cookie فعلی HttpOnly، SameSite=Lax و در Production Secure است؛ عمر و Sliding Expiration حفظ شده‌اند.
- هیچ Schema یا Migration تغییر نکرد.
- هیچ رمز عبور، Secret یا حساب ثابت جدیدی اضافه نشد.

## کارهای باقی‌مانده
1. تأیید نتیجه نهایی CI روی آخرین commit.
2. اجرای مرورگر و تست Integration واقعی.
3. طراحی مهاجرت تدریجی از SHA-256 خام به PasswordHasher استاندارد بدون قفل‌کردن کاربران موجود.
4. تعیین و پیاده‌سازی سیاست Lockout/Rate Limiting در Issue جداگانه.

## شناسه‌های Git
- **Branch:** `agent/UNNUMBERED-secure-login-page`
- **Base:** `main`
- **Latest implementation commit:** [`728bc0a8904c23791c175141b8cac8cebb7df068`](https://github.com/hamidreza1394/Accounting/commit/728bc0a8904c23791c175141b8cac8cebb7df068)
- **Latest SDK pin commit:** [`b54ac4214a6ae54fe297682791445ce465ab0894`](https://github.com/hamidreza1394/Accounting/commit/b54ac4214a6ae54fe297682791445ce465ab0894)
- **Pull Request:** [#8](https://github.com/hamidreza1394/Accounting/pull/8)
