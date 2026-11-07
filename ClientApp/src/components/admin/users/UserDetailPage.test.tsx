import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { RouteView, RouterProvider, type Route } from '../../../lib/router';
import { PageBreadcrumbBar, PageBreadcrumbProvider } from '@/components/shell/PageBreadcrumb';
import { json, stubFetch, userDetails } from '../../../test/adminUsers';
import { rememberUserListLocation, resetUserListReturnForTests } from './userListReturn';
import { UserDetailPage } from './UserDetailPage';
import { clearUserFlash, setUserFlash } from './userFlash';

const onUnauthenticated = vi.fn((): boolean => false);
let canUpdate = false;
function DetailRoute() {
  return <UserDetailPage onUnauthenticated={onUnauthenticated} canUpdate={canUpdate} />;
}
const routes: Route[] = [
  { path: '/admin/users', element: () => <h1>فهرست</h1>, title: 'فهرست' },
  { path: '/admin/users/:id', element: DetailRoute, title: 'جزئیات' },
];

function setup(id = 'user-2') {
  window.history.pushState(null, '', `/app/admin/users/${id}`);
  return render(
    <RouterProvider routes={routes}>
      <PageBreadcrumbProvider fallback={[{ label: 'جزئیات' }]}>
        <RouteView />
        <PageBreadcrumbBar />
      </PageBreadcrumbProvider>
    </RouterProvider>,
  );
}

describe('UserDetailPage', () => {
  beforeEach(() => {
    onUnauthenticated.mockReset();
    onUnauthenticated.mockReturnValue(false);
    canUpdate = false;
    clearUserFlash();
    resetUserListReturnForTests();
  });

  it('shows no edit button without the update permission', async () => {
    stubFetch(() => json(userDetails(2)));
    setup();
    await screen.findByRole('heading', { level: 1, name: 'نام2 خانوادگی2' });
    expect(screen.queryByRole('link', { name: 'ویرایش' })).toBeNull();
  });

  it('shows an edit link in the header with the update permission', async () => {
    canUpdate = true;
    stubFetch(() => json(userDetails(2)));
    setup();
    expect(await screen.findByRole('link', { name: 'ویرایش' })).toHaveAttribute('href', '/app/admin/users/user-2/edit');
  });

  it('shows a one-shot success message handed over by a form page', async () => {
    setUserFlash('user-2', 'created');
    stubFetch(() => json(userDetails(2)));
    const view = setup();
    const message = await screen.findByText('کاربر با موفقیت ایجاد شد.');
    // The message is placed inside a polite live region that exists independently of the text.
    expect(message.parentElement).toHaveAttribute('aria-live', 'polite');
    view.unmount();
    stubFetch(() => json(userDetails(2)));
    setup();
    await screen.findByRole('heading', { level: 1, name: 'نام2 خانوادگی2' });
    expect(screen.queryByText('کاربر با موفقیت ایجاد شد.')).toBeNull();
  });

  it('ignores a message that was meant for another user', async () => {
    setUserFlash('someone-else', 'updated');
    stubFetch(() => json(userDetails(2)));
    setup();
    await screen.findByRole('heading', { level: 1, name: 'نام2 خانوادگی2' });
    expect(screen.queryByText('تغییرات کاربر ذخیره شد.')).toBeNull();
  });

  it('shows breadcrumb, header and the general tab first', async () => {
    const fetchMock = stubFetch(() => json(userDetails(2)));
    setup();
    expect(await screen.findByRole('heading', { level: 1, name: 'نام2 خانوادگی2' })).toBeInTheDocument();
    expect(String(fetchMock.mock.calls[0]?.[0])).toBe('/api/v1/admin/users/user-2');
    // The shell bar receives the page declaration in an effect right after the record loads.
    const crumbs = within(await screen.findByRole('navigation', { name: 'مسیر صفحه' }));
    expect(crumbs.getByText('مدیریت سیستم')).toBeInTheDocument();
    expect(crumbs.getByRole('link', { name: 'کاربران' })).toHaveAttribute('href', '/app/admin/users');
    expect(await crumbs.findByText('نام2 خانوادگی2')).toHaveAttribute('aria-current', 'page');
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

  it('shows the back link in the ready state too and returns to the remembered list query (Issue #444 stage 1)', async () => {
    const user = userEvent.setup();
    stubFetch(() => json(userDetails(2)));
    // The list page remembers its query in memory; simulate a visit with filters and paging.
    rememberUserListLocation('q=%D8%B3%D8%A7%D8%B1%D8%A7&status=active&page=2&pageSize=20');
    setup();
    expect(await screen.findByRole('heading', { level: 1, name: 'نام2 خانوادگی2' })).toBeInTheDocument();
    const back = screen.getByRole('link', { name: 'بازگشت به فهرست کاربران' });
    expect(back).toHaveAttribute('href', '/app/admin/users?q=%D8%B3%D8%A7%D8%B1%D8%A7&status=active&page=2&pageSize=20');
    await user.click(back);
    expect(window.location.pathname).toBe('/app/admin/users');
    expect(window.location.search).toBe('?q=%D8%B3%D8%A7%D8%B1%D8%A7&status=active&page=2&pageSize=20');
  });

  it('falls back to the plain list when the visit did not pass by the list (fresh load)', async () => {
    stubFetch(() => json(userDetails(2)));
    setup();
    expect(await screen.findByRole('heading', { level: 1, name: 'نام2 خانوادگی2' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'بازگشت به فهرست کاربران' })).toHaveAttribute('href', '/app/admin/users');
  });

  it('keeps the back link available while loading', () => {
    stubFetch(() => new Promise(() => undefined)); // never settles
    setup();
    expect(screen.getByRole('link', { name: 'بازگشت به فهرست کاربران' })).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('در حال دریافت اطلاعات کاربر…');
  });
});
