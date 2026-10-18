# راهنمای توسعه محلی

## پیش‌نیازها
- Visual Studio 2022 با workload مربوط به ASP.NET and web development یا .NET SDK 9 نصب‌شده.
- SQL Server متناسب با Connection String محیط محلی.
- Git؛ Python 3 فقط برای بررسی ممنوعیت CDN در اسکریپت CI.
- برای دریافت/به‌روزرسانی NuGet و LibMan در مرحله آماده‌سازی به اینترنت دسترسی لازم است؛ **اجرای برنامه پس از دریافت وابستگی‌ها نباید برای CSS/JS/فونت/آیکون به اینترنت خارجی نیاز داشته باشد**.

## تنظیم اسرار محلی
مقادیر محرمانه را در `appsettings.json` یا `appsettings.Development.json` وارد نکنید. نمونه Connection String توسعه فعلی از Windows Integrated Authentication استفاده می‌کند؛ آن را فقط مطابق SQL Server محلی خود تنظیم کنید. برای تنظیم User Secrets:

```powershell
dotnet user-secrets init --project Server/Server.csproj
dotnet user-secrets set "ConnectionStrings:ConnectionString" "Server=.;Database=YourLocalDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;" --project Server/Server.csproj
```

ورود مدیریتی فعلی هنوز legacy است و برای Production تأیید نمی‌شود؛ راهنمای `docs/SECURITY.md` را بخوانید. مقادیر `MasterUsername`، `MasterPassword` و `ActivationKeys` باید فقط در User Secrets/Environment/Secret Store تنظیم شوند و خالی‌بودن آن‌ها نباید با مقدار پیش‌فرض ناامن دور زده شود.

## Restore و اجرا
```powershell
dotnet restore Template.sln
dotnet build Template.sln --configuration Release
dotnet test Template.sln --configuration Release
dotnet run --project Server/Server.csproj
```

## کتابخانه‌های مرورگر
`Server/libman.json` مرجع نسخه LibMan است و فایل‌های مورد استفاده مرورگر داخل `Server/wwwroot/lib/` نگهداری می‌شوند. پس از تغییر نسخه، فایل‌های محلی را واقعاً به‌روزرسانی و UI را تست کنید:

```powershell
libman restore --config Server/libman.json
python scripts/check_static_assets.py
```

دستور `libman restore` به منبع دانلود وابسته است؛ پس از دریافت فایل‌ها، اجرای برنامه در حالت عادی نباید به CDN نیاز داشته باشد. فایل‌های vendored را بدون بررسی مجوز، نسخه و اثر UI جایگزین نکنید.

## EF Core migrations
در حال حاضر Migrationهای رسمی و فرایند استقرار دیتابیس باید پیش از استفاده محصولی بررسی و تثبیت شوند. Migration را بدون بازبینی مدل، SQL تولیدی، محیط هدف و نسخه پشتیبان روی دیتابیس واقعی اجرا نکنید. دستورهای مرجع پس از تثبیت فرایند:

```powershell
dotnet ef migrations list --project Infrastructure/Persistence/Infrastructure.Persistence.csproj --startup-project Server/Server.csproj
# افزودن migration فقط پس از تغییر مدل و بررسی وضعیت موجود:
# dotnet ef migrations add <DescriptiveName> --project Infrastructure/Persistence/Infrastructure.Persistence.csproj --startup-project Server/Server.csproj
```

## نکته نسخه پلتفرم
هدف فعلی بنا به درخواست پروژه .NET 9 است، اما پشتیبانی آن در نوامبر 2026 پایان می‌یابد. قبل از شروع قابلیت‌های محصولی، تصمیم ثبت‌شده برای .NET 10 LTS را بررسی کنید. تغییر نسخه باید در یک PR مستقل با Restore/Build/Test انجام شود.
