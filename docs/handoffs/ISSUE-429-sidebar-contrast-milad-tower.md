# Handoff — Issue #429: Sidebar contrast and Milad Tower

## Goal
Correct the low-contrast light sidebar visible in the owner's 1905×818 screenshot and replace the overly thin, poorly composed Milad Tower illustration.

## Root cause
Legacy selectors targeting disabled sidebar navigation items had greater CSS specificity than the later light-sidebar variant. They continued applying near-white text and badges on the pale sidebar. The previous SVG also used a 2700px viewBox while most artwork occupied only the first 1800px, and the tower silhouette was too narrow.

## Changes
- Give light-sidebar navigation, disabled labels, status chips, separators, and footer explicit high-contrast colors.
- Use slate text utility classes in the sidebar markup rather than pale blue-on-white classes.
- Keep the active navigation item vivid blue-to-teal with a restrained gold accent.
- Replace the panorama with a full-width 2700×360 local SVG; the Milad Tower has a broader observation pod, crown, antenna, tapered shaft, structural bands, and a Tehran skyline spanning the canvas.
- Add regression assertions for readable sidebar text classes.

## Scope
Presentation only. No API, authorization, authentication, tenant, fiscal-period, database, migration, or dependency changes.

## Validation
Run ClientApp Vitest, typecheck/build, offline asset checks, and repository CI. Browser visual QA at desktop and mobile widths remains necessary; a screenshot from the owner's localhost is not available to this agent.
