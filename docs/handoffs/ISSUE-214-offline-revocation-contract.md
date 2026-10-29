# Handoff — ISSUE-214 offline revocation contract

- **Issue:** [#214](https://github.com/hamidreza1394/Accounting/issues/214)
- **Agent/owner:** ChatGPT GitHub workflow
- **Date:** 2026-10-29
- **Status:** Implemented in branch; CI verification pending

## Goal and acceptance criteria

- [x] Define a versioned, signed offline revocation snapshot envelope and payload.
- [x] Verify exact bounded UTF-8 bytes with ECDSA P-256/SHA-256 before parsing JSON.
- [x] Reject unknown schema, malformed/noncanonical payload, invalid key, invalid signature, expired list, duplicate/unsorted IDs, excessive validity and replayed/non-increasing sequence.
- [x] Keep signer implementation unavailable/fail-closed; no private key, KMS adapter, customer DB, or `Server/**` changes.

## Contract

`Licensing.Contracts/OfflineRevocationList.cs` defines the snapshot payload and envelope. The key ID is a lookup hint only: callers must resolve the public key from independent deployment trust. The validator is intentionally provider-neutral and does not sign, persist the highest accepted sequence, or enforce revocation inside a customer installation.

## Test coverage added

- Valid signed list with sequence advancement.
- Tampering rejected before parsing.
- Expired snapshot rejected.
- Replayed/rolled-back sequence rejected.
- Duplicate or non-ordinal-sorted license identifiers rejected.
- Missing trusted key and wrong-curve public key rejected.

## Limitations / separate work

- A customer-side verifier must persist the highest accepted sequence atomically and fail closed on rollback; that persistence and enforcement must be reviewed in a separate PR.
- The real signer remains blocked by the provider decision in Issue #236.
- No claim is made that offline customer installations are revoked by Vendor status changes alone.

## Verification

| Check | Result |
|---|---|
| Local `dotnet test` | Not run in this environment |
| GitHub CI | Pending for final branch SHA |
