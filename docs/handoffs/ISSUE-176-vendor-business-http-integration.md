# Issue #176 — Vendor business HTTP integration coverage

## Current increment (2026-10-27)

Branch: `test/vendor-business-http-integration`

Added `tests/Vendor.Host.Tests/VendorBusinessMutationAuthorizationIntegrationTests.cs`:
- Exercises the production ASP.NET Core host with `WebApplicationFactory<Program>`.
- Asserts anonymous requests receive HTTP 401 for customer, installation, contract, and license collection reads.
- Asserts anonymous PUT/status requests receive HTTP 401 for the six mutation routes.

## Verification status

- **Not yet run locally:** no repository shell/build execution is available in this connector workflow.
- **CI required:** verify compilation and the test results before merge.
- These tests establish only the anonymous access boundary. They are not a substitute for authenticated HTTP/SQL Server coverage.

## Remaining acceptance criteria

- Successful updates for all four resource types with current RowVersion.
- Missing/malformed RowVersion => 400; stale RowVersion => 409 with no persistence.
- Missing/invalid anti-forgery token rejected for authenticated mutation requests.
- Anonymous and insufficient-permission denial, while retaining production authorization handlers.
- License contract/installation/customer relationship validation.
- Contract/license Draft-only edit policy and allowed operational status transitions.
- Audit only after successful writes and rollback when audit append fails.
- Duplicate unique-key race => stable 409 for SQL Server errors 2601/2627.
- Disposable SQL Server DB isolation and deterministic cleanup under parallel CI.
- CI green and documentation updated with observed evidence.
