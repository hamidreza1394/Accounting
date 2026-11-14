# Faranou — browser E2E tests

Real Chromium against a **published** server (Production environment) and a **real SQL Server**.
They run in CI (job `e2e` of `.github/workflows/dotnet.yml`); the job:

1. starts SQL Server 2022 as a service container;
2. publishes with `scripts/publish.sh`;
3. creates the schema with `dotnet Server.dll --migrate`;
4. seeds random accounts/companies with `npm run seed` (passwords are generated per run and kept in
   the git-ignored `.e2e-state.json`);
5. runs `npx playwright test` (starts two server instances: one with the database, one in first-run
   setup mode).

The suite runs sequentially and concentrates its requests on the shared seeded accounts, so the
E2E instance of the server raises two rate limits through environment variables only
(`RateLimiting__Login__PermitLimit` and `RateLimiting__ApiSensitive__PermitLimit`, see
`playwright.config.mjs`). Production keeps the `appsettings.json` defaults, and the
membership-workflow spec still proves on every run that the sensitive limit really rejects with
429 at the configured permit count.

Local run (disposable database only — its name must start with `FaranouE2E`):
```bash
cd tests/e2e && npm ci && npx playwright install chromium
export FARANOU_E2E_PUBLISH=/path/to/publish
export FARANOU_E2E_SQL="Server=localhost,1433;Database=FaranouE2E;User Id=...;Password=...;TrustServerCertificate=True;Encrypt=True"
ConnectionStrings__ConnectionString="$FARANOU_E2E_SQL" dotnet "$FARANOU_E2E_PUBLISH/Server.dll" --migrate
npm run seed && npx playwright test
```

The role-administration spec creates a separate installation administrator referencing the existing global `Admin` role
for its own run, using credentials generated in the ignored state file. It verifies authorization
against the live database, and removes its account, login/audit records and created company
role in a `finally` block, including on assertion failure. Shared Alice/Bob/admin accounts are not
changed. It uses the existing seeded companies, runs sequentially with the suite, and disables
trace/screenshots for this spec to avoid retaining its credentials or antiforgery token. As with
all E2E data, use a disposable database; hard process termination or loss of SQL connectivity can
prevent `finally` cleanup, so discard the database after the run.

## The user-profile spec (stage 3)

`specs/user-profile.spec.mjs` drives the seven-tab profile workspace of Issue #444 stage 3
against the published server and SQL Server: the HR link management (connect/disconnect
through the real HR endpoints), the inter-company membership request states (Pending,
Approved with the decider, Rejected with the reason), the effective permissions with their
source role, the D-073 §1 step-up session revocation (proven dead by the target user's next
401), the authentication history and the management change history. It also runs axe
(WCAG 2.x A/AA) on the opened tabs and writes real browser screenshots of every tab to
`artifacts/ui-preview/stage-3-profile/` (uploaded as the `stage-3-profile-screenshots`
artifact; never captured while the step-up dialog is open, so no credential can leak).
