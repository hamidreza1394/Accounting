# Vendor Identity SchemaVersion 3 migration drift — handoff

## Goal
Restore strict EF Core migration validation for the independent Vendor control-plane database and make the runtime Identity model match the recorded migrations.

## Root cause observed
The SQL Server integration test compared the runtime design-time model against the last migration snapshot and reported:
- AlterColumn: VendorAdminUserTokens.Name, nvarchar(450) -> nvarchar(128)
- AlterColumn: VendorAdminUserTokens.LoginProvider, nvarchar(450) -> nvarchar(128)
- AlterColumn: VendorAdminUsers.PhoneNumber, nvarchar(max) -> nvarchar(256)
- AlterColumn: VendorAdminUserLogins.ProviderKey, nvarchar(450) -> nvarchar(128)
- AlterColumn: VendorAdminUserLogins.LoginProvider, nvarchar(450) -> nvarchar(128)
- CreateIndex: IX_VendorAdminUserPasskeys_UserId
- AddForeignKey: FK_VendorAdminUserPasskeys_VendorAdminUsers_UserId

These differences are consistent with the configured IdentitySchemaVersions.Version3.

## Changes on branch
- Restored the strict Vendor EF snapshot check; removed the temporary continue-on-error introduced into main by PR #137.
- Added migration 20261026084500_VendorAlignIdentitySchemaV3.cs and its separate Designer file.
- Updated VendorDbContextModelSnapshot to represent the runtime model.
- The migration explicitly drops/recreates PK_VendorAdminUserTokens and PK_VendorAdminUserLogins around column-width changes. SQL Server rejected altering VendorAdminUserTokens.Name while its primary key depended on it.
- Updated the diagnostic test to initialize the snapshot model and compare against IDesignTimeModel.Model, and to report operation details.
- Reordered Vendor CI so the SQL Server integration test executes before the strict snapshot check; this makes both diagnostics visible in a failed run.
- No customer accounting DbContext or customer migrations were changed.

## Validation
- Run [40293940211](https://github.com/hamidreza1394/Accounting/actions/runs/40293940211) identified the exact seven model differences.
- An earlier migration attempt failed because SQL Server required the affected primary keys to be dropped before altering their columns. The current migration revision adds this ordering.
- The latest Vendor CI for the current revision must still prove: Build, SQL Server migration/authentication tests, dotnet ef migrations has-pending-model-changes, and Publish. Until that run is green, status is Not verified.
- PR #138 is Draft and must not be merged before all checks pass.

## Remaining Vendor Production blockers
1. Secure one-time first-administrator bootstrap and forced password-change enforcement.
2. Fine-grained RBAC and administrator lifecycle management.
3. Wire audit writer to privileged operations and add independent integrity checkpoints/export.
4. Implement maker-checker for high-risk operations.
5. Implement real CustomerAccount, Installation, Contract, subsystem and License APIs with server-side authorization and audit.
6. Implement license signing through the approved KMS/HSM abstraction; never place private signing keys in Git, CI, or customer packages.
7. Finish UI workflows and verify cloud/dedicated-server deployment.

## Next step
Inspect the latest CI run on PR #138. Fix any remaining model/migration or SQL Server failure before proceeding to the secure bootstrap work.
