# Handoff — Issue #391: Runtime Entitlement Enforcement

**Date:** 2026-11-04 (updated)
**Branch:** `feat/issue-391-runtime-entitlement-enforcement`
**Base:** `main` @ `9966eb6dc79d40589b993f05acf99ff8dfd4786e`
**Latest commits on branch:**
- `91b037d` — pure `EntitlementPolicyEngine`
- `664fc921` — unit tests for company / module / feature / seat / legacy

**Depends on:**
- Contract V2 (#390 / PR #472) — Implemented on main
- Runtime Evaluator base (#526) — Merged
- Customer license import/persistence (#384) — still required for real envelopes in production paths

## Goal
Enforce commercial entitlements at trusted server-side operation boundaries. UI-only checks are insufficient.

## What landed in this PR slice (pure decision layer)

### `Licensing.Contracts/EntitlementPolicyEngine.cs`
Pure, side-effect-free static engine. Callers **must** already have validated the envelope via `ICustomerLicenseRuntimeEvaluator` / revocation-aware validators. This engine never grants rights on missing or ambiguous data (fail-closed).

| Method | Behaviour |
|--------|-----------|
| `ResolveCompany` | Returns the v2 `CompanyLicenseEntitlement` for an opaque company reference, or null |
| `EvaluateCompany` | Deny if company not in v2 list (`company_not_entitled`) |
| `EvaluateModule` | Deny unknown code, missing company, or module not listed; accepts `TRADE_IMPORT` only when listed |
| `EvaluateFeature` | Requires owning module + matching feature entry + type-correct value; max_count and enabled supported |
| `EvaluateSeatCapacity` | block-new: `active >= MaxUsers` → `seat_limit_reached`; MaxUsers ≤ 0 → deny |
| `EvaluateLegacySubsystem` | v1 installation-wide allowlist only; never grants Trade |

### Tests (`EntitlementPolicyEngineTests.cs`)
Coverage for:
- company present / absent / v1 payload
- TRADE_IMPORT only when listed
- unknown module code
- feature enabled true/false, max_count exceeded, owning-module required
- seat under / at limit / zero max
- legacy subsystem allow + Trade not granted via legacy path

**Local .NET execution of these tests has not been performed in the agent environment.** CI on the PR head is required before any Verified claim.

## Still missing (next commits / follow-up)
1. Runtime service that:
   - Calls `ICustomerLicenseRuntimeEvaluator` for installation usability
   - Obtains the validated `LicensePayload` (may require a small, careful extension of the evaluator result)
   - Resolves the current Company reference from **server** WorkContext / CompanyAccessService (never client-supplied)
   - Counts active human memberships for seat checks
   - Surfaces stable API error codes without leaking license material
2. Wire seat check into membership creation / reactivation paths (block-new).
3. Wire module/feature checks into application services and API filters that need them.
4. Replace Trade `DenyAllTradeModuleEntitlement` with a real implementation that uses this engine (owned by #403 once policy is proven).
5. Integration tests against SQL Server for the full persistence → evaluator → policy path.
6. Cache policy (only if invalidation on import/revocation/amendment is solid).

## Explicit non-goals of the current slice
- No Server filter or membership mutation yet
- No migration
- No change to Trade DI (still DenyAll)
- No claim of “Runtime Enforcement complete”

## Recommendation
- Merge or CI-verify this pure-engine slice first.
- Next commit on the same branch: thin `IEntitlementPolicy` service + evaluator composition + membership seat counter.
- Only after that, begin endpoint wiring and Trade gate replacement under #403.

## Status
- Pure decision engine + unit tests: **Implemented** (awaiting CI)
- Full runtime enforcement + production call sites: **NotImplemented**
