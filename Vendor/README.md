# سامانهٔ مرکزی فروشندهٔ فرانو

این پوشه میزبان مستقل سامانهٔ فروشنده است و از برنامهٔ حسابداری مشتری جدا نگه داشته می‌شود.

## وضعیت

- اسکلت اولیهٔ Host روی `net10.0` ایجاد شده است.
- `/health/live` فقط زنده‌بودن فرایند وب را اعلام می‌کند؛ وضعیت دیتابیس یا جزئیات عملیاتی را افشا نمی‌کند.
- لایهٔ persistence مستقل (`VendorDbContext`، جداول Identity مجزا، `VendorAuditEvents` و Migration اولیه) در این مرحله اضافه شده است؛ CI باید اجرای Migration روی SQL Server آزمایشی و هماهنگی snapshot را تأیید کند.
- ورود مدیر، سیاست MFA/passkey/TOTP، RBAC، bootstrap اولیه، سرویس ثبت/اعتبارسنجی زنجیرهٔ ممیزی، maker-checker، دادهٔ مشتری تجاری/قرارداد/لایسنس، صدور امضا و KMS/HSM هنوز پیاده‌سازی نشده‌اند.
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

## احراز هویت Vendor — Issue #103 (شاخهٔ در حال بازبینی)
- APIهای پایهٔ password→TOTP، passkey sign-in/enrollment، TOTP setup/confirmation، session/me و logout با ASP.NET Core Identity 10 در شاخه پیاده‌سازی شده‌اند؛ تا CI سبز و ادغام، Production-verified نیستند.
- password-only برای حساب فاقد MFA رد می‌شود؛ کوکی موقت TOTP به‌تنهایی مجوز دسترسی به APIهای محافظت‌شده نیست.
- Passkey assertion قبل از صدور نشست بررسی می‌شود؛ مدیر باید فعال باشد و شمارندهٔ credential به‌روزشده ذخیره شود.
- کوکی‌ها host-only با پیشوند __Host-، Secure، HttpOnly، SameSite=Strict و عمر کوتاه‌اند. همهٔ POSTهای auth ضدجعل درخواست دارند و تلاش‌های credential بر اساس IP محدود می‌شوند.
- Production به مسیر پایدار و مستقل Data Protection و دامنهٔ صریح passkey نیاز دارد؛ AllowedHosts wildcard ممنوع است.
- bootstrap نخستین مدیر، UI پنل، RBAC ریزدانه، maker-checker و سرویس checkpoint ممیزی هنوز باقی‌اند.


## Migration ثبت‌شده برای Passkey
- Migration `20261026045412_VendorAddPasskeySupport` و تغییرات `VendorDbContextModelSnapshot` اکنون در source ثبت شده‌اند؛ workflow دیگر Migration تولید نمی‌کند و فقط `dotnet ef migrations has-pending-model-changes` را اجرا می‌کند.
