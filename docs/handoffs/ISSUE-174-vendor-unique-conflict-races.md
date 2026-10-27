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
