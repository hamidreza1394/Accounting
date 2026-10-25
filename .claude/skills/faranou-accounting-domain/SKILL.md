---
name: faranou-accounting-domain
description: Use when planning or implementing accounting entries, chart of accounts, journals, fiscal periods, financial reports, money calculations, or accounting controls.
---

# Faranou Accounting Domain

## Required discovery
Read `AGENTS.md`, product requirements, requirements traceability, database and architecture docs, fiscal-period/company-group decisions, and any accounting-specific ADRs. Inspect current Domain/Application models and tests. The presence of a roadmap item is not evidence that an accounting module exists.

## Domain invariants to confirm with product requirements
- A posted journal entry must balance: total debits equal total credits under the defined currency and rounding rules.
- Money must use decimal types and explicit precision/rounding rules, never binary floating point by default.
- Define document lifecycle, numbering, correction/reversal, approval, period locking/closing, audit trail, and concurrency semantics before implementation.
- Distinguish accounting company, customer account, installation, company group, fiscal period, and license entitlement according to accepted ADRs.
- Define calendar/display conventions (including Jalali display if required) separately from storage and period-boundary logic; never assume behavior that has not been specified.
- Financial reports need documented inclusion rules, date boundaries, currency treatment, and reproducible results.
- Any cross-company aggregation/consolidation must respect explicit permissions, group relationships, eliminations and reporting rules; do not equate simple aggregation with formal consolidation.

## Safe implementation approach
1. Translate confirmed business rules into acceptance criteria and examples.
2. Model invariants in testable Domain/Application logic, with database constraints where appropriate.
3. Test boundary values, rounding, reversals, closed periods, duplicate numbers, concurrent posting and unauthorized cross-company access.
4. Preserve immutable/auditable history where required by the approved accounting policy.
5. Inspect migrations and SQL Server semantics; plan backfill, rollback and deployment ordering.
6. Ask the product owner when accounting policy is ambiguous. Do not invent local regulatory requirements or silently select accounting conventions.

## Scope control
Do not create ledger tables, journal APIs, accounting reports or migrations merely because this skill exists. The current repository status and explicit Issue govern whether a feature is approved. Never state that accounting functionality is implemented without source and test evidence.
