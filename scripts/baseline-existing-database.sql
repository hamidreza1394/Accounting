-- Baseline for the databases that were created with Database.EnsureCreated().
--
-- These databases contain the tables of the model, but they do not contain the
-- migration history, because EnsureCreated() does not write it. The script
-- creates the history table when it is missing and registers the
-- InitialCreate migration, so that EF Core considers the existing schema as
-- applied and the following migrations can be applied normally.
--
-- The script never drops or alters a table. Run it against a copy of the
-- database first and compare the schema with the script of the migration:
--
--   dotnet ef migrations script 0 20251017120000_InitialCreate \
--     --project Infrastructure/Persistence/Infrastructure.Persistence.csproj \
--     --startup-project Server/Server.csproj \
--     --output artifacts/initial-create.sql

SET NOCOUNT ON;

IF OBJECT_ID(N'__EFMigrationsHistory', N'U') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId]    nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32)  NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
SELECT N'20251017120000_InitialCreate', N'10.0.13'
WHERE NOT EXISTS (
    SELECT 1
    FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251017120000_InitialCreate');

SELECT [MigrationId], [ProductVersion]
FROM [__EFMigrationsHistory]
ORDER BY [MigrationId];
