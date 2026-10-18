# چک‌لیست امنیت

## یافته‌های مهم در نسخه ورودی
- MasterPassword به شکل SHA-256 Base64 در appsettings ذخیره شده بود؛ مقدار قبلی با یک رمز ضعیف/قابل حدس مرتبط بود. مقدار از تنظیمات tracked حذف شده است.
- نام کاربری و برخی Claims در Login به‌صورت hardcode نوشته شده بودند؛ نام کاربری اکنون از تنظیمات می‌آید، اما سازوکار رمز عبور همچنان legacy است.
- یک حساب مدیر با ایمیل/شماره تلفن و رمز عبور آزمایشی در EF Core `HasData` تعریف شده بود؛ این Seed ثابت حذف شد تا حساب پیش‌فرض و اطلاعات شخصی نمونه ساخته نشوند.
- کلیدهای Activation در تنظیمات tracked بودند؛ مقادیر حذف شده‌اند و باید از Secret/Environment وارد شوند.
- تنظیمات Google OAuth در کد کامنت شده بود؛ Client Secret واقعی نباید در مخزن ذخیره شود.
- خطای عمومی سفارشی موجود ممکن است Exception را ببلعد؛ logging و correlation ID باید در فاز امنیت/observability اصلاح شوند.

## اصلاحات امنیتی این بازبینی
- مقصد `ReturnUrl` پس از ورود با `Url.IsLocalUrl` بررسی می‌شود؛ URL خارجی به صفحه اصلی هدایت می‌شود تا Open Redirect جلوگیری شود.
- Cookie احراز هویت HttpOnly و SameSite=Lax است؛ در Production فقط روی HTTPS ارسال می‌شود و عمر 30 دقیقه‌ای با Sliding Expiration دارد. این تغییرها جایگزین سیستم هویت استاندارد و تست امنیتی نیستند.

## الزامات
- پیش از Production، احراز هویت سفارشی با ASP.NET Core Identity یا `IPasswordHasher<TUser>` جایگزین شود؛ SHA-256 خام برای رمز عبور مناسب نیست.
- اصل حداقل دسترسی (Least Privilege)، دفاع در عمق (Defense in Depth) و deny-by-default رعایت شود.
- برای صفحات تغییر‌دهنده داده، Anti-forgery فعال و بررسی شود.
- Cookies: `HttpOnly`, `Secure`, `SameSite` و زمان انقضای مناسب.
- کنترل مجوز سمت سرور انجام شود؛ پنهان‌کردن دکمه UI کنترل امنیت نیست.
- اعتبارسنجی ورودی سمت سرور، encoding خروجی، Query پارامتری و جلوگیری از XSS/SQL Injection.
- محدودسازی تلاش ورود، خطای عمومی login و عدم افشای وجود حساب.
- اسرار فقط در User Secrets (توسعه) یا Secret Store/Environment (استقرار) باشند؛ اگر مقدار واقعی قبلاً commit شده، آن را افشاشده فرض و Rotate کن.
- Dependabot/NuGet audit و اسکن Secretها در CI فعال شوند.
- خطاهای تولیدی نباید Stack Trace یا داده حساس به کاربر نشان دهند.

## تنظیم محلی موقت برای Login قدیمی
```bash
dotnet user-secrets init --project Server/Server.csproj
dotnet user-secrets set "ApplicationSettings:MasterUsername" "your-local-user" --project Server/Server.csproj
dotnet user-secrets set "ApplicationSettings:MasterPassword" "<Base64-SHA256-hash>" --project Server/Server.csproj
```
این فقط برای سازگاری موقت با Login فعلی است و راهکار توصیه‌شده برای Production نیست.

## OWASP Top 10 و DevSecOps
هر PR باید اثر تغییر بر Access Control، Cryptographic Failures، Injection، Insecure Design، Security Misconfiguration، Vulnerable Components، Authentication Failures، Integrity Failures، Logging/Monitoring و SSRF را بررسی کند. امنیت بخشی از چرخه توسعه و CI است، نه بررسی انتهایی.
