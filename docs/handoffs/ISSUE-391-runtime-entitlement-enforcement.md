# Handoff — Issue #391: Runtime Entitlement Enforcement

**Date:** 2026-11-04
**Branch:** `feat/issue-391-runtime-entitlement-enforcement`
**Base:** `main` @ `9966eb6dc79d40589b993f05acf99ff8dfd4786e`
**Depends on:**
- Contract V2 (#390 / PR #472) — Implemented (verification PR pending)
- Runtime Evaluator base (#526) — Merged
- Customer license import/persistence (#384) — still required for real envelopes

## Goal (from Issue + D-059)
Enforce commercial entitlements at trusted server-side operation boundaries for both Cloud and Dedicated/Offline deployments. UI-only checks are insufficient.

Required checks:
1. Installation Entitlement (overall usable license via existing evaluator)
2. Company-scoped Entitlement
3. Seat / User Capacity (active human memberships per Company; block-new on reduction)
4. Module Entitlement
5. Feature Entitlement

## Current real data path (code inspection 2026-11-04)

```
License Import / Persistence (#384)
        ↓
InstalledLicenses table (StateCurrent + EnvelopeJson)
        ↓
ICustomerLicenseRuntimeEvaluator.EvaluateCurrentAsync(installationId)
        ↓
RevocationAwareLicenseEnvelopeValidator (signature + offline revocation + trust)
        ↓
LicenseValidationResult (Status + Payload)
```

**What exists today**
- `CustomerLicenseRuntimeEvaluator` only answers “is the current installed license usable at all?” (Valid / ExpiringSoon / Restricted vs anything else). Fail-closed on missing, malformed, revoked, TPM/trust failure, cancellation preserved.
- Payload V2 parsing and `CompanyLicenseEntitlement` exist in `Licensing.Contracts`.
- `LicenseSubsystemCodes.IsAllowedCompanyModule` correctly allows `TRADE_IMPORT` only for company-scoped checks.
- Trade currently injects `DenyAllTradeModuleEntitlement` (correct fail-closed stub).

**What does NOT exist yet**
- Any service that takes a validated Payload V2 + current CompanyId (from server context) and answers:
  - Does this Company appear in the entitlement list?
  - Is module X enabled for this Company?
  - Is feature Y allowed / within limit?
  - Current active seat count vs maxUsers (block-new policy)?
- Central policy boundary used by API filters / application services.
- Cache with proper invalidation on import / revocation / amendment.
- Integration of the above into membership creation, module routes, feature-limited operations.

## Mandatory design constraints (must not be violated)

1. **Reuse existing evaluator** — never build a parallel Runtime Evaluator. Extend or compose on top of `ICustomerLicenseRuntimeEvaluator`.
2. **Company / Tenant from server context only** — use `CompanyAccessService` / WorkContext; never trust client-supplied CompanyId or TenantId.
3. **Fail-closed** — missing payload, parse error, evaluator rejection, unknown company, missing module/feature → deny. No silent grant.
4. **Seat policy = block-new** — never deactivate existing memberships; only block new activation / creation when over limit. Expose over-limit state to authorized admins.
5. **No UI-only enforcement** — every real API / application-service path that creates seats, enables modules or uses features must call the policy.
6. **Cache only with valid invalidation** — revocation or new amendment must not be hidden behind stale cache.
7. **API error contract** — distinct but non-sensitive codes for Entitlement vs Permission failures; never leak license material, signatures or full payload.
8. **No new migration** unless absolutely required and reviewed; prefer reading the already-persisted EnvelopeJson.
9. **Trade** will consume the same policy via a real `ITradeModuleEntitlement` implementation once this issue lands (currently DenyAll).

## Proposed surface (to be implemented on this branch or follow-up commits)

```csharp
public interface IEntitlementPolicy
{
    Task<EntitlementDecision> EvaluateInstallationAsync(CancellationToken ct);
    Task<EntitlementDecision> EvaluateCompanyAsync(Guid companyId, CancellationToken ct);
    Task<EntitlementDecision> EvaluateModuleAsync(Guid companyId, string moduleCode, CancellationToken ct);
    Task<EntitlementDecision> EvaluateFeatureAsync(Guid companyId, string featureCode, CancellationToken ct);
    Task<SeatCapacityDecision> EvaluateSeatCapacityAsync(Guid companyId, CancellationToken ct);
}
```

- `EvaluateInstallationAsync` → thin wrapper over existing `ICustomerLicenseRuntimeEvaluator`.
- Company / Module / Feature / Seat decisions parse the already-validated Payload (v1 legacy vs v2) and apply the rules above.
- Seat count = number of active human memberships for that Company (person in two companies consumes two seats).

## Test matrix that must pass before any “complete” claim

- Installation usable / missing / revoked / expired / Restricted
- Company present / absent in entitlement
- Seat at limit / over limit (block-new) / under limit
- Module enabled / disabled / unknown code
- Feature allowed / denied / limit exceeded
- Client-supplied CompanyId / TenantId ignored (server context wins)
- Concurrent membership creation race (transaction / serializable or equivalent)
- Missing or corrupt EnvelopeJson → deny
- Evaluator throws / returns non-success → deny
- Integration tests against real SQL Server for persistence + enforcement path

## Explicit non-goals for the first slice
- Startup / license-first gate (#396)
- Vendor amendment issuance (#393)
- Full online/offline sync (#394)
- Changing Trade permission catalog
- Any production-ready cache or telemetry

## Current blocker / dependency note
Until #384 reliably persists a valid signed envelope for the local InstallationId, the evaluator (and therefore any policy built on it) will correctly return fail-closed for almost all real deployments. Independent unit tests with synthetic envelopes can still be written and should be the first deliverable.

## Recommendation
1. Finish / merge the #390 verification PR (docs only).
2. On this branch implement `IEntitlementPolicy` + unit tests with synthetic V1/V2 payloads first.
3. Wire seat check into membership creation path.
4. Replace `DenyAllTradeModuleEntitlement` only after the policy is proven (that becomes part of #403).
5. Do not claim “Runtime Enforcement complete” until the test matrix above has real CI evidence on SQL Server.

## Status of this handoff
- Design + gap analysis only.
- No production code change yet.
- No claim of Implemented or Verified.
