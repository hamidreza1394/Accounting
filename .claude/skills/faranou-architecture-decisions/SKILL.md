---
name: faranou-architecture-decisions
description: Use when an architectural or technology decision is proposed, accepted, rejected, deferred, or needs its rationale recorded in the Faranou repository.
metadata:
  origin: adapted-from-ECC-architecture-decision-records
---

# Faranou Architecture Decision Workflow

This skill adapts ECC's decision-record discipline to the repository's existing single index/document, `docs/DECISIONS.md`. Do not introduce a parallel ADR directory.

## Mandatory sources

Read `AGENTS.md`, `docs/INDEX.md`, `docs/DECISIONS.md`, `docs/ARCHITECTURE.md`, and the relevant Issue/handoff. Verify implementation claims against the current code and tests.

## When to record a decision

Record material choices that affect architecture boundaries, security/tenancy, authentication, persistence, migration/deployment, licensing, public API contracts, major dependencies, offline operation, or long-term compatibility. Routine local implementation details do not need an ADR.

## Required decision structure

Follow the existing format and vocabulary in `docs/DECISIONS.md`. Include:

- Stable next decision number; determine it from the current file rather than memory.
- Title, date, decision status, and implementation/evidence status as separate concepts.
- Context and constraints grounded in repository evidence.
- Decision and explicit scope.
- Alternatives considered, with correctness, complexity, security, performance, operability, cost, reversibility, and maintenance trade-offs where relevant.
- Consequences, risks, migration/rollback implications, and unresolved questions.
- Evidence links to code, tests, CI, Issues, and related decisions.

## Rules

- The next decision number must be read from the current `docs/DECISIONS.md`; never reuse or renumber an existing ID.
- Update the relevant index or cross-reference in `docs/INDEX.md` if the decision changes documentation navigation.
- A proposal is not an accepted decision. Do not implement `Proposed`, `NeedsApproval`, or `Deferred` items as though approved.
- Do not change an accepted decision silently to match an implementation. Record implementation drift and resolve it through the project governance process.
- Do not mark a behavior `Verified` unless the relevant check actually ran and its result is available.
- Do not create `docs/adr/`, a new decision registry, or duplicate records unless the repository owner explicitly approves a structural change.

## Required output

Provide the decision number/title, rationale, alternatives, consequences, linked evidence, implementation status, and follow-up documentation/test changes. If no decision has been made, label the recommendation as a proposal.
