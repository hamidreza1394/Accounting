# تحویل وظیفه — ISSUE-145-totp-cookie-selection

- **Issue:** [#145](https://github.com/hamidreza1394/Accounting/issues/145)
- **تاریخ:** 2026-10-26
- **Branch:** `fix/issue-145-totp-cookie-selection`
- **وضعیت:** اصلاح آزمون ثبت شده؛ CI پس از تغییر هنوز باید اجرا شود

## هدف و علت
Vendor CI run [40352627665](https://github.com/hamidreza1394/Accounting/actions/runs/40352627665) پس از Merge شدن PR #144 در آزمون `PasswordOnlyIsDeniedAndTotpChallengeDoesNotIssueApplicationCookie` شکست خورد. خطا `Sequence contains more than one matching element` در استخراج کوکی Application بعد از تأیید TOTP بود.

## بررسی ریشه‌ای
در `VendorAuthenticationEndpoints.cs`، مسیر TOTP ابتدا `TwoFactorAuthenticatorSignInAsync` را فراخوانی می‌کند و سپس برای افزودن claim مربوط به عامل تأییدشده (`amr=otp`) از `SignInWithClaimsAsync` استفاده می‌کند. هر دو مرحله می‌توانند هدر `Set-Cookie` برای کوکی Application اضافه کنند؛ بنابراین `Single` در آزمون فرض نامعتبر «دقیقاً یک هدر منطبق» داشت. اصلاح انتخاب آخرین کوکی Application را انجام می‌دهد، نه حذف کنترل امنیتی.

## تغییرات
- `tests/Vendor.Host.Tests/VendorAuthenticationSecurityTests.cs`: استفاده از آخرین مقدار کوکی Application در هدرهای پاسخ TOTP و توضیح دلیل فنی.
- `docs/WORK_LOG.md`: ثبت خطای واقعی CI، علت و وضعیت اعتبارسنجی.
- این handoff: گزارش فارسی برای ادامهٔ کار.

## محدوده امنیتی
- هیچ assertion درباره رد ورود password-only، نیاز به TOTP، ایجاد نشست پس از TOTP یا antiforgery حذف نشده است.
- منطق احراز هویت Production، دیتابیس مشتری و Migrationهای مشتری تغییر نکرده‌اند.

## آزمون و اعتبارسنجی
- CI مبنا: Vendor CI run `40352627665`؛ Build و تست UI موفق بودند، اما آزمون Integration فوق شکست خورد.
- اصلاح روی شاخه GitHub ثبت شده است.
- **هنوز اجرا/تأیید نشده:** `dotnet test Vendor.sln --configuration Release` روی SQL Server، EF model snapshot gate و publish standalone پس از اصلاح.
- تا CI تازه سبز نشود، تغییر `Verified` نیست.

## شناسه‌ها
- **Issue:** #145
- **Branch:** `fix/issue-145-totp-cookie-selection`
- **PR:** پس از آماده‌شدن این شاخه ایجاد می‌شود.