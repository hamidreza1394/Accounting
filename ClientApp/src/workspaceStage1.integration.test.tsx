import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { openSystemMenu } from './test/sidebar';
import { App } from './App';
import { resetFormsForTests } from './lib/formGuard';
import { antiforgeryRoute, company, contextBody, period, selected, session as apiSession, stubApi, type RouteHandler } from './test/api';

/**
 * The consolidated workspace integration of the stage 1 completion (Issue #444, PR 7):
 * the REAL `App` with a stubbed API, driving the flows the owner mission lists —
 * several tabs, the in-tab create form, the context-change guard with the problem-tabs
 * dialog, the final confirmation, the cancel safety and the overlay selection screen.
 */

const session = apiSession;
const period1405 = period('p1', 'سال مالی ۱۴۰۵');
const period1406 = period('p2', 'سال مالی ۱۴۰۶', { startDate: '2027-03-21', endDate: '2028-03-20' });
const companyA = company('a', 'شرکت الف', [period1405, period1406], '10101010101');
const companyB = company('b', 'شرکت ب', [period1405, period1406], '20202020202');

const employee = {
  id: '0f3a1d2c-0000-4000-8000-000000000001',
  employeeNumber: 'EMP-101',
  firstName: 'علی',
  lastName: 'محمدی',
  employmentStatus: 1,
  hiredOnUtc: '2026-01-10T08:30:00Z',
  terminatedOnUtc: null,
  hasLinkedUser: false,
  unitId: null,
  unitName: null,
  positionId: null,
  positionName: null,
};

const overview = {
  company: { ...companyA, fiscalPeriods: [period1405] },
};

function workContext() {
  return contextBody({
    entry: 'directEntry',
    current: { company: { id: companyA.id, name: companyA.name, nationalId: companyA.nationalId, roleName: companyA.roleName }, fiscalPeriod: period1405 },
    companies: [companyA, companyB],
  });
}

function routes(extra: Record<string, RouteHandler> = {}) {
  return {
    ...antiforgeryRoute,
    'GET /api/v1/session': { status: 200, body: session },
    'GET /api/v1/context': { status: 200, body: workContext() },
    'POST /api/v1/context': { status: 200, body: selected(companyB, period1406) },
    'GET /api/v1/companies/a/overview': { status: 200, body: overview },
    'GET /api/v1/me/permissions': { status: 200, body: { isInstallationAdministrator: true, company: { id: 'a', permissionCodes: ['hr.employees.list', 'hr.employees.create'] } } },
    'GET /api/v1/companies/a/org/employees?page=1&pageSize=25': { status: 200, body: { items: [employee], totalCount: 1 } },
    'GET /api/v1/companies/a/org/units?view=tree': { status: 200, body: { roots: [] } },
    'POST /api/v1/companies/a/org/employees': { status: 201, body: { id: '0f3a1d2c-0000-4000-8000-000000000009' } },
    ...extra,
  } as Record<string, RouteHandler>;
}

function paneKeys(): string[] {
  return [...document.querySelectorAll('[data-tab-pane]')].map((pane) => pane.getAttribute('data-tab-pane') ?? '');
}

beforeEach(() => {
  window.history.replaceState(null, '', '/app/');
});
afterEach(() => {
  window.history.pushState(null, '', '/app/');
  resetFormsForTests();
});

describe('workspace stage 1 completion — the consolidated integration (PR 7)', () => {
  it('multi-tab, in-tab create, the context guard and the overlay selection work end to end', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi(routes());
    render(<App navigate={vi.fn()} />);
    expect(await screen.findByRole('heading', { level: 1, name: /به سامانه یکپارچه فرانو خوش آمدید/ })).toBeInTheDocument();

    // 1) The employees list opens as its own tab through the real sidebar.
    const nav = screen.getByRole('navigation', { name: 'منوی اصلی' });
    await openSystemMenu(user, nav);
    // The sidebar gates the HR entry behind the company permission hints (fail-closed while
    // they load) — wait for the answer, then navigate.
    await user.click(await within(nav).findByRole('link', { name: 'کارکنان' }, { timeout: 10000 }));
    await waitFor(() => expect(paneKeys()).toEqual(['/', '/hr/org/employees']));
    expect(await screen.findByRole('heading', { level: 1, name: 'کارکنان' })).toBeInTheDocument();

    // 2) The create form opens INSIDE the list tab (no new tab) and the form goes Dirty.
    await user.click(await screen.findByRole('button', { name: 'کارمند جدید' }, { timeout: 10000 }));
    expect(await screen.findByRole('heading', { level: 1, name: 'ثبت کارمند' }, { timeout: 10000 })).toBeInTheDocument();
    expect(paneKeys()).toEqual(['/', '/hr/org/employees']);
    expect(window.location.search).toBe('?form=new');
    await user.type(screen.getByLabelText(/شماره پرسنلی/), 'EMP-777');
    await user.type(screen.getByLabelText(/^نام\*$/), 'زهرا');

    // 3) The context change is BLOCKED: the problem-tabs dialog names the tab.
    await user.click(screen.getByRole('button', { name: /شرکت جاری شرکت الف، تغییر شرکت/ }));
    const blocked = await screen.findByRole('dialog', { name: 'تغییر زمینهٔ کاری ممکن نیست' });
    // The tab keeps its ROUTE title while the create form is open inside it (UI_COMPONENTS:
    // route title first, then the declared RECORD name — a create form has no record), so the
    // dialog names the tab «کارکنان» — the tab the user actually has to resolve.
    expect(blocked.textContent).toContain('کارکنان');
    expect(blocked.textContent).not.toContain('ثبت کارمند');
    expect(fetchMock.mock.calls.some((call) => `${call[1]?.method ?? 'GET'} ${String(call[0])}` === 'POST /api/v1/context')).toBe(false);

    // 4) Jumping to the problem tab keeps the flow; the dialog closes, the form stays.
    await user.click(within(blocked).getByRole('button', { name: 'رفتن به تب کارکنان' }));
    await waitFor(() => expect(window.location.search).toBe('?form=new'));
    expect(screen.getByLabelText(/^نام\*$/)).toHaveValue('زهرا');

    // 5) The user resolves the form (explicit cancel through the state-aware exit)...
    await user.click(screen.getByRole('button', { name: 'انصراف' }));
    await user.click(await screen.findByRole('button', { name: 'لغو تغییرات و بازگشت' }));
    expect(await screen.findByRole('heading', { level: 1, name: 'کارکنان' })).toBeInTheDocument();
    expect(window.location.search).toBe('');

    // 6) ...and the final confirmation appears automatically (all forms clean now).
    const confirm = await screen.findByRole('dialog', { name: 'تأیید نهایی تغییر شرکت یا دورهٔ مالی' }, { timeout: 8000 });

    // 7) Cancelling the final confirmation keeps EVERYTHING untouched.
    await user.click(within(confirm).getByRole('button', { name: 'انصراف' }));
    expect(paneKeys()).toEqual(['/', '/hr/org/employees']);
    expect(window.location.pathname).toBe('/app/hr/org/employees');
    expect(fetchMock.mock.calls.some((call) => `${call[1]?.method ?? 'GET'} ${String(call[0])}` === 'POST /api/v1/context')).toBe(false);

    // 8) Confirming hands over to the selection screen, which rides OVER the workspace.
    await user.click(screen.getByRole('button', { name: /شرکت جاری شرکت الف، تغییر شرکت/ }));
    await user.click(await screen.findByRole('button', { name: 'ادامه و انتخاب زمینهٔ جدید' }));
    expect(await screen.findByRole('heading', { name: 'فرانو - دوره مالی' }, { timeout: 10000 })).toBeInTheDocument();
    // The workspace is inert underneath but its tabs still exist in the DOM.
    expect(document.querySelectorAll('[data-tab-pane]').length).toBe(2);

    // 9) Cancelling the selection returns to the exact same workspace.
    await user.click(screen.getByRole('button', { name: 'انصراف' }));
    expect(await screen.findByRole('heading', { level: 1, name: 'کارکنان' })).toBeInTheDocument();
    expect(paneKeys()).toEqual(['/', '/hr/org/employees']);
  }, 30000);

  it('logout with unresolved forms asks first and cancelling keeps the session', async () => {
    const user = userEvent.setup();
    stubApi(routes());
    render(<App navigate={vi.fn()} />);
    await screen.findByRole('heading', { level: 1, name: /به سامانه یکپارچه فرانو خوش آمدید/ });

    const nav = screen.getByRole('navigation', { name: 'منوی اصلی' });
    await openSystemMenu(user, nav);
    await user.click(await within(nav).findByRole('link', { name: 'کارکنان' }, { timeout: 10000 }));
    await user.click(await screen.findByRole('button', { name: 'کارمند جدید' }, { timeout: 10000 }));
    await user.type(await screen.findByLabelText(/شماره پرسنلی/), 'EMP-778');

    await user.click(screen.getByRole('button', { name: 'خروج' }));
    const dialog = await screen.findByRole('alertdialog');
    expect(dialog).toHaveTextContent('خروج از حساب کاربری با فرم‌های تعیین‌تکلیف‌نشده');
    await user.click(within(dialog).getByRole('button', { name: 'انصراف' }));
    expect(screen.getByLabelText(/شماره پرسنلی/)).toHaveValue('EMP-778');
  }, 30000);
});
