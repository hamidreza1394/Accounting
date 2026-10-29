# Issue #235 — Vendor acknowledgement intake implementation plan

Status: design handoff only; no runtime behavior or database schema is changed by this document.

## Current verified boundary

- The signed acknowledgement contract is in `Licensing.Contracts/CustomerProvisioningAcknowledgement.cs`.
- Its verifier checks the signature over the exact bounded UTF-8 bytes before parsing JSON, then enforces canonical representation and binds request ID, nonce, payload hash, customer reference, installation ID, result and timestamp.
- `VendorProvisioningEndpoints` can create, approve and export a signed package, but has no acknowledgement intake route.
- `VendorProvisioningRequest` already has `ConfirmedAtUtc`, `FailureCode`, `DeliveredAtUtc` and SQL Server `rowversion`; it does not persist acknowledgement digest/key metadata.
- The default provisioning signer is deliberately unavailable/fail-closed. This work must not add a software-key fallback.

## Required endpoint shape

Implement in a separate code PR against Issue #235:

`POST /api/vendor/provisioning-requests/{id}/acknowledgement`

The route is machine-to-machine and must not depend on a Vendor administrator's browser cookie or antiforgery token. The acknowledgement's ECDSA signature is the proof of origin only when the key is resolved from an independently configured trusted-key store. `KeyId` in the submitted envelope is only a lookup hint and must never establish trust by itself.

Before accepting a request:
1. Enforce a small HTTP request-body limit and strict content type; apply an explicit per-IP/per-key rate limit and generic failure responses.
2. Load the Vendor request and its customer/installation binding; reject unknown IDs without leaking cross-customer metadata.
3. Resolve the key ID in an independently configured acknowledgement trust store. Reject missing/ambiguous/revoked keys; do not read a key from the submitted package or request body.
4. Parse the original persisted payload only after its integrity hash has been recomputed and checked. Derive expected customer reference and installation code from that persisted payload and the Vendor records, not from caller-supplied values.
5. Call `CustomerProvisioningAcknowledgementValidator.TryValidateAndVerify` with the trusted public key and the expected request ID, nonce, payload hash, customer reference and installation ID.
6. Require the package to have been delivered before processing an acknowledgement. Exporting a package alone must never set `Confirmed`.

## Persistence and state transitions

Add an additive Vendor-only migration and update the EF model snapshot in the same PR. Persist at minimum:
- SHA-256 digest of the exact canonical acknowledgement JSON plus signature/envelope identity needed for safe retry comparison;
- trusted acknowledgement key ID and accepted timestamp.

Do not persist credentials or customer business payload in the acknowledgement fields.

Use a serializable transaction or an equivalent concurrency-safe conditional update that checks the expected rowversion/status. Add a unique database constraint for the acknowledgement identity/digest where appropriate. Define and test these transitions:
- `PendingDelivery` + valid `Applied` or `AlreadyApplied` → `Confirmed`, with `ConfirmedAtUtc` set only after the verified acknowledgement and audit record commit.
- `PendingDelivery` + valid `Rejected` → `Failed`, with a validated allow-listed error code.
- Exact same verified acknowledgement retried → return the original outcome without duplicate audit/state side effects.
- Different acknowledgement after a terminal outcome → conflict; never overwrite a confirmed/rejected outcome.
- Two concurrent submissions → exactly one state transition; the other returns the stable idempotent outcome or a conflict.
- Invalid signature, unknown/revoked key, binding mismatch, or undelivered package → no state mutation.

Audit only identifiers, outcome/error code, digest, and trace ID; never log the signature, full envelope, raw payload, secrets, or trusted key bytes. Ensure audit and state change are in the same transaction.

## Trust configuration

Introduce a narrow `IProvisioningAcknowledgementTrustStore` abstraction and configuration binding that fails closed when no trusted key is configured. Public keys must be supplied by deployment configuration/secret-management infrastructure independently of the incoming envelope. Validate key format and algorithm at startup or when loading the trust set. Support key rotation with explicit active/revoked state and tests. Do not silently pick a cloud KMS/HSM vendor; that decision belongs to the separately tracked production signer issue #236.

## Tests required before merge

- HTTP integration: unauthenticated machine client can submit a valid signed acknowledgement without browser antiforgery, but only the independently trusted key is accepted.
- Missing trust configuration fails closed; unknown, revoked, malformed and ambiguous key IDs are rejected.
- Invalid signature, canonical-byte tampering, wrong request ID/nonce/hash/customer/installation, future timestamp, rejected result without error code, and acknowledgement before delivery are rejected without mutation.
- Applied, AlreadyApplied and Rejected transitions persist expected status/timestamps and an audit event atomically.
- Exact retry is idempotent; conflicting terminal acknowledgement fails; concurrent duplicate requests do not double-apply.
- SQL Server integration tests exercise rowversion/unique constraints and rollback when audit persistence fails.
- Run Vendor CI and .NET CI, including migration snapshot guard and E2E; merge only after all required checks pass.

## Scope boundaries

- This intake PR must not implement the customer-side provisioning consumer or modify the customer accounting database. That consumer and any customer schema change remain a separate, explicitly reviewed part of #235/#228.
- A signed acknowledgement proves the customer-side signer identity, not that the customer system is uncompromised. Customer-side provisioning still must verify the package, atomically claim request ID/nonce, provision transactionally, audit the outcome, and sign the acknowledgement.
- This document does not claim the flow is implemented or production-ready.
