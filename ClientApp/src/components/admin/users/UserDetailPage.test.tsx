import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { RouteView, RouterProvider, type Route } from '../../../lib/router';
import { json, stubFetch, userDetails } from '../../../test/adminUsers';
import { UserDetailPage } from './UserDetailPage';

const onUnauthenticated = vi.fn((): boolean => false);
function DetailRoute() {
  return <UserDetailPage onUnauthenticated={onUnauthenticated} />;
}
const routes: Route[] = [
  { path: '/admin/users', element: () => <h1>فهرست</h1>, title: 'فهرست' },
  { path: '/admin/users/:id', element: DetailRoute, title: 'جزئیات' },
];

function setup(id = 'user-2') {
  window.history.pushState(null, '', `/app/admin/users/${id}`);
  return render(
    <RouterProvider routes={routes}>
      <RouteView />
    </RouterProvider>,
  );
}

describe('UserDetailPage', () => {
  beforeEach(() => {
    onUnauthenticated.mockReset();
    onUnauthenticated.mockReturnValue(false);
  });

  it('shows breadcrumb, header and the general tab first', async () => {
    const fetchMock = stubFetch(() => json(userDetails(2)));
    setup();
    expect(await screen.findByRole('heading', { level: 1, name: 'نام2 خانوادگی2' })).toBeInTheDocument();
    expect(String(fetchMock.mock.calls[0]?.[0])).toBe('/api/v1/admin/users/user-2');
    const crumbs = within(screen.getByRole('navigation', { name: 'مسیر صفحه' }));
    expect(crumbs.getByText('مدیریت سیستم')).toBeInTheDocument();
    expect(crumbs.getByRole('link', { name: 'کاربران' })).toHaveAttribute('href', '/app/admin/users');
    expect(crumbs.getByText('نام2 خانوادگی2')).toHaveAttribute('aria-current', 'page');
    expect(screen.getByText('@user2')).toHaveAttribute('dir', 'ltr');
    const panel = screen.getByRole('tabpanel');
    expect(within(panel).getByText('نقش قدیمی حساب')).toBeInTheDocument();
    expect(within(panel).getByText('Accountant')).toBeInTheDocument();
    expect(within(panel).getByText('user2')).toHaveAttribute('dir', 'ltr');
  });

  it('has exactly the three tabs backed by existing API fields', async () => {
    stubFetch(() => json(userDetails(2)));
    setup();
    await screen.findByRole('tablist');
    expect(screen.getAllByRole('tab').map((tab) => tab.textContent)).toEqual(['اطلاعات عمومی', 'اطلاعات تماس', 'شرکت‌ها و عضویت‌ها']);
  });

  it('shows contact data with verification badges (icon and text)', async () => {
    const user = userEvent.setup();
    stubFetch(() => json(userDetails(2)));
    setup();
    await user.click(await screen.findByRole('tab', { name: 'اطلاعات تماس' }));
    const panel = within(screen.getByRole('tabpanel'));
    expect(panel.getByText('user2@example.test')).toBeInTheDocument();
    expect(panel.getByText('09120000002')).toBeInTheDocument();
    expect(panel.getByText('تأیید شده')).toBeInTheDocument();
    expect(panel.getByText('تأیید نشده')).toBeInTheDocument();
  });

  it('shows memberships in a table and supports keyboard tab navigation', async () => {
    const user = userEvent.setup();
    stubFetch(() => json(userDetails(2)));
    setup();
    const first = await screen.findByRole('tab', { name: 'اطلاعات عمومی' });
    first.focus();
    await user.keyboard('{End}');
    expect(screen.getByRole('tab', { name: 'شرکت‌ها و عضویت‌ها' })).toHaveAttribute('aria-selected', 'true');
    const table = within(screen.getByRole('table', { name: 'عضویت‌های شرکتی کاربر' }));
    expect(table.getByText('شرکت نمونه')).toBeInTheDocument();
    expect(table.getByText(/حسابدار/)).toBeInTheDocument();
    expect(table.getByText('(ACCOUNTANT)')).toBeInTheDocument();
    expect(table.getByText('غیرفعال')).toBeInTheDocument();
    expect(table.getByText('۱۴۰۵/۰۷/۰۹')).toBeInTheDocument();
    await user.keyboard('{ArrowRight}');
    expect(screen.getByRole('tab', { name: 'اطلاعات تماس' })).toHaveAttribute('aria-selected', 'true');
  });

  it('shows an empty message for a user without memberships', async () => {
    const user = userEvent.setup();
    stubFetch(() => json(userDetails(2, { memberships: [] })));
    setup();
    await user.click(await screen.findByRole('tab', { name: 'شرکت‌ها و عضویت‌ها' }));
    expect(screen.getByText('این حساب هنوز عضویت شرکتی ندارد.')).toBeInTheDocument();
  });

  it('never renders a password or hash field', async () => {
    stubFetch(() => json(userDetails(2, { passwordHash: 'secret-hash', password: 'secret' })));
    setup();
    await screen.findByRole('tablist');
    expect(document.body.textContent).not.toMatch(/secret/);
  });

  it('shows a not-found state for 404 and an invalid id (400), with a link back', async () => {
    stubFetch(() => json({}, 404));
    setup('missing');
    expect(await screen.findByRole('alert')).toHaveTextContent('کاربر پیدا نشد');
    expect(screen.getByRole('link', { name: 'بازگشت به فهرست کاربران' })).toHaveAttribute('href', '/app/admin/users');
  });

  it('treats a 400 for a malformed id as not found', async () => {
    stubFetch(() => json({}, 400));
    setup('not-a-guid');
    expect(await screen.findByRole('alert')).toHaveTextContent('کاربر پیدا نشد');
  });

  it('shows a forbidden message for 403 without retry', async () => {
    stubFetch(() => json({}, 403));
    setup();
    expect(await screen.findByRole('alert')).toHaveTextContent('مجوز مشاهدهٔ اطلاعات این کاربر را ندارید');
    expect(screen.queryByRole('button', { name: 'تلاش دوباره' })).toBeNull();
  });

  it('shows a generic error with retry, then recovers', async () => {
    const user = userEvent.setup();
    let fail = true;
    stubFetch(() => (fail ? json({}, 500) : json(userDetails(2))));
    setup();
    expect(await screen.findByRole('alert')).toHaveTextContent('خطای غیرمنتظره');
    fail = false;
    await user.click(screen.getByRole('button', { name: 'تلاش دوباره' }));
    expect(await screen.findByRole('heading', { level: 1, name: 'نام2 خانوادگی2' })).toBeInTheDocument();
  });

  it('shows a loading state, then hands a 401 to the unauthenticated flow', async () => {
    onUnauthenticated.mockReturnValue(true);
    stubFetch(() => new Response(null, { status: 401 }));
    setup();
    expect(screen.getByRole('status')).toHaveTextContent('در حال دریافت اطلاعات کاربر…');
    await waitFor(() => expect(onUnauthenticated).toHaveBeenCalled());
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('renders an h1 in every state and names the tab after the user once loaded', async () => {
    stubFetch(() => json(userDetails(2)));
    const first = setup();
    expect(screen.getByRole('heading', { level: 1 })).toBeInTheDocument(); // loading
    await screen.findByRole('heading', { level: 1, name: 'نام2 خانوادگی2' });
    await waitFor(() => expect(document.title).toBe('نام2 خانوادگی2 | فرانو'));
    first.unmount();
    for (const status of [403, 404, 500]) {
      stubFetch(() => json({}, status));
      const view = setup();
      await screen.findByRole('alert');
      expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
      view.unmount();
    }
  });
});
