# Handoff Issue #174 — deterministic unique-key conflicts in Vendor APIs

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/174
- **Branch:** `fix/vendor-unique-conflict-races`
- **Status:** Implementation committed; CI verification pending.

## Problem
The preflight `AnyAsync` duplicate check improves error clarity but cannot prevent two concurrent writes from both passing. SQL Server unique indexes are the final integrity boundary and may reject one write with error 2601 or 2627.

## Implementation
- `VendorBusinessEndpoints.cs` maps SQL Server unique-key errors (2601/2627 only) to HTTP 409 using stable per-resource error codes for customer number, installation code, contract number, and license number.
- Applied to create and update operations.
- `DbUpdateConcurrencyException` remains separately mapped to `stale_row_version`.
- Foreign-key, connectivity, timeout, and other unexpected database failures are not converted into duplicate conflicts.
- Failed writes return before audit append; the uncommitted transaction is disposed and rolled back.

## Verification and limitations
- CI has not yet been confirmed for this branch.
- A SQL Server concurrency integration test that forces two simultaneous writes against the same unique key remains follow-up work.
- No schema/migration or customer accounting database changes.


## Deterministic HTTP race coverage (2026-10-27)

- Added to `tests/Vendor.Host.Tests/VendorCustomerMutationHttpIntegrationTests.cs` on branch `test/vendor-unique-key-race-http`.
- A test-only EF Core command interceptor gates the two customer INSERT commands until both authenticated HTTP requests have passed the preflight duplicate check. This forces SQL Server's unique index—not request scheduling or the preflight query—to arbitrate the duplicate key.
- Acceptance assertions: exactly one request returns HTTP 201, the other returns HTTP 409 with `customer_number_exists`, exactly one customer persists, and exactly one matching create audit event exists.
- The gate/interceptor is registered only by the test WebApplicationFactory and is enabled only around the two race requests. No production registration, schema, or customer accounting database is changed.
- **Validation pending:** CI must compile the test and run it against SQL Server with `FARANO_REQUIRE_VENDOR_SQLSERVER=1`. If the interceptor does not observe both INSERT commands, the test intentionally times out/fails rather than silently passing.


## Race-gate assertion hardening (2026-10-27)

- Follow-up to merged PR #182: the HTTP 201/409 contract could also be satisfied by the preflight duplicate check if the interceptor's SQL text predicate did not match.
- The test now asserts that exactly two INSERT commands were observed by the synchronization gate. A mismatch can no longer silently pass as a successful race test.
- Evidence for PR #182's pre-hardening baseline: Vendor CI run [40531062507](https://github.com/hamidreza1394/Accounting/actions/runs/40531062507) passed with `FARANO_REQUIRE_VENDOR_SQLSERVER=1` and zero skipped tests; this follow-up requires its own CI run to verify the new assertion.
