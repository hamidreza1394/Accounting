---
name: faranou-ui-ux-design
description: Use for designing or reviewing Faranou screens, forms, tables, dashboards, design systems, responsive behavior, accessibility, Persian typography, and RTL workflows.
---

# Faranou UI/UX Design

## Required context
Read `AGENTS.md`, `docs/INDEX.md`, relevant UI requirements/design notes, current screenshots or live implementation, and the relevant Issue. Inspect existing React components, CSS/Tailwind configuration, local fonts/assets, tests and current dependency versions before choosing a pattern or library. Preserve accepted visual decisions unless the user approves a redesign.

## Product constraints
- UI is Persian-first and RTL. Verify keyboard order, focus movement, mixed Persian/Latin numbers, currency/date alignment, screen-reader labels, validation messages and responsive behavior.
- The React client is in `ClientApp`, served under `/app`; use the same-origin API contract and never access SQL directly.
- Keep CSS, JS, fonts, icons and other runtime assets local and offline-capable. No external runtime CDN. Verify licenses for bundled assets.
- Follow the installed Tailwind version and its existing Vite pipeline. Do not introduce a second styling system or component library without evidence and approval.
- Maintain visual consistency: typography scale, spacing, color tokens, focus/error/success states, loading/empty/error states, and data-dense financial table patterns.
- Design for desktop accounting workflows first where requirements indicate, while remaining usable at smaller widths and under keyboard-only navigation.

## Workflow
1. Inspect the current UI and identify specific usability problems using evidence.
2. State user goals, primary task, hierarchy, edge states, and acceptance criteria.
3. Prefer existing components and tokens; make the smallest coherent change.
4. Use Product Design for exploration/prototyping or structured UX review when available, not as proof that implementation is accessible or functionally correct.
5. Verify with responsive checks, automated tests, and axe-core or equivalent where appropriate and available.
6. Do not add UI dependencies until the current component inventory and unmet requirement are documented.

## Output
Include a concise design rationale, RTL/accessibility considerations, edge states, affected components, and a test plan. Distinguish visual proposal from implemented and verified behavior.
