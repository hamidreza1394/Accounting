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

## مدل چندمستاجری فعلی و هدف معماری (D-087)

**مدل هدف:** `Tenant` مرز مالکیت و جداسازی داده مشتری فرانو است؛ `Company` شرکت حقوقی/عملیاتی داخل Tenant است. هر Tenant می‌تواند یک یا چند Company داشته باشد. این مدل در هر دو استقرار مشترک و اختصاصی ثابت می‌ماند:
- **Shared database:** چند Tenant در یک دیتابیس؛ تمام داده‌های tenant-owned با `TenantId` جدا می‌شوند و داده‌های company-owned علاوه بر آن `CompanyId` دارند.
- **Dedicated database:** دیتابیس مستقل برای یک Tenant؛ TenantId همچنان در مدل منطقی وجود دارد و CompanyId شرکت داخل آن را مشخص می‌کند. جداسازی فیزیکی جایگزین authorization و کنترل Tenant/Company در برنامه نیست.
- هر Company باید به دقیقاً یک Tenant تعلق داشته باشد. FK مرکب/قیدهای معادل باید ناسازگاری TenantId و CompanyId را در روابط داده‌ای ناممکن کنند.
- `UserCompanyMembership`: عضویت کاربر در یک Company مشخص و نقش همان Company؛ مجوز شرکت به شرکت دیگر یا Tenant دیگر ارث نمی‌رسد.
- `FiscalPeriod` و `CompanyRole`: متعلق به Company هستند؛ TenantId در کلیدها و قیدها نقش جداسازی بالادستی دارد.
- `CustomerBoundaryId` در D-038 یک شناسه opaque برای محدودکردن روابط گروهی/مالکیت است؛ TenantId نیست و جایگزین Tenant نمی‌شود.

**وضعیت کد فعلی:** مدل موجود تاریخی، `Company` را عملاً مرز Tenant گرفته و در چند جدول `TenantId` برابر شناسه Company است. این وضعیت با D-087 یکسان نیست و نباید با تغییر نام یا فرض‌گرفتن معنای جدید، سازگار اعلام شود. S3-0 باید همه Entityها، FKها، unique indexها، queryها، هویت/عضویت و migration pipeline را ممیزی کند و طرح گذار افزایشی و سازگار با هر دو نوع استقرار ارائه دهد. تا تصویب طرح گذار، هیچ ستون/کلید/مهاجرت Tenant جدید یا backfill خودکار ساخته نشود.

**وضعیت تصمیم‌های Tenant (2026-11-15):** D-089 (`Accepted`، پیاده‌سازی `NotImplemented`): ایجاد Tenant فقط توسط مدیر نصب؛ نگاشت یکتای `CustomerReference → TenantId`؛ Provisioning فقط به Tenant موجود متصل می‌شود؛ Tenant از `CompanyId`/`CustomerBoundaryId`/گروه/نام استنتاج نمی‌شود؛ ستون‌های `TenantId` فعلی (= `Company.Id`) داده میراثی‌اند و تبدیل آن‌ها فقط در S3 با تأیید جداگانهٔ Schema و Backfill انجام می‌شود. هیچ جدول یا ستون Tenant در Schema وجود ندارد. تصمیم‌های OD-11/12/13/14/17 در D-090..D-094 `Proposed` هستند (PR #671).

### قاعده مالکیت پیش‌فرض داده‌های مشتری (D-105)

- داده‌های عملیاتی و کسب‌وکاری مشتری در شروع طراحی **Company-owned** هستند؛ در مدل هدف هر رکورد این گروه هم TenantId و هم CompanyId معتبر دارد. TenantId باید Tenant واقعیِ مالک Company باشد، نه کپی یا نام دیگری از CompanyId.
- هیچ موجودیتی صرفاً به دلیل احتمال استفاده مشترک، از ابتدا Tenant-owned نمی‌شود. هر نوع داده تا زمان تصمیم مستقل اشتراک، برای هر Company جدا و مستقل نگهداری می‌شود.
- اگر اشتراک یک نوع موجودیت تصویب شد، رکورد مشترک در سطح همان Tenant نگهداری می‌شود: TenantId دارد و CompanyId مستقیم ندارد؛ شرکت‌های مجاز از جدول پیوند مستقل به آن متصل می‌شوند. قیود رابطه باید تعلق رکورد و شرکت‌های پیوندخورده به همان Tenant را تضمین کنند.
- اشتراک فقط میان Companyهای همان Tenant است. عضویت CorporateGroup، OwnershipRelationship یا CustomerBoundaryId به‌تنهایی مجوز یا مرز اشتراک داده نیست.
- فعلاً کاتالوگ/مرجع سراسری کسب‌وکاری برای داده‌های مشتری نداریم؛ ایجاد آن به نیاز واقعی و تصمیم مستقل آینده موکول است.
- این قاعده فقط برای داده‌های عملیاتی/کسب‌وکاری مشتری است؛ جداول Vendor، Installation، فنی/زیرساختی، هویتی سراسری و روابط بین‌شرکتی بر اساس مالکیت واقعی جداگانه طبقه‌بندی می‌شوند و الزام مکانیکی دو ستون ندارند.
- وضعیت legacy تغییر نمی‌کند: در برخی مدل‌ها TenantId فعلی عملاً Company.Id است و Tenant واقعی نیست. D-105 هیچ Schema، Migration، Backfill یا Runtime جدیدی را مجاز نمی‌کند.

### قرارداد داده برای توسعه جدید
- موجودیت tenant-owned: `TenantId` اجباری.
- موجودیت company-owned: `TenantId` و `CompanyId`؛ هر دو از context معتبر سمت سرور تعیین و با هم تطبیق داده شوند.
- کلید خارجی و unique index در داده company-owned باید مرز Tenant را نیز حفظ کند، مگر ممیزی S3-0 دلیل مستند و آزمون‌پذیر دیگری ارائه دهد.
- کلاینت حق تعیین TenantId معتبر را ندارد؛ CompanyId ارسالی نیز فقط درخواست انتخاب است و باید با عضویت فعال تطبیق داده شود.
- هیچ Global Query Filter بدون پوشش صریح مسیرهای ورود، عملیات سیستمی، jobها و دسترسی‌های بدون Company جاری اضافه نشود (D-013).

## هویت کاربران مشتری و عضویت چندشرکتی (D-118؛ مدل هدف)

- هر User مشتری دقیقاً به یک Tenant تعلق دارد؛ `Users.TenantId` در مدل هدف اجباری است و User به Company وابسته نیست.
- Username در محدوده Tenant یکتا است؛ دو Tenant مستقل می‌توانند Username یکسان داشته باشند. Login ابتدا Tenant را از مسیر معتبر D-103 resolve می‌کند و سپس `TenantId + normalized Username` را جستجو می‌کند.
- `UserCompanyMemberships` یک رابطه Company-owned است و در مدل هدف `TenantId` و `CompanyId` مستقل دارد. یکتایی آن `(UserId, TenantId, CompanyId)` است؛ FK مرکب به User و Company باید هم‌تعلقی Tenant را تضمین کند.
- `UserSessions` به User همان Tenant تعلق دارد؛ برای تضمین FK مرکب، TenantId در Session یا معادل رابطه‌ای قابل اثبات لازم است. Session به Company وابسته نیست، چون Company پس از Login انتخاب می‌شود.
- `Users.DefaultCompanyId` صرفاً ترجیح است؛ اگر حفظ شود باید به Company همان Tenant اشاره کند و به‌تنهایی مجوز ایجاد نمی‌کند.
- `LoginLogs` می‌تواند Tenant را از UserId معتبر resolve کند؛ افزودن TenantId/CompanyId صرفاً برای تکرار داده مجاز نیست.
- Email، CellPhoneNumber و verification keyها طبق D-118 فعلاً Unique Index سراسری موجود را حفظ می‌کنند؛ تغییر دامنه آن‌ها نیازمند تصمیم مستقل است.
- Master infrastructure account طبق D-029/D-064 خارج از جدول هویت کاربران مشتری است؛ Vendor identity نیز در سامانه جداگانه می‌ماند.

این قرارداد، هدف معماری است و با وضعیت فعلی Schema اشتباه گرفته نشود. در Schema فعلی هنوز User فاقد TenantId است، Username/Email/Mobile سراسری‌اند، UserSession فاقد TenantId است و `UserCompanyMemberships` از TenantId legacy به‌عنوان CompanyId استفاده می‌کند. تغییرات فقط پس از بسته‌شدن گیت‌های S3-0 و مجوز مستقل Migration انجام می‌شوند.

## توصیف تاریخی مدل چندمستاجری افزوده‌شده در Issue #16
- در مدل فعلی `Company` ریشه داده و مرز Tenant تلقی شده است؛ این توصیف، وضعیت تاریخی پیاده‌سازی است و مدل هدف D-087 نیست.
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

**به‌روزرسانی 2026-11-15 (#384/#228):** جدول‌های `InstallationIdentities`، `InstalledLicenses`، `CustomerOfflineRevocationStates` و `ProvisioningRequestClaims` با Migrationهای خودشان در `Infrastructure/Persistence/Migrations` اضافه شده‌اند؛ متن بالا فقط سابقهٔ تاریخی آن نقطه است.

**به‌روزرسانی 2026-11-16 (#392 بازسازی):** جدول `CompanySetupRecords` با Migration `20261116070805_AddCompanySetupRecords` اضافه شد (فقط جدول جدید؛ بقیهٔ Schema لمس نشده): نگاشت یک‌به‌یک `CompanyReference` امضاشدهٔ Vendor به Company با FK با `Restrict`، ایندکس یکتای `CompanyId`، و ایندکس یکتای مرجع روی Collation `Latin1_General_BIN2` (پس‌پردهٔ Migration پیش از ساخت ایندکس اجرا می‌شود تا ایندکس از Collation باینری پیروی کند — پاسخ به یافتهٔ L-2 بازبینی Vendor). حذف Company دارای نگاشت با FK رد می‌شود؛ `Down` فقط همین جدول را حذف می‌کند. معنای عملیاتی در [Runbook #392](handoffs/ISSUE-392-customer-implementation-runbook.md) است.


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

## ستون‌های شرکت پیش‌فرض و مرز مشتری (D-038، D-039) — 2026-10-23
- **`Users.DefaultCompanyId`** (`Guid?`): کلید خارجی به `Companies`، رفتار حذف `ON DELETE SET NULL`، ایندکس‌شده. Migration `20261023190000_AddUserDefaultCompany` (PR #68). فقط پیش‌انتخاب است و دسترسی نمی‌دهد (D-039).
- **`Companies.CustomerBoundaryId`** (`Guid?`): شناسه مات و تصادفی مرز مشتری، بدون PII، ایندکس‌شده، **بدون FK** (عمداً؛ مرز به هیچ جدول مشتری/فروشنده اشاره نمی‌کند و هرگز برابر `CustomerAccount.Id` نیست). `null` = Provision‌نشده = Fail-closed و حالت دائمی مجاز؛ ستون Nullable می‌ماند. Migration `20261023191000_AddCompanyCustomerBoundary` (PR #68). تنها نویسنده: Endpoint اپراتور `assign` (مقدار غیرتهی از راه API تغییر نمی‌کند).
- **اجرا:** هر دو Migration افزایشی‌اند؛ اعمال روی دیتابیس مشتری/تولید ادعا نمی‌شود. Backfill خودکار وجود ندارد؛ Provision فقط با اپراتور و راهنمای `diagnostics` (D-038).
- **محدودیت:** اعمال «غیرتهی تغییر نکند» و «روابط از مرز عبور نکنند» فقط در برنامه است؛ SQL مستقیم آن را دور می‌زند. تریگر اختیاری دیتابیس `NeedsApproval`؛ FK ترکیبی و تریگر روی جداول رابطه ردشده (D-038).


## Migration داده‌ای کاتالوگ مجوز Trade
- `20261031140000_SeedTradePermissionCatalog` فقط ۴۶ ردیف اولیه را به جدول مشترک `PermissionDefinitions` اضافه می‌کند؛ هیچ جدول یا ستون جدیدی نمی‌سازد و به `TradeDbContext` مربوط نیست.
- درج بر اساس Code و با `IF NOT EXISTS` است؛ ردیف از قبل موجود و نام/شرح سفارشی مدیر بازنویسی نمی‌شود. هیچ تخصیص نقش ایجاد نمی‌شود.
- Down فقط شناسه‌های ثابت seedشده را حذف می‌کند، آن هم در صورت نبود ارجاع از `CompanyRolePermissions`. این Migration روی دیتابیس مشتری اجرا نشده است.


## مدل مصوب دسترسی سال مالی و دوره حسابداری (D-066/D-077)

- `FiscalPeriods` موجود به‌عنوان سال مالی حفظ می‌شود؛ جدول `FiscalYears` موازی ممنوع است.
- `AccountingPeriods` زیرمجموعه `FiscalPeriods` است و با FK ترکیبی `(FiscalPeriodId, TenantId)` به سال همان شرکت متصل می‌شود.
- `UserCompanyMemberships` مرجع عضویت است؛ تخصیص نقش در `UserCompanyRoleAssignments` مستقل از عضویت نگهداری می‌شود. `UserCompanyRoleAssignmentPeriods` دوره‌های انتخابی policy نوع Explicit را ثبت می‌کند.
- Composite FKها، filtered unique indexها، RowVersion و قیود دوره/شرکت باید در Migration افزایشی پیاده شوند. شرح کامل: [`FISCAL_ACCESS_DATA_MODEL.md`](FISCAL_ACCESS_DATA_MODEL.md).
- هیچ grant سالانه برای کاربران فعلی به‌صورت خودکار backfill نمی‌شود. RoleId قدیمی عضویت فقط به Company-scoped assignment منتقل می‌شود تا رفتار اداری حفظ شود؛ دسترسی سال/دوره فقط با تخصیص صریح مدیر ایجاد خواهد شد.
- Migration فقط پس از تکمیل همه مصرف‌کنندگان، تست SQL Server، بررسی rollback و CI سبز ارائه می‌شود؛ هیچ Migration روی دیتابیس مشتری در این مرحله اجرا نشده است.

## قواعد پیشنهادی ارجاع و حذف اطلاعات پایه (D-083؛ `Proposed` + `NeedsApproval`، 2026-11-13)

- FK به اطلاعات پایه (Party، Product و …) `Restrict`/`NoAction` باشد؛ حذف آبشاری اسناد و سوابق به‌واسطهٔ حذف اطلاعات پایه ممنوع است (هم‌راستا با «همه FKها Restrict» در V3.2 و `CORPORATE_GROUPS_DATA_MODEL.md`).
- رکورد استفاده‌شده با غیرفعال‌سازی مدیریت می‌شود؛ شکل حذف رکورد استفاده‌نشده (فیزیکی یا `SoftDelete`/`IsDeleted`) به پرسش Q-1 در D-083 وابسته است، چون V3.2 حذف فیزیکی در Trade را پیش‌بینی نکرده است. ایندکس‌های یکتای فعلی Party/Product با `[IsDeleted] = 0` فیلتر می‌شوند و `IsActive` در آن نیست (Q-4).
- هیچ Migration یا تغییر Schema با این بخش انجام نمی‌شود.

## Migration Host: Core و Trade — PR-1 (S3-0f، 2026-11-14)
**اصل:** Migration متعلق به دیتابیس است؛ مجوز استفاده از ماژول متعلق به Tenant یا نصب اختصاصی. این دو با هم ترکیب نمی‌شوند و `--migrate` هیچ Entitlement را نمی‌خواند یا تغییر نمی‌دهد.

**قبل:** `Server.dll --migrate` فقط `DatabaseContext` (Core) را اجرا می‌کرد؛ `TradeDbContext` (تاریخچهٔ `__TradeMigrationsHistory`، Assembly مستقل) هیچ مسیر اجرایی نداشت. **بعد:** `Infrastructure.Hosting.DatabaseMigrationHost` یک نقطهٔ اجرا برای هر دو Context است.

| فرمان | رفتار |
|---|---|
| `Server.dll --migrate` | فقط Core. Trade اجرا نمی‌شود (پیش‌فرض امن). |
| `Server.dll --migrate --include-trade` | اول Core، سپس Trade؛ اگر Core شکست بخورد Trade شروع نمی‌شود. |

**کد خروج:** `0` موفق؛ `1` شکست Core؛ `2` شکست Trade؛ `3` قفل گرفته نشد؛ `4` وضعیت ناسازگار Trade (جدول بدون History).

**Shared و Dedicated (همان فرمان؛ تفاوت فقط در اینکه چه کسی و کِی `--include-trade` را می‌زند):**
```bash
# دیتابیس مشترک ابری: Core همیشه؛ Trade یک‌بار برای کل دیتابیس، هنگام فعال‌شدن اولین مشتری Trade (نه به ازای هر Tenant)
ConnectionStrings__ConnectionString="<مشترک>" dotnet Server.dll --migrate
ConnectionStrings__ConnectionString="<مشترک>" dotnet Server.dll --migrate --include-trade
# نصب اختصاصی: Trade فقط وقتی برای همان نصب خریداری و فعال شده است
dotnet Server.dll --migrate                 # پیش از خرید Trade
dotnet Server.dll --migrate --include-trade # پس از خرید، بدون نصب مجدد
```
**تفکیک وضعیت (2026-11-15):** «Migration Host پیاده‌شده» (کد و تست‌ها در `main@1c0eec0`) با «سیاست عملیاتی Migration» یکی نیست. سیاست نهایی (اتصال Entitlement، Shared/Dedicated، نصب فاقد مجوز، Backup/Recovery، ثبت ممیزی اجرا) هنوز تعیین نشده و پیشنهاد آن D-094 (`Proposed`) است. هیچ ادعای امنیتی دربارهٔ Migration Host فراتر از تست‌های `DatabaseMigrationHostSqlServerTests` ثبت نمی‌شود و اجرا روی دیتابیس مشتری مجوز عملیاتی جداگانه می‌خواهد.

**قرارداد برای آینده (در این PR پیاده نشد):** اتصال خرید/فعال‌سازی Trade به اجرای `--include-trade` در PR جداگانه انجام می‌شود؛ نقطهٔ اجرا `DatabaseMigrationHost.RunAsync(includeTrade)` است. تشخیص Shared/Dedicated از روی Connection String یا تعداد شرکت‌ها انجام نمی‌شود؛ Deployment Mode معتبری در پروژه وجود ندارد و تنظیم جدیدی افزوده نشد.

**قفل هم‌زمانی:** `sp_getapplock` (Exclusive، `LockOwner = 'Session'`، `LockTimeout = 0`) روی یک اتصال اختصاصی بدون Pool با منبع `Farano.DatabaseMigration:<نام دیتابیس>`. اجرای دوم بلافاصله با کد `3` رد می‌شود. قفل در موفقیت و شکست با `sp_releaseapplock` آزاد می‌شود و با بسته‌شدن اتصال (قطع اجرا یا مرگ پروسه) سرور آن را آزاد می‌کند. اگر دیتابیس هنوز وجود ندارد (EF آن را می‌سازد)، قفل از طریق `master` با همان نام منبع گرفته می‌شود.

**شکست و اجرای مجدد:** هر Context تاریخچهٔ مستقل دارد و Migrationهای موفق دوباره اجرا نمی‌شوند. شکست Trade پس از موفقیت Core نتیجه را `2` می‌کند؛ اجرای مجدد Core را «به‌روز» می‌بیند و از Trade ادامه می‌دهد. خطاها فقط نوع استثنا، شمارهٔ خطای SQL و پیام با تنظیمات اتصال حذف‌شده (`Password`, `User ID`, `Data Source`, ...) چاپ می‌کنند.

**دیتابیس قدیمی بدون History:** اگر جدول‌های Schema `trade` وجود دارند و `__TradeMigrationsHistory` نیست یا خالی است، اجرا با کد `4` متوقف می‌شود. هیچ Baseline خودکار، حذف یا بازسازی انجام نمی‌شود. روش بازیابی (دستی، با تأیید مالک): ۱) پشتیبان‌گیری کامل؛ ۲) مقایسهٔ Schema جدول‌های `trade` با Migration `20261113072823_InitialTradeMasterData` (`dotnet ef migrations script --context TradeDbContext`)؛ ۳) فقط پس از اثبات هم‌ارزی، تصمیم دربارهٔ ثبت ردیف History با مالک؛ ۴) در غیر این صورت انتقال داده به Schema جدید. Baseline/Backfill در PR-1 انجام نشد. در `Farano-Local` جدول‌های Trade ممکن است اصلاً نباشند؛ در این حالت اجرای عادی `--include-trade` آن‌ها را می‌سازد.

**خارج از دامنه (Backlog):** هشدار «Migration Trade در انتظار» هنگام شروع برنامه (امروز فقط Core گزارش می‌شود).

## زیرساخت Database Topology Routing (D-115، Issue #728) — 2026-11-20

طبق D-115 (Accepted) و بند ۴/۵ D-111، لایه کوچک Resolve مقصد دیتابیس در `Server/Infrastructure/DatabaseTopology/` پیاده شده است و **فقط زیرساخت است**: `Data.DatabaseContext` همچنان با Connection String منفرد فعلی کار می‌کند و سوییچ عملیاتی Context به Resolver تغییر مجزای نیازمند تأیید مالک است.

- `IDatabaseTargetRegistry` / `ConfigurationDatabaseTargetRegistry`: تنها منبع معتبر Routing، Registry سمت سرور (پیکربندی `DatabaseTopology:*`). مقصد دیتابیس از هیچ ورودی کلاینت پذیرفته نمی‌شود (D-115 §1).
- کلیدهای Matching توپولوژی‌های مصوب: TenantDatabase → TenantId؛ CompanyDatabase → TenantId+CompanyId؛ CompanyFiscalPeriodDatabase → هر سه. تطبیق کامل هویت است؛ Scope با مرز Tenant دیگر هرگز به دیتابیس Company دیگر نمی‌رسد. این تطبیق «ایمنی هویتی» Routing است، نه اثبات مالکیت واقعی FiscalPeriod→Company (D-114)؛ اثبات مالکیت در مرجع معتبر سمت سرور و در گام اتصال عملیاتی انجام می‌شود (D-115 §7).
- توپولوژی Pending (TenantFiscalPeriodDatabase، گزینه ۳ D-111): طبق D-115 §8 تا تأیید مالک **قابل‌انتخاب و قابل‌فعال‌سازی نیست** — Resolve آن را fail-closed رد می‌کند و `Validate` در Startup همان پیکربندی را متوقف می‌کند؛ هیچ مسیر Configuration جزیی برای فعال‌کردن آن وجود ندارد (handoff `ISSUE-728-tenant-dedicated-decisions.md`).
- Fail-closed با کدهای ثابت و بدون داده حساس: `topology_disabled` (پیش‌فرض؛ رفتار میزبان عین قبل)، `topology_kind_invalid`، `topology_kind_pending_owner_decision`، `scope_invalid`، `assignment_missing`، `assignment_conflict` (دو Assignment هم‌پوشان برای یک Scope خطای پیکربندی است)، `connection_missing`. هیچ fallback به دیتابیس پیش‌فرض وجود ندارد.
- Connection String یک Secret است و **فقط در Registry نگه داشته می‌شود** (D-115 §3): پاسخ Routing یعنی `DatabaseTarget` فقط شناسه مرجع (`DatabaseId`) دارد و هیچ Credentialای در آن نیست؛ مقدار اتصال تنها از مسیر داخلی `IDatabaseConnectionSecrets.TryGetConnectionString` (پیاده‌سازی explicit روی Registry) به لایه مسئول ساخت اتصال (`Data.DatabaseContext` در اسمبلی `Infrastructure.Persistence` از طریق `InternalsVisibleTo`) داده می‌شود و مصرف‌کنندگان عمومی Routing هیچ راهی برای دریافت آن ندارند. محدودیت زبان C# صریح است: دید internal در سطح اسمبلی اعمال می‌شود، نه محل فراخوانی؛ مصرف‌کننده Production دیگری به این مسیر اضافه نمی‌شود (قانون Code Review). `ToString`، JSON Serialization، Structured Logging، Exception، Audit، API Response و پیام شکست تست هیچ‌کدام به Secret راه ندارند. منبع خواندن، همان منبع امن فعلی پیکربندی میزبان است و Secret در هیچ ساختار دومی تکرار نمی‌شود.
- Lifetime پیکربندی: Registry با `IOptions<DatabaseTopologyOptions>` یک‌بار در Startup بایند و اعتبارسنجی می‌شود و snapshot را در سازنده خود می‌گیرد؛ یک Hosted Service گارد (`DatabaseTopologyStartupSnapshot`) ساخت Registry را در Start میزبان اجباری می‌کند تا پنجره lazy بین اعتبارسنجی و اولین Resolve بسته شود. `IOptionsMonitor`/`IOptionsSnapshot` عمداً مصرف نمی‌شود و هیچ listener تغییر ثبت نشده است، بنابراین Reload فایل Configuration هرگز مقصد درخواست‌های جاری یا آینده را تغییر نمی‌دهد و درخواست‌های هم‌زمان همیشه یک پیکربندی سازگار می‌بینند. تغییر مقصد فقط از مسیر کنترل‌شده و Auditشده D-115 §6 با Restart میزبان انجام می‌شود؛ هیچ Cutover یا تغییر Connection String واقعی در این PR فعال نشده است.
- Parse توپولوژی سخت‌گیرانه است: مقدار باید نام عضو تعریف‌شده (case-insensitive) یا مقدار عددی دقیق یک عضو تعریف‌شده باشد؛ رشته‌های عددی تعریف‌نشده مانند `999` با `Enum.IsDefined` رد می‌شوند (`topology_kind_invalid` در Resolve و Validate) — هیچ توپولوژی ناشناخته‌ای به Target معتبر تبدیل نمی‌شود.
- ثبت DI با اعتبارسنجی fail-fast: `AddDatabaseTopologyResolution` (ValidateOnStart). پیکربندی ناقص (Assignment بدون Connection، کلیدهای ناهم‌خوان با توپولوژی، Assignment تکراری برای یک Scope، توپولوژی Pending، Topology نامعتبر/تعریف‌نشده) و **Routing فعال بدون هیچ Assignment** (`registry_empty`) میزبان را در Startup متوقف می‌کند؛ پیکربندی خالی/غیرفعال معتبر است.
- تست‌ها: `DatabaseTopologyRoutingTests` — ۳۰ Fact + ۲ Theory (۳۴ اجرای تست) با پوشش سناریوهای الزامی مأموریت: Routing سه توپولوژی مصوب، رد توپولوژی Pending (resolve + validate)، پذیرش عدد تعریف‌شده و رد عدد تعریف‌نشده/رشته نامعتبر/مقدار خالی، Scopeهای ناقص/نامعتبر، ترکیب هویتی نامرتبط و FiscalPeriod شرکت دیگر و Tenant نامرتبط، Assignment تکراری/متناقض/مفقود، Connection نامعتبر، نبود هیچ مسیر عمومی به Secret (بازتاب سطح عمومی)، عدم افشا در ToString/JSON/Logging/Exception، دسترسی محدود از مسیر internal، deterministic بودن Resolveهای مکرر، فعال بدون Assignment، غیرفعال، و ایزوله‌بودن Scope در Resolveهای هم‌زمان. تست‌های مسیر عملیاتی Context (اتصال Resolver به DbContext) در PR سوییچ عملیاتی خواهند آمد.


## مالکیت داده بین توپولوژی‌های دیتابیس و ادامه پرونده در سال مالی جدید (D-117)

- تصمیم مالک و مرزهای چهار توپولوژی در DECISIONS.md، D-117 ثبت شده‌اند.
- مدل منطقی جداول، فیلدها، قیود، ایندکس‌ها، Snapshot، Carry-forward، شماره‌گذاری و افتتاحیه در FISCAL_PERIOD_DATA_MODEL.md است.
- در Single-DB، داده پایه و پرونده‌های بلندمدت در همان دیتابیس نگهداری می‌شوند و تفکیک سال با شناسه‌های Company/FiscalPeriod انجام می‌شود.
- در Company + FiscalPeriod DB، داده‌های مرجع و پرونده‌های بلندمدت در Company-level owner باقی می‌مانند؛ اطلاعات پایه موردنیاز سال جدید به‌صورت Snapshot نسخه‌دار با StableId به دیتابیس مقصد منتقل می‌شود؛ اسناد مالی در دیتابیس همان دوره‌اند.
- OriginFiscalPeriodId در همه پرونده‌های دامنه‌ای اجباری و immutable است و با FiscalPeriodId عملیاتی سند تفاوت دارد. این فیلد Trade را period-scoped نمی‌کند و TradeOperationalPeriod ایجاد نمی‌شود.
- فهرست ادامه پرونده‌ها از جدول مشارکت تخصصی زیرسیستم‌ها ساخته می‌شود؛ فقط پرونده‌های صریحاً Open به‌صورت پیش‌فرض Included هستند. مدیر مجاز همان Company می‌تواند Excluded/Included کند و Audit الزامی است.
- شماره‌گذاری پرونده‌ها و اسناد تجاری Company/نوع سند قابل تنظیم و پیش‌فرض CompanyContinuous است؛ شمارنده پیوسته در Company-level owner قرار دارد.
- افتتاحیه موقت و قابل بازسازی است؛ بازسازی اسناد سال جدید را بی‌قیدوشرط بازنویسی نمی‌کند.
- این ثبت مستنداتی است: هیچ Schema، Entity، Migration، Backfill یا Runtime تغییر نکرده است. Tenant + FiscalPeriod DB همچنان Pending است؛ S3-0 و Security Review برای N-5 باز می‌مانند.
