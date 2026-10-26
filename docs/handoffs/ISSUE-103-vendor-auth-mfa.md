# تحویل وظیفه — ISSUE-103-vendor-auth-mfa

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/103
- **Branch:** `security/issue-103-vendor-auth-mfa`
- **تاریخ:** 2026-10-26
- **وضعیت:** Migration passkey از artifact CI بازبینی و commit شده؛ منتظر CI مجدد

## تغییرات
- ثبت ASP.NET Core Identity با SchemaVersion v3 و APIهای رسمی passkey در .NET 10.
- Password → TOTP challenge، passkey sign-in، passkey enrollment، TOTP setup/confirmation، session/me، logout و anti-forgery token.
- Cookieهای application/2FA امن و کوتاه‌عمر؛ Data Protection مجزا و اجباری‌بودن persistent key directory در Production.
- fallback authorization requirement: کاربر باید در دیتابیس فعال باشد و claim احراز هویت TOTP/passkey با عامل ثبت‌شده منطبق باشد.
- محدودسازی تلاش‌های credential به ۵ در دقیقه بر اساس IP و سیاست عمومی auth با سقف بالاتر تا جریان چندمرحله‌ای ورود خودش مسدود نشود.
- Integration tests روی SQL Server برای password-only denial، disabled admin, TOTP challenge بدون application cookie، تکمیل TOTP، passkey creation options، logout، antiforgery، rate limit و نبود registration عمومی.

## نکات امنیتی مهم
- Passkey assertion با `PerformPasskeyAssertionAsync` اعتبارسنجی می‌شود؛ فقط پس از اطمینان از فعال‌بودن حساب و ذخیرهٔ شمارندهٔ به‌روزشده، نشست نهایی صادر می‌شود.
- TOTP challenge از کوکی موقت Identity استفاده می‌کند؛ fallback policy آن را برای APIهای محافظت‌شده قبول نمی‌کند.
- تمام POSTهای auth ضدجعل درخواست دارند؛ هیچ JWT سفارشی، localStorage token، SMS یا OAuth خارجی اضافه نشده است.
- در Production، `FARANO_VENDOR_DataProtection__KeysDirectory` و `FARANO_VENDOR_IdentityPasskeys__ServerDomain` باید صریح و پایدار تنظیم شوند. هنوز محیط deploy انتخاب نشده است.

## Migration و CI
- `VendorDbContext` باید `IdentityUserPasskey<Guid>` را با کلید `CredentialId` و owned JSON data نگاشت کند.
- Migration `20261026045412_VendorAddPasskeySupport.cs` و تغییر `VendorDbContextModelSnapshot.cs` از artifact run `40271317565` بازبینی و commit شدند. گام تولید/آپلود Migration از workflow حذف شده است؛ workflow فقط بررسی drift و تست Migration ثبت‌شده را اجرا می‌کند.
- Build/Test محلی اجرا نشده؛ SDK در محیط ابزار موجود نبود. run `40271317565` به‌علت نبود `AddHealthChecks()` در تست‌های Host شکست خورد؛ این ثبت در کد اصلاح شده و CI مجدد باید build، snapshot check، تست SQL Server و secret scan را تأیید کند.

## خارج از محدوده
- bootstrap نخستین مدیر، registration عمومی، UI پنل React، RBAC ریزدانه، maker-checker و audit checkpoint خارجی.
- هرگونه تغییر schema/migration دیتابیس مشتری یا کد اجرایی مشتری.