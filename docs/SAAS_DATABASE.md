# پایه دیتابیس SaaS تراز آوا

## تصمیم‌ها
- یک هویت مرکزی برای هر شخص؛ عضویت چندشرکتی با `CompanyMembership`.
- دیتابیس SQL Server مشترک؛ `TenantId` برابر `TenantCompany.Id` است.
- نقش‌ها در محدوده شرکت هستند؛ نقش در شرکت الف مجوزی در شرکت ب نمی‌دهد.
- اتصال از `ConnectionStrings:ConnectionString` می‌آید و با User Secrets/environment قابل جایگزینی است.
- جداول SaaS در schema `saas` و با migration history مستقل قرار می‌گیرند.

## اتصال محلی و استقرار
نمونه اتصال محلی در `Server/appsettings.Development.json` استفاده می‌کند:
```json
{ "ConnectionStrings": { "ConnectionString": "Server=.;Database=TarazAva;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;" } }
```
برای تغییر سرور بدون تغییر کد، در PowerShell مقدار `ConnectionStrings__ConnectionString` را روی محیط اجرا تنظیم کن. رمز واقعی را در Git یا appsettings قرار نده؛ از User Secrets/Secret Store استفاده کن. برای Production، Encrypt و اعتبارسنجی گواهی TLS را فعال نگه دار؛ TrustServerCertificate صرفاً برای توسعه محلی است.

## امنیت Tenant
Global Query Filters برای دوره مالی، نقش شرکت، تخصیص مجوز نقش و سرسند اعمال شده‌اند. بدون Tenant انتخاب‌شده، این queryها هیچ ردیفی برنمی‌گردانند. عضویت‌ها عمداً فیلتر سراسری ندارند تا انتخاب شرکت ممکن باشد؛ query عضویت باید با UserId احرازشده و IsActive محدود شود. برای هر عملیات، عضویت و مجوز را دوباره بررسی کن؛ ورودی TenantId از URL/form/header قابل اعتماد نیست. FK مرکب سرسند به دوره مالی، اتصال به دوره شرکت دیگر را منع می‌کند. مسیرهای `IgnoreQueryFilters` باید محدود و تست شوند.

## هویت و محدوده تحویل
`ApplicationUser` از ASP.NET Core Identity است و رمز عبور را خودش به‌صورت استاندارد مدیریت می‌کند. این commit زیرساخت UserManager و مدل را ثبت می‌کند؛ هنوز login legacy جایگزین نشده و MFA، تأیید ایمیل واقعی، انتخاب شرکت/دوره و audit writer پیاده‌سازی نشده‌اند.

## Migration
Migration تولیدی در این تغییر ساخته نشده است؛ ابزار EF CLI و build باید روی SDK .NET 10 اجرا شود. پس از نصب ابزار هم‌نسخه:
```powershell
dotnet tool install --global dotnet-ef --version 10.0.13
dotnet ef migrations add SaaSInitialCreate --context SaaSIdentityContext --project Infrastructure/Persistence/Infrastructure.Persistence.csproj --startup-project Server/Server.csproj --output-dir Migrations/SaaS
dotnet ef migrations script --context SaaSIdentityContext --project Infrastructure/Persistence/Infrastructure.Persistence.csproj --startup-project Server/Server.csproj
```
SQL خروجی را قبل از اجرا بازبینی کن. `DatabaseContext` قدیمی هنوز `Database.EnsureCreated()` دارد؛ تغییر آن باید جداگانه انجام شود تا دیتابیس legacy ناخواسته تغییر نکند.
