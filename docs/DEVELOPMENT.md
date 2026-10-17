# راهنمای توسعه محلی

## پیش‌نیازها
- Visual Studio 2026 (نسخه 18.0 یا بالاتر) با workload مربوط به ASP.NET and web development و .NET 10 SDK نصب‌شده. برای بازکردن و هدف‌گذاری رسمی net10.0، Visual Studio 2022 کافی نیست.
- SQL Server متناسب با Connection String محیط محلی.
- Git؛ Python 3 فقط برای بررسی ممنوعیت CDN در اسکریپت CI.
- برای دریافت/به‌روزرسانی NuGet و LibMan در مرحله آماده‌سازی به اینترنت دسترسی لازم است؛ **اجرای برنامه پس از دریافت وابستگی‌ها نباید برای CSS/JS/فونت/آیکون به اینترنت خارجی نیاز داشته باشد**.

## تنظیم اسرار محلی
مقادیر محرمانه را در `appsettings.json` یا `appsettings.Development.json` وارد نکنید. نمونه Connection String توسعه فعلی از Windows Integrated Authentication استفاده می‌کند؛ آن را فقط مطابق SQL Server محلی خود تنظیم کنید. برای تنظیم User Secrets:

```powershell
dotnet user-secrets init --project Server/Server.csproj
dotnet user-secrets set "ConnectionStrings:ConnectionString" "Server=.;Database=TarazAvaDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;" --project Server/Server.csproj
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


## تنظیم اتصال SQL Server برای محیط‌های مختلف

- مقدار توسعه محلی در `Server/appsettings.json` فقط از Windows Integrated Authentication استفاده می‌کند و رمز دیتابیس در Git ذخیره نمی‌شود.
- برای جایگزینی اتصال در دستگاه توسعه، از User Secrets استفاده کنید.
- برای سرور مشتری یا فضای ابری، متغیر محیطی `ConnectionStrings__ConnectionString` را در تنظیمات سرویس میزبانی تنظیم کنید؛ این مقدار بر appsettings پیش‌فرض اولویت دارد.
- برای SQL Login در محیط عملیاتی، رمز را فقط از Secret Store/Environment امن تزریق کنید؛ آن را در appsettings یا Git commit نکنید.
- تغییر Connection String فقط محل SQL Server را عوض می‌کند؛ قبل از استقرار باید Schema از طریق Migration بازبینی‌شده اعمال شود. Migration و انتقال داده هنوز بخشی از این تغییر نیست.

نمونه متغیر محیطی PowerShell برای نشست فعلی:

```powershell
$env:ConnectionStrings__ConnectionString = "Server=localhost;Database=TarazAvaDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;"
```

## .NET 10 و Visual Studio

- مخزن به `.NET 10 LTS` و EF Core 10 ارتقا داده شده است.
- برای توسعه رسمی `net10.0` از Visual Studio 2026 نسخه 18.0 یا بالاتر استفاده کنید. نصب SDK به‌تنهایی پشتیبانی هدف‌گذاری net10.0 را به Visual Studio 2022 اضافه نمی‌کند.
- پس از نصب، از Developer PowerShell اجرا کنید: `dotnet --info`, `dotnet restore Template.sln`, `dotnet build Template.sln --configuration Release`, `dotnet test Template.sln --configuration Release`.
