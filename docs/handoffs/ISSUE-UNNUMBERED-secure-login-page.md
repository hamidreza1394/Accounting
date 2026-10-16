# تحویل وظیفه — ISSUE-UNNUMBERED-secure-login-page

- **Issue:** [#7](https://github.com/hamidreza1394/Accounting/issues/7)
- **Agent/مسئول:** Ashna-X1
- **تاریخ:** 2026-10-16
- **وضعیت:** ناقص — نیازمند اجرای Build/Test روی محیط دارای .NET SDK و بازبینی Maintainer

## هدف و معیارهای پذیرش
- [x] بازطراحی واکنش‌گرای صفحه ورود با Bootstrap و دارایی‌های محلی
- [x] پشتیبانی از RTL/LTR و Localization فعلی
- [x] حفظ ارسال واقعی فرم به سمت سرور و صدور Cookie موجود
- [x] حذف لینک‌های گمراه‌کننده Google OAuth و بازیابی غیرفعال
- [ ] اجرای موفق Restore/Build/Test و تست مرورگر
- [ ] افزودن تست Integration واقعی برای احراز هویت

## خلاصه تغییرات
- صفحه ورود به طراحی دو ستونه سازمانی با رنگ‌های سرمه‌ای و فیروزه‌ای، حالت موبایل، آیکون، فوکوس، نمایش/مخفی‌کردن رمز عبور و وضعیت Loading ارتقا یافت.
- از `fa-IR`/`en-US` و Partial تغییر زبان موجود استفاده شد؛ Layout فعلی RTL/LTR همچنان `lang` و `dir` را تنظیم می‌کند.
- فرم همچنان با Razor Pages و اعتبارسنجی سمت سرور کار می‌کند؛ `ReturnUrl` به‌صورت hidden ارسال و در PageModel فقط با `Url.IsLocalUrl` پذیرفته می‌شود.
- Remember Me همچنان از `AuthenticationProperties.IsPersistent` استفاده می‌کند و Logout موجود با `SignOutAsync` نشست را خاتمه می‌دهد.
- ارجاع به `_ValidationScriptsPartial` حذف شد چون چنین فایل مشترکی در مخزن موجود نیست و اسکریپت‌های validation از Layout بارگذاری می‌شوند.

## فایل‌های مهم تغییرکرده
- `Server/Pages/Account/Login.cshtml` — UI، دسترس‌پذیری، پیام‌ها و فرم واقعی
- `Server/wwwroot/css/rtl/site.css` — طراحی ورود RTL و فونت محلی موجود
- `Server/wwwroot/css/ltr/site.css` — طراحی ورود LTR
- `Server/wwwroot/js/site.js` — نمایش رمز، Loading و حفظ TinyMCE موجود با guard
- `docs/handoffs/ISSUE-UNNUMBERED-secure-login-page.md` — گزارش تحویل
- `docs/WORK_LOG.md` — ثبت وضعیت وظیفه

## تصمیم‌های فنی
- Tailwind اضافه نشد؛ Bootstrap محلی و CSS موجود برای جلوگیری از وابستگی و تغییر معماری استفاده شد.
- هیچ Schema، User Store یا پکیج جدیدی اضافه نشد.
- SHA-256 خام همچنان فقط به‌عنوان سازگاری با تنظیمات فعلی باقی مانده و Production-ready تلقی نمی‌شود. جایگزینی امن نیازمند User Store واقعی، `IPasswordHasher<TUser>`/Identity، طرح مهاجرت تدریجی و تست سازگاری است و در این وظیفه اجرا نشد.
- لینک OAuth و بازیابی رمز که پشتوانه عملیاتی ندارند از UI حذف شدند تا رفتار گمراه‌کننده ایجاد نشود.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet restore Template.sln` | اجرا نشده | ابزار اجرای Shell/SDK در این محیط در دسترس نبود |
| `dotnet build Template.sln --no-restore --configuration Release` | اجرا نشده | ابزار اجرای Shell/SDK در این محیط در دسترس نبود |
| `dotnet test Template.sln --no-build --configuration Release` | اجرا نشده | ابزار اجرای Shell/SDK در این محیط در دسترس نبود |
| `python3 scripts/check_static_assets.py` | اجرا نشده | ابزار اجرای Shell در این محیط در دسترس نبود |
| `git diff --check` | اجرا نشده | working tree محلی در اختیار نبود |
| مرورگر/RTL/LTR/کوکی | اجرا نشده | محیط مرورگر و اجرای برنامه در اختیار نبود |

## امنیت، داده و سازگاری
- Cookie در `Program.cs` همچنان HttpOnly، SameSite=Lax و در Production با Secure اجباری است؛ عمر ۳۰ دقیقه و Sliding Expiration حفظ شده است.
- Razor Pages برای فرم POST محافظت Anti-forgery استاندارد دارد؛ اعتبارسنجی سمت سرور در PageModel و DataAnnotations حفظ شده است.
- پیام خطا برای نام کاربری و رمز عبور عمومی و ترجمه‌پذیر باقی مانده است.
- ورود موفق Cookie امضاشده ASP.NET Core صادر می‌کند؛ اطلاعات رمز عبور در Cookie قرار نمی‌گیرد.
- محدودیت مهم باقی‌مانده: احراز هویت legacy با SHA-256 بدون salt و کاربر مرکزی برای Production مناسب نیست و lockout/rate limiting استاندارد در این تغییر اضافه نشده است.
- هیچ تغییر دیتابیس یا Migration انجام نشد.

## ریسک‌ها و کارهای باقی‌مانده
1. اجرای واقعی Build/Test و اصلاح خطاهای احتمالی Razor/CSS در CI یا محیط توسعه.
2. افزودن Integration Test با SQL Server/host واقعی برای موفقیت، شکست، Cookie، Remember Me، Logout و ReturnUrl.
3. طراحی و اجرای مهاجرت تدریجی از SHA-256 خام به PasswordHasher استاندارد بدون قفل‌کردن کاربران موجود.
4. افزودن lockout/rate limiting پس از تعیین سیاست امنیتی سازگار با معماری پروژه.
5. بازبینی UI در مرورگرهای هدف و بررسی کنتراست/ترجمه انگلیسی با داده واقعی منابع.

## شناسه‌های Git
- **Branch:** `agent/UNNUMBERED-secure-login-page`
- **Commit:** در چند commit محدود روی شاخه ثبت شده؛ آخرین commit این handoff پس از ثبت Work Log مشخص می‌شود.
- **Pull Request:** پس از تکمیل ثبت مستندات و بازبینی نهایی ایجاد می‌شود.
