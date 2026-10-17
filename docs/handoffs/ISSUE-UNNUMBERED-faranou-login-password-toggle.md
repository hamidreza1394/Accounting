# تحویل وظیفه — ISSUE-UNNUMBERED-faranou-login-password-toggle

- **Issue:** UNNUMBERED
- **Agent/مسئول:** AshnaAI
- **تاریخ:** 2026-10-17
- **وضعیت:** اصلاحات اعمال‌شده؛ CI نهایی در حال اجرا

## هدف و معیارهای پذیرش
- [x] حذف تداخل handlerهای نمایش رمز بدون آسیب به صفحات قدیمی
- [x] هماهنگی selector، نوع فیلد، مقدار رمز، متن، aria و دکمه بدون submit
- [x] بازگردانی محدود پس‌زمینه مستند قبلی از Commit `30fe3e15cf028e18b69234380fb32c52d912ba6c`
- [x] حفظ جریان احراز هویت، Anti-forgery، Lockout، ReturnUrl و پیام‌های فارسی
- [x] افزودن تست رگرسیون برای قرارداد UI/JavaScript

## خلاصه تغییرات
- `site.js` اکنون فقط روی صفحات قدیمی handler عمومی را فعال می‌کند و فرم فرانو را کنار می‌گذارد.
- `taraz-login.js` یک تابع واحد برای تغییر وضعیت رمز، aria، title، متن و آیکون دارد و با `preventDefault` مانع submit ناخواسته می‌شود.
- فرم فرانو دکمه `type="button"`، شناسه هماهنگ `ViewModel_Password` و نشانگر دیداری وضعیت دارد.
- پس‌زمینه navy/teal، هاله‌ها و grid نسخه قبلی به‌صورت scoped در فایل جداگانه اضافه و از Login بارگذاری شده است.
- منطق احراز هویت PageModel و تنظیمات امنیتی تغییر نکرده‌اند.

## فایل‌های مهم تغییرکرده
- `Server/Pages/Account/Login.cshtml` — markup، aria، آیکون و اتصال CSS
- `Server/wwwroot/js/site.js` — جداسازی handler قدیمی
- `Server/wwwroot/js/taraz-login.js` — منطق واحد toggle
- `Server/wwwroot/css/rtl/faranou-login-legacy-background.css` — پس‌زمینه scoped بازیابی‌شده
- `tests/Template.Tests/LoginPasswordToggleTests.cs` — تست رگرسیون قرارداد UI/JS

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| بررسی شاخه و مقایسه با `main` | موفق | شاخه کاری مستقل؛ تغییر کاربر پیش از شروع وجود نداشت |
| بازبینی استاتیک selector/handler/CSS | موفق | دو مالک مستقل: فرانو و صفحات legacy |
| `dotnet build Template.sln --no-restore --configuration Release` | در انتظار CI | محیط API امکان اجرای SDK محلی ندارد |
| `dotnet test Template.sln --no-build --configuration Release` | در انتظار CI | محیط API امکان اجرای SDK محلی ندارد |
| Browser/keyboard/manual UI test | اجرا نشده | Browser Control در دسترس نیست؛ ادعای تست مرورگر نمی‌شود |
| GitHub Actions `.NET CI` | در حال اجرا | نتیجه نهایی هنوز منتشر نشده است |

## امنیت، داده و سازگاری
- مقدار رمز فقط در همان input تغییر نوع می‌دهد و به JavaScript، log یا سرور دیگری ارسال نمی‌شود.
- Anti-forgery و POST واقعی Razor Pages حفظ شده‌اند.
- تغییر UI اعتبارسنجی سمت سرور را دور نمی‌زند.
- کتابخانه جدید یا CDN اضافه نشده است.

## ریسک‌ها و کارهای باقی‌مانده
- نتیجه نهایی CI باید سبز شود؛ در صورت شکست، پیش از PR اصلاح خواهد شد.
- تست رفتاری واقعی مرورگر نیازمند Browser Control یا اجرای محلی برنامه است.

## شناسه‌های Git
- **Branch:** `fix/faranou-login-password-toggle`
- **Commit:** این گزارش با پیام مورد توافق ثبت می‌شود.
- **Pull Request:** هنوز ایجاد نشده است.
