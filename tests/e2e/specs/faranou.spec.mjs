// End-to-end: real browser (Chromium) -> published server (Production) -> SQL Server.
// Covers sign-in, session, company and fiscal-period selection, anti-forgery, company isolation, session
// revalidation and the enforced Content-Security-Policy of the pages that are visited.
import { expect, test } from '@playwright/test';
import { readFileSync } from 'node:fs';
import sql from 'mssql';

const state = JSON.parse(readFileSync(new URL('../.e2e-state.json', import.meta.url), 'utf8'));
const { alice, bob, carol, dave, erin, frank } = state.users;
const { a, b, c, d, e, f } = state.companies;

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
  // Only static resources count. API fetches that a navigation cancels (net::ERR_ABORTED, e.g.
  // after signing out) and intentional 4xx answers of the API are asserted by the tests.
  const staticTypes = new Set(['document', 'stylesheet', 'script', 'font', 'image']);
  page.on('requestfailed', (request) => {
    if (staticTypes.has(request.resourceType())) {
      page.__failedRequests.push(`${request.failure()?.errorText ?? 'failed'} ${request.url()}`);
    }
  });
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
  // Set the option explicitly: a remembered username comes back with the box already ticked.
  const box = page.locator('#ViewModel_RememberMe');
  if ((await box.isChecked()) !== rememberMe) {
    await page.locator('label.taraz-remember').click();
  }
  await expect(box).toBeChecked({ checked: rememberMe });
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
  // The sign-in itself leads to the company/fiscal period selection (no stop on a success page).
  await expect(page).toHaveURL(/\/app\/$/);
  await expect(page.getByRole('heading', { name: 'فرانو - دوره مالی' })).toBeVisible();
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
  await expect(page).toHaveURL(/\/app\/$/);

  const cookie = (await context.cookies()).find((current) => current.name === '.Faranou.Authentication');
  expect(cookie?.expires).toBeGreaterThan(Date.now() / 1000);
});

test('remember me stores only the username in an HttpOnly preference cookie and restores it on the next visit', async ({ page, context }) => {
  await signIn(page, alice, { rememberMe: true });
  await expect(page).toHaveURL(/\/app\/$/);

  const remembered = (await context.cookies()).find((current) => current.name === '.Faranou.RememberedUsername');
  expect(remembered?.value).toBe(alice.username);
  expect(remembered?.httpOnly).toBe(true);
  expect(remembered?.sameSite).toBe('Lax');
  expect(remembered?.path).toBe('/');
  expect(remembered?.expires).toBeGreaterThan(Date.now() / 1000);
  // Only the username is kept: no cookie of the app and no browser storage holds the password.
  const everything = JSON.stringify(await context.cookies()) +
    (await page.evaluate(() => JSON.stringify({ ...localStorage }) + JSON.stringify({ ...sessionStorage })));
  expect(everything.includes(alice.password)).toBe(false);

  await context.clearCookies({ name: '.Faranou.Authentication' });
  await page.goto('/Account/Login');
  await expect(page.locator('#ViewModel_Username')).toHaveValue(alice.username);
  await expect(page.locator('#ViewModel_RememberMe')).toBeChecked();
  await expect(page.locator('#ViewModel_Password')).toHaveValue('');
  await expectCleanPage(page);
});

test('without the option no username is remembered, and turning it off removes the remembered one', async ({ page, context }) => {
  await signIn(page, alice);
  await expect(page).toHaveURL(/\/app\/$/);
  expect((await context.cookies()).some((cookie) => cookie.name === '.Faranou.RememberedUsername')).toBe(false);
  await context.clearCookies();

  // Remember it first...
  await signIn(page, alice, { rememberMe: true });
  await expect(page).toHaveURL(/\/app\/$/);
  expect((await context.cookies()).some((cookie) => cookie.name === '.Faranou.RememberedUsername')).toBe(true);
  await context.clearCookies({ name: '.Faranou.Authentication' });

  // ...then sign in again with the option off: the preference cookie is removed and not restored.
  await signIn(page, alice, { rememberMe: false });
  await expect(page).toHaveURL(/\/app\/$/);
  expect((await context.cookies()).some((cookie) => cookie.name === '.Faranou.RememberedUsername')).toBe(false);
  await context.clearCookies();
  await page.goto('/Account/Login');
  await expect(page.locator('#ViewModel_Username')).toHaveValue('');
  await expect(page.locator('#ViewModel_RememberMe')).not.toBeChecked();
});

test('a failed sign-in with the option does not remember the username, and shows no password', async ({ page, context }) => {
  await page.goto('/Account/Login');
  await page.locator('#ViewModel_Username').fill(alice.username);
  await page.locator('#ViewModel_Password').fill(`${alice.password}-wrong`);
  await page.locator('label.taraz-remember').click();
  await page.locator('form.taraz-login-form button[type="submit"]').click();

  await expect(page).toHaveURL(/\/Account\/Login/i);
  expect((await context.cookies()).some((cookie) => cookie.name === '.Faranou.RememberedUsername')).toBe(false);
  await expect(page.locator('#ViewModel_Password')).toHaveValue('');
  await page.goto('/Account/Login');
  await expect(page.locator('#ViewModel_Username')).toHaveValue('');
});

test('the sign-in form is marked for the browser password manager, and the app says it does not store the password', async ({ page }) => {
  await page.goto('/Account/Login');
  await expect(page.locator('#ViewModel_Username')).toHaveAttribute('autocomplete', 'username');
  await expect(page.locator('#ViewModel_Password')).toHaveAttribute('autocomplete', 'current-password');
  await expect(page.locator('.taraz-security-note', { hasText: 'فقط نام کاربری را ذخیره می‌کند' })).toBeVisible();
});

async function chooseAlicesCompany(page) {
  await page.goto('/app/');
  await expect(page.getByRole('heading', { name: 'فرانو - دوره مالی' })).toBeVisible();
  await chooseOption(page, a.name, /سال مالی ۱۴۰۵/);
}

// The radios are visually hidden inside their label cards, so the label is what a user clicks.
// The flow is year first (right panel), then the company (left panel).
async function chooseOption(page, companyName, periodPattern) {
  const periodRadio = page.getByRole('radio', { name: periodPattern });
  if (!(await periodRadio.isChecked())) {
    await page.locator('label', { has: periodRadio }).click();
  }
  await expect(periodRadio).toBeChecked();
  const companyRadio = page.getByRole('radio', { name: new RegExp(companyName) });
  await page.locator('label', { has: companyRadio }).click();
  await expect(companyRadio).toBeChecked();
  await page.getByRole('button', { name: /ورود به محیط کاری/ }).click();
  await expect(page.getByRole('heading', { name: 'داشبورد' })).toBeVisible();
}

test('several companies: the user chooses, a closed period is view-only and foreign data is not offered', async ({ page }) => {
  await signIn(page, alice);
  await page.goto('/app/');

  await expect(page.getByRole('heading', { name: 'فرانو - دوره مالی' })).toBeVisible();
  await expect(page.getByText(b.name)).toHaveCount(0);
  await expect(page.getByText(state.secretPeriodTitle)).toHaveCount(0);
  // A company without a fiscal period cannot be entered: it is named in a note, never offered as a row.
  await expect(page.getByRole('radio', { name: new RegExp(c.name) })).toHaveCount(0);
  await expect(page.getByText('دوره مالی قابل ورود برای این شرکت‌ها وجود ندارد')).toContainText(c.name);

  // The closed period is offered, marked as view-only; it is not hidden.
  await page.locator('label', { has: page.getByRole('radio', { name: /سال مالی ۱۴۰۴/ }) }).click();
  await expect(page.getByText('دوره بسته فقط برای مشاهده و گزارش‌گیری')).toBeVisible();
  await page.locator('label', { has: page.getByRole('radio', { name: new RegExp(a.name) }) }).click();
  await expect(page.getByText('فقط مشاهده و گزارش‌گیری', { exact: true }).first()).toBeVisible();

  await chooseAlicesCompany(page);
  await expect(page.locator('table').getByText('۱۴۰۵/۰۱/۰۱')).toBeVisible();
  await expect(page.getByText('فقط مشاهده و گزارش‌گیری')).toHaveCount(0);
  await expect(page.getByLabel('زمینه کاری جاری')).toContainText(a.name);
  await expectCleanPage(page);
});

test('right after sign-in, with no manual navigation, every account kind reaches its proper step', async ({ page }) => {
  // Several companies and periods: the selection (and not the old success page).
  await signIn(page, alice);
  await expect(page).toHaveURL(/\/app\/$/);
  await expect(page.getByRole('heading', { name: 'فرانو - دوره مالی' })).toBeVisible();
  await expect(page.getByText(b.name)).toHaveCount(0);
  await page.context().clearCookies();

  // Exactly one company with one open period: direct entry (D-031).
  await signIn(page, bob);
  await expect(page.getByRole('heading', { name: 'داشبورد' })).toBeVisible();
  await expect(page.getByLabel('زمینه کاری جاری')).toContainText(b.name);
  await page.context().clearCookies();

  // Company without a usable period, and no company: the status page, never a blank page.
  await signIn(page, dave);
  await expect(page.getByRole('heading', { name: 'دوره مالی قابل ورودی وجود ندارد' })).toBeVisible();
  await page.context().clearCookies();
  await signIn(page, erin);
  await expect(page.getByRole('heading', { name: 'شرکتی برای ورود در دسترس نیست' })).toBeVisible();
});

test('the old success URL only forwards a signed-in user, and an anonymous one is sent to sign-in', async ({ page }) => {
  await page.goto('/Account/LoginSuccess');
  await expect(page).toHaveURL(/\/Account\/Login\?ReturnUrl=/i);

  await signIn(page, alice);
  await page.goto('/Account/LoginSuccess');
  await expect(page).toHaveURL(/\/app\/$/);
  await expect(page.getByRole('heading', { name: 'فرانو - دوره مالی' })).toBeVisible();
});

test('a single company with a single open period is entered directly', async ({ page }) => {
  await signIn(page, bob);
  await page.goto('/app/');

  await expect(page.getByRole('heading', { name: 'داشبورد' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'فرانو - دوره مالی' })).toHaveCount(0);
  await expect(page.getByLabel('زمینه کاری جاری')).toContainText(b.name);
  await expectCleanPage(page);
});

test('a company without a fiscal period blocks the entry with an explanation', async ({ page }) => {
  await signIn(page, dave);
  await page.goto('/app/');

  await expect(page.getByRole('heading', { name: 'دوره مالی قابل ورودی وجود ندارد' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'داشبورد' })).toHaveCount(0);
  await expectCleanPage(page);
});

test('a closed period opens for viewing only, and switching back leaves nothing of it behind', async ({ page }) => {
  await signIn(page, alice);
  await page.goto('/app/');
  await expect(page.getByRole('heading', { name: 'فرانو - دوره مالی' })).toBeVisible();
  await chooseOption(page, a.name, /سال مالی ۱۴۰۴/);
  await expect(page.getByLabel('زمینه کاری جاری')).toContainText('فقط مشاهده و گزارش‌گیری');

  // Switching in the same sign-in: no new login, the badge belongs to the closed period only.
  await page.getByRole('button', { name: /تغییر شرکت یا دوره/ }).click();
  await expect(page.getByRole('heading', { name: 'فرانو - دوره مالی' })).toBeVisible();
  await chooseOption(page, a.name, /سال مالی ۱۴۰۵/);
  await expect(page.getByLabel('زمینه کاری جاری')).not.toContainText('فقط مشاهده و گزارش‌گیری');
  await expectCleanPage(page);
});

test('a user without any company is blocked with an explanation', async ({ page }) => {
  await signIn(page, erin);
  await page.goto('/app/');

  await expect(page.getByRole('heading', { name: 'شرکتی برای ورود در دسترس نیست' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'داشبورد' })).toHaveCount(0);
  await expectCleanPage(page);
});

test('every new sign-in asks again; the previous choice is not restored', async ({ page }) => {
  await signIn(page, alice);
  await chooseAlicesCompany(page);

  await page.getByRole('button', { name: 'خروج' }).click();
  await expect(page).toHaveURL(/\/Account\/Login/i);

  await signIn(page, alice);
  await page.goto('/app/');
  await expect(page.getByRole('heading', { name: 'فرانو - دوره مالی' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'داشبورد' })).toHaveCount(0);
});

test('year first: same-name companies are told apart by their national id, and an archived year cannot be chosen', async ({ page }) => {
  await signIn(page, frank);
  await expect(page).toHaveURL(/\/app\/$/);
  await expect(page.getByRole('heading', { name: 'فرانو - دوره مالی' })).toBeVisible();
  await expect(page.getByText('ابتدا یک دوره مالی انتخاب کنید.')).toBeVisible();
  await expect(page.getByRole('button', { name: /ورود به محیط کاری/ })).toBeDisabled();

  // The archived year is shown but not selectable.
  await expect(page.getByRole('radio', { name: /سال مالی ۱۴۰۳/ })).toBeDisabled();

  await page.locator('label', { has: page.getByRole('radio', { name: /سال مالی ۱۴۰۵/ }) }).click();
  const sameName = page.getByRole('radio', { name: new RegExp(e.name) });
  await expect(sameName).toHaveCount(2);
  await expect(page.getByText('۱۰۲۰۰۲۳۴۵۶۷', { exact: false }).or(page.getByText('10200234567'))).toBeVisible();
  await expect(page.getByText('۱۰۳۰۰۳۴۵۶۷۸', { exact: false }).or(page.getByText('10300345678'))).toBeVisible();
  await expect(page.getByRole('button', { name: /ورود به محیط کاری/ })).toBeDisabled();
  await expectCleanPage(page);
});

test('year first: switching the year quickly leaves only the companies of the last year', async ({ page }) => {
  await signIn(page, frank);
  const open = page.locator('label', { has: page.getByRole('radio', { name: /سال مالی ۱۴۰۵/ }) });
  const closed = page.locator('label', { has: page.getByRole('radio', { name: /سال مالی ۱۴۰۴/ }) });
  for (let i = 0; i < 4; i += 1) {
    await open.click();
    await closed.click();
  }
  await expect(page.getByRole('radio', { name: /سال مالی ۱۴۰۴/ })).toBeChecked();
  // 1404 exists for the long-named company and for the second same-name company only.
  await expect(page.getByRole('radio', { name: new RegExp(d.name.slice(0, 20)) })).toHaveCount(1);
  await expect(page.getByRole('radio', { name: new RegExp(e.name) })).toHaveCount(1);
  await expect(page.getByRole('button', { name: /ورود به محیط کاری/ })).toBeDisabled();
});

test('year first: the chosen pair enters once, with a loading state and no double submit', async ({ page }) => {
  await signIn(page, frank);
  await page.locator('label', { has: page.getByRole('radio', { name: /سال مالی ۱۴۰۵/ }) }).click();
  await page.locator('label', { has: page.getByRole('radio', { name: new RegExp(d.name.slice(0, 20)) }) }).click();
  let posts = 0;
  page.on('request', (request) => {
    if (request.method() === 'POST' && request.url().endsWith('/api/v1/context')) posts += 1;
  });
  const enter = page.getByRole('button', { name: /ورود به محیط کاری/ });
  await enter.dblclick();
  await expect(page.getByRole('heading', { name: 'داشبورد' })).toBeVisible();
  expect(posts).toBe(1);
  await expect(page.getByLabel('زمینه کاری جاری')).toContainText(d.name.slice(0, 20));
});

test('the selection page loads only local fonts and images and shows the official mark', async ({ page }) => {
  const external = [];
  const requested = [];
  page.on('request', (request) => {
    const url = new URL(request.url());
    requested.push(url.pathname);
    if (!/^(localhost|127\.0\.0\.1)$/.test(url.hostname)) external.push(request.url());
  });
  await signIn(page, frank);
  await expect(page.getByRole('heading', { name: 'فرانو - دوره مالی' })).toBeVisible();
  await page.evaluate(() => document.fonts.ready);
  expect(external).toEqual([]);
  expect(requested).toContain('/images/faranou-brand-mark.webp');
  expect(requested.some((path) => path.includes('/fonts/vazirmatn/'))).toBe(true);
  const family = await page.evaluate(() => getComputedStyle(document.querySelector('h1')).fontFamily);
  expect(family).toContain('Vazirmatn');
  await expect(page.getByRole('img', { name: 'نشان فرانو' })).toBeVisible();
});

test('final design: header has only real controls, companies are single rows, both searches filter real data', async ({ page }) => {
  await signIn(page, frank);
  await expect(page.getByRole('heading', { name: 'فرانو - دوره مالی' })).toBeVisible();
  // Header: brand, user and sign-out only; no dashboard title, no invented bell/settings.
  await expect(page.getByText('فرانو - داشبورد')).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'خروج' })).toBeVisible();
  await expect(page.getByRole('button', { name: /اعلان|تنظیمات/ })).toHaveCount(0);
  await expect(page.getByText(/همگروه|زیرمجموعه|شرکت مادر/)).toHaveCount(0);

  // Year search filters the real list.
  await page.getByRole('searchbox', { name: /دوره/ }).fill('۱۴۰۴');
  await expect(page.getByRole('radio', { name: /سال مالی ۱۴۰۴/ })).toBeVisible();
  await expect(page.getByRole('radio', { name: /سال مالی ۱۴۰۵/ })).toHaveCount(0);
  await page.getByRole('searchbox', { name: /دوره/ }).fill('');
  await page.locator('label', { has: page.getByRole('radio', { name: /سال مالی ۱۴۰۵/ }) }).click();

  // One company per row: every row starts at the same left edge and rows are stacked.
  const rows = page.getByRole('radiogroup', { name: /شرکت/ }).locator('label');
  await expect(rows).toHaveCount(3);
  const boxes = await rows.evaluateAll((nodes) => nodes.map((node) => node.getBoundingClientRect()).map((r) => ({ x: Math.round(r.x), w: Math.round(r.width), y: Math.round(r.y) })));
  expect(new Set(boxes.map((b) => b.x)).size).toBe(1);
  expect(new Set(boxes.map((b) => b.w)).size).toBe(1);
  expect(boxes[1].y).toBeGreaterThan(boxes[0].y);

  // Company search by national id keeps only the matching real company.
  await page.getByRole('searchbox', { name: /شرکت/ }).fill('10300345678');
  await expect(rows).toHaveCount(1);
  await expectCleanPage(page);
});

test('the context API refuses a foreign company, a foreign period and tampering', async ({ page }) => {
  await signIn(page, alice);
  await page.goto('/app/');

  const result = await page.evaluate(async ({ foreign, own }) => {
    const token = await (await fetch('/api/v1/antiforgery/token')).json();
    const post = async (body, withToken = true) => {
      const response = await fetch('/api/v1/context', {
        method: 'POST',
        credentials: 'same-origin',
        headers: { 'Content-Type': 'application/json', ...(withToken ? { [token.headerName]: token.requestToken } : {}) },
        body: JSON.stringify(body),
      });
      return { status: response.status, body: await response.text() };
    };
    const context = await (await fetch('/api/v1/context')).json();
    const ownPeriod = context.companies.find((company) => company.id === own).fiscalPeriods[0].id;
    return {
      foreignCompany: await post({ companyId: foreign, fiscalPeriodId: ownPeriod }),
      unknownPeriod: await post({ companyId: own, fiscalPeriodId: crypto.randomUUID() }),
      noToken: await post({ companyId: own, fiscalPeriodId: ownPeriod }, false),
    };
  }, { foreign: b.id, own: a.id });

  expect(result.foreignCompany.status).toBe(404);
  expect(result.unknownPeriod.status).toBe(404);
  expect(result.foreignCompany.body).toBe(result.unknownPeriod.body);
  expect(result.noToken.status).toBe(400);
  expect(result.foreignCompany.body).not.toContain(state.secretPeriodTitle);
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
  await chooseAlicesCompany(page);

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
  await page.goto('/app/');
  await expect(page).toHaveURL(/\/Account\/Login\?ReturnUrl=%2Fapp%2F/i);

  await page.locator('#ViewModel_Username').fill(alice.username);
  await page.locator('#ViewModel_Password').fill(alice.password);
  await page.locator('form.taraz-login-form button[type="submit"]').click();
  await expect(page).toHaveURL(/\/app\//);
  await expect(page.getByRole('heading', { name: 'فرانو - دوره مالی' })).toBeVisible();
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
  // The infrastructure account has no company: it lands on the Razor home, not on the selection.
  await expect.poll(() => new URL(page.url()).pathname).toBe('/');

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

  await expect(page).toHaveTitle(/فرانو/);
  await expect(page.locator('#Input_AuthenticationMode')).toBeVisible();
  await expect(page.locator('[data-sql-only]').first()).toBeHidden();
  await page.locator('#Input_AuthenticationMode').selectOption('Sql');
  await expect(page.locator('[data-sql-only]').first()).toBeVisible();

  // Password show/hide works without inline script.
  await page.locator('#Input_Password').fill('not-a-real-secret');
  await page.locator('[data-toggle-password]').click();
  await expect(page.locator('#Input_Password')).toHaveAttribute('type', 'text');
  await page.locator('[data-toggle-password]').click();
  await expect(page.locator('#Input_Password')).toHaveAttribute('type', 'password');

  // An empty submit shows every independent error next to its own field, in one pass.
  await page.locator('#Input_Password').fill('');
  await page.locator('[data-submit]').click();
  for (const field of ['SetupToken', 'Server', 'UserName', 'Password']) {
    await expect(page.locator(`[data-error-for="${field}"]`)).toBeVisible();
  }
  await expectCleanPage(page);
});
