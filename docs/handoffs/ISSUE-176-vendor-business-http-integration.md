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
