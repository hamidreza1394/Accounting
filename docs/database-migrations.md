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

## 7. The checks of this repository

| Check | Command | What it verifies |
| --- | --- | --- |
| The model of the code base | `python3 scripts/ef_model.py --repo-root .` | The tables, the columns, the types, the keys, the indexes and the foreign keys of the model |
| The generated migrations | `python3 scripts/generate_migrations.py --repo-root . --check` | The committed migration and snapshot are up to date |
| The migration and the model | `python3 scripts/verify_migration_model.py --repo-root .` | The migration and the snapshot describe the model of the code base, and the C# syntax is valid |
| The model tests | `dotnet test` | The tables, the keys, the foreign keys, the indexes and the types of the model |

`scripts/generate_migrations.py` writes the migration, its designer file and
the model snapshot in the format of the EF Core generators. It exists because
the migrations of this repository were written without a .NET SDK; it can be
replaced by `dotnet ef migrations add` as soon as the SDK is available, the
result is the same.
