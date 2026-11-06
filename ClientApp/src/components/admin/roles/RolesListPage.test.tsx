import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { RouteView, RouterProvider, type Route } from '../../../lib/router';
import { json, stubApi, type ApiCall } from '../../../test/adminUsers';
import { companiesBody, roleItem, systemRole } from '../../../test/roles';
import { RolesListPage } from './RolesListPage';

const onUnauthenticated = vi.fn((): boolean => false);

const onForbidden = vi.fn();

function setup(search = '', canManage = true) {
  const routes: Route[] = [
    { path: '/admin/roles', element: () => <RolesListPage onUnauthenticated={onUnauthenticated} defaultCompanyId="a" can={(permission) => canManage && permission === 'admin.roles.create'} onForbidden={onForbidden} />, title: 'گروه‌ها' },
  ];
  window.history.pushState(null, '', `/app/admin/roles${search}`);
  return render(
    <RouterProvider routes={routes}>
      <RouteView />
    </RouterProvider>,
  );
}

const roleCalls = (calls: ApiCall[]) => calls.filter((call) => call.path.includes('/roles')).map((call) => call.path);

function api(rolesFor: (companyId: string) => Response = () => json([roleItem(), systemRole])) {
  return stubApi((call) => {
    if (call.path === '/api/v1/companies') return json(companiesBody);
    const match = /^\/api\/v1\/admin\/companies\/([^/]+)\/roles$/.exec(call.path);
    return match?.[1] ? rolesFor(match[1]) : new Response(null, { status: 404 });
  });
}

describe('RolesListPage', () => {
  beforeEach(() => {
    onUnauthenticated.mockReset();
    onUnauthenticated.mockReturnValue(false);
    onForbidden.mockReset();
  });

  it('lists the roles of the default company with badges, counts and a link per row', async () => {
    const { calls } = api();
    setup();
    expect(await screen.findByRole('heading', { level: 1, name: 'گروه‌های دسترسی' })).toBeInTheDocument();
    const link = await screen.findByRole('link', { name: 'کارشناس فروش' });
    expect(link).toHaveAttribute('href', '/app/admin/roles/role-1?company=a');
    const row = link.closest('tr') as HTMLElement;
    expect(within(row).getByText('SALES')).toHaveAttribute('dir', 'ltr');
    expect(within(row).getByText('سفارشی')).toBeInTheDocument();
    expect(within(row).getByText('فعال')).toBeInTheDocument();
    const systemRow = screen.getByRole('link', { name: 'مدیر شرکت' }).closest('tr') as HTMLElement;
    expect(within(systemRow).getByText('سیستمی')).toBeInTheDocument();
    expect(within(systemRow).getByText('۳')).toBeInTheDocument();
    expect(roleCalls(calls)).toEqual(['/api/v1/admin/companies/a/roles']);
  });

  it('shows the company selector with the user-companies gap note and switches company through the URL', async () => {
    const user = userEvent.setup();
    const { calls } = api((id) => json(id === 'b' ? [] : [roleItem()]));
    setup();
    await screen.findByRole('link', { name: 'کارشناس فروش' });
    const select = await screen.findByLabelText('شرکت');
    expect(screen.getByText('فهرست شرکت‌ها محدود به شرکت‌های عضو شماست.')).toBeInTheDocument();
    await waitFor(() => expect(select).toHaveValue('a'));
    await user.selectOptions(select, 'b');
    expect(window.location.search).toBe('?company=b');
    expect(await screen.findByText('گروه دسترسی‌ای ثبت نشده است')).toBeInTheDocument();
    expect(roleCalls(calls)).toEqual(['/api/v1/admin/companies/a/roles', '/api/v1/admin/companies/b/roles']);
  });

  it('uses the company from the URL even when it is not among the user companies', async () => {
    const { calls } = api();
    setup('?company=zzz');
    await screen.findByRole('link', { name: 'کارشناس فروش' });
    expect(roleCalls(calls)).toEqual(['/api/v1/admin/companies/zzz/roles']);
    expect(await screen.findByRole('option', { name: /خارج از فهرست شما/ })).toBeInTheDocument();
  });

  it('shows the create link only for managers, with the company in the URL', async () => {
    api();
    const view = setup('?company=b');
    expect(await screen.findByRole('link', { name: 'ایجاد گروه دسترسی' })).toHaveAttribute('href', '/app/admin/roles/new?company=b');
    view.unmount();
    api();
    setup('', false);
    await screen.findByRole('link', { name: 'کارشناس فروش' });
    expect(screen.queryByRole('link', { name: 'ایجاد گروه دسترسی' })).toBeNull();
  });

  it('shows the 403, 404 and error states', async () => {
    const user = userEvent.setup();
    api(() => new Response(null, { status: 403 }));
    const forbidden = setup();
    expect(await screen.findByRole('alert')).toHaveTextContent('شما مجوز مشاهدهٔ گروه‌های دسترسی را ندارید.');
    expect(screen.queryByRole('button', { name: 'تلاش دوباره' })).toBeNull();
    forbidden.unmount();

    api(() => new Response(null, { status: 404 }));
    const missing = setup();
    expect(await screen.findByText('شرکت پیدا نشد')).toBeInTheDocument();
    missing.unmount();

    let fail = true;
    api(() => (fail ? json({ error: 'x' }, 500) : json([roleItem()])));
    setup();
    await screen.findByText('خطای غیرمنتظره‌ای در سرور رخ داد. لطفاً دوباره تلاش کنید.');
    fail = false;
    await user.click(screen.getByRole('button', { name: 'تلاش دوباره' }));
    expect(await screen.findByRole('link', { name: 'کارشناس فروش' })).toBeInTheDocument();
  });

  it('keeps working when the company list cannot be loaded', async () => {
    stubApi((call) => (call.path === '/api/v1/companies' ? json({}, 500) : json([roleItem()])));
    setup();
    expect(await screen.findByRole('link', { name: 'کارشناس فروش' })).toBeInTheDocument();
    expect(await screen.findByText(/خطای غیرمنتظره/)).toBeInTheDocument();
  });
  it('asks the app to re-read the real permissions when the server answers 403', async () => {
    api(() => new Response(null, { status: 403 }));
    setup();
    await waitFor(() => expect(onForbidden).toHaveBeenCalledTimes(1));
  });

  it('offers the create button only with roles.create', async () => {
    api();
    const first = setup('', false);
    await screen.findByText('SALES');
    expect(screen.queryByText('ایجاد گروه دسترسی')).toBeNull();
    first.unmount();
    api();
    setup('', true);
    expect(await screen.findByText('ایجاد گروه دسترسی')).toBeInTheDocument();
  });
});
