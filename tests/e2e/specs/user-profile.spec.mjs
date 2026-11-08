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

    const targetContext = await browser.newContext({ baseURL: ORIGIN, locale: 'fa-IR', timezoneId: 'Asia/Tehran' });
    const targetPage = await targetContext.newPage();
    await enterWorkspace(targetPage, target);
    await targetContext.close();

    await signIn(page, admin);
    await page.goto(profilePath(target.id));
    await expect(page.getByRole('heading', { level: 1, name: target.displayName })).toBeVisible();

    const panel = page.getByRole('tabpanel');
    await expect(panel.getByText('@' + target.username)).toBeVisible();
    await expect(panel.getByText('پرونده پرسنلی متصل نشده است.')).toBeVisible();

    const employeesPath = `/api/v1/companies/${destination.id}/org/employees`;
    const created = await post(page, employeesPath, {
      employeeNumber: 'E2E-77',
      firstName: target.displayName,
      lastName: 'آزمونی',
      hiredOnUtc: '2026-01-10T08:30:00Z',
    });
    expect(created.status, 'POST /org/employees as installation administrator').toBe(201);

    await panel.getByRole('button', { name: `اتصال پرونده پرسنلی در ${destination.name}` }).click();
    const linkDialog = page.getByRole('dialog', { name: `اتصال پرونده پرسنلی — ${destination.name}` });
    await linkDialog.getByLabel('جست‌وجوی کارمند').fill('E2E-77');
    await linkDialog.getByRole('button', { name: 'جست‌وجو' }).click();
    const results = linkDialog.getByRole('list', { name: 'کارمندان بدون حساب متصل' });
    await expect(results.getByRole('listitem')).toHaveCount(1);
    await results.getByRole('button', { name: `انتخاب ${target.displayName} آزمونی` }).click();
    await linkDialog.getByRole('button', { name: `اتصال ${target.displayName} آزمونی` }).click();
    await expect(page.getByRole('status')).toHaveTextContent('پرونده پرسنلی «' + target.displayName + ' آزمونی» به این حساب متصل شد.');
    await expect(panel.getByText('E2E-77')).toBeVisible();
    await capture(page, '02-identity-hr-link.png');
    await expectAccessible(page, '[aria-labelledby="profile-hr-heading"]');

    await panel.getByRole('button', { name: `قطع اتصال پرونده پرسنلی در ${destination.name}` }).click();
    const unlinkDialog = page.getByRole('alertdialog', { name: `قطع اتصال — ${destination.name}` });
    await unlinkDialog.getByRole('button', { name: 'قطع اتصال' }).click();
    await expect(page.getByRole('status')).toHaveTextContent('اتصال پرونده پرسنلی قطع شد.');
    await expect(panel.getByText('پرونده پرسنلی متصل نشده است.')).toBeVisible();

    await capture(page, '01-profile-workspace.png');
    await expectAccessible(page, '[aria-labelledby="profile-identity-heading"]');

    await page.getByRole('tab', { name: 'گروه‌های دسترسی و مجوزهای مؤثر' }).click();
    const permissionsSection = page.getByRole('region').filter({ hasText: 'مجوزهای مؤثر' });
    await expect(permissionsSection.getByText('users.list')).toBeVisible();
    await expect(permissionsSection.getByText(/منابع: عضو مقصد/)).toBeVisible();
    await capture(page, '05-effective-permissions.png');
    await expectAccessible(page, 'main');

    await page.getByRole('tab', { name: 'شرکت‌ها و عضویت‌ها' }).click();
    const membershipTable = page.getByRole('table', { name: 'عضویت‌های شرکتی کاربر' });
    await expect(membershipTable.getByText(destination.name)).toBeVisible();
    await expect(membershipTable.getByText('عضو مقصد')).toBeVisible();
    await expectAccessible(page, 'main');
  });

  test('request states reach the profile and the step-up revocation really ends the session', async ({ page, browser }) => {
    test.setTimeout(300_000);
    const { source, destination } = fixture;
    const target = await fixture.addUser('target2');
    await fixture.addMembership(target, source, source.memberRole.id);

    const targetContext = await browser.newContext({ baseURL: ORIGIN, locale: 'fa-IR', timezoneId: 'Asia/Tehran' });
    const targetPage = await targetContext.newPage();
    await enterWorkspace(targetPage, target);
    const destinations = await callApi(targetPage, 'GET', `${destinationsPath}${encodeURIComponent(destination.name)}`);
    expect(destinations.status, 'GET destinations').toBe(200);
    const destinationEntry = destinations.body.items.find((item) => item.companyId === destination.id);
    expect(destinationEntry, 'the destination company is discoverable inside the boundary').toBeTruthy();
    const requested = await post(targetPage, requestPath, { destinationCompanyId: destination.id });
    expect(requested.status, 'POST /me/membership-requests').toBe(201);
    const requestId = requested.body.id;

    await signIn(page, admin);
    await page.goto(profilePath(target.id));
    await page.getByRole('tab', { name: 'شرکت‌ها و عضویت‌ها' }).click();
    const requestTable = page.getByRole('table', { name: 'درخواست‌های عضویت کاربر' });
    await expect(requestTable.getByText(destination.name)).toBeVisible();
    await expect(requestTable.getByText('در انتظار تأیید')).toBeVisible();
    await expect(page.getByRole('link', { name: 'رفتن به صف تأیید مقصد' })).toBeVisible();
    await expectAccessible(page, 'main');
    await capture(page, '03-membership-pending.png');

    const approved = await post(page, approvePath(destination.id, requestId), { roleId: String(destination.memberRole.id) });
    expect(approved.status, 'POST approve').toBe(200);
    await page.reload();
    await page.getByRole('tab', { name: 'شرکت‌ها و عضویت‌ها' }).click();
    const afterApprove = page.getByRole('table', { name: 'درخواست‌های عضویت کاربر' });
    await expect(afterApprove.getByText('تأیید شده')).toBeVisible();
    await expect(afterApprove.getByText('عضو مقصد')).toBeVisible();

    const secondRequest = await callApi(targetPage, 'GET', `${destinationsPath}${encodeURIComponent(destination.name)}`);
    expect(secondRequest.status).toBe(200);
    const secondTarget = await fixture.addUser('target3');
    await fixture.addMembership(secondTarget, source, source.memberRole.id);
    const targetContext2 = await browser.newContext({ baseURL: ORIGIN, locale: 'fa-IR', timezoneId: 'Asia/Tehran' });
    const targetPage2 = await targetContext2.newPage();
    await enterWorkspace(targetPage2, secondTarget);
    const destinations2 = await callApi(targetPage2, 'GET', `${destinationsPath}${encodeURIComponent(destination.name)}`);
    const destinationEntry2 = destinations2.body.items.find((item) => item.companyId === destination.id);
    expect(destinationEntry2, 'the destination is discoverable for the second requester').toBeTruthy();
    const secondRequested = await post(targetPage2, requestPath, { destinationCompanyId: destination.id });
    expect(secondRequested.status, 'POST /me/membership-requests (second)').toBe(201);

    const rejected = await callApi(page, 'POST', rejectPath(destination.id, secondRequested.body.id), {
      body: { reason: 'در دورهٔ بسته بود' },
      token: await (async () => {
        const tokenResponse = await callApi(page, 'GET', '/api/v1/antiforgery/token');
        return { headerName: tokenResponse.body.headerName, requestToken: tokenResponse.body.requestToken };
      })(),
    });
    expect(rejected.status, 'POST reject').toBe(200);

    await page.goto(profilePath(secondTarget.id));
    await page.getByRole('tab', { name: 'شرکت‌ها و عضویت‌ها' }).click();
    const rejectedTable = page.getByRole('table', { name: 'درخواست‌های عضویت کاربر' });
    await expect(rejectedTable.getByText('رد شده')).toBeVisible();
    await expect(rejectedTable.getByText('در دورهٔ بسته بود')).toBeVisible();
    await capture(page, '04-membership-rejected.png');

    await page.getByRole('tab', { name: 'سوابق تغییرات مدیریتی' }).click();
    const auditTable = page.getByRole('table', { name: 'سوابق تغییرات مدیریتی کاربر' });
    await expect(auditTable.getByText('درخواست عضویت شرکت')).toBeVisible();
    await capture(page, '07-management-history.png');

    await page.goto(profilePath(secondTarget.id));
    await page.getByRole('tab', { name: 'حساب کاربری و امنیت' }).click();
    const sessionsTable = page.getByRole('table', { name: 'نشست‌های فعال کاربر' });
    await expect(sessionsTable.getByRole('row')).toHaveCount(2);
    await expectAccessible(page, 'main');
    await capture(page, '06-active-sessions.png');

    const before = await callApi(targetPage2, 'GET', '/api/v1/session');
    expect(before.status, 'the target session works before the revocation').toBe(200);

    await sessionsTable.getByRole('button', { name: /ابطال نشست ایجادشده/ }).click();
    const stepUp = page.getByRole('alertdialog', { name: 'ابطال این نشست' });
    await stepUp.getByRole('button', { name: 'ابطال نشست' }).click();
    await expect(stepUp.getByText('برای تأیید هویت، رمز عبور خودتان لازم است.')).toBeVisible();

    await stepUp.getByLabel(/رمز عبور خودتان/).fill(admin.password);
    await stepUp.getByLabel(/دلیل ابطال/).selectOption({ label: 'خروج کاربر از سازمان' });
    await stepUp.getByRole('button', { name: 'ابطال نشست' }).click();
    await expect(page.getByRole('status')).toHaveTextContent('نشست کاربر پایان یافت.');
    await expect(page.getByText('نشست فعالی برای این حساب وجود ندارد.')).toBeVisible();
    await capture(page, '08-session-revoked.png');

    const after = await callApi(targetPage2, 'GET', '/api/v1/session');
    expect(after.status, 'the revoked session is really dead').toBe(401);
    await targetContext2.close();

    await page.goto(profilePath(target.id));
    await page.getByRole('tab', { name: 'سوابق ورود و خروج' }).click();
    const historyTable = page.getByRole('table', { name: 'سوابق ورود و خروج کاربر' });
    await expect(historyTable.getByText('ورود موفق')).toBeVisible();
    await capture(page, '09-login-history.png');
  });
});
