// End-to-end: real browser (Chromium) -> published server (Production) -> SQL Server.
// Covers sign-in, session, company selection, anti-forgery, company isolation, session
// revalidation and the enforced Content-Security-Policy of the pages that are visited.
import { expect, test } from '@playwright/test';
import { readFileSync } from 'node:fs';
import sql from 'mssql';

const state = JSON.parse(readFileSync(new URL('../.e2e-state.json', import.meta.url), 'utf8'));
const { alice, bob, carol } = state.users;
const { a, b, c } = state.companies;

// Every page records CSP violations (enforced and report-only) and script errors.
test.beforeEach(async ({ page }) => {
  await page.addInitScript(() => {
    window.__cspViolations = [];
    document.addEventListener('securitypolicyviolation', (event) => {
      window.__cspViolations.push({
        disposition: event.disposition,
        directive: event.effectiveDirective,
        blocked: event.blockedURI,
        source: `${event.sourceFile}:${event.lineNumber}`,
      });
    });
  });
  page.__errors = [];
  page.on('pageerror', (error) => page.__errors.push(error.message));
  page.__failedRequests = [];
  page.on('requestfailed', (request) => page.__failedRequests.push(request.url()));
  page.on('response', (response) => {
    if (response.status() >= 400 && /\.(css|js|woff2?|svg|png|jpe?g|ico)(\?|$)/i.test(response.url())) {
      page.__failedRequests.push(`${response.status()} ${response.url()}`);
    }
  });
});

async function violations(page, disposition = 'enforce') {
  const all = await page.evaluate(() => window.__cspViolations ?? []);
  return all.filter((violation) => violation.disposition === disposition);
}

async function expectCleanPage(page) {
  expect(await violations(page), `CSP violations on ${page.url()}`).toEqual([]);
  expect(page.__errors, `script errors on ${page.url()}`).toEqual([]);
  expect(page.__failedRequests, `failed static resources on ${page.url()}`).toEqual([]);
}

async function signIn(page, user, { rememberMe = false } = {}) {
  await page.goto('/Account/Login');
  await page.locator('#ViewModel_Username').fill(user.username);
  await page.locator('#ViewModel_Password').fill(user.password);
  if (rememberMe) {
    await page.locator('label.taraz-remember').click();
  }
  await page.locator('form.taraz-login-form button[type="submit"]').click();
}

async function setUserActive(user, active) {
  const pool = await sql.connect(process.env.FARANOU_E2E_SQL);
  try {
    await pool.request().input('id', sql.UniqueIdentifier, user.id).input('active', sql.Bit, active)
      .query('UPDATE Users SET IsActive = @active WHERE Id = @id');
  } finally {
    await pool.close();
  }
}

test('the sign-in page renders with its local font, without CSP violations', async ({ page }) => {
  await page.goto('/Account/Login');

  await expect(page.locator('#ViewModel_Username')).toBeVisible();
  await expect(page.locator('#ViewModel_Password')).toBeVisible();
  // The local IRANSansX faces that the page uses are loaded (font-src 'self'; no CDN).
  const loadedFaces = await page.evaluate(async () => {
    await document.fonts.ready;
    return [...document.fonts]
      .filter((face) => face.family.replace(/["']/g, '') === 'IRANSansX' && face.status === 'loaded')
      .map((face) => face.weight);
  });
  expect(loadedFaces.length).toBeGreaterThan(0);
  expect(page.__failedRequests).toEqual([]);

  // The password toggle (taraz-login.js) still works under the enforced CSP.
  await page.locator('[data-password-toggle]').first().click();
  await expect(page.locator('#ViewModel_Password')).toHaveAttribute('type', 'text');

  await expectCleanPage(page);
});

test('a wrong password and an inactive account are refused', async ({ page }) => {
  await signIn(page, { username: alice.username, password: 'Wrong-Password-1' });
  await expect(page).toHaveURL(/\/Account\/Login/i);
  await expect(page.getByText('نام کاربری یا کلمه عبور صحیح نیست.')).toBeVisible();

  await signIn(page, carol);
  await expect(page).toHaveURL(/\/Account\/Login/i);
  expect((await page.context().cookies()).some((cookie) => cookie.name === '.Faranou.Authentication')).toBe(false);
  await expectCleanPage(page);
});

test('sign-in issues an HttpOnly session cookie and the Razor home shows only the own companies', async ({ page, context }) => {
  await signIn(page, alice);
  await expect(page).toHaveURL(/\/account\/loginsuccess/i);
  await expectCleanPage(page);

  const cookie = (await context.cookies()).find((current) => current.name === '.Faranou.Authentication');
  expect(cookie?.httpOnly).toBe(true);
  expect(cookie?.sameSite).toBe('Lax');
  expect(cookie?.expires).toBe(-1); // session cookie without "remember me"

  await page.goto('/');
  await expect(page.getByText(a.name)).toBeVisible();
  await expect(page.getByText(c.name)).toBeVisible();
  await expect(page.getByText(b.name)).toHaveCount(0);
  await expectCleanPage(page);
});

test('remember me issues a persistent cookie', async ({ page, context }) => {
  await signIn(page, alice, { rememberMe: true });
  await expect(page).toHaveURL(/\/account\/loginsuccess/i);

  const cookie = (await context.cookies()).find((current) => current.name === '.Faranou.Authentication');
  expect(cookie?.expires).toBeGreaterThan(Date.now() / 1000);
});

test('the React dashboard shows real data, switches companies and ignores a foreign company id', async ({ page }) => {
  await signIn(page, alice);

  await page.goto(`/app/?company=${b.id}`);
  await expect(page.getByRole('heading', { name: a.name })).toBeVisible();
  await expect(page.locator('table').getByText('۱۴۰۵/۰۱/۰۱')).toBeVisible();
  await expect(page.getByText(state.secretPeriodTitle)).toHaveCount(0);

  await page.getByLabel('شرکت', { exact: true }).selectOption(c.id);
  await expect(page.getByRole('heading', { name: c.name })).toBeVisible();
  await expect(page.getByText('هنوز دوره مالی برای این شرکت ثبت نشده است.')).toBeVisible();
  expect(new URL(page.url()).searchParams.get('company')).toBe(c.id);

  await expectCleanPage(page);
});

test('another tenant’s company and an unknown company give the same 404', async ({ page }) => {
  await signIn(page, alice);
  await page.goto('/app/');

  const result = await page.evaluate(async ({ foreign, unknown }) => {
    const get = async (id) => {
      const response = await fetch(`/api/v1/companies/${id}/overview`, { credentials: 'same-origin' });
      return { status: response.status, body: await response.text() };
    };
    return { foreign: await get(foreign), unknown: await get(unknown), own: (await fetch('/api/v1/companies')).status };
  }, { foreign: b.id, unknown: crypto.randomUUID() });

  expect(result.own).toBe(200);
  expect(result.foreign.status).toBe(404);
  expect(result.unknown.status).toBe(404);
  expect(result.foreign.body).toBe(result.unknown.body);
  expect(result.foreign.body).not.toContain(state.secretPeriodTitle);
});

test('API sign-out needs the anti-forgery token; the dashboard button signs out', async ({ page }) => {
  await signIn(page, alice);
  await page.goto('/app/');
  await expect(page.getByRole('heading', { name: 'داشبورد' })).toBeVisible();

  const forged = await page.evaluate(async () =>
    (await fetch('/api/v1/session/logout', { method: 'POST', credentials: 'same-origin' })).status);
  expect(forged).toBe(400);
  expect(await page.evaluate(async () => (await fetch('/api/v1/session')).status)).toBe(200);

  await page.getByRole('button', { name: 'خروج' }).click();
  await expect(page).toHaveURL(/\/Account\/Login/i);
  expect(await page.evaluate(async () => (await fetch('/api/v1/session')).status)).toBe(401);
  await expectCleanPage(page);
});

test('the Razor sign-out form works; a GET does not sign out', async ({ page }) => {
  await signIn(page, alice);

  await page.goto('/Account/Logout');
  expect(await page.evaluate(async () => (await fetch('/api/v1/session')).status)).toBe(200);
  await expectCleanPage(page);

  await page.locator('main form[method="post"] button[type="submit"], form[method="post"] button.btn-danger').first().click();
  await expect(page).toHaveURL(/\/account\/login/i);
  expect(await page.evaluate(async () => (await fetch('/api/v1/session')).status)).toBe(401);
});

test('an expired session in the dashboard leads back to the sign-in page and returns', async ({ page }) => {
  await page.goto('/app/?company=x');
  await expect(page).toHaveURL(/\/Account\/Login\?ReturnUrl=%2Fapp%2F%3Fcompany%3Dx/i);

  await page.locator('#ViewModel_Username').fill(alice.username);
  await page.locator('#ViewModel_Password').fill(alice.password);
  await page.locator('form.taraz-login-form button[type="submit"]').click();
  await expect(page).toHaveURL(/\/app\/\?company=x/);
  await expect(page.getByRole('heading', { name: 'داشبورد' })).toBeVisible();
});

test('deactivating an account ends its open session on the next request', async ({ page }) => {
  await signIn(page, bob);
  await page.goto('/');
  await expect(page.getByText(b.name)).toBeVisible();

  await setUserActive(bob, false);
  try {
    await page.goto('/');
    await expect(page).toHaveURL(/\/Account\/Login/i);
    expect(await page.evaluate(async () => (await fetch('/api/v1/session')).status)).toBe(401);
  } finally {
    await setUserActive(bob, true);
  }
});

test('administration pages of the infrastructure account have no enforced CSP violation', async ({ page }) => {
  await signIn(page, state.master);
  await expect(page).toHaveURL(/\/account\/loginsuccess/i);

  for (const path of ['/', '/Admin/Users', '/Admin/Roles', '/Admin/MenuItems', '/Admin/MenuItems/Create',
    '/Admin/PageCategories', '/Admin/ApplicationHandlers', '/Admin/FileManager', '/Account/Logout']) {
    await page.goto(path);
    expect(page.url(), `${path} was redirected`).not.toMatch(/\/Account\/(Login|AccessDenied)/i);
    await expectCleanPage(page);
  }

  // The icon picker (moved from an inline script to a file) still initialises.
  await page.goto('/Admin/MenuItems/Create');
  await page.locator('input.iconpicker').click();
  await expect(page.locator('.iconpicker-popover')).toBeVisible();
  await expectCleanPage(page);

  // The page editor stays report-only: nothing is blocked there, violations are only recorded.
  await page.goto('/Admin/PageManager/Create');
  expect(await violations(page, 'enforce')).toEqual([]);
  const reportOnly = await violations(page, 'report');
  test.info().annotations.push({ type: 'report-only violations on /Admin/PageManager/Create', description: JSON.stringify(reportOnly) });
});

test('the first-run setup page works under the enforced CSP', async ({ page }) => {
  await page.goto('http://localhost:5091/DatabaseSetup');

  await expect(page.locator('#authMode')).toBeVisible();
  await expect(page.locator('#sqlUserField')).toBeHidden();
  await page.locator('#authMode').selectOption('Sql');
  await expect(page.locator('#sqlUserField')).toBeVisible();
  await expectCleanPage(page);
});
