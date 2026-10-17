# Database management of the Taraz Ava (تراز آوا) application

This document describes how the schema of the database is managed, which
commands have to be used and how an existing database is upgraded.

## 1. The rules of the project

| Rule | Where it is implemented |
| --- | --- |
| The runtime never creates or drops the schema. | `Infrastructure/Persistence/DatabaseContext.cs` (there is no `EnsureCreated()` or `EnsureDeleted()`) |
| The schema is owned by versioned migrations that are committed to Git. | `Infrastructure/Persistence/Migrations/` |
| The design time tools know the context without the web host. | `Infrastructure/Persistence/DesignTimeDatabaseContextFactory.cs` |
| The production environment never applies the migrations on its own. | `Server/Program.cs` (`Database:ApplyMigrations` is `false` in `appsettings.json`) |
| The connection string never lives in the source code. | `Server/appsettings.json` (the `ConnectionStrings` section was removed), user secrets, environment variables |

The migrations use the `Data.Migrations` namespace, because the root namespace
of the persistence project is `Data` (`Infrastructure.Persistence.csproj`).

## 2. The tools

| Tool | Version | Comment |
| --- | --- | --- |
| .NET SDK | 10.0.100 (`global.json`) | `rollForward: latestFeature` |
| EF Core | 10.0.13 | `Microsoft.EntityFrameworkCore.SqlServer`, `.Proxies`, `.Tools` and `.Design` are aligned |
| `dotnet-ef` | 10.0.13 | `dotnet tool install --global dotnet-ef --version 10.0.13` |

All EF Core packages of the solution are pinned in `Directory.Packages.props`
and are on the same version. A mixed version of the provider and of the design
package is not supported and would break the scaffolding of the migrations.

## 3. The commands of this solution

The persistence project does not reference the web project, therefore the
startup project has to be given explicitly.

```bash
# The context that is used by the tools.
export TARAZAVA_CONNECTIONSTRING='Server=localhost;Database=TarazAva_Development;Trusted_Connection=True;TrustServerCertificate=True'

# A new migration after a change of the model.
dotnet ef migrations add <MigrationName> \
  --project Infrastructure/Persistence/Infrastructure.Persistence.csproj \
  --startup-project Server/Server.csproj \
  --output-dir Migrations

# The SQL script of the migrations (review it before it is executed).
dotnet ef migrations script --idempotent \
  --project Infrastructure/Persistence/Infrastructure.Persistence.csproj \
  --startup-project Server/Server.csproj \
  --output artifacts/migration.sql

# The SQL script of a single migration.
dotnet ef migrations script <FromMigration> <ToMigration> \
  --project Infrastructure/Persistence/Infrastructure.Persistence.csproj \
  --startup-project Server/Server.csproj

# Apply the migrations to a database.
dotnet ef database update \
  --project Infrastructure/Persistence/Infrastructure.Persistence.csproj \
  --startup-project Server/Server.csproj

# The state of the model and of the migrations.
dotnet ef migrations list \
  --project Infrastructure/Persistence/Infrastructure.Persistence.csproj \
  --startup-project Server/Server.csproj

# Fails when the model and the snapshot of the migrations differ.
dotnet ef migrations has-pending-model-changes \
  --project Infrastructure/Persistence/Infrastructure.Persistence.csproj \
  --startup-project Server/Server.csproj

# Remove the last migration (only while it was not applied).
dotnet ef migrations remove \
  --project Infrastructure/Persistence/Infrastructure.Persistence.csproj \
  --startup-project Server/Server.csproj
```

The connection string of the tools is read, in this order, from
`TARAZAVA_CONNECTIONSTRING`, from `ConnectionStrings__ConnectionString` and
finally from the local database of the developer machine. The fallback of the
design time factory is only used when none of them is set.

## 4. The procedures per environment

### Development

`Server/appsettings.Development.json` contains the connection string of the
local database and `Database:ApplyMigrations` is `true`. The server applies
the pending migrations when it starts in the development environment. The
migrations can also be applied manually with `dotnet ef database update`.

### Test

`scripts/apply_migrations_to_testdb.sh` applies the migrations to an isolated
test database and verifies the result. The script refuses to run against a
database whose name does not contain `test`, `dev` or `scratch`, so that a
production or a customer database can not be touched by accident.

```bash
TARAZAVA_TEST_CONNECTIONSTRING='Server=localhost;Database=TarazAva_Test;...' \
  scripts/apply_migrations_to_testdb.sh
```

### Production

* The migrations are **not** applied by the application. `Database:ApplyMigrations`
  is `false` and the server only writes a warning when the database is behind
  the migrations.
* The deployment applies the migrations with `dotnet ef database update`
  (or with the reviewed SQL script) as a separate, controlled step.
* The connection string comes from the user secrets, from the environment
  variables of the host or from the secret store of the platform.

## 5. The upgrade of a database that was created with `EnsureCreated()`

The databases that were created by the previous version of the application do
not contain the table `__EFMigrationsHistory`, because `EnsureCreated()` does
not write it. Such a database must **not** receive the `InitialCreate`
migration, the tables already exist and the `CreateTable` statements would fail.

The upgrade has three steps.

1. **Verify the schema.** Compare the existing tables with the model:

   ```bash
   dotnet ef migrations script 0 20251017120000_InitialCreate \
     --project Infrastructure/Persistence/Infrastructure.Persistence.csproj \
     --startup-project Server/Server.csproj \
     --output artifacts/initial-create.sql
   ```

   Review the script and check the existing schema against it (the tables, the
   columns, the types, the indexes and the foreign keys). The differences that
   are found are applied with a dedicated migration (for example
   `AddMissingColumn`) and **not** by editing the `InitialCreate` migration.

2. **Write the baseline.** Insert the history row of the `InitialCreate`
   migration, so that EF Core considers the existing schema as applied:

   ```sql
   IF OBJECT_ID(N'__EFMigrationsHistory', N'U') IS NULL
   BEGIN
       CREATE TABLE [__EFMigrationsHistory] (
           [MigrationId]    nvarchar(150)  NOT NULL,
           [ProductVersion] nvarchar(32)   NOT NULL,
           CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
       );
   END;

   INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
   SELECT N'20251017120000_InitialCreate', N'10.0.13'
   WHERE NOT EXISTS (
       SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20251017120000_InitialCreate');
   ```

   The script is in `scripts/baseline-existing-database.sql`.

3. **Apply the following migrations.**

   ```bash
   dotnet ef database update \
     --project Infrastructure/Persistence/Infrastructure.Persistence.csproj \
     --startup-project Server/Server.csproj
   ```

   The result must be verified with `dotnet ef migrations list` and with
   `dotnet ef migrations has-pending-model-changes`.

## 6. The model

The model is described by the domain model (`Domain/`), by the entity
configurations (`Infrastructure/Persistence/Configurations/`) and by the
conventions of the context (`Infrastructure/Persistence/Conventions/`).

* The legacy models (`Role`, `User`, `Page`, `LoginLog`, `MenuItem`,
  `Permission`, `PageCategory`, `ApplicationHandler`, `PostCategory`) and the
  multi tenant models (`Company`, `CompanyRole`, `CompanyRolePermission`,
  `PermissionDefinition`, `UserCompanyMembership`, `FiscalPeriod`,
  `SecurityAuditEvent`) live side by side. The `InitialCreate` migration
  contains **all** of them.
* The money is never stored as `float` or `real`, the amounts use `decimal`.
* The `TenantId` of a company scoped row is the foreign key to
  `Companies.Id`. The `CompanyRole` has the alternate key `(Id, TenantId)` and
  the `UserCompanyMembership` uses the composite foreign key
  `(RoleId, TenantId)` on it, so that a membership can only reference a role of
  its own company.
* The isolation of the tenants is **not** enforced by the database: the
  `TenantId` columns and the foreign keys exist, but there is no row level
  security and no filter that restricts a query to the tenant of the current
  user. This has to be implemented in the application layer and is reported as
  an open point.
* Besides the 24 indexes that the configurations declare with `.HasIndex(...)`,
  the model has 8 more indexes that no configuration writes down. The EF Core
  `ForeignKeyIndexConvention` creates an index for every foreign key whose
  columns are not already the leading columns of a key or of another index. Any
  tool that derives the model from the configuration files has to apply the same
  rule, otherwise the snapshot misses those indexes and
  `has-pending-model-changes` fails: `IX_CompanyRolePermissions_PermissionId`,
  `IX_LoginLogs_UserId`, `IX_MenuItems_ParentId`, `IX_Pages_CreatorUserId`,
  `IX_Pages_PageCategoryId`, `IX_Permissions_ApplicationHandlerId`,
  `IX_UserCompanyMemberships_RoleId_TenantId` and `IX_Users_RoleId`.

## 7. The migrations of this repository

| Migration | What it does |
| --- | --- |
| `20251017120000_InitialCreate` | Creates every table of the model. Published and immutable. |
| `20261017090000_AlignSchemaWithModel` | Brings the schema of `InitialCreate` in line with the model of the code base. |

`20261017090000_AlignSchemaWithModel` exists because the snapshot that was
committed with `InitialCreate` described a model that differs from the model of
the code base (see the deviations below). It applies exactly that difference:

1. `DropIndex` on `IX_PostCategory_ParentId_Title`. SQL Server refuses
   `ALTER COLUMN` on a column that participates in an index (error 5074), so the
   index is dropped first; note that `MigrationsModelDiffer` yields the column
   operations of a table before its index operations.
2. `AlterColumn` on `PostCategory.Title`: `nvarchar(50) NULL` becomes
   `nvarchar(50) NOT NULL`, because `PostCategoryConfiguration` calls
   `.IsRequired()`. Rows that were written with a `NULL` title before this
   migration are set to an empty string first, otherwise the `ALTER` fails.
3. `CreateIndex` on `IX_PostCategory_ParentId_Title` with the filter
   `[ParentId] IS NOT NULL`. The `SqlServerIndexConvention` drops the
   `[Title] IS NOT NULL` part as soon as `Title` is no longer nullable.
4. Eight `CreateIndex` operations for the indexes that the
   `ForeignKeyIndexConvention` implies and that no configuration writes down:
   `IX_CompanyRolePermissions_PermissionId`, `IX_LoginLogs_UserId`,
   `IX_MenuItems_ParentId`, `IX_Pages_CreatorUserId`,
   `IX_Pages_PageCategoryId`, `IX_Permissions_ApplicationHandlerId`,
   `IX_UserCompanyMemberships_RoleId_TenantId` and `IX_Users_RoleId`. They are
   missing from the database as well, because `InitialCreate` was generated from
   the same model that did not know the convention.

`Down` reverses the steps.

The operations of the migration are not written down anywhere by hand:
`scripts/generate_migrations.py` computes them from the difference between the
model of the `InitialCreate` designer file and the model of the code base, so
that they can not drift apart from the snapshot.

## 8. The checks of this repository

| Check | Command | What it verifies |
| --- | --- | --- |
| The model of the code base | `python3 scripts/ef_model.py --repo-root .` | The tables, the columns, the types, the keys, the indexes and the foreign keys of the model |
| The generated migrations | `python3 scripts/generate_migrations.py --repo-root . --check` | The committed model snapshot and the files of the newest migration are up to date |
| The migration and the model | `python3 scripts/verify_migration_model.py --repo-root .` | The snapshot describes the model of the code base (the equivalent of `has-pending-model-changes`), the published migration matches its own designer file, the newest migration applies exactly the difference between the two, and the C# syntax of all generated files is valid |
| The model tests | `dotnet test` | The tables, the keys, the foreign keys, the indexes and the types of the model, and `Database.HasPendingModelChanges()` |
| The official check | `dotnet ef migrations has-pending-model-changes --project Infrastructure/Persistence/Infrastructure.Persistence.csproj --startup-project Server/Server.csproj` | The comparison that `MigrateAsync` makes at runtime. It throws `PendingModelChangesWarning` when the snapshot and the model differ. |

The last four checks also run in `.github/workflows/dotnet.yml`. The workflow
passes `--configuration Release --no-build` to `dotnet ef`, because `--no-build`
reuses the output of the build step and would otherwise look for a Debug build
that does not exist.

`scripts/verify_migration_model.py` needs `tree-sitter` and
`tree-sitter-c-sharp` for the C# syntax check; without them it reports that the
syntax was not validated and fails.

`scripts/generate_migrations.py` writes the model snapshot and the files of the
newest migration in the format of the EF Core generators. It exists because the
migrations of this repository were written without a .NET SDK. It is a
re-implementation of parts of EF Core and **not** a replacement for
`dotnet ef migrations add`: whenever a deviation between the two was found, the
model of the application won and the script was corrected. Replace it with
`dotnet ef migrations add` as soon as the SDK is available.

### The deviations that were found and corrected

The script is the reason why `/DatabaseSetup` failed with
`PendingModelChangesWarning`: the snapshot it had written described a model that
differs from the model of the code base.

| Deviation of the script | What EF Core 10.0.13 really does | Effect on `has-pending-model-changes` |
| --- | --- | --- |
| Only the indexes that a configuration declares with `.HasIndex(...)` were collected, so the eight indexes that the foreign keys imply were missing from the snapshot. | `ForeignKeyIndexConvention.CreateIndex` adds an index for every foreign key whose columns are not already the leading columns of a key or of another index (`AreIndexedBy`). No configuration ever writes those indexes down. | **Yes — this was the whole warning.** `MigrationsModelDiffer` reported exactly eight `CreateIndexOperation`s: `IX_CompanyRolePermissions_PermissionId`, `IX_LoginLogs_UserId`, `IX_MenuItems_ParentId`, `IX_Pages_CreatorUserId`, `IX_Pages_PageCategoryId`, `IX_Permissions_ApplicationHandlerId`, `IX_UserCompanyMemberships_RoleId_TenantId` and `IX_Users_RoleId`. |
| `.IsRequired()` without an argument was ignored, so `PostCategory.Title` was written as nullable although `PostCategoryConfiguration` requires it. | `PropertyBuilder.IsRequired(bool required = true)` defaults to `true`. | **Yes.** `MigrationsModelDiffer.ColumnStructureEquals` compares `IsNullable`, so an `AlterColumnOperation` is produced. |
| Because `Title` was nullable, the automatic filter of the unique index kept `[Title] IS NOT NULL`. | `SqlServerIndexConvention` recomputes the filter of a unique, non-clustered index when the nullability of one of its columns changes. | **Yes.** `IndexStructureEquals` compares `Filter`; an index whose filter differs is dropped and created again. |
| Only `.IsUnicode(false)` was written into the snapshot. | `CSharpSnapshotGenerator.GenerateFluentApiForIsUnicode` writes the annotation whenever it is present, `.IsUnicode(true)` included. | No. `IsUnicode` is compared while *matching* columns, but it is not part of the `AlterColumnOperation` trigger, because a real change of it always changes the store type (`varchar` ↔ `nvarchar`). |
| `.ValueGeneratedOnAdd()` was written for every `Guid` primary key. | `Seedwork.Entity.Id` carries `[DatabaseGenerated(DatabaseGeneratedOption.None)]`, which the `DatabaseGeneratedAttributeConvention` applies with `ConfigurationSource.DataAnnotation`; the `ValueGenerationConvention` uses `ConfigurationSource.Convention` and can not override it, so the value generation is `Never`. | No. `ColumnStructureEquals` does not compare `ValueGenerated`. |

The two rows in the middle are snapshot fidelity only: they do not produce a
migration operation, but they made the committed snapshot different from the
snapshot that `dotnet ef migrations add` would have written.
