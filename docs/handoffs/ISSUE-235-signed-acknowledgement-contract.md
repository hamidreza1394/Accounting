# Handoff — Issue #235: signed provisioning acknowledgement contract

- Date: 2026-10-29
- Branch: `feature/issue-235-signed-provisioning-ack-contract`
- Scope: shared contract only; no customer database migration, provisioning consumer, Vendor status transition, or production key configuration.

## Implemented in this slice

- Added a versioned acknowledgement DTO and canonical JSON serializer.
- Added strict envelope validation and ECDSA P-256 / SHA-256 signature verification over the exact bounded UTF-8 bytes before parsing JSON.
- Requires a caller-resolved trusted public key; the envelope's `KeyId` is only a lookup hint and does not establish trust.
- Validates canonical JSON, schema version, fixed-width SHA-256 payload hash, bounded identifiers, result allow-list, rejection error-code requirements, and a limited future timestamp skew.
- Binds acknowledgement to the original request ID, nonce, payload hash, customer reference, and installation ID.
- Added tests for valid acknowledgement, tampering, nonce/hash mismatch, unknown result, missing rejection code, and future-dated acknowledgement.

## Important boundary

This is not the customer-side provisioning consumer. It does not claim replay protection, atomic database provisioning, idempotent retries, signed-ack transport, or a Vendor `Confirmed` state transition. Those require a separate reviewed implementation in #235, including a unique database constraint and transaction/concurrency tests.

No private key or production signing configuration was added. The customer acknowledgement signing key must be managed by the customer's approved deployment trust configuration, independently of the acknowledgement envelope.

## Validation status

- Local build/tests were not run through the GitHub-only editing path used for this change.
- PR #287 was merged as commit `d74ca15a517ddbb72d8711b55b0379381ecc34dd`.
- Vendor CI run `40854670655`, .NET CI run `40854670549`, Semgrep run `40854670451`, and PR checks run `40854665623` all completed successfully.
- Post-merge main workflows `40855628545`, `40855628381`, `40855628365`, and `40855628192` were still in progress at the latest check; final main CI status remains pending.
