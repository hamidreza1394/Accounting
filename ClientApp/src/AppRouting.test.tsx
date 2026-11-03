import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { act } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { App } from './App';
import { json, listBody, userDetails, userItem } from './test/adminUsers';
import { company, contextBody, period, selected, session } from './test/api';
import { catalogItems, companiesBody, roleItem, systemRole, templates } from './test/roles';

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
      'GET /api/v1/admin/users/new': () => json({}, 404),
      'GET /api/v1/companies': () => json(companiesBody),
      'GET /api/v1/admin/companies/a/roles': () => json([roleItem(), systemRole]),
      'GET /api/v1/admin/companies/a/roles/new': () => json({}, 404),
      'GET /api/v1/admin/permission-catalog': () => json(catalogItems),
      'GET /api/v1/admin/permission-templates': () => json(templates),
      'GET /api/v1/admin/companies/a/membership-requests/pending': () =>
        json({ items: [{ id: 'req-1', userId: 'u1', requesterDisplayName: 'متقاضی نمونه', username: 'applicant', requestedAtUtc: '2026-11-01T08:00:00Z', isRequesterActive: true }], page: 1, pageSize: 20, totalCount: 1 }),
      'GET /api/v1/admin/security/audit-events': () =>
        json({ items: [{ id: 'e1', userId: 'u1', actorUsername: 'admin', actorDisplayName: 'مدیر نمونه', companyId: 'a', companyName: 'شرکت الف', eventType: 'CompanyRoleCreated', entityType: 'CompanyRole', entityId: 'r1', occurredAtUtc: '2026-11-03T10:00:00Z', correlationId: null }], totalCount: 1, page: 1, pageSize: 25 }),
      'GET /api/v1/me/membership-requests': () =>
        json({ items: [{ id: 'r1', destinationCompanyId: 'b', destinationCompanyName: 'شرکت مقصد نمونه', status: 'Pending', requestedAtUtc: '2026-11-01T08:00:00Z', decidedAtUtc: null, decisionReason: null, approvedRoleCode: null, approvedRoleName: null }], page: 1, pageSize: 20, totalCount: 1 }),
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
    await waitFor(() => expect(document.title).toBe('دسترسی ندارید | فرانو'));
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
    expect(screen.queryByRole('link', { name: /سیاست‌های امنیتی/ })).toBeNull();
    expect(screen.getByText('سیاست‌های امنیتی').closest('[aria-disabled="true"]')).toBeTruthy();
  });

  it('opens /admin/users/new as the create form, not as a user with id "new"', async () => {
    window.history.replaceState(null, '', '/app/admin/users/new');
    const fetchMock = stubAll(true);
    render(<App navigate={vi.fn()} />);
    expect(await screen.findByRole('heading', { level: 1, name: 'ایجاد کاربر' })).toBeInTheDocument();
    expect(screen.getByRole('form', { name: 'فرم ایجاد کاربر' })).toBeInTheDocument();
    expect(fetchMock.mock.calls.some((call) => String(call[0]).startsWith('/api/v1/admin/users/new'))).toBe(false);
    await waitFor(() => expect(document.title).toBe('ایجاد کاربر | فرانو'));
  });

  it('opens the edit form for a deep link and loads that user', async () => {
    window.history.replaceState(null, '', '/app/admin/users/user-2/edit');
    stubAll(true);
    render(<App navigate={vi.fn()} />);
    expect(await screen.findByRole('heading', { level: 1, name: 'ویرایش کاربر' })).toBeInTheDocument();
    expect(await screen.findByLabelText(/^نام کاربری/)).toHaveValue('user2');
  });

  it('reaches create and edit through the list and detail buttons for an administrator', async () => {
    const user = userEvent.setup();
    window.history.replaceState(null, '', '/app/admin/users');
    stubAll(true);
    render(<App navigate={vi.fn()} />);
    await user.click(await screen.findByRole('link', { name: 'ایجاد کاربر' }));
    expect(await screen.findByRole('form', { name: 'فرم ایجاد کاربر' })).toBeInTheDocument();
    act(() => {
      window.history.back();
    });
    await user.click(await screen.findByRole('link', { name: 'نام2 خانوادگی2' }));
    await user.click(await screen.findByRole('link', { name: 'ویرایش' }));
    expect(await screen.findByRole('form', { name: 'فرم ویرایش کاربر' })).toBeInTheDocument();
    expect(window.location.pathname).toBe('/app/admin/users/user-2/edit');
  });

  it('shows a forbidden page and sends no request for create and edit deep links of a non-administrator', async () => {
    for (const path of ['/app/admin/users/new', '/app/admin/users/user-2/edit']) {
      window.history.replaceState(null, '', path);
      const fetchMock = stubAll(false);
      const view = render(<App navigate={vi.fn()} />);
      expect(await screen.findByRole('alert')).toHaveTextContent('دسترسی ندارید');
      expect(screen.queryByRole('form')).toBeNull();
      expect(fetchMock.mock.calls.some((call) => String(call[0]).startsWith('/api/v1/admin/users'))).toBe(false);
      view.unmount();
    }
  });

  it('reaches the roles list and the permission catalog from the menu, with the active company by default', async () => {
    const user = userEvent.setup();
    const fetchMock = stubAll(true);
    render(<App navigate={vi.fn()} />);
    await screen.findByRole('heading', { name: dashboardHeading });
    const nav = screen.getByRole('navigation', { name: 'منوی اصلی' });
    await user.click(within(nav).getByRole('link', { name: 'گروه‌های دسترسی' }));
    expect(await screen.findByRole('heading', { level: 1, name: 'گروه‌های دسترسی' })).toBeInTheDocument();
    expect(window.location.pathname).toBe('/app/admin/roles');
    expect(await screen.findByRole('link', { name: 'کارشناس فروش' })).toHaveAttribute('href', '/app/admin/roles/role-1?company=a');
    expect(within(nav).getByRole('link', { name: 'گروه‌های دسترسی' })).toHaveAttribute('aria-current', 'page');
    expect(fetchMock.mock.calls.some((call) => String(call[0]) === '/api/v1/admin/companies/a/roles')).toBe(true);

    await user.click(within(nav).getByRole('link', { name: 'کاتالوگ و جزئیات مجوزها' }));
    expect(await screen.findByRole('heading', { level: 1, name: 'کاتالوگ مجوزها' })).toBeInTheDocument();
    expect(window.location.pathname).toBe('/app/admin/permissions');
    expect(within(nav).getByRole('link', { name: 'کاتالوگ و جزئیات مجوزها' })).toHaveAttribute('aria-current', 'page');
  });

  it('opens /admin/roles/new as the create form, not as a role with id "new"', async () => {
    window.history.replaceState(null, '', '/app/admin/roles/new');
    const fetchMock = stubAll(true);
    render(<App navigate={vi.fn()} />);
    expect(await screen.findByRole('heading', { level: 1, name: 'ایجاد گروه دسترسی' })).toBeInTheDocument();
    expect(await screen.findByRole('form', { name: 'فرم ایجاد گروه دسترسی' })).toBeInTheDocument();
    expect(fetchMock.mock.calls.some((call) => String(call[0]).endsWith('/roles/new'))).toBe(false);
  });

  it('opens a role deep link and a system role read-only', async () => {
    window.history.replaceState(null, '', '/app/admin/roles/role-sys?company=a');
    stubAll(true);
    render(<App navigate={vi.fn()} />);
    expect(await screen.findByRole('heading', { level: 1, name: 'مدیر شرکت' })).toBeInTheDocument();
    expect(await screen.findByText(/این گروه سیستمی است/)).toBeInTheDocument();
  });

  it('shows a forbidden page, no menu entry and no request for any role or catalog route of a non-administrator', async () => {
    for (const path of ['/app/admin/roles', '/app/admin/roles/new', '/app/admin/roles/role-1?company=a', '/app/admin/permissions']) {
      window.history.replaceState(null, '', path);
      const fetchMock = stubAll(false);
      const view = render(<App navigate={vi.fn()} />);
      expect(await screen.findByRole('alert')).toHaveTextContent('دسترسی ندارید');
      expect(screen.queryByRole('link', { name: 'گروه‌های دسترسی' })).toBeNull();
      expect(screen.queryByRole('link', { name: 'کاتالوگ و جزئیات مجوزها' })).toBeNull();
      expect(screen.queryByRole('form')).toBeNull();
      expect(fetchMock.mock.calls.some((call) => String(call[0]).startsWith('/api/v1/admin/'))).toBe(false);
      expect(fetchMock.mock.calls.some((call) => String(call[0]) === '/api/v1/companies')).toBe(false);
      view.unmount();
    }
  });

  it('reaches the membership request pages from the menu for an administrator', async () => {
    const user = userEvent.setup();
    stubAll(true);
    render(<App navigate={vi.fn()} />);
    await screen.findByRole('heading', { name: dashboardHeading });
    const nav = screen.getByRole('navigation', { name: 'منوی اصلی' });

    await user.click(within(nav).getByRole('link', { name: 'درخواست‌های عضویت' }));
    expect(await screen.findByRole('heading', { level: 1, name: 'درخواست‌های عضویت در انتظار تأیید' })).toBeInTheDocument();
    expect(window.location.pathname).toBe('/app/admin/membership-requests');
    expect(await screen.findByText('متقاضی نمونه')).toBeInTheDocument();
    expect(within(nav).getByRole('link', { name: 'درخواست‌های عضویت' })).toHaveAttribute('aria-current', 'page');

    await user.click(within(nav).getByRole('link', { name: 'درخواست‌های عضویت من' }));
    expect(await screen.findByRole('heading', { level: 1, name: 'درخواست‌های عضویت من' })).toBeInTheDocument();
    expect(await screen.findByRole('cell', { name: 'شرکت مقصد نمونه' })).toBeInTheDocument();
  });

  it('lets a non-administrator open their own membership requests by deep link, with no admin menu', async () => {
    window.history.replaceState(null, '', '/app/account/membership-requests');
    stubAll(false);
    render(<App navigate={vi.fn()} />);
    expect(await screen.findByRole('heading', { level: 1, name: 'درخواست‌های عضویت من' })).toBeInTheDocument();
    const nav = screen.getByRole('navigation', { name: 'منوی اصلی' });
    expect(within(nav).queryByRole('link', { name: 'درخواست‌های عضویت' })).toBeNull();
  });

  it('reaches the security report from the menu for an administrator, and not for others', async () => {
    const user = userEvent.setup();
    stubAll(true);
    const first = render(<App navigate={vi.fn()} />);
    await screen.findByRole('heading', { name: dashboardHeading });
    await user.click(within(screen.getByRole('navigation', { name: 'منوی اصلی' })).getByRole('link', { name: 'گزارش امنیت و سوابق' }));
    // The first load of this lazy page also transforms the calendar library, which is slow under jsdom.
    expect(await screen.findByRole('heading', { level: 1, name: 'گزارش امنیت و سوابق' }, { timeout: 15000 })).toBeInTheDocument();
    expect(window.location.pathname).toBe('/app/admin/security/audit');
    expect(await screen.findByText('ایجاد گروه دسترسی')).toBeInTheDocument();
    first.unmount();

    window.history.replaceState(null, '', '/app/admin/security/audit');
    const fetchMock = stubAll(false);
    render(<App navigate={vi.fn()} />);
    expect(await screen.findByRole('alert')).toBeInTheDocument();
    expect(fetchMock.mock.calls.some((call) => String(call[0]).includes('/admin/security/audit-events'))).toBe(false);
  }, 30000);
});
