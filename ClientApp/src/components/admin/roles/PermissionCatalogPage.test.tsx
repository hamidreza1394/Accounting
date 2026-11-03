import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { RouteView, RouterProvider, type Route } from '../../../lib/router';
import { json, stubApi } from '../../../test/adminUsers';
import { catalogItems } from '../../../test/roles';
import { PermissionCatalogPage } from './PermissionCatalogPage';

const onUnauthenticated = vi.fn((): boolean => false);
const routes: Route[] = [{ path: '/admin/permissions', element: () => <PermissionCatalogPage onUnauthenticated={onUnauthenticated} />, title: 'کاتالوگ' }];

function setup(search = '') {
  window.history.pushState(null, '', `/app/admin/permissions${search}`);
  return render(
    <RouterProvider routes={routes}>
      <RouteView />
    </RouterProvider>,
  );
}

const rowCount = () => within(screen.getByRole('table')).getAllByRole('row').length - 1;

describe('PermissionCatalogPage', () => {
  beforeEach(() => {
    onUnauthenticated.mockReset();
    onUnauthenticated.mockReturnValue(false);
  });

  it('lists the catalog with code, subsystem and the UI-derived operation, and requests it once', async () => {
    const { calls } = stubApi(() => json(catalogItems));
    setup();
    expect(await screen.findByRole('heading', { level: 1, name: 'کاتالوگ مجوزها' })).toBeInTheDocument();
    await waitFor(() => expect(rowCount()).toBe(10));
    expect(screen.getByText('trade.orders.delete')).toHaveAttribute('dir', 'ltr');
    expect(screen.getByRole('columnheader', { name: 'نوع عملیات (استنباط‌شده)' })).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('۱۰ مجوز از ۱۰');
    expect(calls.map((call) => `${call.method} ${call.path}`)).toEqual(['GET /api/v1/admin/permission-catalog']);
  });

  it('filters by search and subsystem in the browser and keeps both in the URL', async () => {
    const user = userEvent.setup();
    const { calls } = stubApi(() => json(catalogItems));
    setup();
    await waitFor(() => expect(rowCount()).toBe(10));
    await user.selectOptions(screen.getByLabelText('زیرسیستم'), 'trade');
    expect(window.location.search).toBe('?subsystem=trade');
    expect(rowCount()).toBe(5);
    await user.type(screen.getByRole('searchbox', { name: 'جستجوی مجوزها' }), 'delete');
    await waitFor(() => expect(window.location.search).toBe('?subsystem=trade&q=delete'));
    expect(rowCount()).toBe(1);
    expect(screen.getByRole('status')).toHaveTextContent('۱ مجوز از ۱۰');
    expect(calls).toHaveLength(1);
  });

  it('restores filters from the URL and offers to clear them when nothing matches', async () => {
    const user = userEvent.setup();
    stubApi(() => json(catalogItems));
    setup('?q=zzz&subsystem=admin');
    expect(await screen.findByText('مجوزی پیدا نشد')).toBeInTheDocument();
    expect(screen.getByRole('searchbox', { name: 'جستجوی مجوزها' })).toHaveValue('zzz');
    expect(screen.getByLabelText('زیرسیستم')).toHaveValue('admin');
    await user.click(screen.getByRole('button', { name: 'پاک‌کردن فیلترها' }));
    await waitFor(() => expect(rowCount()).toBe(10));
    expect(window.location.search).toBe('');
  });

  it('shows the details of a permission in an expandable row', async () => {
    const user = userEvent.setup();
    stubApi(() => json(catalogItems));
    setup();
    await waitFor(() => expect(rowCount()).toBe(10));
    const summary = screen.getByText('مشاهدهٔ کاربران');
    const details = summary.closest('details') as HTMLDetailsElement;
    expect(details.open).toBe(false);
    await user.click(summary);
    expect(details.open).toBe(true);
    expect(within(details).getByText('id-admin.users.view')).toBeInTheDocument();
  });

  it('shows a forbidden message without retry for 403', async () => {
    stubApi(() => new Response(null, { status: 403 }));
    setup();
    expect(await screen.findByRole('alert')).toHaveTextContent('شما مجوز مشاهدهٔ کاتالوگ مجوزها را ندارید.');
    expect(screen.queryByRole('button', { name: 'تلاش دوباره' })).toBeNull();
  });

  it('shows an error with retry and recovers', async () => {
    const user = userEvent.setup();
    let fail = true;
    stubApi(() => (fail ? json({ error: 'x' }, 500) : json(catalogItems)));
    setup();
    expect(await screen.findByRole('alert')).toHaveTextContent('خطای غیرمنتظره');
    fail = false;
    await user.click(screen.getByRole('button', { name: 'تلاش دوباره' }));
    await waitFor(() => expect(rowCount()).toBe(10));
  });

  it('shows the empty state for an empty catalog and reports a broken contract as an error', async () => {
    stubApi(() => json([]));
    const view = setup();
    expect(await screen.findByText('هنوز مجوزی در کاتالوگ ثبت نشده است.')).toBeInTheDocument();
    view.unmount();
    stubApi(() => json([{ id: 1 }]));
    setup();
    expect(await screen.findByRole('alert')).toHaveTextContent('دریافت کاتالوگ مجوزها انجام نشد');
  });

  it('hands a 401 to the shell instead of showing it', async () => {
    onUnauthenticated.mockReturnValue(true);
    stubApi(() => new Response(null, { status: 401 }));
    setup();
    await waitFor(() => expect(onUnauthenticated).toHaveBeenCalled());
    expect(screen.queryByRole('alert')).toBeNull();
  });
});
