# Handoff — ISSUE-214 offline revocation contract

- **Issue:** [#214](https://github.com/hamidreza1394/Accounting/issues/214)
- **Date reviewed:** 2026-10-30
- **Main baseline:** `43d28253eb9f0f38ff25d0dfea143aeacdb06871`
- **Status:** signed snapshot contract and validator are merged and CI-verified in main; customer-side durable anti-rollback persistence/enforcement and Production signer remain incomplete.

## Merged implementation

- **PR #292** merged as `a3690a567c692292d899bc26f968a093b7ba20cb`.
- Added `Licensing.Contracts/OfflineRevocationList.cs` with versioned payload/envelope, bounded canonical JSON, exact UTF-8 signature verification, ECDSA P-256/SHA-256 and independently configured public-key trust.
- Rejects malformed/noncanonical payloads, unknown schema/algorithm, invalid or untrusted key, invalid signature, expired or excessively long validity, duplicate/unsorted license IDs and non-increasing sequence.
- No private key, KMS/HSM adapter, customer DB migration or runtime revocation enforcement was introduced.

## CI evidence on PR #292 head

- .NET CI [40864326688](https://github.com/hamidreza1394/Accounting/actions/runs/40864326688) — success.
- Vendor CI [40864326665](https://github.com/hamidreza1394/Accounting/actions/runs/40864326665) — success.
- Semgrep [40864326732](https://github.com/hamidreza1394/Accounting/actions/runs/40864326732) — success.

## Remaining acceptance criteria

1. Customer-side verifier must resolve `KeyId` only through an independent trusted public-key store.
2. Persist the highest accepted sequence and snapshot digest atomically in the customer installation; reject replay/rollback after restart and backup restore where the durable trust boundary permits.
3. Apply accepted revocation state to the actual installed-license evaluation/enforcement path. A validator that returns a list without durable storage or enforcement is not a complete consumer.
4. Define recovery behavior when database state is restored from an older backup; do not claim rollback resistance if the high-water state is restored with it.
5. Exercise SQL Server persistence, concurrent update, rollback, restart, backup/restore, key rotation and offline installation scenarios.
6. Keep Production signing unavailable/fail-closed until the approved external signer in #236 is configured.

## Architecture and limitations

- The current repository has the signed snapshot contract, but the customer-side L3 installed-license tables/evaluation path are not yet present as an integrated runtime feature. Do not bolt revocation state onto unrelated provisioning claims.
- A DB-only high-water mark cannot detect restoration of an older full database backup unless its trust anchor is held in a separate protected failure domain or refreshed by a trusted online source. The threat model must state this limit explicitly.
- A Vendor status change does not immediately revoke an artifact already held by a fully offline customer.
- KMS/HSM selection, workload identity, key custody, rotation and recovery remain #236.
- Independent Vendor audit checkpoint storage remains #107.

## Verification boundaries

- PR #292 CI verified the contract and tests; this handoff did not run local .NET tests.
- No customer-side durable consumer, production signer, real offline revocation or backup/restore drill is claimed complete.
