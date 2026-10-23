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


## راه‌اندازی اولیه و فایل تنظیمات محلی — Issue #22 (بازطراحی 2026-10-23)

> جزئیات کامل، جدول مهاجرت از `TarazAva` و ماتریس رفتار: [`DATABASE_SETUP.md`](DATABASE_SETUP.md). تصمیم: D-036 (`NeedsApproval`). وضعیت: `Implemented` در شاخه `feature/database-setup-redesign` (ادغام‌نشده).

- اگر Connection String صریح از Configuration Providers وجود نداشته باشد، `Server/Program.cs` فایل محلی محافظت‌شده را بررسی می‌کند؛ اگر آن هم قابل استفاده نباشد، درخواست‌های پویا به `/DatabaseSetup` هدایت می‌شوند. فایل ناخوانا هرگز صفحه ساده first-run نشان نمی‌دهد.
- ~~`%LOCALAPPDATA%/TarazAva/Database/database-settings.json`~~ **منسوخ.** مسیر فعلی: `Farano:Settings:Directory` (متغیر `Farano__Settings__Directory`) و در نبود آن `LocalApplicationData/Farano/Settings` (فایل `database-connection.json`، `keys/`، `backups/`، `migration.json`، `setup-token.txt`). مسیر سفارشی باید absolute و بیرون پوشه برنامه باشد؛ هرگز به پوشه برنامه برنمی‌گردد. فایل قدیمی `TarazAva` فقط خوانده و مهاجرت می‌شود و هرگز تغییر یا حذف نمی‌شود.
- این تنظیمات از فایل‌های داده SQL Server (`.mdf/.ldf`) جداست؛ برنامه آن‌ها را نمی‌سازد، جابه‌جا یا حذف نمی‌کند.
- کلیدهای Data Protection (`ApplicationName=Farano`) در `keys/` کنار فایل قرار دارند و در Windows با DPAPI (وابسته به حساب) محافظت می‌شوند. فایل تنظیمات و کلیدها را با هم و با همان هویت اجرایی پشتیبان بگیرید؛ ciphertext بدون کلید قابل رمزگشایی نیست.
- فرم از `SqlConnectionStringBuilder` استفاده می‌کند، اتصال را به `master` می‌سنجد (`Encrypt` همیشه فعال)، نام دیتابیس را با پارامتر و `QUOTENAME` به SQL می‌دهد و هرگز `DROP` اجرا نمی‌کند.
- وضعیت دیتابیس موجود: `Missing`/`Empty`/`UpToDate`/`PartiallyMigrated`/`ForeignOrLegacy`. دیتابیس خالی یا ساخته‌شده Migration می‌شود؛ دیتابیس نیمه‌Migration فقط با تیک صریح؛ دیتابیس بیگانه (جدول بدون تاریخچه Migration یا شناسه ناشناس) رد می‌شود و دست نمی‌خورد.
- پس از ذخیره تنظیمات برنامه را Restart کنید. مقدار صریح `ConnectionStrings:ConnectionString` بر فایل محلی اولویت دارد (در آن حالت Store ساخته نمی‌شود).
- برای تنظیمات ایجادشده از فرم، Migration خودکار در هر بار شروع فعال نیست (جلوگیری از اعمال کورکورانه روی دیتابیس قدیمی `EnsureCreated()`). از `dotnet run --project Server -- --migrate` فقط پس از بازبینی Schema استفاده کنید.
- فرمان اپراتور: `dotnet Server.dll --settings status` و `--settings migrate [--replace-current|--keep-current]` (کد خروج ۰/۱/۲).

## بازبینی مدل داده برای انتخاب شرکت و دوره (D-031) — 2026-10-21
- **نتیجه: Schema و Migration تغییر نکرد.** `FiscalPeriod` پیش‌تر `TenantId`، `StartDate`، `EndDate` و `Status` دارد؛ ایندکس `(TenantId, Status)` و یکتایی `(TenantId, StartDate, EndDate)` برای پرس‌وجوی انتخاب کافی است. فیلد «دوره جاری» یا «آخرین انتخاب» ساخته نشد (حدسی؛ انتخاب فقط در نشست است).
- **شکاف‌های ثبت‌شده، بدون اقدام:** محدودیت CHECK برای `StartDate < EndDate` و جلوگیری از هم‌پوشانی دوره‌های یک شرکت وجود ندارد؛ در صورت تأیید محصول به‌صورت Migration جداگانه پیشنهاد می‌شود (`NeedsApproval`). `UserCompanyMemberships.UserId` همچنان بدون FK است (D-029).
- پایگاه داده عملیاتی با پایگاه لایسنس فروشنده ترکیب نشد.

## جداول گروه شرکتی و تلفیق (D-033، 2026-10-22)
- جداول جدید: `CorporateGroups`، `CorporateGroupMembers`، `OwnershipRelationships`، `ConsolidationProfiles`، `ConsolidationRuns`، `ConsolidationRunCompanies`. ستون‌ها، کلیدها، CHECKها و ایندکس‌ها: [`CORPORATE_GROUPS_DATA_MODEL.md`](CORPORATE_GROUPS_DATA_MODEL.md).
- تنها تغییر روی جدول موجود: قید یکتای `AK_FiscalPeriods_Id_TenantId` برای FK ترکیبی `(FiscalPeriodId, CompanyId)` از `ConsolidationRunCompanies`؛ دیتابیس دوره مالی شرکت دیگر را نمی‌پذیرد.
- درصدها `decimal(9,6)`؛ تاریخ‌های اعتبار `date`؛ همه FKها `Restrict`.
- کاتالوگ مجوز گروه با درج idempotent (`IF NOT EXISTS`) در همان Migration؛ هیچ داده مشتری یا عدد مالی درج نمی‌شود.
- هم‌پوشانی بازه‌ها، جمع درصدها و چرخه کنترلی در سرویس با تراکنش `Serializable` کنترل می‌شوند، نه در دیتابیس؛ نوشتن مستقیم SQL این قواعد را دور می‌زند.
