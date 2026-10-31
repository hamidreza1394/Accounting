/*
  Vendor audit append-only permissions.
  Run against the Vendor database using SQLCMD mode:
    sqlcmd -S <server> -d <VendorDatabase> -E -v VendorRuntimeUser="VendorAppRuntime" VendorMigrationUser="VendorMigration" -i scripts/sql/vendor-audit-runtime-permissions.sql

  This script does not create logins/users and does not grant migration permissions.
  VendorRuntimeUser must be a dedicated non-privileged application principal.
  VendorMigrationUser must be a different principal used only by controlled migrations.
*/
:setvar VendorRuntimeUser "VendorAppRuntime"
:setvar VendorMigrationUser "VendorMigration"

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @RuntimeUser sysname = N'$(VendorRuntimeUser)';
DECLARE @MigrationUser sysname = N'$(VendorMigrationUser)';
DECLARE @RuntimeUserId int = DATABASE_PRINCIPAL_ID(@RuntimeUser);
DECLARE @MigrationUserId int = DATABASE_PRINCIPAL_ID(@MigrationUser);
DECLARE @AuditObjectId int = OBJECT_ID(N'dbo.VendorAuditEvents', N'U');

IF @RuntimeUser = @MigrationUser
    THROW 51000, 'Vendor runtime and migration principals must be different.', 1;

IF @RuntimeUserId IS NULL OR @MigrationUserId IS NULL
    THROW 51001, 'Both Vendor runtime and migration database users must exist before applying permissions.', 1;

IF @AuditObjectId IS NULL
    THROW 51002, 'dbo.VendorAuditEvents does not exist. Apply Vendor migrations first.', 1;

IF IS_SRVROLEMEMBER(N'sysadmin', @RuntimeUser) = 1
   OR IS_ROLEMEMBER(N'db_owner', @RuntimeUser) = 1
   OR IS_ROLEMEMBER(N'db_ddladmin', @RuntimeUser) = 1
   OR IS_ROLEMEMBER(N'db_securityadmin', @RuntimeUser) = 1
   OR IS_ROLEMEMBER(N'db_accessadmin', @RuntimeUser) = 1
    THROW 51003, 'Vendor runtime principal has a privileged role that bypasses the append-only boundary.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'VendorAuditAppendOnly' AND type = 'R')
    EXEC(N'CREATE ROLE [VendorAuditAppendOnly] AUTHORIZATION [dbo];');

IF IS_ROLEMEMBER(N'db_datareader', @RuntimeUser) <> 1
    EXEC(N'ALTER ROLE [db_datareader] ADD MEMBER ' + QUOTENAME(@RuntimeUser) + N';');

IF IS_ROLEMEMBER(N'db_datawriter', @RuntimeUser) <> 1
    EXEC(N'ALTER ROLE [db_datawriter] ADD MEMBER ' + QUOTENAME(@RuntimeUser) + N';');

IF IS_ROLEMEMBER(N'VendorAuditAppendOnly', @RuntimeUser) <> 1
    EXEC(N'ALTER ROLE [VendorAuditAppendOnly] ADD MEMBER ' + QUOTENAME(@RuntimeUser) + N';');

GRANT SELECT, INSERT ON OBJECT::dbo.VendorAuditEvents TO [VendorAuditAppendOnly];
DENY UPDATE, DELETE, ALTER ON OBJECT::dbo.VendorAuditEvents TO [VendorAuditAppendOnly];

-- The migration principal is intentionally not modified by this script.
PRINT N'Vendor audit append-only permissions configured for runtime principal ' + QUOTENAME(@RuntimeUser) + N'.';
PRINT N'Keep the migration principal separate and never use db_owner/sysadmin for the running Vendor host.';
