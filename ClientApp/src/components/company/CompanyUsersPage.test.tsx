import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { RouteView, RouterProvider, type Route } from '../../lib/router';
import { json, stubApi, type ApiCall } from '../../test/adminUsers';
import { directoryPage, directoryUser } from '../../test/companyDirectory';
import { CompanyUsersPage } from './CompanyUsersPage';

const onUnauthenticated = vi.fn((): boolean => false);
const usersPath = '/api/v1/companies/a/users';

function api(handler: (url: URL) => Response = () => json(directoryPage([directoryUser(1), directoryUser(2, { membershipIsActive: false, roleName: 'مشاهده‌گر', roleCode: 'VIEWER', roleId: 'role-2' })]))) {
  const urls: URL[] = [];
  const calls: ApiCall[] = [];
  const stub = stubApi((call) => {
    calls.push(call);
    if (call.path !== usersPath) return new Response(null, { status: 404 });
    const url = new URL(String(stub.mock.mock.calls.at(-1)?.[0]), 'http://localhost');
    urls.push(url);
    return handler(url);
  });
  return { ...stub, urls, calls };
}

function setup(isAdministrator = false) {
  const routes: Route[] = [
    { path: '/company/users', element: () => <CompanyUsersPage onUnauthenticated={onUnauthenticated} companyId="a" companyName="شرکت الف" isAdministrator={isAdministrator} />, title: 'کاربران' },
  ];
  window.history.pushState(null, '', '/app/company/users');
  return render(
    <RouterProvider routes={routes}>
      <RouteView />
    </RouterProvider>,
  );
}

describe('CompanyUsersPage', () => {
  beforeEach(() => {
    onUnauthenticated.mockReset();
    onUnauthenticated.mockReturnValue(false);
  });

  it('lists the members of the active company only, read-only, with role and status', async () => {
    const { urls, calls } = api();
    setup();
    expect(await screen.findByRole('heading', { level: 1, name: 'کاربران شرکت' })).toBeInTheDocument();
    expect(screen.getByText(/شرکت «شرکت الف»/)).toBeInTheDocument();
    const row = (await screen.findByRole('button', { name: /نام1 خانوادگی1/ })).closest('tr') as HTMLElement;
    expect(within(row).getByText('member1')).toHaveAttribute('dir', 'ltr');
    expect(within(row).getByText('حسابدار')).toBeInTheDocument();
    const inactive = screen.getByRole('button', { name: /نام2 خانوادگی2/ }).closest('tr') as HTMLElement;
    expect(within(inactive).getByText('مشاهده‌گر')).toBeInTheDocument();
    expect(urls[0]?.search).toBe('?page=1&pageSize=25');
    // Nothing else is requested: only this company's directory, only GET.
    expect(calls.every((call) => call.method === 'GET' && call.path === usersPath)).toBe(true);
    // The page offers no way to change anything.
    expect(screen.queryByRole('button', { name: /ویرایش|حذف|ایجاد|تغییر|غیرفعال‌سازی/ })).toBeNull();
  });

  it('searches with one request per pause in typing, from page 1', async () => {
    const user = userEvent.setup();
    const { urls } = api();
    setup();
    await screen.findByText('نام1 خانوادگی1', { exact: false });
    await user.type(screen.getByLabelText('جستجو'), 'علی');
    await waitFor(() => expect(urls.at(-1)?.searchParams.get('search')).toBe('علی'));
    expect(urls.filter((url) => url.searchParams.has('search'))).toHaveLength(1);
    expect(urls.at(-1)?.searchParams.get('page')).toBe('1');
  });

  it('filters by membership status', async () => {
    const user = userEvent.setup();
    const { urls } = api();
    setup();
    await screen.findByText('نام1 خانوادگی1', { exact: false });
    await user.selectOptions(screen.getByLabelText('وضعیت عضویت'), 'inactive');
    await waitFor(() => expect(urls.at(-1)?.searchParams.get('membershipActive')).toBe('false'));
    await user.selectOptions(screen.getByLabelText('وضعیت عضویت'), 'active');
    await waitFor(() => expect(urls.at(-1)?.searchParams.get('membershipActive')).toBe('true'));
    await user.selectOptions(screen.getByLabelText('وضعیت عضویت'), 'all');
    await waitFor(() => expect(urls.at(-1)?.searchParams.has('membershipActive')).toBe(false));
  });

  it('shows the details of a member, warns when access is closed, and moves focus to them', async () => {
    const user = userEvent.setup();
    api();
    setup();
    await user.click(await screen.findByRole('button', { name: /نام2 خانوادگی2/ }));
    const detail = await screen.findByRole('region', { name: 'جزئیات نام2 خانوادگی2' });
    await waitFor(() => expect(detail).toHaveFocus());
    expect(detail).toHaveTextContent('VIEWER');
    expect(detail).toHaveTextContent('با حساب، عضویت یا گروه غیرفعال، کاربر به داده‌های این شرکت دسترسی ندارد.');
    await user.click(within(detail).getByRole('button', { name: 'بستن جزئیات' }));
    expect(screen.queryByRole('region', { name: /جزئیات/ })).toBeNull();
  });

  it('links to the group page only for an administrator', async () => {
    const user = userEvent.setup();
    api();
    const first = setup(true);
    await user.click(await screen.findByRole('button', { name: /نام1 خانوادگی1/ }));
    const link = await screen.findByRole('link', { name: 'مشاهدهٔ مجوزهای این گروه' });
    expect(link).toHaveAttribute('href', '/app/admin/roles/role-1?company=a');
    first.unmount();

    api();
    setup(false);
    await user.click(await screen.findByRole('button', { name: /نام1 خانوادگی1/ }));
    await screen.findByRole('region', { name: /جزئیات/ });
    expect(screen.queryByRole('link', { name: 'مشاهدهٔ مجوزهای این گروه' })).toBeNull();
  });

  it('shows a no-access panel without a table for 403 (no users.list) and for 404 (other company)', async () => {
    api(() => new Response(null, { status: 403 }));
    const first = setup();
    expect(await screen.findByText(/مجوز مشاهدهٔ فهرست کاربران این شرکت \(users\.list\) را ندارید/)).toBeInTheDocument();
    expect(screen.queryByRole('table')).toBeNull();
    expect(screen.queryByRole('search')).toBeNull();
    first.unmount();

    api(() => new Response(null, { status: 404 }));
    setup();
    expect(await screen.findByText('شرکت پیدا نشد یا به آن دسترسی ندارید.')).toBeInTheDocument();
    expect(screen.queryByRole('table')).toBeNull();
  });

  it('shows empty states with and without a filter, an error with a retry, and hands a 401 over', async () => {
    const user = userEvent.setup();
    api(() => json(directoryPage([])));
    const first = setup();
    expect(await screen.findByText('برای این شرکت هنوز کاربری ثبت نشده است.')).toBeInTheDocument();
    await user.selectOptions(screen.getByLabelText('وضعیت عضویت'), 'inactive');
    expect(await screen.findByText('با جستجو یا فیلتر فعلی کاربری پیدا نشد.')).toBeInTheDocument();
    first.unmount();

    let fail = true;
    api(() => (fail ? new Response(null, { status: 500 }) : json(directoryPage([directoryUser(1)]))));
    const second = setup();
    const retry = await screen.findByRole('button', { name: /تلاش/ });
    fail = false;
    await user.click(retry);
    expect(await screen.findByRole('button', { name: /نام1 خانوادگی1/ })).toBeInTheDocument();
    second.unmount();

    onUnauthenticated.mockReturnValue(true);
    api(() => new Response(null, { status: 401 }));
    setup();
    await waitFor(() => expect(onUnauthenticated).toHaveBeenCalled());
  });

  it('moves to the last existing page when the current one became empty', async () => {
    const user = userEvent.setup();
    let call = 0;
    const bodies = [directoryPage([directoryUser(1)], { totalCount: 60 }), directoryPage([], { page: 2, totalCount: 1 }), directoryPage([directoryUser(1)], { totalCount: 1 })];
    const { urls } = api(() => json(bodies[Math.min(call++, bodies.length - 1)]));
    setup();
    await screen.findByRole('button', { name: /نام1 خانوادگی1/ });
    await user.click(screen.getByRole('button', { name: 'بعدی' }));
    await waitFor(() => expect(urls).toHaveLength(3));
    expect(urls.map((url) => url.searchParams.get('page'))).toEqual(['1', '2', '1']);
  });
});
