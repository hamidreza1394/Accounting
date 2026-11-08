// End-to-end + accessibility (axe) of the seven-tab user profile workspace (#444 stage 3)
// against the published Production server and a real SQL Server. No API mocks.
//
// Covered here, on top of the real workflow endpoints:
//   1. the installation administrator opens the profile workspace of a target user;
//   2. tab 1 shows the identity fields and connects / disconnects the linked personnel record
//      through the real HR endpoints (the installation administrator is scoped by the server);
//   3. tab 3 shows the memberships and, lazily, the target user's inter-company request states
//      (Pending -> Approved with the decider, a second request Rejected with the reason);
//   4. tab 5 shows the effective permissions with the granting role as the source;
//   5. tab 2 shows the allow-listed active sessions and revokes one through the D-073 §1
//      step-up (the administrator's own password + a server-owned reason code); the revoked
//      session is proven dead by the target user's very next API call (401);
//   6. tab 6 shows the authentication history of the target user;
//   7. tab 7 shows the management change history recorded for the target user's own actions;
//   8. axe (WCAG 2.x A/AA) runs on the opened tabs; real browser screenshots of every tab are
//      written to artifacts/ui-preview/stage-3-profile (never of the open step-up dialog).
import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';
import { mkdirSync, readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { createMembershipFixture } from '../lib/membershipFixture.mjs';
import { callApi, enterWorkspace, post, signIn } from '../lib/session.mjs';

const state = JSON.parse(readFileSync(new URL('../.e2e-state.json', import.meta.url), 'utf8'));
const admin = state.users.admin;

// The published-server origin from playwright.config.mjs; a fresh secondary context cannot
// derive it from `page.url()` because the primary page may still be on about:blank.
const ORIGIN = 'http://localhost:5090';

const screenshotDir = fileURLToPath(new URL('../../../artifacts/ui-preview/stage-3-profile/', import.meta.url));
async function capture(page, name) {
  mkdirSync(screenshotDir, { recursive: true });
  await page.screenshot({ path: `${screenshotDir}${name}`, fullPage: true });
}

/** WCAG 2.x A/AA automated rules (includes color-contrast); any violation fails with its details. */
async function expectAccessible(page, scope) {
  let builder = new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']);
  if (scope) builder = builder.include(scope);
  const { violations } = await builder.analyze();
  expect(
    violations.map((v) => ({ id: v.id, impact: v.impact, nodes: v.nodes.slice(0, 3).map((n) => n.target.join(' ')) })),
    `axe violations on ${page.url()}`,
  ).toEqual([]);
}

const profilePath = (userId) => `/app/admin/users/${userId}`;
const destinationsPath = '/api/v1/me/membership-requests/destinations?search=';
const requestPath = '/api/v1/me/membership-requests';
const approvePath = (companyId, requestId) => `/api/v1/admin/companies/${companyId}/membership-requests/${requestId}/approve`;
const rejectPath = (companyId, requestId) => `/api/v1/admin/companies/${companyId}/membership-requests/${requestId}/reject`;
const sessionPath = (userId) => `/api/v1/admin/users/${userId}/sessions`;
const revokePath = (userId, sessionId) => `${sessionPath(userId)}/${sessionId}/revoke`;

test.use({ trace: 'off', screenshot: 'off' });

test.describe('user profile workspace: HR link, request states, permissions, sessions', () => {
  let fixture;

  test.beforeEach(async () => {
    fixture = await createMembershipFixture('پروفایل');
  });

  test.afterEach(async () => {
    if (fixture) await fixture.destroy();
    fixture = undefined;
  });

  test('identity, HR link management, memberships and effective permissions work end to end', async ({ page, browser }) => {
    test.setTimeout(300_000);
    const { destination } = fixture;
    const target = await fixture.addUser('target');
    await fixture.addMembership(target, destination, destination.memberRole.id);

    // ------------------------------------------------------------ the target signs in once
    // (a real LoginSucceeded event and one active session for later tabs)
    const targetContext = await browser.newContext({ baseURL: ORIGIN, locale: 'fa-IR', timezoneId: 'Asia/Tehran' });
    const targetPage = await targetContext.newPage();
    await enterWorkspace(targetPage, target);
    await targetContext.close();

    // ------------------------------------------------------------ the administrator opens the profile
    await signIn(page, admin);
    await page.goto(profilePath(target.id));
    await expect(page.getByRole('heading', { level: 1, name: target.displayName })).toBeVisible();

    // ---------------------------------------------- tab 1: identity + personnel record link
    // PLACEHOLDER_CONTINUE
  });
});
