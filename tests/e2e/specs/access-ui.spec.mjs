// End-to-end + accessibility (axe) of the user-access administration UI (#444) against the published
// server and a real SQL Server. Runs only in CI (job "e2e"); never counted as passing before that run.
import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';
import { readFileSync } from 'node:fs';

const state = JSON.parse(readFileSync(new URL('../.e2e-state.json', import.meta.url), 'utf8'));
const { alice, bob, heidi, admin } = state.users;
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

// Creates two temporary roles through the actual administrator API, so the test does not rely on
// database shortcuts or give the seeded accountant role any extra permissions. Bob is restored even
// if a UI assertion fails; the suite shares its disposable database with the other specs.
test('role transfer updates the real member list and company directory permission', async ({ page, browser }) => {
  test.setTimeout(60_000);
  await signIn(page, admin);
  await page.goto('/app/');
  const rolesPath = `/api/v1/admin/companies/${b.id}/roles`;
  const api = async (method, path, body, token) => page.evaluate(async ({ method, path, body, token }) => {
    const headers = {};
    if (body !== undefined) headers['Content-Type'] = 'application/json';
    if (token) headers[token.headerName] = token.requestToken;
    const response = await fetch(path, {
      method, headers, credentials: 'same-origin',
      ...(body === undefined ? {} : { body: JSON.stringify(body) }),
    });
    const text = await response.text();
    return { status: response.status, body: text ? JSON.parse(text) : null };
  }, { method, path, body, token });

  const tokenResponse = await api('GET', '/api/v1/antiforgery/token');
  expect(tokenResponse.status).toBe(200);
  const token = tokenResponse.body;
  const suffix = admin.username.slice(-6).toUpperCase();
  const create = async (code, name, permissionCodes) => {
    const result = await api('POST', rolesPath, { code, name, templateCode: null, permissionCodes }, token);
    expect(result.status).toBe(201);
    return result.body.id;
  };
  const source = await create(`E2E-SRC-${suffix}`, 'گروه مبدأ آزمون', ['users.list']);
  const target = await create(`E2E-DST-${suffix}`, 'گروه مقصد آزمون', []);
  let assigned = false;
  let bobContext;
  try {
    const assign = await api('POST', `${rolesPath}/${source}/members/${bob.id}`, undefined, token);
    expect(assign.status).toBe(204);
    assigned = true;

    bobContext = await browser.newContext({ baseURL: new URL(page.url()).origin, locale: 'fa-IR', timezoneId: 'Asia/Tehran' });
    const bobPage = await bobContext.newPage();
    await signIn(bobPage, bob);
    await bobPage.goto('/app/company/users');
    await expect(bobPage.getByRole('heading', { level: 1, name: 'کاربران شرکت' })).toBeVisible();
    const directory = bobPage.getByRole('table', { name: 'فهرست کاربران شرکت' });
    await expect(directory.getByText(bob.username)).toBeVisible();
    await expect(directory.getByText(admin.username)).toBeVisible();
    await expect(directory.getByText(alice.username)).toHaveCount(0);
    await expect(bobPage.getByText(`${bob.username}@e2e.test`)).toHaveCount(0);

    await page.goto(`/app/admin/roles/${source}?company=${b.id}`);
    const sourceTable = page.getByRole('table', { name: 'اعضای گروه گروه مبدأ آزمون' });
    await expect(sourceTable.getByText(bob.username)).toBeVisible();
    await expect(sourceTable.getByText(admin.username)).toHaveCount(0);
    await page.getByLabel('گروه مقصد').selectOption(target);
    await page.getByRole('button', { name: 'انتقال اعضا…' }).click();
    const dialog = page.getByRole('dialog', { name: 'انتقال همهٔ اعضای گروه' });
    await expect(dialog).toBeVisible();
    await dialog.getByRole('button', { name: 'انتقال اعضا', exact: true }).click();
    await expect(page.getByText(/عضو به گروه «گروه مقصد آزمون» منتقل شد/)).toBeVisible();
    await expect(page.getByText('این گروه عضو فعالی ندارد')).toBeVisible();

    await page.goto(`/app/admin/roles/${target}?company=${b.id}`);
    await expect(page.getByRole('table', { name: 'اعضای گروه گروه مقصد آزمون' }).getByText(bob.username)).toBeVisible();
    // Permissions are checked again on the server; the target role has no users.list.
    expect(await apiStatus(bobPage, `/api/v1/companies/${b.id}/users?page=1&pageSize=10`)).toBe(403);
    await bobPage.reload();
    await expect(bobPage.getByText('دسترسی ندارید', { exact: true })).toBeVisible();
  } finally {
    if (bobContext) await bobContext.close();
    if (assigned) {
      const restored = await api('POST', `${rolesPath}/${b.roleId}/members/${bob.id}`, undefined, token);
      expect(restored.status).toBe(204);
    }
  }
});


test('membership discovery fails closed without a shared customer boundary', async ({ page }) => {
  await signIn(page, heidi);
  await page.goto('/app/account/membership-requests');
  await expect(page.getByRole('heading', { level: 1, name: 'درخواست‌های عضویت من' })).toBeVisible();
  const search = page.getByLabel('نام دقیق شرکت مقصد');
  await search.fill('ab');
  await page.getByRole('button', { name: 'جستجوی شرکت' }).click();
  await expect(page.getByRole('alert')).toContainText('۳');
  await search.fill(b.name);
  const result = page.waitForResponse((response) => response.url().includes('/membership-requests/destinations?') && response.request().method() === 'GET');
  await page.getByRole('button', { name: 'جستجوی شرکت' }).click();
  expect((await result).status()).toBe(200);
  await expect(page.getByText('شرکت قابل درخواستی پیدا نشد.')).toBeVisible();
  await expect(page.getByRole('button', { name: /درخواست عضویت در/ })).toHaveCount(0);
  expect(await apiStatus(page, '/api/v1/me/membership-requests/destinations?search=ab')).toBe(400);
  expect(await apiStatus(page, `/api/v1/admin/companies/${b.id}/membership-requests/pending`)).toBe(404);
});
