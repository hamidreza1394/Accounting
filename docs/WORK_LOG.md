# گزارش کار و تحویل Agentها

> گزارش‌ها را به انتهای فایل اضافه کن؛ سابقه قبلی را حذف یا بازنویسی نکن.

## 2026-10-17 — اصلاح ریشه‌ای DI برای DatabaseSchemaMigrator
- **دامنه:** بررسی خطای `Unable to resolve service for type 'Data.DatabaseContext' while attempting to activate 'Data.DatabaseSchemaMigrator'`.
- **علت اثبات‌شده:** `DatabaseContext` فقط در `Server/Program.cs` و فقط در شاخه `!setupRequired` با `AddDbContext` ثبت می‌شد، اما `IDatabaseSchemaMigrator` بدون شرط و در همه حالت‌ها ثبت می‌شد. در first-run که Connection String وجود ندارد، این دو registration هم‌مسیر نبودند و ساخت Migrator به وابستگی Scoped ثبت‌نشده می‌رسید.
- **اصلاح:** ثبت `Data.IDatabaseSchemaMigrator` و `Data.DatabaseSchemaMigrator` به همان شاخه `!setupRequired` منتقل شد؛ ثبت تکراری ایجاد نشد و مسیر Migration/`--migrate` پس از پیکربندی اتصال معتبر حفظ شد.
- **تست بازگشت:** `tests/Template.Tests/DatabaseDependencyInjectionTests.cs` با `ValidateOnBuild` و `ValidateScopes` اضافه شد تا resolve شدن Migrator با `DatabaseContext` ثبت‌شده بررسی شود.
- **مستندات:** handoff جدید `docs/handoffs/ISSUE-UNNUMBERED-database-migrator-di.md` ثبت شد.
- **اعتبارسنجی:** Build/Test محلی اجرا نشد؛ CI جدید باید روی Branch اصلاحی اجرا شود. هیچ SQL Server واقعی یا دیتابیس مشتری لمس نشد.
- **Git:** Branch `fix/database-migrator-di-registration`; آخرین Commit این تغییر در زمان ثبت، `a81b043fe330fad06a94452d6057ab1dcca85bed` است. PR ساخته نشد.

## 2026-10-17 — رفع ریشه‌ای `PendingModelChangesWarning` در `/DatabaseSetup`
- **دامنه:** شکست `MigrateAsync` در `Server/Infrastructure/DatabaseSetup/DatabaseConnectionSetupService.cs:127` با `Microsoft.EntityFrameworkCore.Migrations.PendingModelChangesWarning`.
- **علت اثبات‌شده:** مدل درست بود؛ `DatabaseContextModelSnapshot.cs` اشتباه بود. این Snapshot با `scripts/ef_model.py` + `scripts/generate_migrations.py` تولید شده بود، نه با `dotnet ef migrations add`. آن ابزار `.IsRequired()` بدون آرگومان را نادیده می‌گرفت، پس `PostCategory.Title` در Snapshot nullable نوشته شد در حالی که `PostCategoryConfiguration` آن را `.IsRequired()` کرده است؛ به‌همین دلیل `SqlServerIndexConvention` هم فیلتر `IX_PostCategory_ParentId_Title` را با `[Title] IS NOT NULL` اضافه نگه داشت. `MigrationsModelDiffer` فیلدهای `IsNullable` (ستون) و `Filter` (ایندکس) را مقایسه می‌کند، پس اختلاف واقعی و خطا قطعی بود.
- **اصلاح:** رفع پارس `.IsRequired()` و `[DatabaseGenerated(DatabaseGeneratedOption.None)]` در `scripts/ef_model.py`؛ اصلاح دقت Snapshot در `scripts/generate_migrations.py` (`.IsUnicode(true)` و `ValueGenerated`)؛ افزودن ماژول `scripts/migration_model.py`؛ بازتولید Snapshot؛ و Migration جدید `20261017090000_AlignSchemaWithModel` (DropIndex → UPDATE → AlterColumn → CreateIndex) بدون دست‌زدن به `InitialCreate`.
- **پنهان‌سازی انجام نشد:** `PendingModelChangesWarning` غیرفعال نشد، `ConfigureWarnings` اضافه نشد، `EnsureDeleted`/`EnsureCreated` استفاده نشد.
- **تست بازگشت:** `TheModelSnapshotDescribesTheCurrentModel` با `Database.HasPendingModelChanges()` در `tests/Template.Tests/DatabaseModelTests.cs`؛ به‌همراه گاردهای جدید در `.github/workflows/dotnet.yml` از جمله `dotnet ef migrations has-pending-model-changes`.
- **مستندات:** `docs/handoffs/ISSUE-UNNUMBERED-pending-model-changes.md` و بازنویسی بخش ابزار/بررسی‌های `docs/database-migrations.md`.
- **اعتبارسنجی:** `ef_model.py`، `generate_migrations.py --check`، `verify_migration_model.py` (با tree-sitter) و `check_static_assets.py` همه exit 0؛ آزمون منفی با Snapshot قبلی exit 1 با ۲۷ خطا. `dotnet build`/`dotnet test`/`dotnet ef` **اجرا نشد**: در sandbox فقط `github.com`, `codeload.github.com`, `api.github.com`, `registry.npmjs.org`, `pypi.org`, `files.pythonhosted.org` باز هستند و `builds.dotnet.microsoft.com`/`dot.net`/`api.nuget.org` با `curl: (35) SSL_ERROR_SYSCALL` رد می‌شوند. رفتار EF Core به‌جای اجرا، با خواندن سورس `dotnet/efcore@release/10.0` تأیید شد.
- **Git:** Branch `arena/e5e905ae-accounting`.

## 2026-10-17 — اصلاح گزارش قبلی: علت اصلی `PendingModelChangesWarning` ایندکس‌های ضمنی FK بود
- **دامنه:** همان خطای `PendingModelChangesWarning`؛ گزارش قبلی این بخش ناتمام بود.
- **چه چیزی در گزارش قبلی اشتباه بود:** نوشته شده بود علت، nullable بودن `PostCategory.Title` و فیلتر ایندکس است و بررسی‌های پایتون (`generate_migrations.py --check` و `verify_migration_model.py`) کافی‌اند. بعد از اجرا روی CI، آزمون `TheModelSnapshotDescribesTheCurrentModel` همچنان شکست (`total: 16`, `failed: 1`). آن دو انحراف واقعی بودند ولی **کل علت نبودند**؛ بررسی‌های پایتون هم چون از همان مدل ناقص پایتون استفاده می‌کردند، این اختلاف را نمی‌دیدند.
- **نحوهٔ اندازه‌گیری واقعی:** یک آزمون تشخیصی موقت، `IMigrationsModelDiffer.GetDifferences(...)` را صدا زد و نتیجه را در `/tmp/pending-model-changes.txt` نوشت؛ یک گام موقت در workflow آن را به‌صورت annotation منتشر کرد (لاگ کامل گام‌ها از API قابل دانلود نیست).
- **نتیجهٔ اندازه‌گیری:** دقیقاً **۸ `CreateIndexOperation`** و هیچ اختلاف دیگری: `IX_CompanyRolePermissions_PermissionId`، `IX_LoginLogs_UserId`، `IX_MenuItems_ParentId`، `IX_Pages_CreatorUserId`، `IX_Pages_PageCategoryId`، `IX_Permissions_ApplicationHandlerId`، `IX_UserCompanyMemberships_RoleId_TenantId`، `IX_Users_RoleId`.
- **علت ریشه‌ای:** `scripts/ef_model.py` فقط ایندکس‌های صریح `.HasIndex(...)` را جمع می‌کرد و `ForeignKeyIndexConvention` را پیاده‌سازی نکرده بود. این Convention برای هر foreign key یک ایندکس می‌سازد مگر ستون‌هایش از قبل ستون‌های ابتدایی یک Key یا ایندکس دیگر باشند (`AreIndexedBy`). چون `InitialCreate` هم از همان مدل تولید شده بود، این ایندکس‌ها در دیتابیس هم نبودند و Snapshot با Migration سازگار بود — اختلاف فقط با مدل واقعی ظاهر می‌شد.
- **اصلاح:** افزودن `are_indexed_by()` و حلقهٔ تولید ایندکس‌های ضمنی به `scripts/ef_model.py` (ترجمهٔ مستقیم `ForeignKeyIndexConvention.CreateIndex`/`AreIndexedBy`)؛ بازتولید `DatabaseContextModelSnapshot.cs`، `20261017090000_AlignSchemaWithModel.cs` و `.Designer.cs`. تعداد ایندکس‌ها از ۲۴ به ۳۲ رسید و دقیقاً با ۸ مورد گزارش‌شدهٔ EF Core یکی شد.
- **حذف موقت‌ها:** آزمون تشخیصی `PendingModelChangesDiagnostic.cs` و گام موقت workflow حذف شدند؛ گام دائمی `Publish the test output` نگه داشته شد.
- **اعتبارسنجی:** `ef_model.py` exit 0 (۳۲ ایندکس، ۸ ضمنی، دقیقاً منطبق بر خروجی `MigrationsModelDiffer`)؛ `generate_migrations.py --check` exit 0؛ `verify_migration_model.py` exit 0؛ `check_static_assets.py` exit 0. آزمون منفی: با حذف `b.HasIndex("RoleId")` از Snapshot، `verify_migration_model.py` با exit 1 و ۲ خطا شکست. Build/Test/`has-pending-model-changes` روی GitHub Actions اجرا شدند.
- **Git:** Branch `arena/e5e905ae-accounting`؛ PR https://github.com/hamidreza1394/Accounting/pull/25.
