# Third-party AI engineering skills

## Purpose and policy

Faranou selectively adapts engineering workflow skills from the official [Everything Claude Code (ECC)](https://github.com/affaan-m/ECC) repository. These are project-local Claude Code instructions; they are not application dependencies and do not execute by themselves.

Upstream source inspected: `affaan-m/ECC`, tree commit `90606c8315ee09cafc303515c5f33024db6b9882` (2026-10-25 audit snapshot). Upstream license: MIT; the notice is preserved in [`third_party/ECC-LICENSE.txt`](../third_party/ECC-LICENSE.txt). For exact upstream files, use the source links below at the pinned commit, not the moving `main` branch.

## Selected skills

| Local skill | Upstream source | Why retained / adaptation |
|---|---|---|
| `dotnet-patterns` | [ECC dotnet-patterns](https://github.com/affaan-m/ECC/blob/90606c8315ee09cafc303515c5f33024db6b9882/skills/dotnet-patterns/SKILL.md) | .NET/C# idioms; explicitly forbids unneeded generic repositories and respects the current .NET 10/EF Core 10 architecture. |
| `csharp-testing` | [ECC csharp-testing](https://github.com/affaan-m/ECC/blob/90606c8315ee09cafc303515c5f33024db6b9882/skills/csharp-testing/SKILL.md) | Testing workflow; bound to xUnit v3 and existing test infrastructure, without assuming extra assertion/mock/test-container packages. |
| `api-design` | [ECC api-design](https://github.com/affaan-m/ECC/blob/90606c8315ee09cafc303515c5f33024db6b9882/skills/api-design/SKILL.md) | API contracts and HTTP semantics; overrides generic token-based examples with Faranou's same-origin Cookie + anti-forgery and company authorization rules. |
| `react-patterns` | [ECC react-patterns](https://github.com/affaan-m/ECC/blob/90606c8315ee09cafc303515c5f33024db6b9882/skills/react-patterns/SKILL.md) | React component/state practices; excludes Next.js/RSC and unapproved new dependencies. |
| `react-testing` | [ECC react-testing](https://github.com/affaan-m/ECC/blob/90606c8315ee09cafc303515c5f33024db6b9882/skills/react-testing/SKILL.md) | Component-test patterns; binds to existing Vitest + React Testing Library and forbids adding MSW/Jest/axe without approval. |
| `e2e-testing` | [ECC e2e-testing](https://github.com/affaan-m/ECC/blob/90606c8315ee09cafc303515c5f33024db6b9882/skills/e2e-testing/SKILL.md) | Playwright practices adapted to the existing `tests/e2e` suite and real SQL Server setup. |
| `change-impact` | [ECC change-impact](https://github.com/affaan-m/ECC/blob/90606c8315ee09cafc303515c5f33024db6b9882/skills/change-impact/SKILL.md) | Read-only pre-change blast-radius analysis and post-change diff reconciliation. |
| `faranou-efcore-migrations` | Inspired by [ECC database-migrations](https://github.com/affaan-m/ECC/blob/90606c8315ee09cafc303515c5f33024db6b9882/skills/database-migrations/SKILL.md) | Rewritten for EF Core 10 + SQL Server and the commands/constraints already documented in `docs/database-migrations.md`; PostgreSQL/Prisma/Drizzle instructions were not imported. |
| `faranou-architecture-decisions` | Inspired by [ECC architecture-decision-records](https://github.com/affaan-m/ECC/blob/90606c8315ee09cafc303515c5f33024db6b9882/skills/architecture-decision-records/SKILL.md) | Rewritten to extend `docs/DECISIONS.md` and its existing D-number/status scheme; no parallel `docs/adr/` system. |

## Precedence and maintenance

1. `AGENTS.md`, `docs/INDEX.md`, accepted decisions in `docs/DECISIONS.md`, and the current issue define project constraints.
2. Actual code and verified CI/test evidence determine what is implemented.
3. The Faranou-specific skill files take precedence over generic upstream examples when they cover the same topic.
4. Upstream skills are guidance, not proof that a dependency, feature, or tool exists. Inspect manifests and code before using any recommendation.
5. Keep these skills selective. Do not install all ECC skills, hooks, MCP servers, agents, or runtime packages by default. Review future upstream updates against this manifest and the repository's architecture/security constraints.
6. Any copied/adapted skill updates must preserve attribution and comply with the upstream license. Re-check the upstream source commit and contents when refreshing.

## Installation distinction

These files are committed in the repository under `.claude/skills/` for Claude Code sessions working in this project. They do **not** install the full ECC plugin into the user's global Claude Code environment. The full plugin has separate hooks and runtime behavior; install it only through the official Claude Code plugin flow after reviewing its permissions, and do not combine the plugin with a full manual install. See the official [ECC README](https://github.com/affaan-m/ECC/blob/main/README.md) for current installation options.
