# تحویل وظیفه — ISSUE-494-company-user-directory-api

- Issue: #494 (زیرمجموعه #444، D-064)
- مسئول: ChatGPT — Backend/API، migration و تست
- وضعیت: پیاده‌سازی در PR؛ Verified فقط پس از CI سبز

## قرارداد
- GET /api/v1/companies/{companyId}/users
- Authorization: User account + active user + active membership + active company + active company role + explicit users.list permission through CompanyRolePermissions.
- No active membership/unknown or foreign company => 404; no users.list => 403; anonymous/infrastructure principal => 401.
- Query: search (max 120), membershipActive, page, pageSize (max 100).
- Results contain only memberships whose TenantId equals the requested company. Pending requests are stored separately and never appear.
- Inactive user/membership/role statuses are represented; they do not grant access.

## Safe response fields
UserId, Username, FirstName, LastName, UserIsActive, MembershipIsActive, RoleId, RoleCode, RoleName, RoleIsActive, MembershipCreatedAtUtc.
No EmailAddress, CellPhoneNumber, Password/hash, LoginLogs, sessions, IP, DetailsJson or permission lists.

## Permission seed
- Code: users.list
- Stable ID: f47a2026-1103-4d00-8000-000000000002
- Migration: 20261103141500_SeedCompanyUserListPermission
- Idempotent insert only when Code does not exist.
- No role or user is granted this permission by migration.
- Down deletes only the exact seeded ID/code and only if no CompanyRolePermissions reference it.

## Tests
- Unit: tenant isolation, pending-request exclusion, inactive status, permission denial, 404 for foreign company, invalid search, anonymous denial and DTO field allow-list.
- SQL Server integration: real migration, permission lookup, join translation, deterministic search/pagination and cross-tenant exclusion.
- Local tests not run in this environment (GitHub DNS and .NET SDK unavailable); CI is authoritative.

## Ownership boundary
- No ClientApp/UI files changed; Claude owns the consuming screen.
- No new Tenant, role, group or membership model.
- No automatic membership activation or role assignment.
- No customer DB operation outside the normal reviewed migration process.
