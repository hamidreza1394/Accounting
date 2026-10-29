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

- Local build/tests were not available through the GitHub-only editing path used for this change.
- Merge is blocked until required GitHub CI checks pass on this PR.
