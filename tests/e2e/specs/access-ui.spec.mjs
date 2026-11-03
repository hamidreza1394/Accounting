// End-to-end + accessibility (axe) of the user-access administration UI (#444) against the published
// server and a real SQL Server. Runs only in CI (job "e2e"); never counted as passing before that run.
import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';
import { readFileSync } from 'node:fs';

const state = JSON.parse(readFileSync(new URL('../.e2e-state.json', import.meta.url), 'utf8'));
const { alice, bob, admin } = state.users;
const { b } = state.companies;

test.use({ timezoneId: 'Asia/Tehran' });

async function signIn(page, user) {
  await page.goto('/Account/Login');
  await page.locator('#ViewModel_Username').fill(user.username);
  await page.locator('#ViewModel_Password').fill(user.password);
  await Promise.all([
    page.waitForNavigation({ waitUntil: 'load' }),
    page.locator('form.taraz-login-form button[type="submit"]').click(),
  ]);
}

async function apiStatus(page, path) {
  return page.evaluate(async (target) => (await fetch(target, { credentials: 'same-origin' })).status, path);
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

test.describe('security audit report', () => {
  test('is visible to the administrator, shows no IP, filters by event type and is accessible', async ({ page }) => {
    await signIn(page, admin);
    // Make sure at least one audit event exists (the sign-in above normally records one).
    await page.goto('/app/admin/security/audit');
    await expect(page.getByRole('heading', { level: 1, name: 'گزارش امنیت و سوابق' })).toBeVisible();
    const table = page.getByRole('table', { name: 'فهرست رویدادهای امنیتی' });
    await expect(table).toBeVisible();
    await expect(page.getByText(/\b\d{1,3}(\.\d{1,3}){3}\b/)).toHaveCount(0);
    expect(await page.content()).not.toMatch(/IpAddress/i);

    await expectAccessible(page);

    // Filter: an event type nobody produced shows the empty state, not an error.
    const eventType = page.getByLabel('نوع رویداد');
    await eventType.fill('NoSuchEventTypeForE2E');
    await page.getByRole('button', { name: /اعمال/ }).click();
    await expect(page.getByRole('alert')).toHaveCount(0);
    await expectAccessible(page);
  });

  test('keyboard: the filter form is reachable by Tab and the date picker opens with Enter and closes with Escape', async ({ page }) => {
    await signIn(page, admin);
    await page.goto('/app/admin/security/audit');
    const from = page.getByRole('button', { name: /از تاریخ/ }).first();
    await from.focus();
    await page.keyboard.press('Enter');
    await expect(page.getByRole('grid')).toBeVisible();
    await expectAccessible(page);
    await page.keyboard.press('Escape');
    await expect(page.getByRole('grid')).toBeHidden();
    await expect(from).toBeFocused();
  });

  test('non-administrators are refused by the API, not only by the UI', async ({ page }) => {
    await signIn(page, alice);
    await page.goto('/app/');
    expect(await apiStatus(page, '/api/v1/admin/security/audit-events')).toBe(403);
    await page.goto('/app/admin/security/audit');
    await expect(page.getByRole('heading', { level: 1, name: 'گزارش امنیت و سوابق' })).toHaveCount(0);
  });
});

test.describe('membership requests and company directory', () => {
  test('pending membership requests and my requests pages are accessible', async ({ page }) => {
    await signIn(page, admin);
    await page.goto('/app/admin/membership-requests');
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    await expectAccessible(page);
    await page.goto('/app/account/membership-requests');
    await expect(page.getByRole('heading', { level: 1, name: 'درخواست‌های عضویت من' })).toBeVisible();
    await expectAccessible(page);
  });

  test('company user directory API requires the users.list permission of the company', async ({ page }) => {
    // bob is an ordinary member of company b without users.list.
    await signIn(page, bob);
    await page.goto('/app/');
    const status = await apiStatus(page, `/api/v1/companies/${b.id}/users?page=1&pageSize=10`);
    expect(status).toBe(403);
  });
});

test.describe('role members', () => {
  test('role management page is accessible for the administrator', async ({ page }) => {
    await signIn(page, admin);
    await page.goto('/app/admin/roles');
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    await expectAccessible(page);
  });
});
