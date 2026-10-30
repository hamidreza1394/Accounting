# Handoff — Issue #235: single-license provisioning payload v4

**Date:** 2026-10-30  
**Branch:** `feat/issue-235-single-license-v4`  
**Status:** Implemented in branch; CI and SQL Server validation pending; do not merge until required checks and owner review pass.

## Accepted product decision

See D-054 in `docs/DECISIONS.md`. The approved model is one provisioning request → one Vendor license → one signed license-envelope sequence.

1. Client input is `licenseId` and an optional subsystem subset only.
2. Vendor resolves entitlements from the latest cryptographically verified signed envelope for that license. The client cannot supply the authoritative entitlement list or sequence.
3. Binding requires the same customer; if the license has an InstallationId it must match the target installation; active customer and installation; Issued license; current time inside signed validity; trusted signature; exact license/installation/sequence binding.
4. New provisioning payload schema v4 adds `licenseId` and `sequence`. Canonical v1-v3 bytes must remain unchanged and the Customer verifier must accept v1-v4.
5. Revalidate at provisioning signature issuance and every package export. Idempotency includes licenseId and sequence.
6. No v3 backfill or inferred license binding.
7. MaxCompanies is explicitly NotImplemented. Adding modules to an existing company and combining multiple licenses are also NotImplemented.
8. Offline limitation: a customer that already received a package cannot discover a later suspension or a newer sequence without reconnecting.

## Implemented in this branch

- Contract payload record and canonical serializer include v4 fields only for schema v4.
- Payload validator accepts v1-v4, requires a canonical non-empty GUID licenseId and positive sequence for v4, and rejects license fields in v1-v3.
- Added a byte-for-byte canonical v3 regression test and v4 contract/signature tests.
- Added `LicenseSigningTrustStore`, using deployment-supplied public keys under `Licensing:TrustedSigningKeys`; no private key is loaded by the Vendor host.
- Added `VendorProvisioningLicenseResolver` to validate stored envelope digest, trusted ECDSA signature, payload structure, license/customer/installation binding, validity, status, and sequence; derive full entitlement or a requested subset.
- Added an eligible-licenses API for the Vendor UI.
- Provisioning creation now accepts a licenseId and optional subset; sequence and entitlements are resolved server-side.
- Revalidates the license before package signing and package export.
- Idempotency compares licenseId, sequence, effective subsystem list, and the rest of the provisioning request.
- Vendor UI selects an eligible signed license and shows only its verified entitlements.
- No database migration and no Customer DB schema change were introduced.
- Updated D-054, licensing workflow docs, licensing overview, index, and work log.

## Tests added/updated

- Licensing contract tests: v3 canonical bytes unchanged; v4 binding fields required and signature verified.
- Vendor request rules: license required, optional subsystem subset, licenseId/sequence included in idempotency.
- Vendor SQL Server resolver tests: signed entitlement derivation, subset acceptance/rejection, latest vs historical sequence, revoked signing key fail-closed, wrong customer/installation, suspended and expired licenses.
- Vendor SQL Server issuance test: a newer signed sequence between request creation and approval blocks package signing.
- Dual-host E2E (stacked PR #323): after first export, a newer signed sequence blocks re-export while the already-exported package remains applicable offline.
- Customer verifier fixture updated to use v4.

## Integration dependency: PR #323

PR #323 contains the independent-host E2E test updated for schema v4. It is stacked on this branch and:
PR #323 seeds a test license and independently signed envelope, calls the eligible-license API, submits only `licenseId` and optional subset, asserts schema v4 `licenseId`/`sequence`, exercises the independent Vendor/Customer hosts through signed acknowledgement and `Confirmed`, and verifies that a newer license sequence blocks a later export while the previously exported package remains usable offline.

Do not merge PR #317; it is the superseded v3 E2E. Close it only after PR #323 and owner review are complete. PR #318 separately fixes malformed/truncated request-body handling.

## Pending before merge

1. Run all .NET tests, Vendor Host SQL Server tests, and Vendor UI tests via CI; fix every failure.
2. Run the final CI on the latest PR #319 head and stacked PR #323 head; fix every remaining failure.
3. Review API contract and trust-key configuration documentation, including operational key rotation/revocation.
4. Merge PR #319 only after its own required checks are green; then retarget PR #323 to main and re-run both independent hosts through Confirmed.
5. Keep MaxCompanies, existing-company module additions, and multi-license aggregation explicitly NotImplemented.
6. Confirm no customer migration, unrelated schema change, or v3 backfill is present.
7. Merge only after all required CI jobs are green and owner review is complete.
