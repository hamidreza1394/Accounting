---
name: faranou-efcore-migrations
description: Use for EF Core model changes, SQL Server migrations, migration scripts, schema drift, database upgrades, or migration rollback planning in Faranou.
metadata:
  origin: adapted-from-ECC-database-migrations
---

# Faranou EF Core and SQL Server Migration Workflow

This skill adapts ECC's migration-safety principles to Faranou's actual EF Core/SQL Server setup. It does not authorize database changes or execution.

## Mandatory discovery

1. Read `AGENTS.md`, `docs/INDEX.md`, `docs/database-migrations.md`, `docs/DATABASE.md`, `docs/DECISIONS.md`, and the current Issue.
2. Inspect the current model, `Infrastructure/Persistence/Migrations/`, `DatabaseContext`, `DesignTimeDatabaseContextFactory`, and relevant migration/model tests.
3. Verify the actual SDK, EF Core package line, `dotnet-ef` availability, current branch, and working tree. Never infer tool availability from documentation alone.

## Non-negotiable rules

- SQL Server is the target provider; do not copy PostgreSQL, Prisma, Drizzle, or other provider-specific SQL from generic guides.
- Migrations are version-controlled schema history. Never change an already-deployed migration to rewrite history.
- The application runtime must not create/drop schemas. Production migrations are not automatically applied by the application.
- Never run `database update`, generated SQL, destructive DDL, or data backfills against customer/production databases without explicit authorization and a reviewed execution plan.
- Never commit connection strings, secrets, real customer data, or generated artifacts containing sensitive information.
- Do not add EF Core packages or change their versions ad hoc. Microsoft EF Core/ASP.NET Core packages must remain aligned with the repository's centrally managed versions.
- Do not use `EnsureCreated` or `EnsureDeleted` to bypass the migration system.

## Workflow

1. **Baseline:** establish whether the change is a model change, data transformation, or both. Determine affected tables, constraints, indexes, existing customer data, application compatibility, and deployment ordering.
2. **Plan:** for risky or production-facing changes, prefer expand-and-contract: add compatible structures first, deploy compatible code, migrate/backfill data in a controlled step, then remove obsolete structures in a later change.
3. **Generate:** after the model change is approved, use the command documented in `docs/database-migrations.md` with the existing persistence project and `Server/Server.csproj` as startup project. Do not invent a different context/project path.
4. **Review:** inspect the complete generated migration and model snapshot. Look for unintended table/column drops, data loss, nullable-to-not-null transitions, default-value semantics, index/constraint changes, SQL Server-specific behavior, and incorrect rename detection. Correct the model/migration deliberately; never blindly accept generated destructive operations.
5. **Check drift:** run `dotnet ef migrations has-pending-model-changes` with the documented project/startup arguments. Review the idempotent SQL script before any deployment use.
6. **Test:** add or update model and migration tests; use SQL Server integration tests when behavior depends on the real provider. Run existing migration guards and the relevant test suite. Record what was and was not executed.
7. **Deploy/rollback:** document compatibility, backup/restore assumptions, ordering, rollback limits, and recovery steps. A down migration is not automatically a safe production rollback when data has been transformed or removed.

## Commands

Use the canonical command lines and environment-variable handling from `docs/database-migrations.md`. Do not install or update `dotnet-ef` automatically; first verify whether the pinned tool is already available and request approval for environment changes.

## Required report

State affected entities/tables, generated files, destructive-risk review, compatibility and rollback plan, commands/results, SQL Server availability, skipped/unverified cases, and whether a migration script was merely generated or actually applied.
