import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { RouteView, RouterProvider, type Route } from '../../../lib/router';
import { PageBreadcrumbBar, PageBreadcrumbProvider } from '@/components/shell/PageBreadcrumb';
import { json, stubApi, userDetails, type ApiCall } from '../../../test/adminUsers';
import { UserDetailPage } from './UserDetailPage';
import { UserEditPage } from './UserEditPage';
import { clearUserFlash, peekUserFlash, setUserFlash } from './userFlash';

const onUnauthenticated = vi.fn((): boolean => false);
const routes: Route[] = [
  { path: '/admin/users', element: () => <h1>فهرست</h1>, title: 'فهرست' },
  { path: '/admin/users/:id', element: () => <UserDetailPage onUnauthenticated={onUnauthenticated} canUpdate />, title: 'جزئیات' },
  { path: '/admin/users/:id/edit', element: () => <UserEditPage onUnauthenticated={onUnauthenticated} />, title: 'ویرایش' },
];

function setup(id = 'user-2') {
  window.history.pushState(null, '', `/app/admin/users/${id}/edit`);
  return render(
    <RouterProvider routes={routes}>
      <PageBreadcrumbProvider fallback={[{ label: 'ویرایش' }]}>
        <RouteView />
        <PageBreadcrumbBar />
      </PageBreadcrumbProvider>
    </RouterProvider>,
  );
}

const firstName = () => screen.getByLabelText(/^نام$/);
const username = () => screen.getByLabelText(/^نام کاربری/);
const email = () => screen.getByLabelText(/^ایمیل/);
const mobile = () => screen.getByLabelText(/^شماره موبایل/);
const active = () => screen.getByRole('checkbox', { name: 'حساب فعال باشد' });
const save = () => screen.getByRole('button', { name: 'ذخیرهٔ تغییرات' });
const puts = (calls: ApiCall[]) => calls.filter((call) => call.method === 'PUT');

/** GET returns the user; PUT is answered by `onPut`. */
function api(onPut: (call: ApiCall) => Response = () => new Response(null, { status: 204 }), user = userDetails(2)) {
  return stubApi((call) => (call.method === 'PUT' ? onPut(call) : json(user)));
}

describe('UserEditPage', () => {
  beforeEach(() => {
    onUnauthenticated.mockReset();
    onUnauthenticated.mockReturnValue(false);
    clearUserFlash();
  });

  it('loads the user, prefills the form and has no password fields', async () => {
    api();
    setup();
    expect(await screen.findByLabelText(/^نام$/)).toHaveValue('نام2');
    expect(screen.getByRole('heading', { level: 1, name: 'ویرایش کاربر' })).toBeInTheDocument();
    expect(screen.getByLabelText(/^نام خانوادگی$/)).toHaveValue('خانوادگی2');
    expect(username()).toHaveValue('user2');
    expect(username()).toHaveAttribute('dir', 'ltr');
    expect(email()).toHaveValue('user2@example.test');
    expect(mobile()).toHaveValue('09120000002');
    expect(active()).toBeChecked();
    expect(screen.queryByLabelText(/رمز عبور/)).toBeNull();
    // The shell bar receives the page declaration in an effect right after the record loads.
    const crumbs = within(await screen.findByRole('navigation', { name: 'مسیر صفحه' }));
    expect(await crumbs.findByRole('link', { name: 'نام2 خانوادگی2' })).toHaveAttribute('href', '/app/admin/users/user-2');
    expect(crumbs.getByText('ویرایش')).toHaveAttribute('aria-current', 'page');
    await waitFor(() => expect(document.title).toBe('ویرایش نام2 خانوادگی2 | فرانو'));
  });

  it('prefills empty values for a user without names or mobile', async () => {
    api(undefined, userDetails(1, { firstName: null, lastName: null, cellPhoneNumber: null }));
    setup('user-1');
    expect(await screen.findByLabelText(/^نام$/)).toHaveValue('');
    expect(mobile()).toHaveValue('');
  });

  it('keeps Save disabled while nothing changed and sends no request on submit', async () => {
    const user = userEvent.setup();
    const { calls } = api();
    setup();
    await screen.findByLabelText(/^نام$/);
    expect(save()).toBeDisabled();
    expect(screen.getByText('تغییری برای ذخیره وجود ندارد.')).toBeInTheDocument();
    await user.type(firstName(), 'x{Backspace}');
    expect(save()).toBeDisabled();
    expect(puts(calls)).toHaveLength(0);
  });

  it('sends the FULL body with the anti-forgery header, then opens the detail page with a success message', async () => {
    const user = userEvent.setup();
    const { calls } = api();
    setup();
    await user.type(await screen.findByLabelText(/^نام$/), 'ی');
    expect(save()).toBeEnabled();
    await user.click(save());
    await screen.findByText('تغییرات کاربر ذخیره شد.');
    const put = puts(calls)[0];
    expect(put?.path).toBe('/api/v1/admin/users/user-2');
    expect(put?.body).toEqual({
      username: 'user2',
      firstName: 'نام2ی',
      lastName: 'خانوادگی2',
      emailAddress: 'user2@example.test',
      cellPhoneNumber: '09120000002',
      isActive: true,
    });
    expect(put?.headers['X-XSRF-TOKEN']).toBe('tok');
    expect(window.location.pathname).toBe('/app/admin/users/user-2');
    // The edit button is on the detail page.
    expect(await screen.findByRole('link', { name: 'ویرایش' })).toHaveAttribute('href', '/app/admin/users/user-2/edit');
  });

  it('does not count a trailing space as a change', async () => {
    const user = userEvent.setup();
    api();
    setup();
    await user.type(await screen.findByLabelText(/^نام$/), '  ');
    expect(save()).toBeDisabled();
  });

  it('drops a stale success message when the edit form opens', async () => {
    setUserFlash('user-2', 'updated');
    api();
    setup();
    await screen.findByLabelText(/^نام$/);
    expect(peekUserFlash('user-2')).toBeNull();
  });

  it('sends null when an optional field is cleared', async () => {
    const user = userEvent.setup();
    const { calls } = api();
    setup();
    await user.clear(await screen.findByLabelText(/^شماره موبایل/));
    await user.click(save());
    await waitFor(() => expect(puts(calls)).toHaveLength(1));
    expect(puts(calls)[0]?.body).toMatchObject({ cellPhoneNumber: null });
  });

  it('validates edited values and does not send them', async () => {
    const user = userEvent.setup();
    const { calls } = api();
    setup();
    await user.clear(await screen.findByLabelText(/^ایمیل/));
    await user.click(save());
    expect(await screen.findByText('ایمیل را وارد کنید.')).toBeInTheDocument();
    expect(email()).toHaveFocus();
    await user.type(email(), 'bad');
    await user.type(mobile(), '1');
    await user.click(save());
    expect(screen.getByText('قالب ایمیل معتبر نیست.')).toBeInTheDocument();
    expect(screen.getByText(/شماره موبایل باید ۱۱ رقم لاتین/)).toBeInTheDocument();
    expect(puts(calls)).toHaveLength(0);
  });

  it('asks for confirmation before deactivating; cancel sends nothing and keeps the choice', async () => {
    const user = userEvent.setup();
    const { calls } = api();
    setup();
    await user.click(await screen.findByRole('checkbox', { name: 'حساب فعال باشد' }));
    await user.click(save());
    const dialog = screen.getByRole('alertdialog', { name: 'غیرفعال‌کردن حساب کاربر' });
    expect(dialog).toHaveTextContent('دیگر نمی‌تواند وارد سامانه شود');
    await user.click(within(dialog).getByRole('button', { name: 'انصراف' }));
    expect(screen.queryByRole('alertdialog')).toBeNull();
    expect(puts(calls)).toHaveLength(0);
    expect(active()).not.toBeChecked();
  });

  it('deactivates after confirmation with isActive=false in the full body', async () => {
    const user = userEvent.setup();
    const { calls } = api();
    setup();
    await user.click(await screen.findByRole('checkbox', { name: 'حساب فعال باشد' }));
    await user.click(save());
    await user.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'غیرفعال‌کردن و ذخیره' }));
    await screen.findByText('تغییرات کاربر ذخیره شد.');
    expect(puts(calls)).toHaveLength(1);
    expect(puts(calls)[0]?.body).toEqual({
      username: 'user2',
      firstName: 'نام2',
      lastName: 'خانوادگی2',
      emailAddress: 'user2@example.test',
      cellPhoneNumber: '09120000002',
      isActive: false,
    });
  });

  it('can be confirmed with the keyboard and cancelled with Escape', async () => {
    const user = userEvent.setup();
    const { calls } = api();
    setup();
    await user.click(await screen.findByRole('checkbox', { name: 'حساب فعال باشد' }));
    await user.click(save());
    expect(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'انصراف' })).toHaveFocus();
    await user.keyboard('{Escape}');
    expect(screen.queryByRole('alertdialog')).toBeNull();
    expect(puts(calls)).toHaveLength(0);
    await user.click(save());
    await user.tab();
    expect(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'غیرفعال‌کردن و ذخیره' })).toHaveFocus();
    await user.keyboard('{Enter}');
    await screen.findByText('تغییرات کاربر ذخیره شد.');
    expect(puts(calls)).toHaveLength(1);
  });

  it('does not ask for confirmation when activating an inactive account', async () => {
    const user = userEvent.setup();
    const { calls } = api(undefined, userDetails(3, { isActive: false }));
    setup('user-3');
    await user.click(await screen.findByRole('checkbox', { name: 'حساب فعال باشد' }));
    await user.click(save());
    await screen.findByText('تغییرات کاربر ذخیره شد.');
    expect(puts(calls)[0]?.body).toMatchObject({ isActive: true });
  });

  it('shows the server refusal to deactivate your own account (409) and keeps the form usable', async () => {
    const user = userEvent.setup();
    const message = 'مدیر سیستم نمی‌تواند حساب جاری خود را غیرفعال کند.';
    api(() => json({ error: message }, 409));
    setup();
    await user.click(await screen.findByRole('checkbox', { name: 'حساب فعال باشد' }));
    await user.click(save());
    await user.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'غیرفعال‌کردن و ذخیره' }));
    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(message);
    // The dialog's focus restore must not steal focus from the message.
    await waitFor(() => expect(screen.queryByRole('alertdialog')).toBeNull());
    await waitFor(() => expect(alert).toHaveFocus());
    expect(window.location.pathname).toBe('/app/admin/users/user-2/edit');
    expect(active()).not.toBeChecked();
    expect(save()).toBeEnabled();
  });

  it('shows the last-administrator refusal and the duplicate conflict as alerts', async () => {
    const user = userEvent.setup();
    let reply = 'غیرفعال‌کردن آخرین مدیر سیستم مجاز نیست.';
    api(() => json({ error: reply }, 409));
    setup();
    await user.click(await screen.findByRole('checkbox', { name: 'حساب فعال باشد' }));
    await user.click(save());
    await user.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'غیرفعال‌کردن و ذخیره' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('آخرین مدیر سیستم');
    reply = 'نام کاربری، ایمیل یا شماره همراه قبلاً ثبت شده است.';
    await user.click(active());
    await user.type(username(), 'x');
    await user.click(save());
    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('قبلاً ثبت شده است'));
    expect(username()).toHaveValue('user2x');
  });

  it('maps server field errors to the fields', async () => {
    const user = userEvent.setup();
    api(() => json({ title: 'One or more validation errors occurred.', errors: { Username: ['نام کاربری مجاز نیست.'] } }, 400));
    setup();
    await user.type(await screen.findByLabelText(/^نام کاربری/), 'x');
    await user.click(save());
    expect(await screen.findByText('نام کاربری مجاز نیست.')).toBeInTheDocument();
    expect(username()).toHaveAttribute('aria-invalid', 'true');
  });

  it('prevents a double submit', async () => {
    const user = userEvent.setup();
    let release: () => void = () => undefined;
    const { calls } = stubApi((call) =>
      call.method === 'PUT' ? new Promise<Response>((resolve) => (release = () => resolve(new Response(null, { status: 204 })))) : json(userDetails(2)),
    );
    setup();
    await user.type(await screen.findByLabelText(/^نام$/), 'ی');
    await user.dblClick(save());
    expect(await screen.findByRole('button', { name: 'در حال ذخیره…' })).toBeDisabled();
    expect(puts(calls)).toHaveLength(1);
    release();
    await screen.findByText('تغییرات کاربر ذخیره شد.');
    expect(puts(calls)).toHaveLength(1);
  });

  it('guards a dirty form on Cancel and on beforeunload, and cancels back to the detail page', async () => {
    const user = userEvent.setup();
    api();
    setup();
    await screen.findByLabelText(/^نام$/);
    const clean = new Event('beforeunload', { cancelable: true });
    window.dispatchEvent(clean);
    expect(clean.defaultPrevented).toBe(false);
    await user.type(firstName(), 'ی');
    const dirty = new Event('beforeunload', { cancelable: true });
    window.dispatchEvent(dirty);
    expect(dirty.defaultPrevented).toBe(true);
    await user.click(screen.getByRole('button', { name: 'انصراف' }));
    await user.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'ادامه ویرایش' }));
    expect(window.location.pathname).toBe('/app/admin/users/user-2/edit');
    await user.click(screen.getByRole('button', { name: 'انصراف' }));
    await user.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'خروج بدون ذخیره' }));
    expect(window.location.pathname).toBe('/app/admin/users/user-2');
  });

  it('shows not found for 404 and an invalid id (400) with a link back, and no form', async () => {
    for (const status of [404, 400]) {
      stubApi(() => json({}, status));
      const view = setup('missing');
      expect(await screen.findByRole('alert')).toHaveTextContent('کاربر پیدا نشد');
      expect(screen.queryByRole('form')).toBeNull();
      expect(screen.getByRole('link', { name: 'بازگشت به فهرست کاربران' })).toHaveAttribute('href', '/app/admin/users');
      view.unmount();
    }
  });

  it('shows 403 without retry, a generic error with retry, and hands 401 to the sign-in flow', async () => {
    const user = userEvent.setup();
    stubApi(() => json({}, 403));
    const first = setup();
    expect(await screen.findByRole('alert')).toHaveTextContent('مجوز ویرایش');
    expect(screen.queryByRole('button', { name: 'تلاش دوباره' })).toBeNull();
    first.unmount();

    let fail = true;
    stubApi(() => (fail ? json({}, 500) : json(userDetails(2))));
    const second = setup();
    expect(await screen.findByRole('alert')).toHaveTextContent('خطای غیرمنتظره');
    fail = false;
    await user.click(screen.getByRole('button', { name: 'تلاش دوباره' }));
    expect(await screen.findByLabelText(/^نام$/)).toHaveValue('نام2');
    second.unmount();

    onUnauthenticated.mockReturnValue(true);
    stubApi(() => new Response(null, { status: 401 }));
    setup();
    await waitFor(() => expect(onUnauthenticated).toHaveBeenCalled());
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('hands a 401 on save to the sign-in flow', async () => {
    const user = userEvent.setup();
    api(() => new Response(null, { status: 401 }));
    setup();
    await user.type(await screen.findByLabelText(/^نام$/), 'ی');
    onUnauthenticated.mockReturnValue(true);
    await user.click(save());
    await waitFor(() => expect(onUnauthenticated).toHaveBeenCalled());
    expect(screen.queryByRole('alert')).toBeNull();
  });
});
