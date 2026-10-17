# تحویل وظیفه — ISSUE-UNNUMBERED-pending-model-changes

- **Issue:** `UNNUMBERED`
- **Agent/مسئول:** Arena.ai Agent Mode
- **تاریخ:** 2026-10-17
- **وضعیت:** انجام‌شده (با محدودیت محیطی در Build/Test)

## هدف و معیارهای پذیرش
- [x] علت ریشه‌ای `PendingModelChangesWarning` در `/DatabaseSetup` پیدا و اصلاح شود.
- [x] مدل فعلی `DatabaseContext` و `DatabaseContextModelSnapshot` هماهنگ باشند.
- [x] Migration جدید برای اختلاف واقعی Schema ساخته شود؛ `InitialCreate` بدون تغییر بماند.
- [x] `PendingModelChangesWarning` غیرفعال یا با `ConfigureWarnings` پنهان نشود.
- [x] ابزار رسمی EF Core در CI اضافه شود (`dotnet ef migrations has-pending-model-changes`).
- [ ] اجرای `dotnet build` / `dotnet test` — در این محیط ممکن نبود (پایین‌تر توضیح داده شده).

## علت ریشه‌ای (اثبات‌شده)

خطا از اختلاف مدل نیست، بلکه از **Snapshot نادرست** است. فایل مسئول:
`Infrastructure/Persistence/Migrations/DatabaseContextModelSnapshot.cs`، که توسط
`scripts/ef_model.py` + `scripts/generate_migrations.py` تولید شده بود، نه با
`dotnet ef migrations add`.

`Migrator.MigrateAsync` پیش از اعمال Migration، مدل Snapshot را با مدل زمان طراحی
مقایسه می‌کند (`Migrator.HasPendingModelChanges` →
`MigrationsModelDiffer.HasDifferences(snapshot.Model.GetRelationalModel(), designTimeModel.Model.GetRelationalModel())`)
و در صورت اختلاف `PendingModelChangesWarning` می‌اندازد. دو انحراف ابزار پایتون از
EF Core 10.0.13 این اختلاف را ساخته بود:

1. **`scripts/ef_model.py:540`** — فراخوانی `.IsRequired()` بدون آرگومان نادیده
   گرفته می‌شد (regex فقط `IsRequired(true)`/`IsRequired(required: true)` را می‌گرفت).
   در نتیجه `PostCategory.Title` که در `PostCategoryConfiguration` با
   `.IsRequired()` اجباری شده، در Snapshot «nullable» نوشته شد.
   `MigrationsModelDiffer.ColumnStructureEquals` فیلد `IsNullable` را مقایسه می‌کند،
   پس یک `AlterColumnOperation` تولید می‌شود.
2. **همان اثر روی فیلتر ایندکس** — `SqlServerIndexConvention` فیلتر ایندکس
   یکتای non-clustered را هنگام تغییر nullability ستون‌ها دوباره محاسبه می‌کند.
   چون `Title` اشتباهاً nullable بود، فیلتر
   `IX_PostCategory_ParentId_Title` به `[ParentId] IS NOT NULL AND [Title] IS NOT NULL`
   نوشته شد. `IndexStructureEquals` فیلد `Filter` را مقایسه می‌کند و ایندکسی که
   فیلترش فرق دارد اصلاً match نمی‌شود، یعنی `DropIndex` + `CreateIndex`.

دو انحراف دیگر هم اصلاح شد که **باعث این خطا نمی‌شوند** ولی Snapshot را از خروجی
واقعی EF Core متفاوت می‌کردند:

3. **`scripts/generate_migrations.py:351`** — فقط `.IsUnicode(false)` نوشته می‌شد،
   در حالی که `CSharpSnapshotGenerator.GenerateFluentApiForIsUnicode` هر مقدار
   Annotation را می‌نویسد (`.IsUnicode(true)` هم). (`IsUnicode` در مرحلهٔ match کردن
   ستون‌ها مقایسه می‌شود ولی در شرط تولید `AlterColumnOperation` نیست، چون تغییر
   واقعی آن همیشه `varchar`↔`nvarchar` و در نتیجه StoreType را عوض می‌کند.)
4. **`.ValueGeneratedOnAdd()` برای همهٔ کلیدهای `Guid`** — `Seedwork.Entity.Id`
   دارای `[DatabaseGenerated(DatabaseGeneratedOption.None)]` است و
   `DatabaseGeneratedAttributeConvention` آن را با `ConfigurationSource.DataAnnotation`
   اعمال می‌کند، پس `ValueGenerationConvention` (با `ConfigurationSource.Convention`)
   نمی‌تواند بازنویسی‌اش کند؛ مقدار واقعی `ValueGenerated.Never` است.
   (`ColumnStructureEquals` فیلد `ValueGenerated` را مقایسه نمی‌کند، پس این مورد
   فقط دقت Snapshot را بهتر کرد.)

هر چهار مورد با خواندن مستقیم سورس `dotnet/efcore` شاخهٔ `release/10.0` تأیید شد
(`MigrationsModelDiffer.cs`، `SqlServerIndexConvention.cs`،
`CSharpSnapshotGenerator.cs`، `CSharpMigrationOperationGenerator.cs`،
`DatabaseGeneratedAttributeConvention.cs`، `Internal/Property.cs`).

## خلاصه تغییرات
- رفع دو باگ ریشه‌ای در `scripts/ef_model.py` (پارس `.IsRequired()` و
  `[DatabaseGenerated(...)]`؛ هر دو قبلاً بی‌صدا نادیده گرفته می‌شدند).
- رفع دو باگ دقت Snapshot در `scripts/generate_migrations.py`
  (`.IsUnicode(true)` و `ValueGenerated`).
- افزودن ماژول مشترک `scripts/migration_model.py` برای خواندن فایل‌های تولیدشده و
  محاسبهٔ اختلاف دو مدل؛ هر اختلافی که پشتیبانی نشود با خطا متوقف می‌شود.
- ساخت Migration جدید `20261017090000_AlignSchemaWithModel` (فایل Migration +
  Designer) و بازتولید `DatabaseContextModelSnapshot.cs`.
- بازنویسی `scripts/verify_migration_model.py`: حالا Snapshot با مدل کد مقایسه می‌شود
  (معادل `has-pending-model-changes`)، Migration منتشرشده با Designer خودش، و
  Migration جدید با اختلاف دقیق آن دو.
- افزودن آزمون بازگشت `TheModelSnapshotDescribesTheCurrentModel` با
  `Database.HasPendingModelChanges()` — همان بررسی رسمی EF Core، بدون نیاز به دیتابیس.
- افزودن بررسی‌های Migration به CI (`.github/workflows/dotnet.yml`)، از جمله
  `dotnet ef migrations has-pending-model-changes`.
- به‌روزرسانی `docs/database-migrations.md` (حذف ادعای نادرست «نتیجه با
  `dotnet ef migrations add` یکی است» و مستندسازی انحراف‌ها و Migration جدید).

## فایل‌های مهم تغییرکرده
- `scripts/ef_model.py` — علت ریشه‌ای: پارس درست `.IsRequired()` و `[DatabaseGenerated]`.
- `scripts/generate_migrations.py` — دقت Snapshot؛ پشتیبانی از چند Migration.
- `scripts/migration_model.py` — جدید؛ پارسر و محاسبه‌گر اختلاف مدل.
- `scripts/verify_migration_model.py` — بازنویسی بررسی‌ها.
- `Infrastructure/Persistence/Migrations/DatabaseContextModelSnapshot.cs` — بازتولید.
- `Infrastructure/Persistence/Migrations/20261017090000_AlignSchemaWithModel.cs` — جدید.
- `Infrastructure/Persistence/Migrations/20261017090000_AlignSchemaWithModel.Designer.cs` — جدید.
- `tests/Template.Tests/DatabaseModelTests.cs` — آزمون بازگشت.
- `.github/workflows/dotnet.yml` — گاردهای CI.
- `docs/database-migrations.md` — مستندات.

## تصمیم‌های فنی
- `InitialCreate` منتشرشده و immutable باقی ماند؛ اختلاف با یک Migration اختصاصی
  اعمال شد (طبق قاعدهٔ بخش ۵ همان سند).
- ترتیب عملیات در `Up`: ابتدا `DropIndex`، سپس `AlterColumn`، سپس `CreateIndex`.
  `MigrationsModelDiffer` عملیات ستون را قبل از ایندکس تولید می‌کند، ولی SQL Server
  روی ستونی که عضو ایندکس است `ALTER COLUMN` را رد می‌کند (خطای ۵۰۷۴)، پس ترتیب
  دستی اصلاح شد.
- پیش از `AlterColumn` یک جملهٔ `UPDATE [PostCategory] SET [Title] = N'' WHERE [Title] IS NULL`
  اجرا می‌شود تا روی دیتابیس موجود با رکورد NULL شکست نخورد. `PostCategory` کد
  قدیمی قالب است و برنامه به آن نمی‌نویسد.
- `PendingModelChangesWarning` دست‌نخورده باقی ماند؛ نه `ConfigureWarnings` اضافه شد
  و نه `EnsureDeleted`/`EnsureCreated`.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح/کد خروج |
|---|---|---|
| `python3 scripts/ef_model.py --repo-root .` | موفق | exit 0 |
| `python3 scripts/generate_migrations.py --repo-root . --check` | موفق | exit 0 |
| `python3 scripts/verify_migration_model.py --repo-root .` | موفق | exit 0؛ شامل اعتبارسنجی syntax با tree-sitter |
| `python3 scripts/check_static_assets.py` | موفق | exit 0 |
| آزمون منفی (برگشت Snapshot به نسخهٔ قبلی) | موفق | exit 1 با ۲۷ خطا، از جمله `nullable` و `filter` |
| `dotnet build Template.sln` | **اجرا نشده** | .NET SDK در sandbox نیست |
| `dotnet test Template.sln` | **اجرا نشده** | .NET SDK در sandbox نیست |
| `dotnet ef migrations has-pending-model-changes` | **اجرا نشده** | .NET SDK و NuGet در دسترس نیست |
| اجرای Migration روی دیتابیس خالی | **اجرا نشده** | SQL Server در sandbox نیست |

دلیل: در این محیط فقط `github.com`, `codeload.github.com`, `api.github.com`,
`registry.npmjs.org`, `pypi.org` و `files.pythonhosted.org` در دسترس‌اند. تلاش برای
`https://builds.dotnet.microsoft.com`, `https://dot.net/v1/dotnet-install.sh` و
`https://api.nuget.org/v3/index.json` با `curl: (35) OpenSSL SSL_connect:
SSL_ERROR_SYSCALL` رد شد، بنابراین نه SDK قابل نصب است و نه `dotnet restore`.
به‌جای آن، رفتار EF Core با خواندن مستقیم سورس `dotnet/efcore@release/10.0` تأیید شد
و بررسی‌های پایتون اجرا شدند.

## امنیت، داده و سازگاری
- معماری، DI، ذخیرهٔ Connection String و توکن راه‌اندازی دست‌نخورده ماند.
- هیچ Connection String یا رمز عبوری در لاگ نوشته نشد و در این تغییر هم اضافه نشد.
- هیچ دیتابیس واقعی حذف/بازسازی نشد؛ `EnsureDeleted`/`EnsureCreated` استفاده نشد.
- تنها تغییر داده‌ای، مقداردهی `Title`های NULL به رشتهٔ خالی در همان Migration است.

## ریسک‌ها و کارهای باقی‌مانده
- Build/Test رسمی روی CI باید اجرا شود؛ اگر تحلیل سورس EF Core در مورد
  `.IsUnicode(true)` یا `ValueGenerated` خطایی داشته باشد، فقط محتوای Snapshot
  متفاوت می‌شود و نه Schema (هیچ‌کدام در `AlterColumnOperation` اثر ندارند).
- اگر `dotnet ef migrations add` روی همین مدل Migration خالی تولید نکند، یعنی
  اختلاف دیگری باقی است؛ `dotnet ef migrations has-pending-model-changes` در CI
  همین را کنترل می‌کند.
- `PostCategory` همچنان کد قدیمی قالب است و `DbSet` ندارد؛ حذف آن از مدل تصمیم
  جداگانه‌ای است و در این تغییر وارد نشد.

## شناسه‌های Git
- Branch: `arena/e5e905ae-accounting`
- Commit: (پس از commit تکمیل می‌شود)
- Pull Request: (پس از ساخت PR تکمیل می‌شود)
