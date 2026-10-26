# سامانهٔ مرکزی فروشندهٔ فرانو

این پوشه میزبان مستقل سامانهٔ فروشنده است و از برنامهٔ حسابداری مشتری جدا نگه داشته می‌شود.

## وضعیت

- اسکلت اولیهٔ Host روی `net10.0` ایجاد شده است.
- `/health/live` فقط زنده‌بودن فرایند وب را اعلام می‌کند؛ وضعیت دیتابیس یا جزئیات عملیاتی را افشا نمی‌کند.
- لایهٔ persistence مستقل (`VendorDbContext`، جداول Identity مجزا، `VendorAuditEvents` و Migration اولیه) در این مرحله اضافه شده است؛ CI باید اجرای Migration روی SQL Server آزمایشی و هماهنگی snapshot را تأیید کند.
- API پایهٔ احراز هویت مدیر، Cookie session، TOTP و passkey بر اساس Identity در ASP.NET Core 10، ضدجعل درخواست، rate limiting و سیاست دسترسی فعال/MFA در شاخهٔ Issue #103 پیاده‌سازی شده‌اند؛ تا CI سبز و ادغام، وضعیت آن NotVerified است.
- bootstrap نخستین مدیر، UI پنل، RBAC ریزدانه، maker-checker، checkpoint مستقل ممیزی، دادهٔ مشتری/قرارداد/لایسنس، صدور امضا و KMS/HSM هنوز پیاده‌سازی نشده‌اند.
- این اسکلت برای Production آماده نیست.

## توسعه و آزمون

از ریشهٔ مخزن:

```bash
dotnet restore Vendor.sln
dotnet build Vendor.sln --no-restore --configuration Release
dotnet test Vendor.sln --configuration Release
dotnet run --project Vendor/Vendor.Host/Vendor.Host.csproj
```

پس از اجرا، مسیر `/health/live` باید HTTP 200 برگرداند.

## استقرار ابری و سرور اختصاصی

Host عمداً به ارائه‌دهندهٔ ابری خاصی وابسته نیست. در هر دو محیط:

- تنظیمات عملیاتی از Configuration Providers و متغیرهای محیطی/Secret Store محیط استقرار تأمین شوند.
- TLS باید در Host یا reverse proxy مورد اعتماد و مستندشده برقرار باشد.
- فقط proxyهای شناخته‌شده در صورت نیاز پیکربندی شوند؛ این اسکلت فعلاً forwarded headers را فعال نمی‌کند.
- حساب سرویس با حداقل مجوز اجرا شود و دسترسی شبکه/دیتابیس تا زمان طراحی مرحلهٔ بعدی باز نشود.
- کلید خصوصی امضا در این برنامه/مخزن قرار نمی‌گیرد؛ Production باید در مرحلهٔ مجزا با KMS/HSM و مجوز فقط‌امضا fail-closed شود.
- از انتشار عمومی endpointهای مدیریتی یا health checkهای جزئیات‌دار خودداری شود.

## مرز امنیتی

`Vendor.sln` مستقل است. برنامهٔ مشتری نباید به پروژه‌های Vendor ارجاع دهد؛ تنها قرارداد مشترک مجاز در مراحل بعدی `Licensing.Contracts` است. دیتابیس مشتری و Migrationهای آن خارج از این Solution باقی می‌مانند.


## CI و بستهٔ build
- `.github/workflows/vendor-ci.yml` به تغییرات Vendor/Contracts و تنظیمات build واکنش نشان می‌دهد و Restore/Build/Test مستقل را اجرا می‌کند.
- فقط push به `main` artifact خروجی `dotnet publish` را تولید می‌کند؛ artifact موقت ۷ روز نگه داشته می‌شود. این مرحله Deploy واقعی نیست و هیچ credential یا کلید امضایی استفاده نمی‌کند.
- workflow مستقل Vendor جایگزین گاردهای مشتری نیست؛ انتشار مشتری همچنان توسط `.github/workflows/dotnet.yml` و اسکریپت‌های publish اعتبارسنجی می‌شود.


## اتصال دیتابیس Vendor
- متغیر محیطی: `FARANO_VENDOR_ConnectionStrings__VendorDb` (به یک دیتابیس مستقل Vendor).
- هیچ connection string پیش‌فرض، seed مدیر، ثبت‌نام عمومی یا endpoint مدیریتی ناشناس وجود ندارد.
- قبل از استفادهٔ واقعی، پیاده‌سازی login/MFA و bootstrap باید تکمیل و کنترل‌های امنیتی/عملیاتی جداگانه تأیید شوند؛ وجود Schema به معنی آماده‌بودن پنل نیست.

## احراز هویت Vendor — شاخهٔ Issue #103
- ورود با رمز فقط در صورتی که حساب فعال و TOTP از قبل فعال باشد به مرحلهٔ چالش دوم‌عامل می‌رود؛ حساب غیرفعال یا فاقد MFA نشست دریافت نمی‌کند.
- ورود TOTP از کوکی موقت Identity استفاده می‌کند؛ کوکی موقت برای APIهای محافظت‌شده پذیرفته نمی‌شود. نشست نهایی claim نوع احراز هویت amr=otp دارد.
- Passkey از APIهای رسمی Identity در ASP.NET Core 10 استفاده می‌کند؛ UserVerificationRequirement=required و ResidentKeyRequirement=required است. اعتبارسنجی assertion قبل از صدور نشست، فعال‌بودن مدیر را بررسی می‌کند و شمارندهٔ credential به‌روزرسانی‌شده را ذخیره می‌کند.
- Cookieهای نشست و MFA کوتاه‌عمر، Secure, HttpOnly, SameSite=Strict, host-only و با پیشوند __Host- هستند. Data Protection در Production به مسیر پایدار مستقل نیاز دارد؛ passkey domain نیز باید صریحاً تنظیم شود.
- همهٔ POSTهای auth از X-CSRF-TOKEN و cookie ضدجعل استفاده می‌کنند. درخواست‌های ورود بر اساس IP محدود می‌شوند؛ forwarded headers ناشناخته فعال نیستند.
- هیچ registration endpoint عمومی، JWT سفارشی، SMS، OAuth خارجی یا token در localStorage وجود ندارد. bootstrap اولین مدیر و UI در Issueهای بعدی می‌آیند.