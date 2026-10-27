# Issue #176 — Vendor business HTTP integration coverage

## Current increment (2026-10-27)

### Initial anonymous-boundary coverage — merged PR #177
- Exercises the production ASP.NET Core host with `WebApplicationFactory<Program>`.
- Anonymous requests to Vendor customer, installation, contract, and license reads and mutation routes are rejected with HTTP 401.
- CI passed for merged head `1c24c019f4d984524440620aba5ec66bb22a0499`; workflow run: https://github.com/hamidreza1394/Accounting/actions/runs/40487770454.

### Authenticated customer update coverage — merged PR #178
Branch `test/vendor-customer-mutation-http` was merged as `63681bc4119f347e706620f8541a571df3b3d7ee`. The first test run failed because the pre-login anti-forgery token was reused after MFA. The fix refreshes the token after MFA. However, the post-fix CI runs were cancelled during/after the merge, so this test still needs a fresh successful CI execution.

### Expanded authenticated mutation coverage — branch `test/vendor-business-mutation-http-coverage`
File: `tests/Vendor.Host.Tests/VendorCustomerMutationHttpIntegrationTests.cs`

The test creates a uniquely named disposable Vendor SQL Server database, applies Vendor migrations, creates an enabled administrator with TOTP MFA and production management permissions for customers, installations, contracts, and licenses, and uses the real HTTP password + TOTP flow.

It asserts:
- missing anti-forgery token => HTTP 400;
- invalid anti-forgery token => HTTP 400;
- malformed RowVersion => HTTP 400 with `invalid_row_version`;
- valid update => HTTP 200;
- replayed old RowVersion => HTTP 409 with `stale_row_version`;
- updated database value persists and exactly one matching customer-update audit event exists;
- installation and Draft contract updates succeed with the current RowVersion and reject stale replay with HTTP 409;
- a license referencing a contract from another customer is rejected with `contract_customer_mismatch`;
- a license with a valid contract but an installation from another customer is rejected with `installation_customer_mismatch`;
- a valid license metadata update succeeds, stale replay is rejected, and exactly one audit event is recorded for each successful installation/contract/license update.

The test uses a generated Vendor-only database name and drops only that database during cleanup. It never uses customer accounting database configuration.

## Verification status

- First Vendor CI attempt (`40489907527`) failed in the authenticated customer update test (68 passed, 1 failed). The log showed that the anonymous anti-forgery request token was reused after the TOTP login changed the claims identity; ASP.NET Core rejected it before RowVersion validation.
- Fix committed as `86828e36ee2f3d9c473ef7fb8df01fca0dc24b93`: fetch a fresh anti-forgery token after successful MFA.
- PR #178 was merged as `63681bc4119f347e706620f8541a571df3b3d7ee` before a successful post-fix CI result was observed; the subsequent runs were cancelled, so merged-code verification remains outstanding.
- Current branch adds HTTP/SQL Server coverage for installation, contract, and license updates, cross-customer contract mismatch, stale RowVersion replay, and audit counts.
- **CI after these changes is not yet confirmed.** No local build/test has been run from this connector-only workflow.

## Remaining acceptance criteria

- Equivalent successful/stale/malformed RowVersion coverage for installation, contract, and license.
- Cross-resource mismatch validation for license contract/installation links.
- Draft-only contract/license edits and Active↔Suspended status transitions.
- Audit failure rolls back the business mutation.
- Deterministic SQL Server duplicate-key race coverage for SQL errors 2601/2627.
- Update this document with observed CI evidence after validation.


## RowVersion no-op replay fix (pending CI)

- The integration run exposed a real edge case: replaying an old RowVersion with an identical request body could return `200 OK` because EF Core can skip issuing SQL `UPDATE` when no property value changes. SQL Server's rowversion predicate is therefore not evaluated for that no-op.
- Commit `7bc21cf62dc2e1076353f652f724ec6ca4ab678a` adds an explicit comparison between the submitted token and the currently loaded rowversion in all six business update/status handlers. A stale token returns `409 stale_row_version` even for an identical payload. The database concurrency check remains in place to protect the race between the pre-check and `SaveChanges`.
- Documentation commit: `fd6a038961e8bcb6af534ac4f0065251ef39d271`.
- CI must pass before considering this fix verified; no local SQL Server execution was available here.


## Operational status HTTP coverage increment (2026-10-27)

- PR #179 was merged to `main` as part of merge commit `b425fd06856da30f187836b4c1b2de547921369a`.
- Added a regression path in `VendorCustomerMutationHttpIntegrationTests` for customer status transitions through the production HTTP host: `Active -> Suspended -> Active` must succeed using the fresh SQL Server RowVersion returned by each response; `Active -> Draft` must return HTTP 409 with `invalid_status_transition`.
- This increment is on branch `test/vendor-operational-status-http-coverage`. No local .NET execution was available through this connector; the new test is not considered verified until Vendor CI completes and proves the SQL Server integration test was executed (not skipped).
- Remaining coverage: installation status transition checks, audit-writer failure rollback, deterministic duplicate-key race tests for SQL Server 2601/2627, and the post-fix CI evidence for PR #178 / merged PR #179.
