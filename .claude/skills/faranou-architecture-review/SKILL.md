---
name: faranou-architecture-review
description: Use before architecture changes, API or layer refactors, new infrastructure, major dependency choices, and repository-wide engineering reviews.
---

# Faranou Architecture Review

## Required inputs
Read `AGENTS.md`, `docs/INDEX.md`, `docs/ARCHITECTURE.md`, `docs/DECISIONS.md`, `docs/PROJECT_STATUS.md`, `docs/ROADMAP.md`, and the relevant Issue/ADR/handoff before proposing a design. Verify their claims against the target commit.

## Review method
1. Describe current behavior and boundaries from source code, not assumptions.
2. Separate accepted decisions from proposed designs and implemented behavior.
3. Trace dependencies, API/data contracts, callers, migrations, deployment modes, offline operation, and backwards compatibility.
4. Compare options by correctness, complexity, security, operability, performance, maintainability, cost, and reversibility.
5. Prefer a modular monolith and incremental migration. Do not introduce microservices, generic repositories, MediatR, event buses, extra projects, or patterns without a concrete requirement.
6. Keep Domain independent of Web/Infrastructure; respect the current staged state rather than claiming Clean Architecture is already complete.
7. Identify decision records and docs that would need updating; never silently rewrite an accepted ADR to match code.
8. For material or ambiguous choices, provide options, trade-offs, recommended choice, acceptance criteria, migration/rollback plan, and unresolved decisions; wait for approval before implementation.

## Required output
- Evidence and current-state diagram/boundaries.
- Invariants and affected contracts.
- Options with pros/cons and risks.
- Recommendation and why rejected alternatives are weaker.
- Files/components likely affected.
- Verification plan and rollback considerations.
- Clear status: Proposed / NeedsApproval / Implemented / Verified.

Never equate an architectural pattern with quality by itself. Optimize for the actual accounting requirements and the repository's accepted decisions.
