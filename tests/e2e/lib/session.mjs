// Shared browser helpers for the membership-workflow E2E specs.
//
// Everything here drives the *published* application: the real Razor sign-in form, the real
// cookie authentication and the real `/api/v1` endpoints. No request is intercepted or mocked.
import { expect } from '@playwright/test';

/** Signs in through the real Razor form and waits for the POST/Redirect/Get to finish. */
export async function signIn(page, user) {
  await page.goto('/Account/Login');
  await page.locator('#ViewModel_Username').fill(user.username);
  await page.locator('#ViewModel_Password').fill(user.password);
  await Promise.all([
    page.waitForNavigation({ waitUntil: 'load' }),
    page.locator('form.taraz-login-form button[type="submit"]').click(),
  ]);
}

export const DASHBOARD_HEADING = 'به سامانه یکپارچه فرانو خوش آمدید';
export const SELECTION_HEADING = 'فرانو - دوره مالی';

/**
 * Signs in and waits until the React shell has settled: either the dashboard (one company with one
 * open period, so the server answers `directEntry`) or the company/period selection (several
 * companies). A blocker page (no company / no period) also counts as settled. Returns which of the
 * three states was reached, so a test can assert on it instead of guessing.
 */
export async function enterWorkspace(page, user) {
  await signIn(page, user);
  await page.goto('/app/');
  const dashboard = page.getByRole('heading', { name: DASHBOARD_HEADING });
  const selection = page.getByRole('heading', { name: SELECTION_HEADING });
  const blocked = page.getByRole('heading', { name: /شرکتی برای ورود در دسترس نیست|دوره مالی قابل ورودی وجود ندارد/ });
  await expect(dashboard.or(selection).or(blocked)).toBeVisible({ timeout: 30_000 });
  if (await dashboard.isVisible()) return 'dashboard';
  if (await selection.isVisible()) return 'selection';
  return 'blocked';
}

/**
 * Calls the real API from the page's own origin, so the authentication and anti-forgery cookies
 * of that browser session are used exactly as the React client would use them.
 */
export async function callApi(page, method, path, { body, token, json = true } = {}) {
  return page.evaluate(async ({ method, path, body, token, json }) => {
    const headers = {};
    if (body !== undefined) headers['Content-Type'] = 'application/json';
    if (token?.headerName && token?.requestToken) headers[token.headerName] = token.requestToken;
    const response = await fetch(path, {
      method,
      credentials: 'same-origin',
      headers,
      ...(body === undefined ? {} : { body: JSON.stringify(body) }),
    });
    const text = await response.text();
    let payload = null;
    if (text && json) {
      try { payload = JSON.parse(text); } catch { payload = text; }
    }
    return {
      status: response.status,
      body: payload,
      cacheControl: response.headers.get('cache-control'),
    };
  }, { method, path, body, token, json });
}

const tokens = new WeakMap();

/**
 * The anti-forgery request token of the current session. It is fetched once per page and reused,
 * so a test does not spend the (real, per-user) sensitive rate-limit budget on tokens.
 */
export async function antiforgery(page, { refresh = false } = {}) {
  if (!refresh && tokens.has(page)) return tokens.get(page);
  const response = await callApi(page, 'GET', '/api/v1/antiforgery/token');
  expect(response.status, 'GET /api/v1/antiforgery/token').toBe(200);
  const token = { headerName: response.body.headerName, requestToken: response.body.requestToken };
  tokens.set(page, token);
  return token;
}

/** `callApi` with the session's anti-forgery token already attached. */
export async function post(page, path, body) {
  return callApi(page, 'POST', path, { body, token: await antiforgery(page) });
}

/** Records enforced CSP violations and uncaught script errors of one page. */
export async function watchPage(page) {
  await page.addInitScript(() => {
    window.__membershipCspViolations = [];
    document.addEventListener('securitypolicyviolation', (event) => {
      window.__membershipCspViolations.push({
        disposition: event.disposition,
        directive: event.effectiveDirective,
        blocked: event.blockedURI,
      });
    });
  });
  const errors = [];
  page.on('pageerror', (error) => errors.push(error.message));
  return {
    errors,
    async assertClean(context) {
      const violations = await page.evaluate(
        () => (window.__membershipCspViolations ?? []).filter((item) => item.disposition === 'enforce'),
      );
      expect(violations, `CSP violations (${context})`).toEqual([]);
      expect(errors, `uncaught script errors (${context})`).toEqual([]);
    },
  };
}
