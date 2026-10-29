# Handoff — Issue #265: reduce duplicate GitHub Actions runs

- **Issue:** https://github.com/hamidreza1394/Accounting/issues/265
- **Scope:** CI trigger configuration only; no application code, database, migrations, secrets, or test cases changed.

## Finding
The .NET CI workflow previously ran on every branch push and on pull requests. Vendor CI also ran on pushes to selected feature branches and on pull requests. A branch with an open PR could therefore trigger duplicate CI for the same changes.

## Proposed change
- Limit push-triggered .NET CI to `main`.
- Limit push-triggered Vendor CI to `main`; keep its existing path filters.
- Preserve `pull_request` triggers so each PR still receives its normal validation.
- Preserve Vendor `workflow_dispatch`.

## Verification required
The PR must pass .NET CI, Vendor CI where applicable, secret scanning, and Semgrep. The change is intended to reduce redundant branch-push runs; it does not replace required status checks or branch rulesets.

## Operational note
Do not merge while required checks are queued or in progress. If a branch has not opened a PR yet, its pushes will no longer trigger these two workflows; open a draft PR when early CI feedback is needed.
