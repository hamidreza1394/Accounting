import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { act } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { App } from './App';
import { json, listBody, userDetails, userItem } from './test/adminUsers';
import { company, contextBody, period, selected, session } from './test/api';

const p1 = period('p1', 'سال مالی ۱۴۰۵');
const companyA = company('a', 'شرکت الف', [p1], '10101010101');
const overview = {
  company: { id: 'a', name: 'شرکت الف', nationalId: '10101010101', roleName: 'حسابدار' },
  fiscalPeriods: [{ id: 'p1', title: 'سال مالی ۱۴۰۵', startDate: '2026-03-21', endDate: '2027-03-20', status: 'Open' }],
};

function stubAll(isAdministrator: boolean) {
  const fetchMock = vi.fn((input: string, init?: RequestInit) => {
    const method = init?.method ?? 'GET';
    const url = new URL(input, 'http://localhost');
    const key = `${method} ${url.pathname}`;
    const routes: Record<string, () => Response> = {
      'GET /api/v1/antiforgery/token': () => json({ headerName: 'X-XSRF-TOKEN', requestToken: 't' }),
      'GET /api/v1/session': () => json({ ...session, isAdministrator }),
      'GET /api/v1/context': () => json(contextBody({ entry: 'directEntry', directSelection: { companyId: 'a', fiscalPeriodId: 'p1' }, companies: [companyA] })),
      'POST /api/v1/context': () => json(selected(companyA, p1)),
      'GET /api/v1/companies/a/overview': () => json(overview),
      'GET /api/v1/admin/users': () => json(listBody([userItem(1), userItem(2)])),
      'GET /api/v1/admin/users/user-2': () => json(userDetails(2)),
    };
    return Promise.resolve(routes[key]?.() ?? new Response(null, { status: 404 }));
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

const dashboardHeading = /به سامانه یکپارچه فرانو خوش آمدید/;

describe('App routing', () => {
  beforeEach(() => {
    window.history.replaceState(null, '', '/app/');
  });

  it('navigates from the sidebar to the user list and to a detail page, then back with the history', async () => {
    const user = userEvent.setup();
    stubAll(true);
    render(<App navigate={vi.fn()} />);
    expect(await screen.findByRole('heading', { name: dashboardHeading })).toBeInTheDocument();
    const nav = screen.getByRole('navigation', { name: 'منوی اصلی' });
    expect(within(nav).getByRole('link', { name: /داشبورد/ })).toHaveAttribute('aria-current', 'page');

    await user.click(within(nav).getByRole('link', { name: 'مدیریت کاربران' }));
    expect(await screen.findByRole('heading', { level: 1, name: 'مدیریت کاربران' })).toBeInTheDocument();
    expect(window.location.pathname).toBe('/app/admin/users');
    expect(within(nav).getByRole('link', { name: 'مدیریت کاربران' })).toHaveAttribute('aria-current', 'page');
    expect(within(nav).getByRole('link', { name: /داشبورد/ })).not.toHaveAttribute('aria-current');
    await waitFor(() => expect(document.title).toBe('مدیریت کاربران | فرانو'));

    await user.click(await screen.findByRole('link', { name: 'نام2 خانوادگی2' }));
    expect(await screen.findByRole('heading', { level: 1, name: 'نام2 خانوادگی2' })).toBeInTheDocument();
    expect(window.location.pathname).toBe('/app/admin/users/user-2');
    // The detail page keeps the users menu entry active.
    expect(within(nav).getByRole('link', { name: 'مدیریت کاربران' })).toHaveAttribute('aria-current', 'page');

    act(() => {
      window.history.back();
    });
    expect(await screen.findByRole('heading', { level: 1, name: 'مدیریت کاربران' })).toBeInTheDocument();
    act(() => {
      window.history.back();
    });
    expect(await screen.findByRole('heading', { name: dashboardHeading })).toBeInTheDocument();
    expect(within(nav).getByRole('link', { name: /داشبورد/ })).toHaveAttribute('aria-current', 'page');
  });

  it('opens a deep link to a user detail page directly', async () => {
    window.history.replaceState(null, '', '/app/admin/users/user-2');
    stubAll(true);
    render(<App navigate={vi.fn()} />);
    expect(await screen.findByRole('heading', { level: 1, name: 'نام2 خانوادگی2' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: dashboardHeading })).toBeNull();
  });

  it('shows a forbidden page, no menu entry and no user request to a non-administrator on a deep link', async () => {
    window.history.replaceState(null, '', '/app/admin/users');
    const fetchMock = stubAll(false);
    render(<App navigate={vi.fn()} />);
    expect(await screen.findByRole('alert')).toHaveTextContent('دسترسی ندارید');
    expect(screen.queryByRole('link', { name: 'مدیریت کاربران' })).toBeNull();
    expect(fetchMock.mock.calls.some((call) => String(call[0]).startsWith('/api/v1/admin/users'))).toBe(false);
    expect(document.title).toBe('دسترسی ندارید | فرانو');
  });

  it('shows the not-found page for an unknown route inside the shell', async () => {
    window.history.replaceState(null, '', '/app/nothing-here');
    stubAll(true);
    render(<App navigate={vi.fn()} />);
    expect(await screen.findByRole('alert')).toHaveTextContent('صفحه پیدا نشد');
    expect(screen.getByRole('navigation', { name: 'منوی اصلی' })).toBeInTheDocument();
  });

  it('keeps the disabled placeholder items non-navigable', async () => {
    stubAll(true);
    render(<App navigate={vi.fn()} />);
    await screen.findByRole('heading', { name: dashboardHeading });
    expect(screen.queryByRole('link', { name: /گروه‌های دسترسی/ })).toBeNull();
    expect(screen.getByText('گروه‌های دسترسی').closest('[aria-disabled="true"]')).toBeTruthy();
  });
});
