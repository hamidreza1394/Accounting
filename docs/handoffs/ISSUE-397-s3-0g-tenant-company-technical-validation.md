# اعتبارسنجی فنی S3-0g — طرح مهاجرت Tenant/Company (D-087)

- **Issue:** #397
- **تاریخ:** 2026-11-14
- **سند مورد بررسی:** `docs/handoffs/ISSUE-397-s3-0f-tenant-company-migration-design.md`
- **نوع کار:** اعتبارسنجی فنی با آزمایش‌های ایزوله. هیچ Migration عملیاتی، Schema، داده، Backfill، Commit، Push، PR یا Merge انجام نشد.
- **وضعیت تصمیم‌ها:** همهٔ پیشنهادهای این سند `Proposed` هستند؛ هیچ تصمیمی `Accepted` نشده است.

## 0. محیط، ابزار و وضعیت Git

| مورد | وضعیت |
|---|---|
| .NET SDK | `10.0.100` (مطابق `global.json`)، نصب موقت خارج از مخزن در `/opt/dotnet` |
| `dotnet-ef` | `10.0.13` (همان نسخهٔ CI در `.github/workflows/dotnet.yml`) |
| Python / tree-sitter | Python 3.13؛ `tree-sitter` و `tree-sitter-c-sharp` با pip نصب شد (مثل CI) |
| SQL Server / Docker | **در دسترس نبود** (`docker` daemon وجود ندارد). هر ادعایی که به اجرای واقعی روی SQL Server نیاز دارد **Not Run** است. |
| دیتابیس واقعی پروژه | هیچ اتصالی ساخته نشد. `dotnet ef migrations add/script` هیچ اتصالی باز نمی‌کند. |
| محل آزمایش‌ها | فقط `/tmp/s30g/` (کپی ایزوله مخزن و یک پروژهٔ کوچک EF). هیچ فایلی از مخزن اصلی تغییر نکرد. |
| `git status --short` مخزن اصلی | فقط سه فایل untracked: گزارش S3-0e، سند S3-0f و همین سند. `git stash list` خالی است. |
| پایهٔ کد | clone سطحی `main@d81bb29`؛ `origin/main@8f103aa` (فقط فایل‌های e2e متفاوت‌اند) |
| کامپیوتر کاربر | به bridge وصل نبود؛ تغییرات commit‌نشدهٔ کاربر بررسی نشد |

نکتهٔ جانبی: `Server/.config/dotnet-tools.json` هنوز `dotnet-ef 6.0.3` را پین کرده است، در حالی که CI و پروژه EF Core `10.0.x` دارند و CI ابزار را سراسری با نسخهٔ `10.0.13` نصب می‌کند. اگر کسی `dotnet tool restore` بزند، ابزار نامتناسب می‌گیرد (ریسک کم؛ گزارش می‌شود، تغییر داده نشد).

## 1. خلاصهٔ اجرایی و جدول فرض‌ها

### 1.1 فرض‌های اثبات‌شده (با خروجی اجرایی)

| # | فرض | شاهد |
|---|---|---|
| P1 | با تغییر نام property، EF Core 10 به‌صورت خودکار `RenameColumn` تولید می‌کند | §2.2 (R1، R2، مدل کوچک A/B) |
| P2 | با **نام‌گذاری صریح** قیدها (`HasConstraintName`/`HasName`/`HasDatabaseName`)، Migration فقط `RenameColumn` است و FK/AK/Index دست نمی‌خورد (روی مخزن واقعی و روی Entity واقعی `FiscalPeriod`) | §2.2: R2 |
| P3 | `Down` تولیدشده معکوس دقیق است (`RenameColumn` برعکس) | §2.2، §2.4 |
| P4 | تغییر نام property با `HasColumnName("TenantId")` هیچ تغییری در دیتابیس ایجاد نمی‌کند (`Up`/`Down` خالی) | §2.2: R3 |
| P5 | Migration host فعلی (`--migrate`) فقط `DatabaseContext` (Core) را Migrate می‌کند؛ Trade اجرا نمی‌شود | §4 |
| P6 | Guardهای فعلی هر Rename را fail-closed رد می‌کنند | §3.2 |

### 1.2 فرض‌های ردشده یا نیازمند اصلاح

| # | فرض S3-0f | نتیجه |
|---|---|---|
| X1 | «Phase A: `Companies.TenantId` nullable + کلید جایگزین `(Id, TenantId)`» (S3-0f §6/§5) | **رد شد.** وقتی روی `Companies` کلید جایگزین `(Id, TenantId)` تعریف شود، EF ستون را `NOT NULL DEFAULT 0` می‌سازد و همزمان FK به `Tenants` اضافه می‌کند؛ روی داده موجود با مقدار پیش‌فرض ناسازگار است (§2.3). |
| X2 | «نام پیش‌فرض قیدها را بپذیریم؛ Rename همهٔ قیدها را همراه می‌کند» (ضمنی در S3-0f §4.5) | **رد شد.** بدون Pin، EF برای FK و AK عملیات `DropForeignKey`/`DropUniqueConstraint` + `Add*` تولید می‌کند (§2.2: R1) و FKهای وابسته (مثلاً از `ProvisioningRequestClaims` و `ConsolidationRunCompanies` به AK جدول `FiscalPeriods`) را در نظر نمی‌گیرد. |
| X3 | «Trade FKها به `Companies.Id` اشاره دارند» (S3-0f §2) | **رد شد برای Trade.** Migration `20261113072823_InitialTradeMasterData` هیچ FK به `Companies` ندارد؛ فقط FK بین جدول‌های خود Trade (`principalTable: "Parties"`) دارد. `TenantId` در Trade یک Guid بدون قید ارجاعی است (§5، ایراد S-4). |
| X4 | «Guardها فقط نیاز به پذیرش Rename دارند» | **ناقص.** گاردها اساساً `HasConstraintName`/`HasDatabaseName`/`HasName`/`HasColumnName` را نمی‌شناسند (صفر مورد در چهار اسکریپت) و نام FK را با فرمول پیش‌فرض می‌سازند (§3.3). |
| X5 | «NOT NULL با `AlterColumn` خودکار EF امن است» (ضمنی) | **رد شد.** EF برای `AlterColumn` غیر-null، `UPDATE ... SET col = 0 WHERE col IS NULL` و `ADD DEFAULT 0` تولید می‌کند و FK/ایندکس وابسته را نمی‌شکند (§2.3). Migration دستی با پیش‌شرط `THROW` اجباری است. |
| X6 | «ترکیب Rename با تغییر دیگر در همان Migration مشکلی ندارد» | **خطرناک.** EF دو ستون را اشتباه جفت کرد (`UserId`→`CompanyId` و `DropColumn TenantId`) وقتی Rename و تغییر نوع هم‌زمان بود (§2.4). |

### 1.3 اثبات‌نشده (Not Run / نیازمند SQL Server)

| # | مورد |
|---|---|
| U1 | رفتار واقعی `sp_rename` روی SQL Server (انتقال خودکار FK/AK/Index) — فقط SQL تولیدی دیده شد، اجرا نشد |
| U2 | شکست `DropUniqueConstraint` در R1 هنگام وجود FK وابسته روی SQL Server (پیش‌بینی بر پایهٔ قواعد SQL Server، اجرا نشد) |
| U3 | رفتار FK مرکب با ستون nullable (`(CompanyId, TenantId)` با `TenantId = NULL`) روی SQL Server — طبق معنای MATCH SIMPLE انتظار می‌رود بررسی نشود؛ اجرا نشد |
| U4 | رفت‌وبرگشت `Up`/`Down` Migration روی SQL Server واقعی |
| U5 | قفل همزمانی Migration (چند نمونهٔ برنامه روی یک دیتابیس مشترک) — طبق مستندات EF قفل دارد؛ اجرا نشد |
| U6 | تأثیر عملکردی (۱۰۰ کاربر هم‌زمان) |
| U7 | رفتار `AlterColumn` (NOT NULL) با نوع `Guid` (آزمایش با `int` انجام شد؛ همان مسیر تولید کد است ولی روی `Guid` اجرا نشد) |

### 1.4 نتیجهٔ کلی

- **Rename-then-Add همچنان بهترین مسیر است، اما فقط با سه شرط:** (۱) نام همهٔ قیدهای legacy Pin شود؛ (۲) Rename هرگز با تغییر دیگر در یک Migration ترکیب نشود؛ (۳) Guard پیش از اولین Migration تغییر نام گسترش یابد. گزینهٔ سادهٔ جدید «فقط تغییر نام property با `HasColumnName`» (P4) می‌تواند Rename فیزیکی را به تعویق بیندازد.
- **ترتیب PRهای S3-0f باید اصلاح شود** (§6): PR-2 پیش‌نیاز اجباری PRهای تغییر نام است، Phase A بدون کلید جایگزین ساخته می‌شود و کلید جایگزین/NOT NULL پس از Backfill می‌آید.
- **شروع PR-0 تا PR-3 منطقی است** (با اصلاح محتوای PR-2 و PR-4؛ §6.3).
- **ورود به Schema Migration هنوز مجاز نیست** تا پیش‌شرط‌های §7 برآورده شود.

## 2. آزمایش رفتار EF Core 10

**روش:** دو آزمایش. (الف) پروژهٔ کوچک ایزوله با مدل مشابه (`CompanyRole`، `Membership` با FK مرکب، `FiscalPeriod`، `Claim`) و Migration واقعی تولیدشده با `dotnet ef migrations add`؛ (ب) کپی ایزوله از مخزن واقعی با تغییر نام `FiscalPeriod.TenantId` به `CompanyId` و Migration واقعی تولیدشده با `DesignTimeDatabaseContextFactory` واقعی پروژه. `dotnet ef migrations script` فقط متن SQL تولید می‌کند و اتصال نمی‌گیرد.

### 2.1 سناریوها

| سناریو | تغییر در مخزن واقعی (کپی) | توضیح |
|---|---|---|
| R1 | Rename property `FiscalPeriod.TenantId`→`CompanyId` بدون Pin | رفتار پیش‌فرض EF |
| R2 | R1 + Pin نام‌های `IX_FiscalPeriods_TenantId_*`، `AK_FiscalPeriods_Id_TenantId`، `FK_FiscalPeriods_Companies_TenantId` | پیشنهاد S3-0f با Pin |
| R3 | Rename property + `HasColumnName("TenantId")` | تغییر فقط در C# |

### 2.2 نتایج روی مخزن واقعی (EF Core `10.0.14`، ابزار `10.0.13`)

**R1 — بدون Pin (Migration واقعی تولیدشده، `Up`):**

```
DropForeignKey  FK_FiscalPeriods_Companies_TenantId
DropUniqueConstraint AK_FiscalPeriods_Id_TenantId
RenameColumn    TenantId -> CompanyId
RenameIndex     IX_FiscalPeriods_TenantId_Status -> IX_FiscalPeriods_CompanyId_Status
RenameIndex     IX_FiscalPeriods_TenantId_StartDate_EndDate -> IX_FiscalPeriods_CompanyId_StartDate_EndDate
AddUniqueConstraint AK_FiscalPeriods_Id_CompanyId (Id, CompanyId)
AddForeignKey   FK_FiscalPeriods_Companies_CompanyId
```

- EF **Rename ستون را خودکار تشخیص داد** (P1) ولی FK و AK را Drop/Add کرد.
- FKهای `FK_ProvisioningRequestClaims_FiscalPeriods_FiscalPeriodId_CompanyId` و `FK_ConsolidationRunCompanies_FiscalPeriods_FiscalPeriodId_CompanyId` که به همان AK ارجاع می‌دهند، در Migration نیامدند؛ چون نامشان تغییر نمی‌کند. روی SQL Server حذف یک Unique Constraint که FK به آن ارجاع می‌دهد خطا می‌دهد (U2؛ **پیش‌بینی، اجرا نشد**). نتیجه: R1 احتمالاً روی دیتابیس واقعی شکست می‌خورد.
- `Down` تولیدشده معکوس همان فهرست است.

**R2 — با Pin (Migration واقعی تولیدشده، `Up` و `Down` کامل):**

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.RenameColumn(name: "TenantId", table: "FiscalPeriods", newName: "CompanyId");
}
protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.RenameColumn(name: "CompanyId", table: "FiscalPeriods", newName: "TenantId");
}
```

- فقط یک `RenameColumn`؛ FK، AK و ایندکس‌ها دست‌نخورده (P2، P3). تفاوت Designer نسبت به Migration قبلی فقط نام property و Pinها است.
- زیرنکتهٔ مهم: در Designer پس از Pin، `HasAlternateKey("Id","CompanyId").HasName(...)` **به‌صورت صریح** ظاهر می‌شود، در حالی که در snapshot قبلی (بدون Pin) این کلید فقط ضمنی از `HasPrincipalKey` بود. Guard باید این تغییر *نمایش* را تغییر *مدل* حساب نکند (§3.3).

**R3 — فقط تغییر property + `HasColumnName("TenantId")`:**

```csharp
protected override void Up(MigrationBuilder migrationBuilder) { }
protected override void Down(MigrationBuilder migrationBuilder) { }
```

- هیچ تغییری در دیتابیس (P4). Designer فقط `HasColumnName("TenantId")` و نام property جدید را دارد. این روش **ایمنی کامپایل** (برای جلوگیری از اشتباه Tenant/Company) را بدون هیچ تغییر دیتابیس و بدون ناسازگاری با نسخهٔ قدیمی برنامه می‌دهد.

### 2.3 نتایج پروژهٔ کوچک (SQL تولیدی، اجرا نشده)

**Rename با Pin (`B_pinned`)، SQL تولیدی:**

```sql
BEGIN TRANSACTION;
EXEC sp_rename N'[Memberships].[TenantId]', N'CompanyId', 'COLUMN';
EXEC sp_rename N'[Memberships].[IX_Memberships_TenantId]', N'IX_Memberships_CompanyId', 'INDEX';
EXEC sp_rename N'[Memberships].[IX_Memberships_RoleId_TenantId]', N'IX_Memberships_RoleId_CompanyId', 'INDEX';
EXEC sp_rename N'[FiscalPeriods].[TenantId]', N'CompanyId', 'COLUMN';
EXEC sp_rename N'[FiscalPeriods].[IX_FiscalPeriods_TenantId]', N'IX_FiscalPeriods_CompanyId', 'INDEX';
EXEC sp_rename N'[CompanyRoles].[TenantId]', N'CompanyId', 'COLUMN';
...
COMMIT;
```

- Down: همین عبارت‌ها برعکس. فقط metadata؛ بدون Drop/Add.
- ایندکس تک‌ستونی FK (`IX_Memberships_TenantId`) را نمی‌توان با API رسمی Pin کرد؛ EF آن را با `RenameIndex` تغییر نام می‌دهد (بی‌خطر، فقط metadata).

**Phase A با کلید جایگزین (`S1_phaseA`، SQL تولیدی) — اثبات X1:**

```sql
ALTER TABLE [Companies] ADD [TenantId] int NOT NULL DEFAULT 0;
ALTER TABLE [Companies] ADD CONSTRAINT [AK_Companies_Id_TenantId] UNIQUE ([Id], [TenantId]);
CREATE TABLE [Tenant] (...);
CREATE INDEX [IX_Companies_TenantId] ON [Companies] ([TenantId]);
ALTER TABLE [Companies] ADD CONSTRAINT [FK_Companies_Tenant_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenant] ([Id]) ...
```

- ستون در C# nullable تعریف شده بود، اما چون عضو کلید جایگزین است، EF آن را non-null کرد. روی دیتابیس دارای Company، مقدار `0` به FK `Tenants.Id` نمی‌خورد. **نتیجه:** Phase A پیشنهادی S3-0f قابل ساخت نیست و FK مرکب فرزندان به `Companies(Id, TenantId)` باید بعد از NOT NULL شدن `Companies.TenantId` بیاید.

**افزودن `TenantId` واقعی و FK مرکب (`S2_realTenant`):**

```sql
ALTER TABLE [CompanyRoles] ADD [TenantId] int NULL;
CREATE INDEX [IX_CompanyRoles_CompanyId_TenantId] ON [CompanyRoles] ([CompanyId], [TenantId]);
ALTER TABLE [CompanyRoles] ADD CONSTRAINT [FK_CompanyRoles_Companies_CompanyId_TenantId]
  FOREIGN KEY ([CompanyId], [TenantId]) REFERENCES [Companies] ([Id], [TenantId]) ON DELETE NO ACTION;
```

- افزودن ستون nullable + ایندکس + FK مرکب افزایشی است و با نسخهٔ قدیمی برنامه سازگار (ستون nullable؛ U3 پیش‌بینی بررسی‌نشدهٔ FK با `NULL`).

**NOT NULL (`S3_notNull`، SQL تولیدی) — اثبات X5:**

```sql
DROP INDEX [IX_CompanyRoles_CompanyId_TenantId] ON [CompanyRoles];
... (drop default constraint) ...
UPDATE [CompanyRoles] SET [TenantId] = 0 WHERE [TenantId] IS NULL;
ALTER TABLE [CompanyRoles] ALTER COLUMN [TenantId] int NOT NULL;
ALTER TABLE [CompanyRoles] ADD DEFAULT 0 FOR [TenantId];
CREATE INDEX [IX_CompanyRoles_CompanyId_TenantId] ON [CompanyRoles] ([CompanyId], [TenantId]);
```

- EF بی‌صدا ردیف‌های NULL را `0` می‌کند (داده نادرست)، FK مرکب را قبل از `ALTER COLUMN` برنمی‌دارد (SQL Server روی ستون عضو FK خطا می‌دهد؛ U7/پیش‌بینی) و `DEFAULT 0` باقی می‌گذارد. پس Migration NOT NULL باید دستی باشد: پیش‌شرط `IF EXISTS (... WHERE TenantId IS NULL) THROW` و بدون `UPDATE` پیش‌فرض و بدون `DEFAULT`. (در مخزن، `verify_alter_column_defaults` و ورودی بازبینی‌شدهٔ `HAND_WRITTEN_SQL` همین مسیر را کنترل می‌کنند.)

### 2.4 جفت‌سازی اشتباه Rename (`E_retype`) — اثبات X6

وقتی در یک جدول هم‌زمان `Membership.TenantId`→`CompanyId` و `UserId`→`AccountId` با تغییر نوع `int`→`long` انجام شد، EF Migration زیر را ساخت:

```
RenameColumn  UserId -> CompanyId        <-- جفت‌سازی اشتباه
DropColumn    TenantId
AddColumn<long> AccountId
```

یعنی EF ستون‌ها را بر اساس نوع/ترتیب حدس می‌زند. در Migration واقعی، چنین خطایی **از دست رفتن داده** است. قاعدهٔ لازم: در یک Migration فقط Rename (بدون هیچ تغییر دیگر در همان جدول)؛ Guard باید جفت‌های Rename را از فهرست بازبینی‌شده بخواند، نه از حدس EF (§3.4).

### 2.5 نتیجه‌گیری بخش ۲ (پاسخ به پرسش‌ها)

| پرسش | پاسخ |
|---|---|
| تغییر نام Property و Column چه Migration می‌سازد؟ | `RenameColumn` خودکار + `RenameIndex` برای ایندکس‌های با نام پیش‌فرض؛ برای FK و AK بدون Pin: Drop/Add |
| EF خودکار Rename تشخیص می‌دهد؟ | بله، با heuristic (نوع، nullability، موقعیت)؛ در حضور چند تغییر همزمان اشتباه جفت می‌کند (§2.4) |
| چه زمانی Drop+Add ستون می‌دهد؟ | وقتی ستون هم‌زمان نوع/facet عوض کند یا چند ستون هم‌زمان حذف/افزوده شوند (§2.4) |
| اثر روی FK/AK/Index؟ | نام پیش‌فرض قیدها شامل نام ستون است؛ با تغییر نام ستون، نام قید در مدل عوض می‌شود → Drop/Add (FK/AK) یا `RenameIndex` (ایندکس) |
| آیا نام‌گذاری صریح رفتار را تغییر می‌دهد؟ | بله: با Pin فقط `RenameColumn` (R2) |
| `Down` چگونه باشد؟ | معکوس دقیق؛ EF خودش درست تولید می‌کند (R1/R2/B). برای NOT NULL باید دستی نوشته شود |

## 3. ابزارهای Guard

### 3.1 وضعیت پایه (خروجی واقعی، مخزن اصلی بدون تغییر)

```
python3 scripts/generate_migrations.py --check   -> The generated migrations are up to date.  (rc=0)
python3 -I scripts/verify_migration_model.py      -> ... 20 migrations verified, Up and Down.  (rc=0)
python3 -I scripts/tests/test_migration_tooling.py          -> Ran 32 tests ... OK
python3 -I scripts/tests/test_migration_guard_mutations.py  -> Ran 31 tests ... OK
```

نکته: `dotnet ef migrations has-pending-model-changes` فقط روی Core (`Infrastructure.Persistence` با `Server`) اجرا می‌شود (`dotnet.yml:149`) و Trade را شامل نیست.

### 3.2 رفتار Guard فعلی روی Rename (روی کپی ایزوله)

| سناریو | `verify_migration_model.py` | `generate_migrations.py --check` |
|---|---|---|
| R1، R2، R3 | `cannot be verified (fail-closed): A relationship of the source model was removed or changed; the migration has to be written with 'dotnet ef migrations add'.` | `The newest migration is ... but CURRENT_MIGRATION_* names ...; advance the constants` |

یعنی Guard امروز هر Migration تغییر نام را رد می‌کند (P6). رد شدن R2 (که از نظر فنی کاملاً درست است) به دلیل «تغییر رابطه» است، نه «Rename»؛ چون کلید رابطه شامل نام ستون‌هاست.

### 3.3 دقیقاً چه چیزهایی باید تغییر کنند

مشاهدات از کد (`scripts/`):

| فایل | وضعیت فعلی | تغییر لازم برای پذیرش Rename امن |
|---|---|---|
| `ef_model.py` | `HasConstraintName`، `HasDatabaseName`، `HasName`، `HasColumnName` هیچ‌کدام پشتیبانی/تشخیص داده نمی‌شوند (صفر مورد در `ef_model.py`، `migration_model.py`، `generate_migrations.py`، `verify_migration_model.py`)؛ `parse_relationship` فراخوانی‌های ناشناختهٔ زنجیره را نادیده می‌گیرد | (۱) تشخیص و ثبت نام Pinشده برای Index، AK و FK؛ (۲) پشتیبانی `HasColumnName`؛ (۳) هر فراخوانی ناشناختهٔ زنجیره باید `ModelExtractionError` بدهد (fail-closed)، نه نادیده‌گرفته‌شدن بی‌صدا |
| `migration_model.py` | `_column_operations` ستون‌ها را با نام مقایسه می‌کند؛ ستون حذف‌شده → «columns differ»؛ کلید رابطه شامل نام ستون است؛ نام FK با فرمول `FK_{table}_{principal}_{cols}` ساخته می‌شود | (۱) مفهوم «Rename ستون» با فهرست بازبینی‌شده (مثلاً `RENAMED_COLUMNS` مشابه `HAND_WRITTEN_SQL`: `(جدول، قدیم، جدید)`)؛ (۲) مقایسهٔ مدل پس از اعمال Rename؛ (۳) نام قید از Pin (یا فرمول پیش‌فرض) خوانده شود؛ (۴) عملیات `rename_column`/`rename_index` و عملیات معکوس |
| `generate_migrations.py` | snapshot و Designer را از مدل می‌سازد؛ AK ضمنی (از `HasPrincipalKey`) را صریح نمی‌نویسد | رندر snapshot/Designer باید AK صریح با `HasName` و `HasColumnName` را درست تولید کند (مشاهدهٔ R2: AK صریح ظاهر می‌شود) |
| `verify_migration_model.py` | `parse_operations` فقط `AddColumn`/`AlterColumn`/`CreateIndex`/`DropIndex`/`AddForeignKey`/... را می‌شناسد | تشخیص `RenameColumn`/`RenameIndex` با `name`/`newName`؛ تطبیق با فهرست بازبینی‌شده؛ رد هر عملیات اضافی (از جمله `DropColumn`)؛ تطبیق `Down` با معکوس دقیق |
| `scripts/tests/*` | هیچ تست Rename وجود ندارد | Mutation testهای §3.4 به‌علاوهٔ تست «Pin ناشناخته → خطا» |

### 3.4 Mutation Test طراحی‌شده و اجرا شده

**محدودیت صادقانه:** چون Guard فعلی Rename را اصلاً نمی‌پذیرد، «پذیرش Rename معتبر» با کد فعلی مخزن ممکن نیست. برای اعتبارسنجی *قواعد پیشنهادی*، یک **نمونهٔ اولیهٔ مستقل** (Prototype) خارج از مخزن نوشته شد (`rename_guard_prototype.py` و `test_rename_guard_prototype.py` در `/tmp/s30g/`؛ جزو مخزن نیست و ادغام نشده). ورودی تست‌ها Designerها و Migration **واقعی** R1/R2 و Designer واقعی Migration قبلی مخزن است. Prototype قواعد زیر را اجرا می‌کند: اختلاف مدل قدیم↔جدید دقیقاً برابر Renameهای اعلام‌شده باشد؛ نام هیچ FK/AK عوض نشود؛ `Up` دقیقاً مجموعهٔ `RenameColumn`/`RenameIndex` مورد انتظار باشد؛ `Down` معکوس دقیق باشد.

خروجی اجرا (`Ran 10 tests ... OK`):

| تست | تغییر (Mutation) | انتظار | نتیجه |
|---|---|---|---|
| 00 | بدون تغییر (R2 واقعی) | پذیرفته | ✅ OK |
| 01 | Migration بدون Pin (R1: Drop/Add FK/AK) | رد با «needs Drop/Add» و «Up differs» | ✅ OK |
| 02 | `RenameColumn` با `DropColumn`+`AddColumn` جایگزین | رد (`Up differs`) | ✅ OK |
| 03 | افزودن `DropColumn Title` ناخواسته به `Up` | رد | ✅ OK |
| 04 | حذف `RenameColumn` از `Down` | رد (`Down is not the exact reverse`) | ✅ OK |
| 05 | جهت یا نام اشتباه در `Down` | رد | ✅ OK |
| 06 | Rename + تغییر `MaxLength` عنوان در Designer | رد (`more than the declared renames`) | ✅ OK |
| 07 | جفت‌سازی اشتباه اعلام‌شده (`TenantId`→`Title`) | رد | ✅ OK |
| 08 | Rename بدون اعلام در فهرست بازبینی‌شده | رد | ✅ OK |
| 09 | تغییر نام قید AK (drift) | رد (`needs Drop/Add`) | ✅ OK |

این تست‌ها **قواعد** را معتبر می‌کنند، نه پیاده‌سازی نهایی Guard مخزن را؛ هنوز باید در PR-2 با ساختار واقعی `scripts/` و `scripts/tests/test_migration_guard_mutations.py` پیاده شوند (و هر قاعده Mutation test خودش را داشته باشد، طبق `farano-migrations`).

## 4. Migration host مربوط به Trade (بدون هیچ تغییر Schema)

### 4.1 مشاهدات (از کد)

| موضوع | مشاهده | مسیر |
|---|---|---|
| مسیر `--migrate` | فقط `IDatabaseSchemaMigrator` → `DatabaseContext.Database.MigrateAsync()`؛ پس از آن `return` | `Server/Program.cs:355-361`، `Infrastructure/Persistence/DatabaseSchemaMigrator.cs` |
| ثبت Trade | `AddTradePersistence(connectionString)` فقط `TradeDbContext` و Storeها را ثبت می‌کند؛ هیچ Migrate صدا زده نمی‌شود | `Modules/Trade/Trade.Infrastructure/Persistence/TradePersistenceServiceCollectionExtensions.cs` |
| Assembly و History | `MigrationsAssembly(Trade.Infrastructure)` و `__TradeMigrationsHistory` در `dbo`؛ Schema جداول `trade` | `TradeDbContextOptionsConfiguration.cs`، `TradeDbContext.cs:28` |
| وضعیت History | یک Migration: `20261113072823_InitialTradeMasterData` (فقط Parties/PartyRoleAssignments/Products) | `Modules/Trade/Trade.Infrastructure/Persistence/Migrations/` |
| نصب بدون Trade | روی `Farano-Local` جدول `trade.*` و `__TradeMigrationsHistory` وجود ندارد | S3-0 §12 |
| شروع برنامه | هشدار «pending migrations» فقط برای Core (`GetPendingMigrationsAsync`) | `Server/Program.cs:371-386` |
| CI | e2e فقط `Server.dll --migrate` (بدون Trade)؛ `has-pending-model-changes` فقط برای Core؛ Guardهای Python فقط Core | `.github/workflows/dotnet.yml:134,149,287-288` |
| تست‌های Trade | `TradeMasterDataMigrationSqlServerTests` و `TradeProductStoreSqlServerTests` خودشان `TradeDbContext.Database.MigrateAsync()` را صدا می‌زنند | `tests/Template.Tests/Integration/*` |
| ارجاع Trade به Core | هیچ FK از Trade به `Companies` وجود ندارد | Migration Trade |
| اجرای تکراری | `MigrateAsync` در حالت بدون pending بدون اثر است (`DatabaseSchemaMigrator` همین را لاگ می‌کند) | `DatabaseSchemaMigrator.cs` |

نتیجه: هر نصبی که فقط `--migrate` اجرا کند، Trade Schema ندارد؛ هر PR که Schema Trade را تغییر دهد (PR-9) روی نصب‌ها اجرا نمی‌شود و هیچ Guard CI Trade را نمی‌پوشاند.

### 4.2 قرارداد اجرایی پیشنهادی (کم‌هزینه؛ **Proposed**)

1. **یک فرمان، دو Context، ترتیب ثابت:** `--migrate` ابتدا Core را Migrate کند، سپس Trade را. چون Trade به Core FK ندارد، ترتیب از نظر داده اجباری نیست؛ ترتیب ثابت فقط برای تکرارپذیری گزارش است.
2. **تعمیم `IDatabaseSchemaMigrator`:** نتیجه به‌تفکیک Context (`Core`, `Trade`) با فهرست pending/applied؛ بدون ثبت سرویس جدید و بدون Host جدید.
3. **شکست:** اگر Core شکست خورد، Trade اجرا نشود و خروجی `exit code ≠ 0`. اگر Trade شکست خورد، Core که قبلاً کامل شده می‌ماند، گزارش «Core applied، Trade failed» و `exit code ≠ 0`. هر Context تراکنش Migration خودش را دارد؛ هیچ تراکنش مشترک دو-Context ساخته نمی‌شود (پیچیدگی بی‌دلیل).
4. **اجرای تکراری:** `MigrateAsync` هر Context idempotent است؛ اجرای مجدد پس از شکست Trade فقط Trade را ادامه می‌دهد.
5. **نصب بدون جداول Trade:** `__TradeMigrationsHistory` خالی/غایب → `InitialTradeMasterData` اعمال شود. اگر جداول `trade.*` از قبل بدون History وجود داشتند (ساخت دستی)، Migration شکست می‌خورد و باید گزارش شود؛ اقدام خودکار (baseline) انجام نشود.
6. **Dedicated و Shared:** هر دو یک دیتابیس با دو History table؛ بدون تفاوت. در Shared با چند نمونهٔ برنامه، فقط یک نمونه `--migrate` را اجرا کند (رویهٔ عملیاتی؛ قفل همزمانی EF اثبات نشده، U5).
7. **هشدار شروع:** `GetPendingMigrationsAsync` هر دو Context را بررسی و گزارش کند.
8. **تست:** (الف) SQL Server: دیتابیس خالی → هر دو History ساخته شود؛ (ب) اجرای دوم no-op؛ (ج) شبیه‌سازی شکست Trade (مثلاً جدول از پیش موجود) → exit code و وضعیت Core؛ (د) `--migrate` روی دیتابیسی که فقط Core دارد؛ (ه) تست ثبت DI. هیچ‌کدام در این مرحله اجرا نشد (Not Run).

این PR هیچ تغییر Schema ندارد و باید قبل از PR-9 باشد؛ مستقل از همهٔ تصمیم‌های محصولی است.

## 5. بازبینی طراحی S3-0f — فهرست ایرادات

شدت: **بحرانی** = طرح بدون اصلاح اجرا نمی‌شود؛ **بالا** = ریسک جدی؛ **متوسط**؛ **پایین**.

| ID | شدت | ایراد | اثر | پیشنهاد اصلاح | مسیر |
|---|---|---|---|---|---|
| S-1 | بحرانی | Phase A («`Companies.TenantId` nullable + AK») قابل ساخت نیست (X1) | Migration اعمال نمی‌شود یا `TenantId=0` می‌سازد | A1: `Tenants` + `Companies.TenantId` nullable + FK + Index **بدون AK**. A2 (پس از Backfill D1): `NOT NULL` دستی با `THROW` + افزودن AK `(Id, TenantId)`. FK مرکب فرزندان (C2) فقط پس از A2 | S3-0f §6/§5/§13 PR-4 |
| S-2 | بحرانی | Rename بدون Pin، Drop/Add FK/AK می‌دهد و FKهای وابسته را نمی‌شکند (X2) | Migration روی SQL Server احتمالاً شکست می‌خورد (U2) | Pin اجباری نام تمام قیدهای legacy قبل از Rename؛ رد Migration بدون Pin در Guard | `Infrastructure/Persistence/Configurations/*`، S3-0f §4.5 |
| S-3 | بحرانی | PR-2 (Guard) موازی فرض شده؛ Guard فعلی Rename و Pin را اصلاً نمی‌شناسد (X4) | هیچ Migration تغییر نام نمی‌تواند وارد شود؛ Pin بی‌صدا نادیده گرفته می‌شود | PR-2 پیش‌نیاز **اجباری** PR-7 تا PR-10؛ بازطراحی §3.3 | `scripts/ef_model.py`، `migration_model.py`، `generate_migrations.py`، `verify_migration_model.py` |
| S-4 | بالا | ادعای «Trade FK به Companies» غلط است (X3)؛ `TenantId` در Trade بدون قید ارجاعی است | بدون FK، یکپارچگی Tenant/Company در Trade فقط با کد تضمین می‌شود؛ ریسک K-9 بالاتر از برآورد | OD-11 را «تصمیم الزامی» اعلام کن؛ دو گزینه: (الف) FK به `dbo.Companies` (نقض مرز ماژول؛ نیازمند ADR)؛ (ب) کنترل منطقی در Host + تست SQL Server (مدل فعلی) | `Modules/Trade/Trade.Infrastructure/Persistence/Migrations/20261113072823_InitialTradeMasterData.cs`، S3-0f §2/§7 |
| S-5 | بالا | Rename Trade خارج از پوشش Guardهای Python و `has-pending-model-changes` CI است | تغییر Schema Trade بدون Guard وارد می‌شود | PR-9 شامل Guard/تست مخصوص Trade (گسترش Guard یا تست SQL Server مستقل برای Migration) و گسترش `has-pending-model-changes` به Trade | `.github/workflows/dotnet.yml:149`، `scripts/*` |
| S-6 | بالا | Migration NOT NULL خودکار EF خطرناک است (X5) | `UPDATE ... =0` بی‌صدا؛ احتمال شکست FK | Migration دستی + ورودی بازبینی‌شده `HAND_WRITTEN_SQL` + تست «NULL باقی‌مانده → شکست» | S3-0f §8.4 |
| S-7 | بالا | Rename همراه با تغییر دیگر در همان Migration خطر جفت‌سازی اشتباه دارد (X6) | از دست رفتن داده | قانون «Rename-only Migration» + فهرست Rename بازبینی‌شده در Guard | S3-0f §4.5 |
| S-8 | بالا | گروه‌بندی G1–G4 در S3-0f «بستار اجباری FK» فرض شده؛ با Pin، Rename هر جدول مستقل است، بدون Pin گروه‌ها کافی نبودند | PRهای بزرگ‌تر از لازم یا ناکافی بسته به حالت | G1–G4 به‌عنوان **گروه‌بندی مدیریتی** حفظ می‌شود؛ کلید گروه‌بندی: (۱) یک PR = یک گروه Entity مرتبط با یک خانوادهٔ تست؛ (۲) هر Rename-only؛ جزئیات §5.1 | S3-0f §6 |
| S-9 | بالا | سطح Platform Admin در S3-0f کوچک‌تر برآورد شده: ۳۱ صفحه/Handler با `[Authorize(Roles = Constants.Role.Admin)]` (کاربران، نقش‌ها، FileManager، PageManager، منوها…) بدون هیچ فیلتر Tenant؛ صفحهٔ `Pages/Admin/Users/Create.cshtml.cs` با `RoleId = ViewModel.RoleId` می‌تواند هر نقش سراسری را بدهد | در DB مشترک، هر `Role.Admin` کل نصب را مدیریت می‌کند؛ OD-1 باید همهٔ این سطح را پوشش دهد | OD-1 را با فهرست کامل سطح Admin به مالک بده؛ تا تصمیم، Shared چندمشتری ممنوع بماند | `Server/Pages/Admin/**`، `Server/Api/UserAdministrationEndpoints.cs`، `SecurityAuditAdministrationEndpoints.cs`، `MePermissionsEndpoints.cs:48` |
| S-10 | بالا | Rollback در حضور داده‌های جدید: `Down` فاز A (حذف `Companies.TenantId`/`Tenants`) پس از Backfill، نگاشت معتبر را از بین می‌برد؛ S3-0f آن را «بی‌ضرر» نوشته بود | از دست رفتن تنها منبع نگاشت Company→Tenant | پس از D1 رول‌بک فاز A = «بازیابی از Backup یا roll-forward»؛ manifest نگاشت نزد مالک بایگانی شود؛ `Down` فقط برای محیط‌های قبل از D1 مجاز | S3-0f §11 |
| S-11 | متوسط | NOT NULL (C3) و Rename فیزیکی با نسخهٔ قدیمی برنامه ناسازگارند؛ Add column nullable سازگار است | در نصب‌های چند نمونه‌ای/Shared، نمونهٔ قدیمی خطا می‌دهد | ترتیب استقرار: کد جدید (بدون وابستگی به ستون جدید) → Migration → کد وابسته؛ Rename فیزیکی در پنجرهٔ نگهداری یا با گام P4 به تعویق افتد | S3-0f §11 |
| S-12 | متوسط | Backfill: فقط `WHERE TenantId IS NULL` کافی نیست؛ ردیف‌هایی که بعداً با Company تخصیص‌یافته ناسازگار شوند شناسایی نمی‌شوند | ناسازگاری بی‌صدا | پرس‌وجوی راستی‌آزمایی `child.TenantId <> Companies.TenantId` پس از هر Backfill (شمار باید صفر باشد) و ممنوعیت بازتخصیص Company | S3-0f §8.2–8.3 |
| S-13 | متوسط | ترتیب PR: کد نوشتن `TenantId` (C2) باید پیش از D2 مستقر شود وگرنه ردیف NULL جدید حین Backfill وارد می‌شود | NOT NULL شکست می‌خورد | ترتیب: کد نوشتن Tenant → Backfill → راستی‌آزمایی → NOT NULL | S3-0f §13 |
| S-14 | متوسط | Provisioning تک‌مشتری (`CustomerProvisioningSettings` با یک `CustomerReference`) و OD-14: S3-0f «Shared آماده است» نوشته، فقط مسیرهای دسترسی/داده آماده می‌شوند، Provisioning نه | ادعای آمادگی Shared گمراه‌کننده | عبارت اصلاح شود: «مدل داده آماده، Provisioning چندمشتری پیاده‌نشده تا OD-14» | S3-0f §10 |
| S-15 | پایین | نام قیدهای legacy پس از Pin همچنان `TenantId` دارند (بدهی ظاهری) | سردرگمی در ابزار DBA | OD-15 جدید: پذیرش یا PR جدا برای `sp_rename` قیدها (بعد از پایان مهاجرت) | S3-0f §4.5 |
| S-16 | پایین | `Server/.config/dotnet-tools.json` نسخهٔ `dotnet-ef 6.0.3` | ابزار نامتناسب با EF 10 | به‌روزرسانی در PR مستقل (خارج از این طرح) | `Server/.config/dotnet-tools.json` |

### 5.1 بستار FKها و ترتیب Migrationها (بازبینی بر پایهٔ `HasPrincipalKey` واقعی)

سایت‌های `HasPrincipalKey` در پیکربندی‌ها:
- Principal = `CompanyRole (Id, TenantId)`: `UserCompanyMembership`، `CompanyMembershipRequest` (نقش تأییدشده).
- Principal = `UserCompanyMembership (Id, TenantId)`: `CompanyEmployee`.
- Principal = `CompanyEmployee/OrganizationalUnit/Position (Id, TenantId)`: `CompanyEmployeeAssignment`، `CompanyEmployeePhoto` و واحدها.
- Principal = `FiscalPeriod (Id, TenantId)`: `ProvisioningRequestClaim`، `ConsolidationRunCompany`.
- Trade: AK `(TenantId, Id)` روی `Party`/`Product` برای `PartyRoleAssignment`، `PartyBankAccount`، `ProductIdentifier` (داخل خود Trade).

نتیجه: (۱) گروه‌بندی S3-0f با این نقشهٔ ارجاع سازگار است؛ (۲) **بدون Pin** هر گروه باید با تمام FKهای وابسته‌اش هم‌زمان Drop/Add شود (و Guard این را نمی‌پذیرد)؛ **با Pin** هر جدول مستقل Rename می‌شود چون `sp_rename` قیدها را همراه می‌کند (U1، اجرا نشد) — پس اندازهٔ PR را مدیریت ریسک تعیین می‌کند، نه بستار FK؛ (۳) ترتیب پیشنهادی بدون وابستگی FK: `FiscalPeriod` (کوچک‌ترین بستار) → G1 (نقش/عضویت/درخواست) → HR → `SecurityAuditEvent` → Trade (آخر، چون خارج از Guard فعلی است و PR-1 را می‌طلبد).

## 6. پاسخ به پرسش‌های بند ۵ تکلیف

### 6.1 آیا Rename-then-Add همچنان بهترین راه است؟

بله، **مشروط** (جزئیات §1.4). مقایسهٔ نهایی گزینه‌ها:

| گزینه | وضعیت پس از آزمایش |
|---|---|
| R) Rename-then-Add با Pin | تأییدشده در تولید Migration (R2)؛ اجرای SQL Server انجام نشده |
| P) فقط تغییر property با `HasColumnName("TenantId")` (جدید) | تأییدشده (R3): بدون هیچ تغییر دیتابیس و بدون ناسازگاری با نسخهٔ قدیمی. محدودیت: ستون واقعی جدید نمی‌تواند `TenantId` نام بگیرد تا ستون legacy تغییر نام فیزیکی نیابد |
| E) Expand/Contract با ستون کپی | همچنان گران‌تر؛ فقط fallback برای Trade اگر Pin/Guard مشکل ایجاد کرد |
| K) نگه‌داشتن «TenantId = Company» دائمی | رد (ریسک R-2 را ثابت می‌کند) |

**پیشنهاد دومرحله‌ای (Proposed):** برای هر گروه ابتدا گام P (فقط C#، Migration خالی، بدون ناسازگاری)، سپس در صورت تأیید OD-12 گام Rename فیزیکی با Pin (Migration تک‌عملیاتی)، سپس افزودن `TenantId` واقعی. این کار churn کد را از ریسک دیتابیس جدا می‌کند.

### 6.2 آیا ترتیب PRهای S3-0f باید تغییر کند؟

بله:

| PR | تغییر نسبت به S3-0f |
|---|---|
| PR-0 | بدون تغییر (S3-0e، S3-0f و S3-0g به‌علاوهٔ یادداشت اصلاحی؛ بدون تغییر وضعیت Accepted) |
| PR-1 | بدون تغییر محتوا؛ قرارداد §4.2 |
| PR-2 | **پیش‌نیاز اجباری PR-7..PR-10**، نه موازی؛ محتوا: پشتیبانی Pin/`HasColumnName` در `ef_model.py`، فهرست Rename بازبینی‌شده، `RenameColumn`/`RenameIndex` در `migration_model.py`/`verify_migration_model.py`، Mutation testهای §3.4، و (S-5) پوشش Trade |
| PR-3 | بدون تغییر |
| PR-4 | **اصلاح:** `Tenants` + `Companies.TenantId` nullable + FK + Index، **بدون AK** |
| PR-5، PR-6 | بدون تغییر (پس از تأیید OD-6/OD-6a/OD-13) |
| PR-4b (جدید) | پس از D1: `NOT NULL` دستی با `THROW` + AK `(Id, TenantId)` روی `Companies` |
| PR-7..10 | هر کدام دو مرحله: «P» (C#) سپس «Rename فیزیکی» (اختیاری، OD-12)؛ Rename-only؛ بدون افزودن ستون در همان Migration |
| PR-11 (C2) | پس از PR-4b و پس از Rename فیزیکی همان گروه؛ ستون `TenantId` nullable + FK مرکب (برای Trade فقط ستون + کنترل منطقی، S-4) |
| PR-12/13 | پس از استقرار کد نوشتن `TenantId` (S-13)؛ NOT NULL دستی |
| PR-E1..E3 | بدون تغییر؛ E1 به PR-4 وابسته است |

### 6.3 آیا شروع PR-0 تا PR-3 منطقی است؟

بله، با اصلاحات:
- **PR-0:** منطقی؛ مستقل.
- **PR-1:** منطقی و اولویت اول فنی؛ قرارداد §4.2.
- **PR-2:** منطقی ولی گسترده‌تر از برآورد S3-0f (Pin، فهرست Rename، پوشش Trade)؛ بهتر است به PR-2a (Pin + `HasColumnName` + fail-closed برای فراخوانی ناشناخته) و PR-2b (Rename بازبینی‌شده + `RenameColumn`/`RenameIndex` + Down) شکسته شود. PR-2a به‌تنهایی امن است چون هیچ Rename را نمی‌پذیرد.
- **PR-3:** منطقی؛ مستقل از Schema.
- **PR-4** در صورت تأیید شکل Tenant (بدون AK) می‌تواند موازی با PR-1..3 باشد؛ ولی ادغام آن Schema را تغییر می‌دهد و فقط پس از §7 مجاز است.

### 6.4 تصمیم‌هایی که همچنان نیازمند تأیید مالک هستند

| ID | وضعیت نسبت به S3-0f |
|---|---|
| OD-1 | **گسترده‌تر شد** (S-9: ۳۱ صفحه/Handler Admin + ساخت کاربر با نقش سراسری) |
| OD-3 | بدون تغییر |
| OD-4، OD-5 | بدون تغییر |
| OD-6، OD-6a، OD-13 | بدون تغییر |
| OD-10 | بدون تغییر؛ حالا شامل دو مرحله (P سپس فیزیکی) |
| OD-11 | **اجباری شد** (S-4: Trade FK ندارد) |
| OD-12 | بدون تغییر؛ با گام P می‌تواند اختیاری شود |
| OD-14 | بدون تغییر |
| **OD-15 (جدید)** | قیدهای legacy با نام `TenantId` پس از Pin بمانند یا در PR جدا تغییر نام یابند |
| **OD-16 (جدید)** | رویهٔ استقرار برای نصب‌های چند نمونه‌ای (چه کسی `--migrate` را اجرا می‌کند؛ ترتیب استقرار) — فقط اگر Shared چندنمونه‌ای انتخاب شود |

OD-2، OD-7، OD-8، OD-9 بدون تغییر و غیرمسدودکنندهٔ فازهای نزدیک.

## 7. پیش‌شرط‌های ورود به Schema Migration

هیچ Migration Tenant/Company (PR-4 به بعد) تا برآورده شدن همهٔ موارد زیر ساخته/ادغام نشود:

1. PR-1 (قرارداد Migration host Trade) ادغام شده و تست‌هایش روی SQL Server واقعی **اجرا و سبز** شده باشد.
2. PR-2a/2b ادغام شده و Mutation testهای Rename واقعاً در `scripts/tests/` اجرا و سبز شده باشند (نه فقط Prototype).
3. یک اجرای **واقعی** روی SQL Server (`scripts/dev/test-sqlserver.sh` یا CI) که U1، U2، U3، U4 را روشن کند: Rename با Pin، رفت‌وبرگشت `Up/Down`، FK مرکب با `NULL`، شکست یا موفقیت R1.
4. تصمیم مالک دربارهٔ OD-6/OD-6a (شکل Tenant) و OD-12؛ برای هر PR تغییر نام: تأیید OD-10.
5. Backup و رویهٔ بازیابی مستند برای هر نصب هدف؛ manifest نگاشت نزد مالک.
6. برای Trade: تصمیم OD-11 و Guard/تست Trade (S-5).
7. طرح اصلاح‌شدهٔ S3-0f (PR-4 بدون AK، PR-4b) در `DECISIONS`/`DATABASE`/`ROADMAP` با وضعیت `Proposed` ثبت شده باشد (پس از تأیید مالک).
8. پوشش تست دو-Tenant برای مسیرهای حساس (PR-E1) پیش از Rename/ستون Tenant در مسیر مجوز.

## 8. اصلاح ارجاعی S3-0f

در S3-0f فقط یک یادداشت کوتاه ارجاعی افزوده شد (بخش اصلی بدون حذف/بازنویسی مانده است) که به این سند اشاره می‌کند و موارد ردشده X1 و X3 را علامت می‌زند.

## 9. محدودیت‌ها و Not Run

- SQL Server یا Docker در دسترس نبود؛ **هیچ SQL و هیچ Migration روی SQL Server اجرا نشد**. همهٔ SQLهای این سند فقط تولید شده‌اند.
- آزمایش NOT NULL و FK مرکب با نوع `int` در پروژهٔ کوچک انجام شد؛ ساختار مخزن واقعی از `Guid` استفاده می‌کند (U7).
- Prototype Guard در مخزن نیست و ادغام نشده؛ ۱۰ تست آن فقط قواعد را اثبات می‌کند.
- تست‌های کامل `dotnet test Template.sln`، ClientApp و e2e اجرا نشد (کار فقط طراحی/اعتبارسنجی بود). اجرای نشده = Not Run.
- بازبینی عملکرد، قفل همزمانی و رفتار نسخهٔ قدیمی برنامه با Schema جدید اجرا نشد.
- شواهد دیتابیس فقط metadata `Farano-Local` (S3-0) است.
- گزارش S3-0e و S3-0f هنوز در `main` نیستند (فقط محلی).

## 10. فرمان‌های اجرا شده (خلاصهٔ شواهد)

```
dotnet-install.sh --version 10.0.100 --install-dir /opt/dotnet          # موقت، خارج مخزن
dotnet tool install -g dotnet-ef --version 10.0.13
pip install --break-system-packages tree-sitter tree-sitter-c-sharp
python3 scripts/generate_migrations.py --check                          # OK، rc=0
python3 -I scripts/verify_migration_model.py                            # 20 migrations verified، rc=0
python3 -I scripts/tests/test_migration_tooling.py                      # 32 tests OK
python3 -I scripts/tests/test_migration_guard_mutations.py              # 31 tests OK
dotnet build Infrastructure/Persistence/Infrastructure.Persistence.csproj -c Release   # روی کپی ایزوله: Build succeeded
dotnet ef migrations add <name> --project Infrastructure/Persistence --no-build        # روی کپی‌های R1/R2/R3
dotnet ef migrations script <from> <to> --no-build                                     # پروژهٔ کوچک
python3 -I /tmp/s30g/test_rename_guard_prototype.py                     # Prototype: 10 tests OK
```

**فهرست ابزار:** استفاده‌شده — `dotnet`، `dotnet-ef`، Python/tree-sitter، git؛ در دسترس ولی استفاده‌نشده — کلید `Agent`/زیرعامل (ضرورتی نبود)؛ در دسترس نبود — SQL Server/Docker، bridge کامپیوتر کاربر؛ اجرا نشده — `dotnet test`، ClientApp، e2e، تست SQL Server.
