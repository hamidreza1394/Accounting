# تحویل وظیفه — ISSUE-UNNUMBERED-database-migrator-di

- **Issue:** UNNUMBERED — خطای Dependency Injection در DatabaseSchemaMigrator
- **Agent/مسئول:** Ashna-X1
- **تاریخ:** 2026-10-17
- **وضعیت:** اصلاح کد انجام شد؛ اعتبارسنجی CI در انتظار اجرا

## هدف و معیارهای پذیرش
- [x] علت ریشه‌ای Resolve نشدن `Data.DatabaseContext` مشخص شد.
- [x] ثبت سرویس Migrator با ثبت `DatabaseContext` هم‌مسیر و مشروط شد.
- [x] تست regression برای Resolve شدن Migrator اضافه شد.
- [ ] Restore/Build/Test روی محیط اجرای محلی این Agent.
- [ ] اجرای واقعی برنامه و اتصال SQL Server.

## خلاصه تغییرات
در `Server/Program.cs` ثبت `IDatabaseSchemaMigrator` از حالت بدون شرط خارج شد. پیش از اصلاح، Migrator همیشه ثبت می‌شد، در حالی که `DatabaseContext` فقط وقتی Connection String مؤثر وجود داشت ثبت می‌شد. در حالت first-run، اعتبارسنجی/ساخت سرویس Migrator بنابراین به وابستگی ثبت‌نشده `Data.DatabaseContext` می‌رسید. اکنون هر دو registration در شاخه `!setupRequired` قرار دارند؛ در مسیر setup اولیه، Migrator عمداً ثبت نمی‌شود چون عملیات migration تا پیکربندی معتبر اتصال قابل اجرا نیست.

## فایل‌های مهم تغییرکرده
- `Server/Program.cs` — هم‌تراز کردن شرط ثبت DbContext و Migrator، بدون ثبت تکراری.
- `tests/Template.Tests/DatabaseDependencyInjectionTests.cs` — تست ساخت ServiceProvider با `ValidateOnBuild`/`ValidateScopes` و Resolve شدن Migrator.
- این فایل — ثبت handoff و وضعیت آزمون.

## تصمیم‌های فنی
- `Data.DatabaseContext` همان Context واقعی پروژه در `Infrastructure/Persistence` با namespace قدیمی `Data` است؛ کلاس جایگزین دیگری در زنجیره مورد استفاده پیدا نشد.
- `DatabaseContext` با `AddDbContext<Data.DatabaseContext>` و lifetime پیش‌فرض Scoped ثبت می‌شود.
- `DatabaseSchemaMigrator` نیز Scoped است و وابستگی‌های سازنده آن `DatabaseContext` و `ILogger<DatabaseSchemaMigrator>` هستند.
- Connection String از `ConnectionStrings:ConnectionString` می‌آید؛ User Secrets/Environment/appsettings بر تنظیمات محلی محافظت‌شده مقدم‌اند.
- هیچ Migration روی دیتابیس واقعی اجرا نشد و کد `EnsureCreated` دوباره فعال نشد. `--migrate` همچنان مسیر صریح اجرای Migration است.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| بررسی ایستای GitHub API | موفق | `Program.cs`، `DatabaseContext.cs`، `DatabaseSchemaMigrator.cs`، پروژه‌ها، مستندات و PRهای مرتبط بررسی شدند. |
| `dotnet restore Template.sln` | اجرا نشده | محیط گفتگو ابزار اجرای SDK محلی در اختیار نداشت. |
| `dotnet build Template.sln --no-restore --configuration Release` | اجرا نشده | باید در CI یا دستگاه دارای .NET 10 SDK اجرا شود. |
| `dotnet test Template.sln --no-build --configuration Release` | اجرا نشده | تست جدید در این محیط اجرا نشد. |
| GitHub Actions پیش از تغییر | موفق | CI روی `main` در Commit `4e96fa74ed245cc36d39ea8e8f7331df30cc44db` با Run `39023372898` موفق بود؛ این نتیجه پیش از اصلاح جدید است. |
| اتصال واقعی SQL Server | اجرا نشده | SQL Server و credential آزمایشی در این محیط ارائه/اجرا نشد. |

## امنیت، داده و سازگاری
- Connection String، رمز و Secret جدیدی در Commit وارد نشد.
- registration سرویس در حالت setup اولیه حذف نشده تا خطا پنهان شود؛ از ساخت سرویس وابسته به Context بدون اتصال جلوگیری می‌شود.
- ثبت Scoped با Scoped سازگار است و Singleton وابسته به DbContext اضافه نشده است.
- Migration روی دیتابیس دارای داده بدون مجوز اجرا نشد.

## ریسک‌ها و کارهای باقی‌مانده
- CI جدید باید Restore/Build/Test را روی Branch اصلاحی اجرا کند.
- تست افزوده‌شده فقط ساخت Container و Resolve شدن سازنده را پوشش می‌دهد و اتصال واقعی SQL Server را آزمایش نمی‌کند.
- آزمون end-to-end مسیر first-run، دیتابیس تازه/خالی و دیتابیس legacy نیازمند SQL Server ایزوله و مجوز عملیاتی است.

## شناسه‌های Git
- Branch: `fix/database-migrator-di-registration`
- Commitهای اصلاحی: `50b9af6f501aa76f1b339b5483eda95976a7a5ca` و `e87f85970594c289d08f0dfbe3b58530d1ca32de`
- Pull Request: ساخته نشد؛ طبق درخواست کاربر بدون اجازه PR ایجاد نشد.
