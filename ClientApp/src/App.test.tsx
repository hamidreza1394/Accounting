import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { App } from './App';
import { antiforgeryRoute, company, contextBody, period, selected, session, stubApi } from './test/api';

const open1405 = period('p1', 'سال مالی ۱۴۰۵');
const open1406 = period('p2', 'سال مالی ۱۴۰۶', { startDate: '2027-03-21', endDate: '2028-03-19' });
const closed1404 = period('p0', 'سال مالی ۱۴۰۴', {
  startDate: '2025-03-21',
  endDate: '2026-03-20',
  status: 'Closed',
  accessMode: 'readOnly',
});

const companyA = company('a', 'شرکت الف', [open1405], '10101010101');
const overviewA = {
  company: { id: 'a', name: 'شرکت الف', nationalId: '10101010101', roleName: 'حسابدار' },
  fiscalPeriods: [
    { id: 'p1', title: 'سال مالی ۱۴۰۵', startDate: '2026-03-21', endDate: '2027-03-20', status: 'Open' },
  ],
};

describe('App', () => {
  beforeEach(() => {
    window.history.replaceState(null, '', '/app/');
  });

  it('enters directly when the server finds one company with one open period', async () => {
    const fetchMock = stubApi({
      ...antiforgeryRoute,
      'GET /api/v1/session': { status: 200, body: session },
      'GET /api/v1/context': {
        status: 200,
        body: contextBody({
          entry: 'directEntry',
          directSelection: { companyId: 'a', fiscalPeriodId: 'p1' },
          companies: [companyA],
        }),
      },
      'POST /api/v1/context': { status: 200, body: selected(companyA, open1405) },
      'GET /api/v1/companies/a/overview': { status: 200, body: overviewA },
    });

    render(<App navigate={vi.fn()} />);

    // No selection screen: the dashboard of the confirmed choice appears.
    expect(await screen.findByRole('heading', { name: 'داشبورد' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'فرانو - دوره مالی' })).not.toBeInTheDocument();

    const post = fetchMock.mock.calls.find((call) => (call[1] as RequestInit | undefined)?.method === 'POST');
    expect(JSON.parse((post?.[1] as RequestInit).body as string)).toEqual({ companyId: 'a', fiscalPeriodId: 'p1' });

    // The context bar names the company and period; real data is shown and none is invented.
    const bar = screen.getByLabelText('زمینه کاری جاری');
    expect(within(bar).getByText('شرکت الف')).toBeInTheDocument();
    expect(within(bar).getByText('سال مالی ۱۴۰۵')).toBeInTheDocument();
    const table = await screen.findByRole('table');
    expect(within(table).getByText('۱۴۰۵/۰۱/۰۱')).toBeInTheDocument();
    expect(screen.getByText(/هیچ آمار مالی/)).toBeInTheDocument();
  });

  it('falls back to the selection screen when the server refuses the automatic entry', async () => {
    stubApi({
      ...antiforgeryRoute,
      'GET /api/v1/session': { status: 200, body: session },
      'GET /api/v1/context': {
        status: 200,
        body: contextBody({
          entry: 'directEntry',
          directSelection: { companyId: 'a', fiscalPeriodId: 'p1' },
          companies: [companyA],
        }),
      },
      'POST /api/v1/context': { status: 409, body: { title: 'این دوره مالی برای ورود در دسترس نیست.' } },
    });

    render(<App navigate={vi.fn()} />);

    expect(await screen.findByRole('heading', { name: 'فرانو - دوره مالی' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'داشبورد' })).not.toBeInTheDocument();
  });

  it('asks the user to choose when there are several companies, and enters only after both choices', async () => {
    const companyB = company('b', 'شرکت ب', [open1406]);
    const postBodies: unknown[] = [];
    stubApi({
      ...antiforgeryRoute,
      'GET /api/v1/session': { status: 200, body: session },
      'GET /api/v1/context': { status: 200, body: contextBody({ companies: [companyA, companyB] }) },
      'POST /api/v1/context': ({ body }) => {
        postBodies.push(body);
        return { status: 200, body: selected(companyB, open1406) };
      },
      'GET /api/v1/companies/b/overview': {
        status: 200,
        body: { company: { id: 'b', name: 'شرکت ب', nationalId: null, roleName: 'حسابدار' }, fiscalPeriods: [] },
      },
    });

    render(<App navigate={vi.fn()} />);

    await screen.findByRole('heading', { name: 'فرانو - دوره مالی' });
    const enter = screen.getByRole('button', { name: /ورود به محیط کاری/ });
    expect(enter).toHaveAttribute('aria-disabled', 'true');

    await userEvent.click(screen.getByRole('radio', { name: /سال مالی ۱۴۰۶/ }));
    // Only company B can be entered in that year, so it is pre-selected, but entering still needs the click.
    expect(screen.getByRole('radio', { name: /شرکت ب/ })).toBeChecked();
    expect(postBodies).toHaveLength(0);

    await userEvent.click(enter);

    expect(await screen.findByRole('heading', { name: 'داشبورد' })).toBeInTheDocument();
    expect(postBodies).toEqual([{ companyId: 'b', fiscalPeriodId: 'p2' }]);
  });

  it('offers every fiscal year of the only company; nothing is chosen for the user, and a closed year is view-only', async () => {
    const company2 = company('a', 'شرکت الف', [open1406, open1405, closed1404]);
    stubApi({
      ...antiforgeryRoute,
      'GET /api/v1/session': { status: 200, body: session },
      'GET /api/v1/context': { status: 200, body: contextBody({ companies: [company2] }) },
    });

    render(<App navigate={vi.fn()} />);

    await screen.findByRole('heading', { name: 'فرانو - دوره مالی' });
    const years = within(screen.getByRole('radiogroup', { name: 'دوره‌های مالی' })).getAllByRole('radio');
    expect(years).toHaveLength(3);
    years.forEach((year) => {
      expect(year).toBeEnabled();
      expect(year).not.toBeChecked();
    });
    expect(screen.getByRole('button', { name: /ورود به محیط کاری/ })).toHaveAttribute('aria-disabled', 'true');

    await userEvent.click(screen.getByRole('radio', { name: /سال مالی ۱۴۰۴/ }));
    expect(screen.getByRole('radio', { name: /شرکت الف/ })).toBeChecked();
    expect(screen.getByText('فقط مشاهده و گزارش‌گیری')).toBeInTheDocument();
  });

  it('enters a closed year as view-only and shows the read-only mode on the dashboard', async () => {
    const company2 = company('a', 'شرکت الف', [open1405, closed1404]);
    const postBodies: unknown[] = [];
    stubApi({
      ...antiforgeryRoute,
      'GET /api/v1/session': { status: 200, body: session },
      'GET /api/v1/context': { status: 200, body: contextBody({ companies: [company2] }) },
      'POST /api/v1/context': ({ body }) => {
        postBodies.push(body);
        return { status: 200, body: selected(company2, closed1404) };
      },
      'GET /api/v1/companies/a/overview': { status: 200, body: overviewA },
    });

    render(<App navigate={vi.fn()} />);

    await screen.findByRole('heading', { name: 'فرانو - دوره مالی' });
    // Two enterable years: neither is chosen for the user.
    expect(screen.getByRole('radio', { name: /سال مالی ۱۴۰۵/ })).not.toBeChecked();

    await userEvent.click(screen.getByRole('radio', { name: /سال مالی ۱۴۰۴/ }));
    await userEvent.click(screen.getByRole('button', { name: /ورود به محیط کاری/ }));

    expect(await screen.findByRole('heading', { name: 'داشبورد' })).toBeInTheDocument();
    expect(postBodies).toEqual([{ companyId: 'a', fiscalPeriodId: 'p0' }]);
    expect(screen.getByText('فقط مشاهده و گزارش‌گیری')).toBeInTheDocument();
  });

  it('drops the data of the previous company when the user switches, and loads the new one', async () => {
    const companyB = company('b', 'شرکت ب', [open1406]);
    const overviewB = {
      company: { id: 'b', name: 'شرکت ب', nationalId: null, roleName: 'حسابدار' },
      fiscalPeriods: [{ id: 'p2', title: 'سال مالی ۱۴۰۶', startDate: '2027-03-21', endDate: '2028-03-19', status: 'Open' }],
    };
    const choices = contextBody({ current: selected(companyA, open1405), companies: [companyA, companyB] });
    const fetchMock = stubApi({
      ...antiforgeryRoute,
      'GET /api/v1/session': { status: 200, body: session },
      'GET /api/v1/context': { status: 200, body: choices },
      'POST /api/v1/context': { status: 200, body: selected(companyB, open1406) },
      'GET /api/v1/companies/a/overview': { status: 200, body: overviewA },
      'GET /api/v1/companies/b/overview': { status: 200, body: overviewB },
    });

    render(<App navigate={vi.fn()} />);
    await screen.findByRole('heading', { name: 'داشبورد' });
    await userEvent.click(await screen.findByRole('button', { name: /تغییر شرکت یا دوره/ }));
    // The current choice (company A, year 1405) is shown; moving to 1406 clears A and pre-selects B.
    expect(await screen.findByRole('radio', { name: /شرکت الف/ })).toBeChecked();
    await userEvent.click(screen.getByRole('radio', { name: /سال مالی ۱۴۰۶/ }));
    expect(screen.getByRole('radio', { name: /شرکت ب/ })).toBeChecked();
    await userEvent.click(screen.getByRole('button', { name: /ورود به محیط کاری/ }));

    await waitFor(() => expect(fetchMock.mock.calls.some((call) => call[0] === '/api/v1/companies/b/overview')).toBe(true));
    // No leftover of company A (its period title) remains once company B is active.
    await waitFor(() => expect(screen.queryByText('سال مالی ۱۴۰۵')).not.toBeInTheDocument());
  });

  it('does not open the working environment when the user has no company', async () => {
    const fetchMock = stubApi({
      ...antiforgeryRoute,
      'GET /api/v1/session': { status: 200, body: session },
      'GET /api/v1/context': { status: 200, body: contextBody({ entry: 'noCompany' }) },
    });

    render(<App navigate={vi.fn()} />);

    expect(await screen.findByRole('heading', { name: 'شرکتی برای ورود در دسترس نیست' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'داشبورد' })).not.toBeInTheDocument();
    expect(fetchMock.mock.calls.some((call) => String(call[0]).includes('/overview'))).toBe(false);
  });

  it('explains that a company has no enterable fiscal period and does not open the environment', async () => {
    const archived = period('p9', 'سال بایگانی', { status: 'Archived', isSelectable: false, accessMode: null });
    stubApi({
      ...antiforgeryRoute,
      'GET /api/v1/session': { status: 200, body: session },
      'GET /api/v1/context': {
        status: 200,
        body: contextBody({ entry: 'noEligiblePeriod', companies: [company('a', 'شرکت الف', [archived])] }),
      },
    });

    render(<App navigate={vi.fn()} />);

    expect(await screen.findByRole('heading', { name: 'دوره مالی قابل ورودی وجود ندارد' })).toBeInTheDocument();
    expect(screen.getByText('شرکت الف')).toBeInTheDocument();
    expect(screen.getByText('بدون دوره مالی قابل ورود')).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'داشبورد' })).not.toBeInTheDocument();
  });

  it('keeps the validated choice of the same sign-in after a reload, without asking again', async () => {
    const fetchMock = stubApi({
      ...antiforgeryRoute,
      'GET /api/v1/session': { status: 200, body: session },
      'GET /api/v1/context': {
        status: 200,
        body: contextBody({ current: selected(companyA, open1405), companies: [companyA, company('b', 'شرکت ب', [open1406])] }),
      },
      'GET /api/v1/companies/a/overview': { status: 200, body: overviewA },
    });

    render(<App navigate={vi.fn()} />);

    expect(await screen.findByRole('heading', { name: 'داشبورد' })).toBeInTheDocument();
    expect(fetchMock.mock.calls.some((call) => (call[1] as RequestInit | undefined)?.method === 'POST')).toBe(false);
  });

  it('lets the user change company or period in the same sign-in, and cancel', async () => {
    const companyB = company('b', 'شرکت ب', [open1406]);
    const contexts = [
      contextBody({ current: selected(companyA, open1405), companies: [companyA, companyB] }),
      contextBody({ current: selected(companyA, open1405), companies: [companyA, companyB] }),
    ];
    const fetchMock = stubApi({
      ...antiforgeryRoute,
      'GET /api/v1/session': { status: 200, body: session },
      'GET /api/v1/context': () => ({ status: 200, body: contexts.shift() }),
      'GET /api/v1/companies/a/overview': { status: 200, body: overviewA },
    });

    render(<App navigate={vi.fn()} />);

    await userEvent.click(await screen.findByRole('button', { name: /تغییر شرکت یا دوره/ }));

    // The selector opens with the current choice pre-selected, and the choices were reloaded.
    expect(await screen.findByRole('heading', { name: 'فرانو - دوره مالی' })).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: /شرکت الف/ })).toBeChecked();
    expect(screen.getByRole('radio', { name: /سال مالی ۱۴۰۵/ })).toBeChecked();
    expect(fetchMock.mock.calls.filter((call) => call[0] === '/api/v1/context')).toHaveLength(2);

    await userEvent.click(screen.getByRole('button', { name: 'انصراف' }));
    expect(await screen.findByRole('heading', { name: 'داشبورد' })).toBeInTheDocument();
  });

  it('sends one request even when the entry button is activated twice quickly', async () => {
    const companyB = company('b', 'شرکت ب', [open1406]);
    let release: (() => void) | undefined;
    let posts = 0;
    stubApi({
      ...antiforgeryRoute,
      'GET /api/v1/session': { status: 200, body: session },
      'GET /api/v1/context': { status: 200, body: contextBody({ companies: [companyA, companyB] }) },
      'POST /api/v1/context': () => {
        posts += 1;
        return { status: 200, body: selected(companyB, open1406) };
      },
      'GET /api/v1/companies/b/overview': {
        status: 200,
        body: { company: { id: 'b', name: 'شرکت ب', nationalId: null, roleName: 'حسابدار' }, fiscalPeriods: [] },
      },
    });
    void release;

    render(<App navigate={vi.fn()} />);
    await screen.findByRole('heading', { name: 'فرانو - دوره مالی' });
    await userEvent.click(screen.getByRole('radio', { name: /سال مالی ۱۴۰۶/ }));
    const enter = screen.getByRole('button', { name: /ورود به محیط کاری/ });

    await userEvent.dblClick(enter);

    await screen.findByRole('heading', { name: 'داشبورد' });
    expect(posts).toBe(1);
  });

  it('shows a clear message and a refresh action when the server refuses a stale choice', async () => {
    const companyB = company('b', 'شرکت ب', [open1406]);
    const fetchMock = stubApi({
      ...antiforgeryRoute,
      'GET /api/v1/session': { status: 200, body: session },
      'GET /api/v1/context': { status: 200, body: contextBody({ companies: [companyA, companyB] }) },
      'POST /api/v1/context': { status: 404 },
    });

    render(<App navigate={vi.fn()} />);
    await screen.findByRole('heading', { name: 'فرانو - دوره مالی' });
    await userEvent.click(screen.getByRole('radio', { name: /سال مالی ۱۴۰۶/ }));
    await userEvent.click(screen.getByRole('button', { name: /ورود به محیط کاری/ }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('دسترسی شما به این شرکت یا دوره مالی تغییر کرده است');
    await waitFor(() => expect(alert).toHaveFocus());
    expect(screen.queryByRole('heading', { name: 'داشبورد' })).not.toBeInTheDocument();

    const isContextGet = (call: unknown[]) =>
      call[0] === '/api/v1/context' && ((call[1] as RequestInit | undefined)?.method ?? 'GET') === 'GET';
    const before = fetchMock.mock.calls.filter(isContextGet).length;
    await userEvent.click(screen.getByRole('button', { name: 'به‌روزرسانی فهرست' }));
    await waitFor(() => expect(fetchMock.mock.calls.filter(isContextGet).length).toBeGreaterThan(before));
  });

  it('shows a server error instead of an empty list, and can retry', async () => {
    const fetchMock = stubApi({
      'GET /api/v1/session': { status: 200, body: session },
      'GET /api/v1/context': { status: 500 },
    });

    render(<App navigate={vi.fn()} />);

    expect(await screen.findByRole('alert')).toHaveTextContent('خطای غیرمنتظره');
    expect(screen.queryByRole('heading', { name: 'شرکتی برای ورود در دسترس نیست' })).not.toBeInTheDocument();

    const callsBeforeRetry = fetchMock.mock.calls.length;
    await userEvent.click(screen.getByRole('button', { name: 'تلاش دوباره' }));
    expect(fetchMock.mock.calls.length).toBeGreaterThan(callsBeforeRetry);
  });

  it('sends an expired session to the existing sign-in page', async () => {
    stubApi({
      'GET /api/v1/session': { status: 401 },
      'GET /api/v1/context': { status: 401 },
    });
    const navigate = vi.fn();

    render(<App navigate={navigate} />);

    expect(await screen.findByText('در حال انتقال به صفحه ورود…')).toBeInTheDocument();
    expect(navigate).toHaveBeenCalledWith('/Account/Login?ReturnUrl=%2Fapp%2F');
  });

  it('sends the user to the sign-in page when the session ends while choosing', async () => {
    const companyB = company('b', 'شرکت ب', [open1406]);
    stubApi({
      ...antiforgeryRoute,
      'GET /api/v1/session': { status: 200, body: session },
      'GET /api/v1/context': { status: 200, body: contextBody({ companies: [companyA, companyB] }) },
      'POST /api/v1/context': { status: 401 },
    });
    const navigate = vi.fn();

    render(<App navigate={navigate} />);
    await screen.findByRole('heading', { name: 'فرانو - دوره مالی' });
    await userEvent.click(screen.getByRole('radio', { name: /سال مالی ۱۴۰۶/ }));
    await userEvent.click(screen.getByRole('button', { name: /ورود به محیط کاری/ }));

    await waitFor(() => expect(navigate).toHaveBeenCalledWith('/Account/Login?ReturnUrl=%2Fapp%2F'));
  });

  it('signs out from the working environment', async () => {
    stubApi({
      ...antiforgeryRoute,
      'GET /api/v1/session': { status: 200, body: session },
      'GET /api/v1/context': { status: 200, body: contextBody({ current: selected(companyA, open1405), companies: [companyA] }) },
      'GET /api/v1/companies/a/overview': { status: 200, body: overviewA },
      'POST /api/v1/session/logout': { status: 204 },
    });
    const navigate = vi.fn();

    render(<App navigate={navigate} />);
    await userEvent.click(await screen.findByRole('button', { name: 'خروج' }));

    await waitFor(() => expect(navigate).toHaveBeenCalledWith('/Account/Login'));
  });
});
