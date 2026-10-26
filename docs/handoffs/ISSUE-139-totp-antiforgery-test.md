# تحویل وظیفه — ISSUE-139-totp-antiforgery-test

- **Issue:** [#139](https://github.com/hamidreza1394/Accounting/issues/139)
- **Agent/مسئول:** ChatGPT / GitHub repository tools
- **تاریخ:** 2026-10-26
- **وضعیت:** پیاده‌سازی در شاخه؛ منتظر CI

## هدف و معیارهای پذیرش
- [x] تشخیص علت شکست آزمون Password → TOTP پس از ادغام PR #138.
- [x] اصلاح آزمون برای دریافت antiforgery token متناسب با هویت موقت TOTP و سپس هویت احراز هویت‌شده.
- [ ] اجرای موفق آزمون Integration روی SQL Server واقعی در CI.
- [ ] اجرای موفق تمام Vendor tests، EF model snapshot check و Publish.

## خلاصه تغییرات
Vendor CI run [40345676162](https://github.com/hamidreza1394/Accounting/actions/runs/40345676162) شکست آزمون `PasswordOnlyIsDeniedAndTotpChallengeDoesNotIssueApplicationCookie` را ثبت کرد: پاسخ TOTP به‌جای 200 برابر 400 بود. لاگ می‌گوید antiforgery token برای claims-based user دیگری صادر شده بود. پس از ورود رمز، Identity موقت 2FA ایجاد می‌شود؛ پس از تأیید TOTP نیز هویت Application تغییر می‌کند. آزمون اکنون در هر دو مرز هویتی token تازه می‌گیرد و کوکی مناسب را صریح می‌فرستد.

## فایل‌های مهم تغییرکرده
- `tests/Vendor.Host.Tests/VendorAuthenticationSecurityTests.cs` — بازتولید درست تغییر هویت و دریافت توکن antiforgery تازه در مراحل TOTP و درخواست‌های احراز هویت‌شده.

## تصمیم‌های فنی
- کنترل antiforgery سمت سرور حفظ شد؛ آن را غیرفعال یا تضعیف نکردیم.
- تغییر فعلی فقط در آزمون Integration است؛ هنوز شواهدی نداریم که رفتار برنامه Production نیازمند تغییر باشد.
- هیچ Schema/Migration یا دیتابیس مشتری تغییر نکرده است.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| بررسی لاگ Vendor CI | ناموفق | Run 40345676162؛ شکست دقیق در assertion خط 191: Expected OK, Actual BadRequest |
| آزمون اصلاح‌شده | اجرا نشده | باید با CI روی PR اجرا شود |
| تمام Vendor tests | اجرا نشده پس از تغییر | منتظر CI |
| EF snapshot و Publish | در اجرای قبلی Skip شد | به‌علت شکست تست؛ باید پس از اصلاح دوباره اجرا شود |
| بازبینی diff | انجام ایستا | فقط یک فایل تست در این commitها تغییر کرده؛ اجرای محلی در این محیط در دسترس نبود |

## امنیت، داده و سازگاری
- هیچ تغییر در Host، endpointهای احراز هویت، سیاست antiforgery، دیتابیس مشتری یا Migrationهای مشتری انجام نشده است.
- آزمون قبل از تکمیل TOTP همچنان بررسی می‌کند که کوکی Application صادر نشود.

## ریسک‌ها و کارهای باقی‌مانده
- هنوز مشخص نیست CI پس از اصلاح سبز می‌شود؛ توکن تازه در هویت 2FA و سپس Application باید با رفتار واقعی Cookie/Antiforgery آزموده شود.
- اگر CI همچنان شکست خورد، باید لاگ تازه بررسی شود؛ از خاموش‌کردن antiforgery یا حذف assertion خودداری شود.

## شناسه‌های Git
- **Branch:** `fix/issue-139-totp-antiforgery-test`
- **Commits:** `4d4c5465e3cb559588c19700c14a78b2bc8d602d`, `2d4ef844a52a24d0eda5cde7d42c2854b9062053`
- **Pull Request:** پس از آماده‌سازی مستندات باز می‌شود.
