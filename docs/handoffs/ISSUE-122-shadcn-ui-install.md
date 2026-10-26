# Handoff — Issue #122: shadcn/ui installation and RTL integration

## Summary
The main branch had merged the shadcn configuration/documentation PR, but not the actual package installation. This branch adds the pinned CLI and real source-owned components.

## Changes
- Pin `shadcn@4.22.1` in ClientApp devDependencies.
- Generate Base UI-based shadcn Button and Card using the official CLI.
- Lock npm dependencies; use the official `cn` package and re-export it from `src/lib/utils.ts`.
- Configure `@/*` aliases in TypeScript and Vite.
- Map shadcn semantic CSS tokens to the existing Farano navy/teal/gold palette without replacing existing styles or fonts.
- Use the shadcn Button for the error retry action and Card for the company/period overview panels.
- Rename the existing custom Button to `FaranouButton.tsx` to avoid Windows/TypeScript case-insensitive collision with shadcn's lowercase `button.tsx`.
- Add tests for keyboard activation, disabled Button behavior, and Card content.
- Remove temporary bootstrap workflow after generating and validating the source/lockfile.

## Validation evidence
- Bootstrap run: https://github.com/hamidreza1394/Accounting/actions/runs/40275781607
- On the pre-integration commit, the run passed npm clean install, typecheck, existing tests, production build, offline resource check, and runtime dependency audit.
- The latest branch commit still requires the permanent .NET CI (including client job) to pass before this change can be considered verified.

## Constraints preserved
- React 19, TypeScript strict, Vite 8, Tailwind CSS v4.
- Persian RTL and local font assets.
- No external runtime CDN/assets.
- No customer DB/schema/migration changes.
- No mass rewrite of existing UI.

## Follow-up
After merge, update Issue #122 and D-048 status with the final CI run. Continue component adoption incrementally, with visual regression checks before replacing additional legacy controls.
