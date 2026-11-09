import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { App } from './App';
import { openSystemMenu } from './test/sidebar';
// Preload the lazy create-form chunk (the same pattern the page-level test files use):
// the first dynamic import in vitest compiles the whole module graph on the fly inside the
// assertion window, which is host-speed dependent (the Linux e2e job and the Windows publish
// job both exceeded the wait there). With the module already in the registry the `lazy()`
// import resolves instantly and the test measures behaviour, not transform speed.
import './features/hr/pages/HrEmployeeCreatePage';
import { json, listBody, userItem } from './test/adminUsers';
import { company, contextBody, period, selected, session } from './test/api';
import { companiesBody, roleItem, systemRole } from './test/roles';

// Each test renders the whole App and a lazy HR chunk: allow slow CI runners (the Windows publish job timed out at 5 s).
vi.setConfig({ testTimeout: 30_000 });

// Integration of the finished HR frontend (PR #555) into the shared shell: routes, sidebar, company permissions.
const p1 = period('p1', 'سال مالی ۱۴۰۵');
const companyA = company('a', 'شرکت الف', [p1], '10101010101');
const overview = {
  company: { id: 'a', name: 'شرکت الف', nationalId: '10101010101', roleName: 'حسابدار' },
  fiscalPeriods: [{ id: 'p1', title: 'سال مالی ۱۴۰۵', startDate: '2026-03-21', endDate: '2027-03-20', status: 'Open' }],
};

type Mode = { installationAdmin?: boolean; codes?: string[]; permissions?: 'ok' | 'fail' | 'pending' };

function stubAll({ installationAdmin = false, codes = [], permissions = 'ok' }: Mode = {}) {
  const fetchMock = vi.fn((input: string, init?: RequestInit) => {
    const method = init?.method ?? 'GET';
    const url = new URL(input, 'http://localhost');
    if (url.pathname === '/api/v1/me/permissions') {
      if (permissions === 'pending') return new Promise<Response>(() => undefined);
      if (permissions === 'fail') return Promise.resolve(new Response(null, { status: 500 }));
      return Promise.resolve(json({ isInstallationAdministrator: installationAdmin, company: { id: 'a', permissionCodes: codes } }));
    }
    const routes: Record<string, () => Response> = {
      'GET /api/v1/antiforgery/token': () => json({ headerName: 'X-XSRF-TOKEN', requestToken: 't' }),
      'GET /api/v1/session': () => json({ ...session, isAdministrator: installationAdmin }),
      'GET /api/v1/context': () => json(contextBody({ entry: 'directEntry', directSelection: { companyId: 'a', fiscalPeriodId: 'p1' }, companies: [companyA] })),
      'POST /api/v1/context': () => json(selected(companyA, p1)),
      'GET /api/v1/companies/a/overview': () => json(overview),
      'GET /api/v1/admin/users': () => json(listBody([userItem(1)])),
      'GET /api/v1/companies': () => json(companiesBody),
      'GET /api/v1/admin/companies/a/roles': () => json([roleItem(), systemRole]),
    };
    return Promise.resolve(routes[`${method} ${url.pathname}`]?.() ?? new Response(null, { status: 404 }));
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

const hrCalls = (fetchMock: ReturnType<typeof stubAll>) => fetchMock.mock.calls.filter((call) => String(call[0]).includes('/org/'));
const unitsLink = { name: 'واحدهای سازمانی' };
const positionsLink = { name: 'سمت‌های سازمانی' };
const employeesLink = { name: 'کارکنان' };
const hrLabels = [unitsLink.name, positionsLink.name, employeesLink.name];

/** Renders the app and opens «مدیریت سیستم» by a click (the sidebar starts folded; the route never opens it). */
async function openShell(path = '/app/') {
  window.history.replaceState(null, '', path);
  render(<App navigate={vi.fn()} />);
  const nav = await screen.findByRole('navigation', { name: 'منوی اصلی' });
  await openSystemMenu(userEvent.setup(), nav);
  return nav;
}

describe('HR sidebar entries (company-scoped permissions)', () => {
  beforeEach(() => window.history.replaceState(null, '', '/app/'));

  it.each([
    ['hr.organizational-units.list', unitsLink, [positionsLink, employeesLink]],
    ['hr.positions.list', positionsLink, [unitsLink, employeesLink]],
  ] as const)('with only %s shows only its own entry', async (code, shown, hidden) => {
    stubAll({ codes: [code] });
    const nav = await openShell();
    expect(await within(nav).findByRole('link', shown)).toBeInTheDocument();
    for (const link of hidden) expect(within(nav).queryByRole('link', link)).toBeNull();
  });

  it('keeps «کارکنان» a disabled «به‌زودی» entry (not a link) even with hr.employees.list', async () => {
    stubAll({ codes: ['hr.employees.list'] });
    const nav = await openShell();
    expect(within(nav).queryByRole('link', employeesLink)).toBeNull();
    expect(within(nav).getByText('کارکنان').closest('[aria-disabled="true"]')).toBeTruthy();
  });

  it('shows no HR entry to a member without HR permissions, but keeps the existing entries', async () => {
    stubAll({ codes: ['users.list'] });
    const nav = await openShell();
    expect(await within(nav).findByRole('link', { name: 'کاربران شرکت جاری' })).toBeInTheDocument();
    for (const label of hrLabels) expect(within(nav).queryByRole('link', { name: label })).toBeNull();
  });

  it('shows all three entries, with the right targets, to an installation administrator', async () => {
    stubAll({ installationAdmin: true });
    const nav = await openShell();
    expect(await within(nav).findByRole('link', unitsLink)).toHaveAttribute('href', '/app/hr/org/units');
    expect(within(nav).getByRole('link', positionsLink)).toHaveAttribute('href', '/app/hr/org/positions');
    expect(within(nav).queryByRole('link', employeesLink)).toBeNull();
    expect(within(nav).getByText('کارکنان').closest('[aria-disabled="true"]')).toBeTruthy();
    expect(within(nav).queryByRole('link', { name: 'گروه‌های دسترسی' })).toBeNull();
    expect(within(nav).queryByText('گروه‌های دسترسی')).toBeNull();
  });

  it('shows no HR entry while permissions are loading and none after a failure (fail-closed)', async () => {
    stubAll({ installationAdmin: true, permissions: 'pending' });
    const nav = await openShell();
    for (const label of hrLabels) expect(within(nav).queryByRole('link', { name: label })).toBeNull();
  });

  it('shows no HR entry after the permission request failed, even for an administrator session', async () => {
    stubAll({ installationAdmin: true, permissions: 'fail' });
    const nav = await openShell();
    expect(await within(nav).findByText('دریافت مجوزهای شرکت انجام نشد.')).toBeInTheDocument();
    for (const label of hrLabels) expect(within(nav).queryByRole('link', { name: label })).toBeNull();
  });

  it('opens the employee create form INSIDE the list tab (Local Form Flow, PR 5)', async () => {
    const user = userEvent.setup();
    stubAll({ codes: ['hr.employees.list', 'hr.employees.create'] });
    await openShell('/app/hr/org/employees');
    expect(await screen.findByRole('heading', { level: 1, name: 'کارکنان' })).toBeInTheDocument();

    await user.click(await screen.findByRole('button', { name: 'کارمند جدید' }));
    // The create form is a lazy chunk inside the list tab; cold transform on a slow CI
    // runner can take several seconds.
    expect(await screen.findByRole('heading', { level: 1, name: 'ثبت کارمند' }, { timeout: 10000 })).toBeInTheDocument();
    // Same tab, no duplicate: the workspace still holds exactly one tab and the query
    // carries the form state.
    expect(window.location.search).toBe('?form=new');
    expect(document.querySelectorAll('[data-tab-pane]')).toHaveLength(1);
    expect(screen.queryByRole('button', { name: 'بستن ثبت کارمند' })).not.toBeInTheDocument();

    // A clean form returns to the list in the same tab without any dialog.
    await user.click(screen.getByRole('button', { name: 'انصراف' }));
    expect(await screen.findByRole('heading', { level: 1, name: 'کارکنان' })).toBeInTheDocument();
    expect(window.location.search).toBe('');
    expect(document.querySelectorAll('[data-tab-pane]')).toHaveLength(1);
  });

  it('navigates from each entry to its page and marks the entry active on nested routes', async () => {
    const user = userEvent.setup();
    stubAll({ codes: ['hr.organizational-units.list', 'hr.positions.list', 'hr.employees.list'] });
    const nav = await openShell();
    await user.click(await within(nav).findByRole('link', unitsLink));
    expect(await screen.findByRole('heading', { level: 1, name: 'واحدهای سازمانی' })).toBeInTheDocument();
    expect(within(nav).getByRole('link', unitsLink)).toHaveAttribute('aria-current', 'page');
    await user.click(within(nav).getByRole('link', positionsLink));
    expect(await screen.findByRole('heading', { level: 1, name: 'سمت‌های سازمانی' })).toBeInTheDocument();
    expect(within(nav).getByRole('link', positionsLink)).toHaveAttribute('aria-current', 'page');
    expect(within(nav).getByRole('link', unitsLink)).not.toHaveAttribute('aria-current');
    // The employees list is deferred in the menu; its route is covered by 'HR routes' (deep link).
    expect(within(nav).queryByRole('link', employeesLink)).toBeNull();
  });

  it.each([
    ['/app/hr/org/units/new', unitsLink],
    ['/app/hr/org/positions/new', positionsLink],
  ] as const)('keeps the entry active on the nested route %s', async (path, link) => {
    stubAll({ codes: ['hr.organizational-units.list', 'hr.positions.list', 'hr.employees.list'] });
    const nav = await openShell(path);
    expect(await within(nav).findByRole('link', link)).toHaveAttribute('aria-current', 'page');
  });
});

describe('HR routes', () => {
  const all = ['hr.organizational-units.list', 'hr.positions.list', 'hr.employees.list', 'hr.employees.view'];

  it.each([
    ['/app/hr/org/units', 'واحدهای سازمانی'],
    ['/app/hr/org/units/new', 'ایجاد واحد سازمانی'],
    ['/app/hr/org/positions', 'سمت‌های سازمانی'],
    ['/app/hr/org/positions/new', 'ایجاد سمت سازمانی'],
    ['/app/hr/org/employees', 'کارکنان'],
    ['/app/hr/org/employees/new', 'ثبت کارمند'],
  ])('opens %s directly (deep link / refresh)', async (path, heading) => {
    stubAll({ codes: all });
    await openShell(path);
    expect(await screen.findByRole('heading', { level: 1, name: heading })).toBeInTheDocument();
  });

  it('routes parameterized HR paths to the detail and edit pages, not to the static /new', async () => {
    const fetchMock = stubAll({ codes: all });
    await openShell('/app/hr/org/employees/8c5b7e2a-1111-4000-8000-00000000abcd/edit');
    await vi.waitFor(() => expect(hrCalls(fetchMock).length).toBeGreaterThan(0));
    expect(screen.queryByRole('heading', { level: 1, name: 'ثبت کارمند' })).toBeNull();
  });

  it('still shows not-found for an unknown HR path and keeps existing routes working', async () => {
    stubAll({ codes: all, installationAdmin: true });
    await openShell('/app/hr/org/unknown-page');
    expect(await screen.findByRole('heading', { level: 1, name: 'صفحه پیدا نشد' })).toBeInTheDocument();
    window.history.replaceState(null, '', '/app/admin/roles');
    const { unmount } = render(<App navigate={vi.fn()} />);
    expect(await screen.findAllByRole('heading', { level: 1, name: 'گروه‌های دسترسی' })).not.toHaveLength(0);
    unmount();
  });

  it('denies a member without the HR permission on a deep link and never requests HR data', async () => {
    const fetchMock = stubAll({ codes: ['hr.positions.list'] });
    await openShell('/app/hr/org/employees');
    expect(await screen.findByRole('heading', { level: 1, name: 'دسترسی ندارید' })).toBeInTheDocument();
    expect(hrCalls(fetchMock)).toHaveLength(0);
  });

  it('shows no HR page and requests no HR data while permissions are loading', async () => {
    const fetchMock = stubAll({ installationAdmin: true, permissions: 'pending' });
    await openShell('/app/hr/org/units');
    expect(await screen.findByText('در حال دریافت مجوزهای شما…')).toBeInTheDocument();
    expect(screen.queryByRole('heading', { level: 1, name: 'واحدهای سازمانی' })).toBeNull();
    expect(hrCalls(fetchMock)).toHaveLength(0);
  });

  it('fails closed on a permission failure for an HR deep link', async () => {
    const fetchMock = stubAll({ installationAdmin: true, permissions: 'fail' });
    await openShell('/app/hr/org/units');
    expect(await screen.findByText(/دریافت مجوزها انجام نشد/)).toBeInTheDocument();
    expect(hrCalls(fetchMock)).toHaveLength(0);
  });
});
