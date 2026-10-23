# بازطراحی صفحه ورود تراز آوا

## هدف
صفحه ورود فارسی و RTL با الهام از تصویر مرجع مأموریت بازطراحی شد. ترکیب نهایی شامل پس‌زمینه چندلایه، بخش معرفی/داشبورد مالی تزئینی در سمت چپ، کارت ورود شیشه‌ای در سمت راست و هویت بصری اختصاصی تراز آوا است.

## فایل‌های تغییرکرده
- `Server/Pages/Account/Login.cshtml` — بازطراحی Markup صفحه بدون تغییر PageModel.
- `Server/wwwroot/css/rtl/taraz-login.css` — رنگ، چیدمان، داشبورد SVG، واکنش‌گرایی، حالت‌های Focus/Error/Disabled و reduced-motion.
- `Server/wwwroot/js/taraz-login.js` — نمایش/مخفی‌سازی رمز عبور و جلوگیری از ارسال تکراری.
- `Server/wwwroot/images/taraz-ava-mark.svg` — لوگوی محلی و اختصاصی برند.

## تصمیم‌های UI/UX
- از پالت `#102B4E`، `#174C83`، `#168CC7`، `#17B8C5` و `#35D5E8` با نارنجی محدود استفاده شده است.
- نمودارها و اعداد داخل داشبورد صرفاً تزئینی‌اند و به داده واقعی کسب‌وکار اشاره نمی‌کنند.
- فونت محلی `IRANSansX` پروژه استفاده می‌شود؛ وابستگی CDN یا تصویر خارجی اضافه نشده است.
- در عرض‌های کوچک بخش داشبورد پنهان و کارت ورود تمام‌عرض می‌شود.

## حفظ عملکرد و امنیت
- `@model Server.Pages.Security.LoginModel`، `ReturnUrl`، Propertyهای ViewModel و POST فعلی حفظ شده‌اند.
- `@Html.AntiForgeryToken()` صریحاً در فرم باقی مانده است.
- اعتبارسنجی سمت سرور، ValidationSummary و پیام‌های خطای فیلدی حفظ شده‌اند.
- Remember Me، Cookie Sign-in، Claims و کنترل `Url.IsLocalUrl` در PageModel تغییر نکرده‌اند.
- هیچ رمز، Token یا داده محرمانه‌ای به دارایی‌های جدید اضافه نشده است.

## اجرا و بررسی
```bash
dotnet restore Template.sln
dotnet build Template.sln --configuration Release
dotnet test Template.sln --configuration Release
python3 scripts/check_static_assets.py
dotnet run --project Server/Server.csproj
```

Build/Test محلی توسط Agent اجرا نشده است. CI برای شاخه فعال شده و باید پس از پایان بررسی شود. اتصال دیتابیس و Secretهای Login برای اجرای کامل برنامه لازم هستند.

## مواردی که Agent بعدی نباید بدون بررسی تغییر دهد
- PageModel و مدل `LoginViewModel`.
- نام فیلدهای `ViewModel.Username`، `ViewModel.Password` و `ViewModel.RememberMe`.
- `ReturnUrl` و کنترل local بودن آن.
- مسیرهای محلی فونت، SVG و فایل‌های CSS/JS.
- ریسک SHA-256 بدون salt؛ اصلاح آن پروژه امنیتی جداگانه و نیازمند طرح مهاجرت است.

## به‌روزرسانی 2026-10-23
هویت بصری جدید فرانو و پالایش UI/UX صفحه ورود روی شاخهٔ `ui/login-professional-polish` انجام شد. توضیحات بالا دربارهٔ «تراز آوا»، پالت قدیمی و پنهان‌شدن داشبورد در عرض‌های کوچک تاریخی است. وضعیت فعلی: نشان `faranou-logo-mark.svg`، داشبورد نمایشی در عرض ≤ ۸۲۰px پنهان است و جزئیات و آزمون‌ها در `docs/handoffs/ISSUE-UNNUMBERED-login-professional-ui.md` آمده است.
