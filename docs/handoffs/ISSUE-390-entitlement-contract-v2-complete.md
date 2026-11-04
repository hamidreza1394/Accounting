# Handoff — Issue #390: Entitlement Contract V2 Completion

**Date:** 2026-11-04
**Branch:** `feat/issue-390-entitlement-contract-v2-complete`
**Base:** `main` @ `9966eb6dc79d40589b993f05acf99ff8dfd4786e`
**Related PR (implementation):** #472 (merged 2026-11-03)

## Summary

The contract-only slice of Issue #390 was delivered and merged via PR #472. This handoff records verification against acceptance criteria, consumer readiness, and remaining scope boundaries. No new schema, migration, Server persistence, or Vendor issuance code is introduced here.

## Acceptance criteria mapping (from Issue #390 + D-059 + comments)

| Criterion | Status | Evidence |
|-----------|--------|----------|
| Versioned, backward-compatible entitlement payload | Implemented | Payload v1 retained (installation-wide modules); Payload v2 adds Installation maxCompanies + Company-scoped modules/maxUsers/typed features |
| Installation identity + monotonic sequence | Implemented | LicenseRevisionRules + pure amendment successor rules (stable LicenseId/InstallationId/ProductCode, strictly increasing sequence) |
| Company identity/reference + company-specific entitlement | Implemented | CompanyLicenseEntitlement in v2 |
| Modules, granular features, user seats per Company | Implemented | v2 schema + LicenseFeatureCatalog + LicenseSubsystemCodes.IsAllowedCompanyModule |
| TRADE_IMPORT exact commercial code only in v2 Company scope | Implemented | LicenseSubsystemCodes.TradeImport; IsAllowedCompanyModule; v1 cannot implicitly grant Trade |
| Independent supportUntilUtc / updatesUntilUtc | Implemented | Present in v2 payload |
| Strict reject unknown v2 JSON properties + non-canonical bytes after signature | Implemented | CanonicalJson + validators |
| Regression coverage (v1 compat, tampering, canonicalization, unknown fields/codes, type confusion, duplicates, bounds, replay/downgrade) | Implemented | Tests accompanying #472 |
| No customer migration / Server persistence / Vendor issuance | Honored | Explicit scope of #472; enforcement deferred to #391 |
| Docs / data-model / workflow / threat / traceability updates | Present in #472 | Further status alignment in this PR |

## Consumers of V2 contract

- `LicenseEnvelopeValidator` / `RevocationAwareLicenseEnvelopeValidator` / `CustomerLicenseRuntimeEvaluator` (#526) correctly route through versioned parsing.
- `LicenseSubsystemCodes.IsAllowedCompanyModule` is the canonical gate for company-scoped modules including TRADE_IMPORT.
- Trade module identity (`TradeModuleIdentity.ModuleCode = "TRADE_IMPORT"`) aligns with the signed payload code.
- No silent fallback that would grant Trade from a v1 payload.

## Remaining / out of scope (explicit)

- Runtime Enforcement of seats / modules / features / installation binding at operation boundaries → **#391**
- Customer license import / persistence completeness → **#384**
- Vendor add-on issuance / amendment generation → **#393**
- Startup / license-first gate → **#396**
- Trade route gating wiring to real entitlement (currently DenyAll) → **#403** (depends on this contract + #391)

## Verification performed

- Code inspection of `Licensing.Contracts` (LicensePayload, CompanyLicenseEntitlement, LicenseSubsystemCodes, CanonicalJson, revision rules, validators).
- Cross-check with D-059, LICENSING_IMPLEMENTATION_PLAN, TRADE_MODULE.md, REQUIREMENTS_TRACEABILITY.
- Confirmed PR #472 merge commit is on main and CI passed on that PR.
- No local .NET 10 SDK / SQL Server execution in this agent run; unit tests from #472 are relied upon for contract correctness. Full CI on this verification branch is required before any claim of Verified.

## Recommendation

- Contract V2 portion of #390 is **Implemented** and ready for consumption by #391 / #403.
- Keep Issue #390 open until a short verification window on main + any final doc/traceability polish is confirmed by owner; do **not** auto-close.
- Proceed to #391 using the existing Runtime Evaluator infrastructure (#526) and this contract.

## Files touched in this handoff PR

- This handoff document.
- Append-only entry in `docs/WORK_LOG.md`.
- Status note in `docs/PROJECT_STATUS.md` (licensing contract section).
