# Handoff — Vendor RBAC foundation (Issue #150)

## Scope
This change introduces the first slice of Vendor role-based authorization: a versioned permission catalog, named permission policies, role-claim-backed permission evaluation, and unit tests for policy composition.

## Architecture
- Permission IDs are stable server-side identifiers; they are not role names or UI labels.
- Permissions are assigned to ASP.NET Core Identity roles through claims of type `vendor:permission`.
- A named policy combines the existing enabled-account/MFA requirement with a specific permission requirement.
- Unknown permission identifiers fail closed even when a caller constructs a policy name manually.
- Existing authentication endpoints and customer accounting persistence are unchanged.

## Explicitly not complete
- No role/permission management API or UI exists yet.
- No resource-based authorization for customer/installation/contract/license resources exists yet.
- Maker-checker is not implemented in this slice.
- No Vendor schema or customer schema migration is included.
- This code is not Production-verified until Vendor CI, SQL Server integration tests, EF snapshot validation, publish checks, and full repository CI pass on the PR SHA.

## Validation plan
1. Vendor UI typecheck/tests/build and runtime dependency audit.
2. Vendor .NET Release build and tests on SQL Server.
3. Strict Vendor EF Core pending-model-changes gate.
4. Vendor publish/artifact scan.
5. Full repository CI and manual review of the diff.

## Next slice
Add a route inventory test for authorization metadata, then implement resource-based authorization and maker-checker as separately reviewable changes under Issue #150.
