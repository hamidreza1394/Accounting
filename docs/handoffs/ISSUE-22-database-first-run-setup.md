# تحویل وظیفه — ISSUE-22-database-first-run-setup

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/22
- **Agent/مسئول:** ChatGPT
- **تاریخ:** 2026-10-17
- **وضعیت:** پیاده‌سازی اولیه انجام‌شده؛ CI و تست SQL Server هنوز تأیید نشده‌اند.

## هدف و معیارهای پذیرش
- [x] فرم اولیه SQL Server در Razor Pages.
- [x] تست اتصال با Windows Authentication یا SQL Login.
- [x] ذخیره تنظیمات خارج از مخزن و رمزنگاری Connection String.
- [x] استفاده از تنظیمات ذخیره‌شده برای اتصال EF Core پس از Restart.
- [x] ساخت دیتابیس جدید و اعمال Migration؛ عدم تغییر خودکار schema دیتابیس موجود.
- [ ] Build/Test/CI سبز و تست واقعی SQL Server.
- [ ] آزمون عملی دسترسی/بازیابی کلید Data Protection در محیط استقرار هدف.

## خلاصه تغییرات
- در نبود اتصال صریح و فایل تنظیمات محلی، برنامه مسیرهای پویا را به `/DatabaseSetup` هدایت می‌کند.
- فرم کد یک‌بارمصرفی را می‌خواهد که خارج از repository ذخیره می‌شود و در log چاپ نمی‌شود.
- Connection String با ASP.NET Core Data Protection محافظت می‌شود؛ کلیدهای DP در Windows با DPAPI و در Unix با محدودیت مجوز پوشه محافظت می‌شوند.
- برای دیتابیس جدید یا خالی Migrationها اعمال می‌شوند. دیتابیس دارای جدول‌ها به‌صورت خودکار migrate نمی‌شود.
- `Server/appsettings.Development.json` دیگر Connection String پیش‌فرض را تحمیل نمی‌کند؛ User Secrets/Environment همچنان بر تنظیمات محلی مقدم‌اند.

## فایل‌های مهم تغییرکرده
- `Server/Program.cs` — تشخیص تنظیمات و مسدودکردن صفحات برنامه تا تکمیل setup.
- `Server/Infrastructure/DatabaseSetup/DatabaseConnectionSettingsStore.cs` — رمزنگاری/خواندن/نوشتن تنظیمات و کد یک‌بارمصرف.
- `Server/Infrastructure/DatabaseSetup/DatabaseConnectionSetupService.cs` — تست اتصال، ایجاد دیتابیس و Migration اولیه امن.
- `Server/Pages/DatabaseSetup.cshtml`, `Server/Pages/DatabaseSetup.cshtml.cs` — فرم RTL و پردازش سمت سرور.
- `Server/appsettings.Development.json` — حذف Connection String محلی از فایل tracked.
- `docs/DEVELOPMENT.md`, `docs/DATABASE.md`, `docs/SECURITY.md`, `docs/ROADMAP.md`, `docs/DECISIONS.md`, `docs/WORK_LOG.md` — مستندات.

## تصمیم‌های فنی
- Configuration Providers استاندارد در صورت وجود اتصال صریح مقدم هستند.
- کلیدها و ciphertext به هویت اجرایی وابسته‌اند؛ انتقال فایل به دستگاه دیگر بدون انتقال امن کلیدها کافی نیست.
- Migration خودکار روی دیتابیس موجود عمداً خاموش است، چون دیتابیس‌های legacy ممکن است با `EnsureCreated()` ساخته شده باشند و baseline نداشته باشند.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `dotnet restore Template.sln` | اجرا نشده | این محیط ابزار اجرای SDK/دستگاه SQL Server را ندارد. |
| `dotnet build Template.sln --no-restore --configuration Release` | اجرا نشده | CI باید بررسی شود. |
| `dotnet test Template.sln --no-build --configuration Release` | اجرا نشده | هنوز تست اختصاصی setup اضافه/اجرا نشده است. |
| اتصال واقعی به SQL Server | اجرا نشده | نیازمند SQL Server و credential معتبر است. |
| بازبینی ایستای فایل‌ها | انجام شد | باید با Build واقعی تأیید شود. |

## امنیت، داده و سازگاری
- رمز SQL در فرم/مدل ورودی فقط هنگام درخواست وجود دارد و به log اضافه نمی‌شود.
- SQL identifier با `QUOTENAME` تولید می‌شود؛ نام دیتابیس به SQL به‌صورت پارامتر منتقل می‌شود.
- دیتابیس دارای جدول‌ها دست‌کاری خودکار نمی‌شود؛ برای legacy `EnsureCreated()` baseline مجزا لازم است.
- HTTPS در شبکه غیرقابل‌اعتماد لازم است؛ کد setup جایگزین TLS یا امنیت شبکه نیست.

## ریسک‌ها و کارهای باقی‌مانده
- CI/build ممکن است خطاهای compile/analyzer نشان دهد؛ تا مشاهده نتیجه نباید PR را merge کرد.
- باید تست واقعی با SQL Server برای Windows Authentication، SQL Login، ساخت DB، دیتابیس خالی، دیتابیس موجود و رمزنگاری فایل انجام شود.
- آزمون IIS/Windows Service و Linux container برای محل کلیدها/مجوزها انجام نشده است.
- فرم برای تغییر تنظیمات پس از راه‌اندازی طراحی نشده؛ برای تغییر اتصال، تنظیمات صریح یا فرایند مدیریت امن جدا لازم است.

## شناسه‌های Git
- Branch: `feature/issue-22-database-first-run-setup`
- Commit آخر هنگام به‌روزرسانی handoff: در PR ثبت شده؛ Head SHA را از صفحه PR بررسی کنید.
- Pull Request: https://github.com/hamidreza1394/Accounting/pull/23 (باز؛ CI در حال اجرا است).
