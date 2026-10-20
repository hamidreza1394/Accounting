import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { App } from './App';

type Route = { status: number; body?: unknown };

function stubApi(routes: Record<string, Route>) {
  const fetchMock = vi.fn((input: string) => {
    const route = routes[input];
    if (!route) {
      return Promise.resolve(new Response(null, { status: 404 }));
    }
    return Promise.resolve(
      new Response(route.body === undefined ? null : JSON.stringify(route.body), {
        status: route.status,
        headers: { 'Content-Type': 'application/json' },
      }),
    );
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

const session = { username: 'alice', fullName: 'آلیس احمدی', accountKind: 'User', isAdministrator: false };
const companyA = { id: 'a', name: 'شرکت الف', nationalId: '10101010101', roleName: 'حسابدار' };
const companyB = { id: 'b', name: 'شرکت ب', nationalId: null, roleName: 'مدیر' };

describe('App', () => {
  beforeEach(() => {
    window.history.replaceState(null, '', '/app/');
  });

  it('shows the real fiscal periods of the company, with Persian dates', async () => {
    stubApi({
      '/api/v1/session': { status: 200, body: session },
      '/api/v1/companies': { status: 200, body: [companyA] },
      '/api/v1/companies/a/overview': {
        status: 200,
        body: {
          company: companyA,
          fiscalPeriods: [{ id: 'p1', title: 'سال مالی ۱۴۰۵', startDate: '2026-03-21', endDate: '2027-03-20', status: 'Open' }],
        },
      },
    });

    render(<App navigate={vi.fn()} />);

    const table = await screen.findByRole('table');
    expect(within(table).getByText('سال مالی ۱۴۰۵')).toBeInTheDocument();
    expect(within(table).getByText('۱۴۰۵/۰۱/۰۱')).toBeInTheDocument();
    expect(within(table).getByText('باز')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'شرکت الف' })).toBeInTheDocument();
    // No figures are invented: the page states that the accounting modules do not exist yet.
    expect(screen.getByText(/هیچ آمار مالی/)).toBeInTheDocument();
  });

  it('shows an empty state when the user has no company', async () => {
    stubApi({
      '/api/v1/session': { status: 200, body: session },
      '/api/v1/companies': { status: 200, body: [] },
    });

    render(<App navigate={vi.fn()} />);

    expect(await screen.findByText('در حال حاضر عضویت فعالی در شرکتی ندارید.')).toBeInTheDocument();
  });

  it('shows an empty state when the company has no fiscal period', async () => {
    stubApi({
      '/api/v1/session': { status: 200, body: session },
      '/api/v1/companies': { status: 200, body: [companyA] },
      '/api/v1/companies/a/overview': { status: 200, body: { company: companyA, fiscalPeriods: [] } },
    });

    render(<App navigate={vi.fn()} />);

    expect(await screen.findByText('هنوز دوره مالی برای این شرکت ثبت نشده است.')).toBeInTheDocument();
  });

  it('shows a server error instead of an empty list, and can retry', async () => {
    const fetchMock = stubApi({
      '/api/v1/session': { status: 200, body: session },
      '/api/v1/companies': { status: 500 },
    });

    render(<App navigate={vi.fn()} />);

    expect(await screen.findByRole('alert')).toHaveTextContent('خطای غیرمنتظره');
    expect(screen.queryByText('در حال حاضر عضویت فعالی در شرکتی ندارید.')).not.toBeInTheDocument();

    const callsBeforeRetry = fetchMock.mock.calls.length;
    await userEvent.click(screen.getByRole('button', { name: 'تلاش دوباره' }));
    expect(fetchMock.mock.calls.length).toBeGreaterThan(callsBeforeRetry);
  });

  it('sends an expired session to the existing sign-in page', async () => {
    stubApi({
      '/api/v1/session': { status: 401 },
      '/api/v1/companies': { status: 401 },
    });
    const navigate = vi.fn();

    render(<App navigate={navigate} />);

    expect(await screen.findByText('در حال انتقال به صفحه ورود…')).toBeInTheDocument();
    expect(navigate).toHaveBeenCalledWith('/Account/Login?ReturnUrl=%2Fapp%2F');
  });

  it('ignores a company in the address bar that the server did not return', async () => {
    window.history.replaceState(null, '', '/app/?company=someone-elses');
    const fetchMock = stubApi({
      '/api/v1/session': { status: 200, body: session },
      '/api/v1/companies': { status: 200, body: [companyA, companyB] },
      '/api/v1/companies/a/overview': { status: 200, body: { company: companyA, fiscalPeriods: [] } },
    });

    render(<App navigate={vi.fn()} />);

    await screen.findByRole('heading', { name: 'شرکت الف' });
    expect(fetchMock.mock.calls.map((call) => call[0])).not.toContain('/api/v1/companies/someone-elses/overview');
  });

  it('switches the company and remembers it in the address bar', async () => {
    stubApi({
      '/api/v1/session': { status: 200, body: session },
      '/api/v1/companies': { status: 200, body: [companyA, companyB] },
      '/api/v1/companies/a/overview': { status: 200, body: { company: companyA, fiscalPeriods: [] } },
      '/api/v1/companies/b/overview': { status: 200, body: { company: companyB, fiscalPeriods: [] } },
    });

    render(<App navigate={vi.fn()} />);

    await screen.findByRole('heading', { name: 'شرکت الف' });
    await userEvent.selectOptions(screen.getByLabelText('شرکت'), 'b');

    expect(await screen.findByRole('heading', { name: 'شرکت ب' })).toBeInTheDocument();
    expect(window.location.search).toBe('?company=b');
  });
});
