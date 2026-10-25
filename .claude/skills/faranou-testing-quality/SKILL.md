---
name: faranou-testing-quality
description: Use for test strategy, CI failures, regression investigation, dependency checks, build verification, migration validation, or release readiness in Faranou.
---

# Faranou Testing and Quality

## Start with evidence
Read `AGENTS.md`, `docs/TESTING.md`, `docs/DEVELOPMENT.md`, `docs/SECURITY.md`, relevant architecture/DB docs, and the Issue. Inspect workflow definitions, project files, lockfiles, and recent CI runs. Do not infer current CI status from an old run.

## Test selection
- Unit: isolated Domain/Application rules, validation, deterministic calculations.
- Integration: HTTP/API, authorization, anti-forgery, DI, EF Core, SQL Server, migrations and persistence.
- E2E: critical login, company/period selection, setup, and other important user journeys.
- Frontend: TypeScript typecheck, Vitest/Testing Library, production build, offline asset check, and Playwright only if the repo's existing E2E setup supports it or a reviewed change adds it.
- Security: negative tests for invalid input, unauthorized access, cross-company attempts, session behavior and secret leakage.

## Canonical checks
Use the exact commands in `AGENTS.md` and project scripts. Common checks include:
- `dotnet restore Template.sln`
- `dotnet build Template.sln --no-restore --configuration Release`
- `dotnet test Template.sln --no-build --configuration Release`
- `python3 scripts/check_static_assets.py`
- In `ClientApp`: `npm ci`, `npm run typecheck`, `npm test`, `npm run build`, `npm run check:offline`
Only execute checks when the environment and dependencies are available. Never install packages, start containers, modify databases, or trigger/rerun GitHub Actions without authorization for that operation.

## Database and migration checks
Review migration diffs, snapshot/model parity, precision/nullability/indexes/constraints, rollback and deployment ordering. Test against SQL Server when SQL Server semantics are material; EF InMemory is not a substitute for relational behavior. Never claim SQL Server or E2E validation from unit tests alone.

## CI triage
Distinguish workflow not triggered, job blocked before start (billing/permissions/runner), queued, cancelled, failed in a step, and passed. A job that never started says nothing about the code's build correctness. Check runner trust boundaries, especially public repositories and pull requests from forks; do not execute untrusted PR code on a privileged self-hosted runner.

## Report format
For every command list exact command, exit/result, environment, and whether it was actually executed. List skipped checks with reasons. Separate pass/fail/blocked/not-run. Do not suppress warnings, tests, analyzers, or security controls to get green status.
