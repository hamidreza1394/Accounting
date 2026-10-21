# راهنمای پایگاه داده

## وضعیت موجود
- `Infrastructure/Persistence/DatabaseContext.cs` شامل DbContext است.
- تنظیمات Entityها در `Infrastructure/Persistence/Configurations` قرار دارند.
- SQL Server و EF Core استفاده می‌شوند.
- Lazy-loading و change-tracking proxies در `Server/Program.cs` فعال نیستند. مدل دامنه شامل Entityهای sealed مانند `Company` است و نیازی نیست برای Change Tracking استاندارد EF Core کلاس‌ها proxy-compatible شوند.
- برای داده‌های مرتبط، از `Include/ThenInclude` یا projection به ViewModel استفاده شود تا بارگذاری داده‌ها آشکار و قابل بررسی باشد و Queryهای پنهان/N+1 ایجاد نشوند.

## استانداردها
- Mapping صریح برای طول رشته، Required/Optional، Precision، Index، کلید و رفتار حذف.
- از Migrationهای EF Core برای تغییر schema استفاده شود؛ تغییر مستقیم دیتابیس بدون Migration ثبت‌شده ممنوع است.
- Migration تولیدی قبل از استقرار بازبینی شود و در محیط حساس ترجیحاً SQL Script تولید و بررسی شود.
- Queryهای صفحه‌بندی‌شده باید ترتیب پایدار و سقف تعداد رکورد داشته باشند.
- از `AsNoTracking` برای خواندن صرف در صورت مناسب‌بودن استفاده شود.
- DbContext به‌صورت Scoped بماند و بین Threadها به اشتراک گذاشته نشود.
- تراکنش و رفتار Cascade براساس قواعد دامنه و تست مشخص شود.
- Connection String واقعی در فایل tracked قرار نگیرد؛ از `ConnectionStrings__ConnectionString` یا Secret Store استفاده شود.

## مدل چندمستاجری افزوده‌شده در Issue #16
- `Company`: محدوده Tenant و ریشه داده شرکت.
- `UserCompanyMembership`: اتصال شناسه کاربر مرکزی به شرکت و نقش همان شرکت؛ ترکیب UserId/TenantId یکتا است.
- `FiscalPeriod`: دوره مالی با تاریخ شروع/پایان و وضعیت Open/Closed/Archived.
- `CompanyRole`: نقش مستقل در محدوده شرکت.
- `PermissionDefinition` و `CompanyRolePermission`: کاتالوگ مجوز و تخصیص به نقش.
- `SecurityAuditEvent`: ثبت رویداد امنیتی بدون ذخیره رمز یا توکن.
- نگاشت‌ها در `Infrastructure/Persistence/Configurations` قرار دارند و به‌وسیله `ApplyConfigurationsFromAssembly` کشف می‌شوند.

**وضعیت Migration (به‌روزرسانی 2026-10-21):** ~~«هنوز Migration تولید نشده است»~~ منسوخ. سه Migration در `Infrastructure/Persistence/Migrations` وجود دارد: `InitialCreate` (`20251017120000_InitialCreate`)، `AlignSchemaWithModel`، `WidenUserPasswordHash`. CI گاردهای `generate_migrations.py --check`، `verify_migration_model.py` و `dotnet ef migrations has-pending-model-changes` را اجرا می‌کند و اجرای شماره `39585708662` سبز بود. وجود Migration به‌معنی اعمال آن روی هر دیتابیس مشتری نیست؛ برای دیتابیس‌های قدیمی ساخته‌شده با `EnsureCreated()` مسیر کنترل‌شده در بخش «راه‌اندازی اولیه» پایین و `database-migrations.md` گفته شده است. نکته: برچسب زمانی `InitialCreate` (`20251017…`) ناهمگون به‌نظر می‌رسد (AUD-2026-10-21-08+)؛ بدون تصمیم مستند بازنویسی نشود.

## ویژگی‌های واقعی Schema مدل چندشرکتی (از `Configurations/*`)
- `Companies`: `Name` حداکثر ۲۰۰؛ `NationalId` حداکثر ۲۰ با نمایه یکتای فیلتردار.
- `FiscalPeriods`: یکتا روی `(TenantId, StartDate, EndDate)`.
- `CompanyRoles`: یکتا روی `(TenantId, Code)` و کلید جایگزین `(Id, TenantId)`.
- `PermissionDefinitions`: `Code` حداکثر ۱۲۰، یکتا.
- `UserCompanyMemberships`: یکتا روی `(UserId, TenantId)`؛ کلید خارجی ترکیبی `(RoleId, TenantId)` به `CompanyRoles` و FK به `Companies`. **`UserId` هیچ FK به `Users` ندارد** (جلوگیری از عضویت یتیم در برنامه/تست لازم است؛ تصمیم اصلاح جداگانه).
- Global Query Filter وجود ندارد (D-013)؛ فیلتر صریح با `CompanyAccessService`.
- موجودیت‌های پایه (`Seedwork/Entity.cs`): `Id` (Guid)، `Ordering`، `InsertDateTime`.

## مدل داده لایسنس (طرح، `NotImplemented`)
هیچ جدول لایسنس/مشتری/نصب وجود ندارد. طرح و ERD در [`LICENSING_DATA_MODEL.md`](LICENSING_DATA_MODEL.md)؛ هر جدول آینده فقط با Migration افزایشی بازبینی‌شده پس از تأیید کارفرما (D-020، D-025).


## راه‌اندازی اولیه و فایل تنظیمات محلی — Issue #22

- اگر Connection String صریح از Configuration Providers وجود نداشته باشد، `Server/Program.cs` فایل محلی محافظت‌شده را بررسی می‌کند؛ اگر آن هم وجود نداشته باشد، درخواست‌های پویا به صفحه `/DatabaseSetup` هدایت می‌شوند.
- تنظیمات محلی در `%LOCALAPPDATA%/TarazAva/Database/database-settings.json` (ویندوز) ذخیره می‌شوند. مقدار Connection String در فایل JSON به‌صورت Data Protection ciphertext است، نه متن ساده.
- کلیدهای Data Protection در پوشه `keys` کنار فایل تنظیمات قرار دارند. در ویندوز با DPAPI محافظت می‌شوند؛ در سیستم‌های Unix مجوز پوشه و فایل محدود می‌شود. فایل تنظیمات و کلیدها را با هم، امن و تحت همان هویت اجرایی پشتیبان‌گیری کنید؛ انتقال ciphertext بدون کلید قابل رمزگشایی نیست.
- کد راه‌اندازی یک‌بارمصرف در `setup-token.txt` قرار دارد، در لاگ چاپ نمی‌شود و بعد از ذخیره موفق حذف می‌شود.
- فرم از `SqlConnectionStringBuilder` استفاده می‌کند و اتصال را به `master` می‌سنجد. نام دیتابیس در SQL با `QUOTENAME` محافظت می‌شود و مقدار ورودی به‌صورت پارامتر منتقل می‌شود.
- اگر دیتابیس وجود نداشته باشد و گزینه ساخت فعال باشد، دیتابیس ساخته و Migrationهای EF Core اعمال می‌شوند. دیتابیس خالی موجود نیز با Migrationها مقداردهی اولیه می‌شود. دیتابیس دارای جدول‌ها/داده‌ها به‌طور خودکار تغییر نمی‌کند؛ وضعیت Migration باید بررسی و در صورت لزوم به‌صورت کنترل‌شده اعمال شود.
- پس از ذخیره تنظیمات، برنامه را Restart کنید. مقدار صریح `ConnectionStrings:ConnectionString` از User Secrets/Environment/configuration بر فایل محلی اولویت دارد.
- برای تنظیمات محلی ایجادشده از فرم، Migration خودکار در هر بار شروع برنامه فعال نیست؛ این کار از اعمال کورکورانه Migration روی دیتابیس قدیمی ایجادشده با `EnsureCreated()` جلوگیری می‌کند. از `dotnet run --project Server -- --migrate` فقط پس از بازبینی وضعیت schema استفاده کنید.

## بازبینی مدل داده برای انتخاب شرکت و دوره (D-031) — 2026-10-21
- **نتیجه: Schema و Migration تغییر نکرد.** `FiscalPeriod` پیش‌تر `TenantId`، `StartDate`، `EndDate` و `Status` دارد؛ ایندکس `(TenantId, Status)` و یکتایی `(TenantId, StartDate, EndDate)` برای پرس‌وجوی انتخاب کافی است. فیلد «دوره جاری» یا «آخرین انتخاب» ساخته نشد (حدسی؛ انتخاب فقط در نشست است).
- **شکاف‌های ثبت‌شده، بدون اقدام:** محدودیت CHECK برای `StartDate < EndDate` و جلوگیری از هم‌پوشانی دوره‌های یک شرکت وجود ندارد؛ در صورت تأیید محصول به‌صورت Migration جداگانه پیشنهاد می‌شود (`NeedsApproval`). `UserCompanyMemberships.UserId` همچنان بدون FK است (D-029).
- پایگاه داده عملیاتی با پایگاه لایسنس فروشنده ترکیب نشد.
