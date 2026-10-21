# راهنمای توسعه محلی

## پیش‌نیازها
- Visual Studio 2026 (نسخه 18.0 یا بالاتر) با workload مربوط به ASP.NET and web development و .NET 10 SDK نصب‌شده. برای بازکردن و هدف‌گذاری رسمی net10.0، Visual Studio 2022 کافی نیست.
- SQL Server متناسب با Connection String محیط محلی.
- Git؛ Python 3 فقط برای بررسی ممنوعیت CDN در اسکریپت CI.
- برای دریافت/به‌روزرسانی NuGet و LibMan در مرحله آماده‌سازی به اینترنت دسترسی لازم است؛ **اجرای برنامه پس از دریافت وابستگی‌ها نباید برای CSS/JS/فونت/آیکون به اینترنت خارجی نیاز داشته باشد**.

## تنظیم اتصال پایگاه داده
در اجرای اولیه، اگر Connection String از User Secrets، متغیر محیطی یا تنظیمات صریح برنامه ارائه نشده باشد، برنامه به صفحه `/DatabaseSetup` هدایت می‌شود. فرم اتصال SQL Server را آزمایش می‌کند، در صورت نیاز دیتابیس را می‌سازد و برای دیتابیس خالی Migrationهای EF Core را اجرا می‌کند.

- فایل تنظیمات محافظت‌شده در مسیر `%LOCALAPPDATA%\\TarazAva\\Database\\database-settings.json` در ویندوز ذخیره می‌شود؛ مسیر دقیق روی صفحه نمایش داده می‌شود.
- Connection String در فایل به کمک ASP.NET Core Data Protection رمزنگاری می‌شود. کلیدهای Data Protection در پوشه `keys` نگه‌داری می‌شوند و در ویندوز با DPAPI محافظت می‌شوند.
- فایل `setup-token.txt` کد یک‌بارمصرف فرم را نگه می‌دارد. آن را فقط از روی همان سرور بخوانید و در اختیار دیگران قرار ندهید؛ پس از ذخیره موفق تنظیمات حذف می‌شود.
- پس از ذخیره تنظیمات، برنامه را متوقف و دوباره اجرا کنید. برای دیتابیس موجود، Migration خودکار انجام نمی‌شود تا schema قدیمی یا دیتابیس ایجادشده با `EnsureCreated()` ناخواسته تغییر نکند.
- در استقرار، پوشه داده و کلیدهای Data Protection باید با همان هویت سرویس قابل دسترس باشند. جابه‌جایی فایل تنظیمات به دستگاه دیگر بدون انتقال امن کلیدها امکان رمزگشایی را تضمین نمی‌کند.
- برای پیکربندی صریح/مدیریت‌شده، مقادیر `ConnectionStrings:ConnectionString` در User Secrets یا متغیر محیطی `ConnectionStrings__ConnectionString` بر فایل محلی مقدم هستند.

برای استفاده از User Secrets:
```powershell
dotnet user-secrets init --project Server/Server.csproj
dotnet user-secrets set "ConnectionStrings:ConnectionString" "Server=localhost;Database=TarazAvaDb;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=False;" --project Server/Server.csproj
```

ورود مدیریتی فعلی هنوز legacy است و برای Production تأیید نمی‌شود؛ راهنمای `docs/SECURITY.md` را بخوانید. مقادیر `MasterUsername` و `MasterPassword` باید فقط در User Secrets/Environment/Secret Store تنظیم شوند و خالی‌بودن آن‌ها نباید با مقدار پیش‌فرض ناامن دور زده شود. (به‌روزرسانی 2026-10-21: گزینه `ActivationKeys` فعلاً بی‌اثر است چون Middleware به Pipeline متصل نیست — D-028؛ لایسنس آینده در `docs/LICENSING.md`.)

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
هدف فعلی پروژه .NET 10 LTS است. Visual Studio 2026 نسخه 18.0 یا بالاتر برای هدف‌گذاری رسمی net10.0 لازم است. پیش از ادغام تغییرات، Restore/Build/Test در CI و روی دستگاه توسعه باید موفق باشند.


## تنظیم اتصال SQL Server برای محیط‌های مختلف

- مقدار توسعه محلی در `Server/appsettings.json` فقط از Windows Integrated Authentication استفاده می‌کند و رمز دیتابیس در Git ذخیره نمی‌شود.
- برای جایگزینی اتصال در دستگاه توسعه، از User Secrets استفاده کنید.
- برای سرور مشتری یا فضای ابری، متغیر محیطی `ConnectionStrings__ConnectionString` را در تنظیمات سرویس میزبانی تنظیم کنید؛ این مقدار بر appsettings پیش‌فرض اولویت دارد.
- برای SQL Login در محیط عملیاتی، رمز را فقط از Secret Store/Environment امن تزریق کنید؛ آن را در appsettings یا Git commit نکنید.
- تغییر Connection String فقط محل SQL Server را عوض می‌کند؛ قبل از استقرار باید Schema از طریق Migration بازبینی‌شده اعمال شود. Migration و انتقال داده هنوز بخشی از این تغییر نیست.

نمونه متغیر محیطی PowerShell برای نشست فعلی:

```powershell
$env:ConnectionStrings__ConnectionString = "Server=localhost\SQLSERVER2025;Database=TarazAvaDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;"
```

## .NET 10 و Visual Studio

- مخزن به `.NET 10 LTS` و EF Core 10 ارتقا داده شده است.
- برای توسعه رسمی `net10.0` از Visual Studio 2026 نسخه 18.0 یا بالاتر استفاده کنید. نصب SDK به‌تنهایی پشتیبانی هدف‌گذاری net10.0 را به Visual Studio 2022 اضافه نمی‌کند.
- پس از نصب، از Developer PowerShell اجرا کنید: `dotnet --info`, `dotnet restore Template.sln`, `dotnet build Template.sln --configuration Release`, `dotnet test Template.sln --configuration Release`.


## کلاینت React (`ClientApp`)
پیش‌نیاز: Node.js 22.12+ (CI: Node 24 / npm 11).

```powershell
cd ClientApp
npm ci
npm run build      # خروجی در Server/wwwroot/app؛ سپس سرور را اجرا و /app را باز کنید
npm run dev        # توسعه: http://localhost:5173/app/ ؛ /api و /Account به https://localhost:5001 پروکسی می‌شوند
```
- آدرس سرور برای پروکسی: متغیر `FARANOU_SERVER_URL`.
- خروجی Build در Git ثبت نمی‌شود (`.gitignore`)؛ در انتشار ابتدا `npm ci && npm run build` و سپس `dotnet publish` اجرا شود. اگر Build کلاینت انجام نشده باشد، `/app` پاسخ 404 می‌دهد و بقیه برنامه کار می‌کند.
- ورود از صفحه Razor `/Account/Login` انجام می‌شود؛ پس از ورود، `/app` یا پیوند «داشبورد جدید (پیش‌نمایش)» در صفحه اصلی را باز کنید.


## انتشار (Production)
```bash
scripts/publish.sh artifacts/publish          # Linux/macOS/CI
scripts/smoke-published-app.sh artifacts/publish   # اختیاری: آزمون HTTP خروجی
```
```powershell
pwsh scripts/publish.ps1 -Output artifacts\publish   # Windows (همان مراحل)
```
- ترتیب ثابت: `npm ci` ← typecheck/تست کلاینت ← `npm run build` ← بررسی آفلاین ← `dotnet publish` ← بررسی وجود `wwwroot/app/index.html`، JS، CSS، فونت و تصاویر در خروجی.
- `dotnet publish` مستقیم بدون Build کلاینت خطا می‌دهد (D-016).
- **توسعه محلی:** `npm run dev` (Vite + پروکسی به سرور) یا یک‌بار `npm run build` و سپس `dotnet run`؛ **Production:** فقط اسکریپت انتشار.
- تنظیمات جدید قابل تغییر در `appsettings`/Environment: `RateLimiting:*` و `SecurityHeaders:EnforceRazorContentSecurityPolicy`.
