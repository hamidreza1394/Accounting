# تحویل وظیفه — ISSUE-152-passkey-options-enumeration

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/152
- **Agent/مسئول:** ChatGPT با اتصال GitHub
- **تاریخ:** 2026-10-26
- **وضعیت:** اصلاح کد ثبت‌شده در شاخه؛ تست و CI هنوز تأیید نشده‌اند

## هدف و معیارهای پذیرش
- [x] پاسخ گزینه‌های Passkey مستقل از نام کاربری و وضعیت ثبت credential باشد.
- [x] حفظ Anti-forgery، محدودسازی نرخ و پاسخ بدون ذخیره‌سازی cache.
- [ ] افزودن آزمون HTTP رگرسیون برای حساب ناموجود، غیرفعال، بدون Passkey و دارای Passkey.
- [ ] اجرای Build/Test و تأیید CI روی PR.

## خلاصه تغییرات
- Endpoint گزینه‌های Passkey دیگر نام کاربری را با UserManager resolve نمی‌کند.
- برای تمام درخواست‌ها MakePasskeyRequestOptionsAsync(user: null) فراخوانی می‌شود تا گزینه‌های discoverable credentials تولید شوند و allowCredentials وابسته به وجود حساب/credential نباشد.
- بدنهٔ درخواست فعلی حفظ شده تا قرارداد HTTP بدون نیاز به تغییر کلاینت سازگار بماند.
- تکرار بی‌دلیل هدر Cache-Control: no-store حذف شد.

## فایل‌های مهم تغییرکرده
- Vendor/Vendor.Host/Authentication/VendorAuthenticationEndpoints.cs — اصلاح endpoint گزینه‌های Passkey.

## تصمیم‌های فنی
- بر اساس مستندات رسمی ASP.NET Core 10، ارسال کاربر مشخص باعث درج credentialهای همان کاربر در allow-list می‌شود؛ ارسال null برای discoverable credentials و ورود بدون نام کاربری مناسب است.
- هیچ تغییر در دیتابیس، Migration مشتری، جریان MFA، Anti-forgery یا rate limiting انجام نشده است.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| بازبینی سورس و مستندات رسمی ASP.NET Core 10 | انجام شد | کاربر مشخص allow-list اختصاصی می‌سازد و null برای discoverable credentials پشتیبانی می‌شود. |
| آزمون HTTP رگرسیون | اجرا نشده | هنوز باید به تست‌های Vendor افزوده شود. |
| dotnet build | اجرا نشده | در این نوبت محیط اجرای مخزن در اختیار نبود. |
| dotnet test | اجرا نشده | منتظر افزودن آزمون و اجرای CI. |
| GitHub Actions | تأیید نشده | PR هنوز ایجاد نشده است. |

## امنیت، داده و سازگاری
- پاسخ options دیگر برای وجود حساب/Passkey شناسه credential را فاش نمی‌کند.
- رفتار سمت کاربر باید با ورود discoverable/username-less سازگار باشد؛ سازگاری کامل UI و مرورگر باید با تست انتها‌به‌انتها بررسی شود.
- هیچ Schema یا Migration تغییر نکرده است.

## ریسک‌ها و کارهای باقی‌مانده
- افزودن آزمون HTTP که در پاسخ‌های تولیدشده هیچ allow-list مختص کاربر افشا نشود.
- اجرای Build، تمام تست‌های Vendor، SQL Server integration، strict EF snapshot gate و publish checks.
- بازبینی frontend برای اینکه در شروع ورود، نام کاربری را شرط موفقیت passkey قرار نداده باشد.

## شناسه‌های Git
- Branch: fix/issue-152-passkey-options-enumeration
- Commit: 9b4ea03e45afc2de1af88293abf3ad998b02e565
- Pull Request: در حال آماده‌سازی
