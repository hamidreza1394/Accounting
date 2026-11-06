import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { RouteView, RouterProvider, type Route } from '../../../lib/router';
import { json, stubApi, type ApiCall } from '../../../test/adminUsers';
import { catalogItems, companiesBody, roleItem, systemRole } from '../../../test/roles';
import { RoleDetailPage } from './RoleDetailPage';
import { clearRoleFlash, setRoleFlash } from './roleFlash';

const onUnauthenticated = vi.fn((): boolean => false);

const onForbidden = vi.fn();

function setup(roleId = 'role-1', canManage = true, search = '?company=a', installationAdmin = true) {
  const routes: Route[] = [
    { path: '/admin/roles', element: () => <h1>فهرست گروه‌ها</h1>, title: 'فهرست' },
    { path: '/admin/roles/:roleId', element: () => <RoleDetailPage onUnauthenticated={onUnauthenticated} defaultCompanyId="a" can={(permission) => canManage && (permission === 'admin.roles.update' || permission === 'admin.roles.assign')} isInstallationAdministrator={installationAdmin} onForbidden={onForbidden} />, title: 'جزئیات' },
  ];
  window.history.pushState(null, '', `/app/admin/roles/${roleId}${search}`);
  return render(
    <RouterProvider routes={routes}>
      <RouteView />
    </RouterProvider>,
  );
}

const puts = (calls: ApiCall[]) => calls.filter((call) => call.method === 'PUT');
const leaf = (name: string) => screen.getByRole('checkbox', { name });
const title = () => screen.getByLabelText(/^عنوان گروه/);
const active = () => screen.getByRole('checkbox', { name: 'گروه فعال باشد' });
const save = () => screen.getByRole('button', { name: 'بررسی و ذخیرهٔ تغییرات' });

/** Serves the roles list from `roles`; a PUT is answered by `onPut`. */
function api(roles: unknown[] = [roleItem(), systemRole], onPut: (call: ApiCall) => Response | Promise<Response> = () => new Response(null, { status: 204 })) {
  return stubApi((call) => {
    if (call.method === 'PUT') return onPut(call);
    if (call.path === '/api/v1/companies') return json(companiesBody);
    if (call.path === '/api/v1/admin/permission-catalog') return json(catalogItems);
    if (call.path === '/api/v1/admin/companies/a/roles') return json(roles);
    if (call.path === '/api/v1/admin/companies/a/roles/role-1/members') return json({ items: [], totalCount: 0, page: 1, pageSize: 20 });
    return new Response(null, { status: 404 });
  });
}

describe('RoleDetailPage', () => {
  beforeEach(() => {
    onUnauthenticated.mockReset();
    onUnauthenticated.mockReturnValue(false);
    onForbidden.mockReset();
    clearRoleFlash();
  });

  it('loads an editable role: prefilled fields, checked permissions, save disabled until a change', async () => {
    api();
    setup();
    expect(await screen.findByRole('heading', { level: 1, name: 'کارشناس فروش' })).toBeInTheDocument();
    expect(await screen.findByRole('form', { name: 'فرم ویرایش گروه دسترسی' })).toBeInTheDocument();
    expect(title()).toHaveValue('کارشناس فروش');
    expect(active()).toBeChecked();
    expect(screen.getByText('SALES')).toHaveAttribute('dir', 'ltr');
    expect(leaf('ایجاد سفارش')).toBeChecked();
    expect(leaf('مشاهدهٔ سفارش‌ها')).toBeChecked();
    expect(leaf('حذف سفارش')).not.toBeChecked();
    expect(save()).toBeDisabled();
    expect(screen.getByText('تغییری برای ذخیره وجود ندارد.')).toBeInTheDocument();
  });

  it('shows a system role read-only with an explanation and no way to save', async () => {
    const user = userEvent.setup();
    const { calls } = api();
    setup('role-sys');
    expect(await screen.findByText(/این گروه سیستمی است و قابل ویرایش نیست/)).toBeInTheDocument();
    expect(title()).toHaveAttribute('readonly');
    expect(active()).toBeDisabled();
    expect(leaf('مشاهدهٔ کاربران')).toBeChecked();
    expect(leaf('مشاهدهٔ کاربران')).toHaveAttribute('aria-readonly', 'true');
    expect(screen.queryByRole('button', { name: 'بررسی و ذخیرهٔ تغییرات' })).toBeNull();
    expect(screen.getByRole('link', { name: 'بازگشت به فهرست گروه‌ها' })).toHaveAttribute('href', '/app/admin/roles?company=a');
    await user.type(title(), 'x');
    expect(title()).toHaveValue('مدیر شرکت');
    expect(puts(calls)).toHaveLength(0);
  });

  it('is read-only for a user without the manage hint', async () => {
    api();
    setup('role-1', false);
    expect(await screen.findByText(/شما مجوز ویرایش گروه‌های دسترسی را ندارید/)).toBeInTheDocument();
    expect(leaf('ایجاد سفارش')).toHaveAttribute('aria-readonly', 'true');
    expect(screen.queryByRole('button', { name: 'بررسی و ذخیرهٔ تغییرات' })).toBeNull();
  });

  it('previews exactly the added and removed permissions before saving, then sends the full body', async () => {
    const user = userEvent.setup();
    const { calls } = api();
    setup();
    await screen.findByRole('form', { name: 'فرم ویرایش گروه دسترسی' });
    await user.clear(title());
    await user.type(title(), '  کارشناس   ارشد ');
    await user.click(leaf('ایجاد سفارش'));
    await user.click(leaf('حذف سفارش'));
    await user.click(leaf('گزارش شرکتی'));
    await user.click(save());

    const dialog = await screen.findByRole('dialog', { name: 'پیش‌نمایش تغییرات گروه' });
    expect(dialog).toHaveTextContent('عنوان: «کارشناس فروش» به «کارشناس ارشد»');
    const added = within(dialog).getByRole('list', { name: 'مجوزهای افزوده‌شده' });
    expect(within(added).getAllByRole('listitem').map((item) => item.textContent)).toEqual([
      '+ حذف سفارش (trade.orders.delete)',
      '+ گزارش شرکتی (trade.reports.company.view)',
    ]);
    const removed = within(dialog).getByRole('list', { name: 'مجوزهای حذف‌شده' });
    expect(within(removed).getAllByRole('listitem').map((item) => item.textContent)).toEqual(['− ایجاد سفارش (trade.orders.create)']);
    expect(dialog).toHaveTextContent('تعداد مجوزها: ۲ ← ۳');
    expect(puts(calls)).toHaveLength(0);

    // Going back changes nothing.
    await user.click(within(dialog).getByRole('button', { name: 'بازگشت به ویرایش' }));
    expect(puts(calls)).toHaveLength(0);
    expect(title()).toHaveValue('  کارشناس   ارشد ');

    await user.click(save());
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'تأیید و ذخیره' }));
    await waitFor(() => expect(puts(calls)).toHaveLength(1));
    expect(puts(calls)[0]).toMatchObject({
      path: '/api/v1/admin/companies/a/roles/role-1',
      body: { name: 'کارشناس ارشد', isActive: true, permissionCodes: ['trade.orders.delete', 'trade.orders.view', 'trade.reports.company.view'] },
    });
    expect(Object.keys(puts(calls)[0]?.body as object).sort()).toEqual(['isActive', 'name', 'permissionCodes']);
    expect(puts(calls)[0]?.headers['X-XSRF-TOKEN']).toBe('tok');
    expect(await screen.findByText('تغییرات گروه دسترسی ذخیره شد.')).toBeInTheDocument();
  });

  it('re-initializes from the saved values after a save: no stale form, no guards, no second save', async () => {
    const user = userEvent.setup();
    let stored = roleItem();
    const { calls } = stubApi((call) => {
      if (call.method === 'PUT') {
        const body = call.body as { name: string; isActive: boolean; permissionCodes: string[] };
        stored = roleItem({ name: body.name, isActive: body.isActive, permissionCodes: body.permissionCodes, permissionCount: body.permissionCodes.length });
        return new Response(null, { status: 204 });
      }
      if (call.path === '/api/v1/companies') return json(companiesBody);
      if (call.path === '/api/v1/admin/permission-catalog') return json(catalogItems);
      if (call.path === '/api/v1/admin/companies/a/roles') return json([stored]);
      if (call.path === '/api/v1/admin/companies/a/roles/role-1/members') return json({ items: [], totalCount: 0, page: 1, pageSize: 20 });
      return new Response(null, { status: 404 });
    });
    setup();
    await screen.findByRole('form', { name: 'فرم ویرایش گروه دسترسی' });
    await user.clear(title());
    await user.type(title(), 'نام جدید');
    await user.click(leaf('ایجاد سفارش'));
    await user.click(leaf('حذف سفارش'));
    await user.click(save());
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'تأیید و ذخیره' }));
    expect(await screen.findByText('تغییرات گروه دسترسی ذخیره شد.')).toBeInTheDocument();
    // Wait for the silent reload to land as well.
    await waitFor(() => expect(calls.filter((call) => call.path.endsWith('/roles')).length).toBeGreaterThanOrEqual(2));
    expect(title()).toHaveValue('نام جدید');
    expect(leaf('ایجاد سفارش')).not.toBeChecked();
    expect(leaf('حذف سفارش')).toBeChecked();
    expect(leaf('مشاهدهٔ سفارش‌ها')).toBeChecked();
    expect(save()).toBeDisabled();
    expect(screen.getByText('تغییری برای ذخیره وجود ندارد.')).toBeInTheDocument();
    const unload = new Event('beforeunload', { cancelable: true });
    window.dispatchEvent(unload);
    expect(unload.defaultPrevented).toBe(false);
    await user.click(screen.getByRole('button', { name: 'انصراف' }));
    expect(screen.queryByRole('alertdialog')).toBeNull();
    expect(window.location.pathname).toBe('/app/admin/roles');
    expect(puts(calls)).toHaveLength(1);
  });

  it('shows the loading state again when the role in the URL changes', async () => {
    api([roleItem(), systemRole]);
    const view = setup('role-1');
    await screen.findByRole('heading', { level: 1, name: 'کارشناس فروش' });
    window.history.pushState(null, '', '/app/admin/roles/role-sys?company=a');
    window.dispatchEvent(new PopStateEvent('popstate'));
    expect(await screen.findByRole('heading', { level: 1, name: 'مدیر شرکت' })).toBeInTheDocument();
    expect(title()).toHaveValue('مدیر شرکت');
    view.unmount();
  });

  it('shows the delegation-ceiling note and labels unknown permission codes in the preview', async () => {
    const user = userEvent.setup();
    api([roleItem({ permissionCodes: ['trade.orders.view', 'legacy.gone.view'] })]);
    setup();
    await screen.findByRole('form', { name: 'فرم ویرایش گروه دسترسی' });
    expect(screen.getByText(/به‌عنوان مدیر نصب سقف تفویض ندارید/)).toBeInTheDocument();
    await user.click(screen.getByRole('checkbox', { name: 'مجوز ناشناخته' }));
    await user.click(save());
    const dialog = await screen.findByRole('dialog');
    expect(within(within(dialog).getByRole('list', { name: 'مجوزهای حذف‌شده' })).getByRole('listitem')).toHaveTextContent('مجوز ناشناخته (legacy.gone.view)');
  });

  it('says so when only the name changes and the permissions stay the same', async () => {
    const user = userEvent.setup();
    api();
    setup();
    await screen.findByRole('form', { name: 'فرم ویرایش گروه دسترسی' });
    await user.type(title(), '۲');
    await user.click(save());
    expect(await screen.findByRole('dialog')).toHaveTextContent('مجوزهای گروه تغییری نمی‌کند.');
  });

  it('asks for confirmation before deactivating and sends isActive false', async () => {
    const user = userEvent.setup();
    const { calls } = api();
    setup();
    await screen.findByRole('form', { name: 'فرم ویرایش گروه دسترسی' });
    await user.click(active());
    await user.click(save());
    const dialog = await screen.findByRole('alertdialog', { name: 'غیرفعال‌کردن گروه و ذخیرهٔ تغییرات' });
    expect(dialog).toHaveTextContent('وضعیت: غیرفعال می‌شود.');
    expect(puts(calls)).toHaveLength(0);
    await user.click(within(dialog).getByRole('button', { name: 'غیرفعال‌کردن و ذخیره' }));
    await waitFor(() => expect(puts(calls)).toHaveLength(1));
    expect(puts(calls)[0]?.body).toEqual({ name: 'کارشناس فروش', isActive: false, permissionCodes: ['trade.orders.view', 'trade.orders.create'] });
  });

  it('shows the server message when deactivation is refused because of active members', async () => {
    const user = userEvent.setup();
    const message = 'ابتدا اعضای فعال این گروه را به گروه دیگری منتقل کنید.';
    api([roleItem({ activeMemberCount: 2 })], () => json({ error: message }, 409));
    setup();
    await screen.findByRole('form', { name: 'فرم ویرایش گروه دسترسی' });
    await user.click(active());
    await user.click(save());
    await user.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'غیرفعال‌کردن و ذخیره' }));
    expect(await screen.findByRole('alert')).toHaveTextContent(message);
    expect(screen.queryByText('تغییرات گروه دسترسی ذخیره شد.')).toBeNull();
    // The form keeps the user's edits so they can be fixed or reverted.
    expect(active()).not.toBeChecked();
    expect(save()).toBeEnabled();
  });

  it('validates the title before previewing', async () => {
    const user = userEvent.setup();
    const { calls } = api();
    setup();
    await screen.findByRole('form', { name: 'فرم ویرایش گروه دسترسی' });
    await user.clear(title());
    await user.click(save());
    expect(await screen.findByText('عنوان گروه الزامی است.')).toBeInTheDocument();
    expect(title()).toHaveFocus();
    expect(screen.queryByRole('dialog')).toBeNull();
    expect(puts(calls)).toHaveLength(0);
  });

  it('guards unsaved edits on cancel and on unload', async () => {
    const user = userEvent.setup();
    api();
    setup();
    await screen.findByRole('form', { name: 'فرم ویرایش گروه دسترسی' });
    const clean = new Event('beforeunload', { cancelable: true });
    window.dispatchEvent(clean);
    expect(clean.defaultPrevented).toBe(false);
    await user.click(leaf('حذف سفارش'));
    const dirty = new Event('beforeunload', { cancelable: true });
    window.dispatchEvent(dirty);
    expect(dirty.defaultPrevented).toBe(true);
    await user.click(screen.getByRole('button', { name: 'انصراف' }));
    await user.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'خروج بدون ذخیره' }));
    expect(window.location.pathname).toBe('/app/admin/roles');
  });

  it('shows a not-found state when the role is not in the company, and a company 404', async () => {
    api([roleItem()]);
    const view = setup('nope');
    expect(await screen.findByText('گروه دسترسی پیدا نشد')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'بازگشت به فهرست گروه‌ها' })).toBeInTheDocument();
    view.unmount();

    stubApi((call) => (call.path === '/api/v1/admin/permission-catalog' ? json(catalogItems) : call.path === '/api/v1/companies' ? json(companiesBody) : new Response(null, { status: 404 })));
    setup();
    expect(await screen.findByText('شرکت پیدا نشد')).toBeInTheDocument();
  });

  it('shows 403 and a retryable error', async () => {
    const user = userEvent.setup();
    stubApi((call) => (call.path === '/api/v1/admin/permission-catalog' ? json(catalogItems) : call.path === '/api/v1/companies' ? json(companiesBody) : new Response(null, { status: 403 })));
    const view = setup();
    expect(await screen.findByRole('alert')).toHaveTextContent('شما مجوز مشاهدهٔ گروه‌های دسترسی را ندارید.');
    view.unmount();

    let fail = true;
    stubApi((call) => {
      if (call.path === '/api/v1/admin/permission-catalog') return json(catalogItems);
      if (call.path === '/api/v1/companies') return json(companiesBody);
      return fail ? json({ error: 'x' }, 500) : json([roleItem()]);
    });
    setup();
    await screen.findByText('خطای غیرمنتظره‌ای در سرور رخ داد. لطفاً دوباره تلاش کنید.');
    fail = false;
    await user.click(screen.getByRole('button', { name: 'تلاش دوباره' }));
    expect(await screen.findByRole('form', { name: 'فرم ویرایش گروه دسترسی' })).toBeInTheDocument();
  });

  it('shows the one-shot created message handed over by the create page', async () => {
    setRoleFlash('role-1', 'created');
    api();
    setup();
    expect(await screen.findByText('گروه دسترسی با موفقیت ایجاد شد.')).toBeInTheDocument();
  });

  it('keeps the editor in step with a transfer: the member count updates and unsaved edits survive', async () => {
    const user = userEvent.setup();
    let moved = false;
    const members = [
      { userId: 'user-1', username: 'm1', firstName: 'الف', lastName: 'ب', isUserActive: true, memberSinceUtc: '2026-10-01T09:00:00Z' },
      { userId: 'user-2', username: 'm2', firstName: 'ج', lastName: 'د', isUserActive: true, memberSinceUtc: '2026-10-01T09:00:00Z' },
    ];
    stubApi((call) => {
      if (call.method === 'POST' && call.path.endsWith('/members/transfer')) {
        moved = true;
        return json({ movedCount: 2 });
      }
      if (call.path === '/api/v1/companies') return json(companiesBody);
      if (call.path === '/api/v1/admin/permission-catalog') return json(catalogItems);
      if (call.path === '/api/v1/admin/companies/a/roles') {
        return json([roleItem({ activeMemberCount: 2 }), roleItem({ id: 'role-2', code: 'ACCOUNTANT', name: 'حسابدار' })]);
      }
      if (call.path === '/api/v1/admin/companies/a/roles/role-1/members') {
        return json({ items: moved ? [] : members, totalCount: moved ? 0 : 2, page: 1, pageSize: 20 });
      }
      return new Response(null, { status: 404 });
    });
    setup();
    await screen.findByRole('form', { name: 'فرم ویرایش گروه دسترسی' });
    const memberField = screen.getByText('اعضای فعال').parentElement as HTMLElement;
    expect(memberField).toHaveTextContent('۲ نفر');

    await user.type(title(), ' ویرایش‌نشده‌ذخیره');
    const edited = (title() as HTMLInputElement).value;
    expect(edited).toContain('ویرایش‌نشده‌ذخیره');

    const select = await screen.findByRole('combobox', { name: /گروه مقصد/ });
    await within(select).findByRole('option', { name: 'حسابدار' });
    await user.selectOptions(select, 'role-2');
    await user.click(screen.getByRole('button', { name: 'انتقال اعضا…' }));
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'انتقال اعضا' }));

    expect(await screen.findByText('این گروه عضو فعالی ندارد')).toBeInTheDocument();
    // The editor's own counter no longer contradicts the table, and the unsaved title was not wiped.
    await waitFor(() => expect(memberField).toHaveTextContent('۰ نفر'));
    expect((title() as HTMLInputElement).value).toBe(edited);
  });
  describe('company administrator (D-071)', () => {
    it('edits a role fully inside the ceiling and shows the company ceiling note', async () => {
      api();
      setup('role-1', true, '?company=a', false);
      expect(await screen.findByRole('form', { name: 'فرم ویرایش گروه دسترسی' })).toBeInTheDocument();
      expect(title()).not.toHaveAttribute('readonly');
      expect(screen.getByText(/سقف تفویض \(D-071\)/)).toBeInTheDocument();
    });

    it('shows a role with permissions beyond the ceiling read-only, so nothing is silently dropped', async () => {
      api([roleItem({ permissionCodes: ['trade.orders.view', 'legacy.gone.view'] })]);
      setup('role-1', true, '?company=a', false);
      expect(await screen.findByText(/خارج از سقف مجوزهای شما است/)).toBeInTheDocument();
      expect(title()).toHaveAttribute('readonly');
      expect(screen.queryByRole('button', { name: 'بررسی و ذخیرهٔ تغییرات' })).toBeNull();
    });

    it('leaves the same role editable for an installation administrator', async () => {
      api([roleItem({ permissionCodes: ['trade.orders.view', 'legacy.gone.view'] })]);
      setup('role-1', true, '?company=a', true);
      await screen.findByRole('form', { name: 'فرم ویرایش گروه دسترسی' });
      expect(title()).not.toHaveAttribute('readonly');
    });

    it('is read-only without roles.update', async () => {
      api();
      setup('role-1', false, '?company=a', false);
      expect(await screen.findByText(/شما مجوز ویرایش گروه‌های دسترسی را ندارید/)).toBeInTheDocument();
      expect(title()).toHaveAttribute('readonly');
    });
  });

  describe('server refusals on save', () => {
    async function submitRename() {
      const user = userEvent.setup();
      await screen.findByRole('form', { name: 'فرم ویرایش گروه دسترسی' });
      await user.clear(title());
      await user.type(title(), 'نام جدید');
      await user.click(save());
      await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'تأیید و ذخیره' }));
      return user;
    }

    it('on 403 shows the message and asks the app to re-read the real permissions', async () => {
      api([roleItem(), systemRole], () => json({ error: 'فراتر از سقف مجوزهای شما است.' }, 403));
      setup();
      await submitRename();
      expect(await screen.findByRole('alert')).toHaveTextContent('فراتر از سقف مجوزهای شما است.');
      expect(onForbidden).toHaveBeenCalledTimes(1);
    });

    it('on 409 shows the message and reloads the latest role on request', async () => {
      const { calls } = api([roleItem(), systemRole], () => json({ error: 'گروه هم‌زمان تغییر کرد.' }, 409));
      setup();
      const user = await submitRename();
      expect(await screen.findByRole('alert')).toHaveTextContent('گروه هم‌زمان تغییر کرد.');
      const loads = () => calls.filter((call) => call.method === 'GET' && call.path === '/api/v1/admin/companies/a/roles').length;
      const before = loads();
      await user.click(screen.getByRole('button', { name: 'دریافت آخرین اطلاعات' }));
      await waitFor(() => expect(loads()).toBe(before + 1));
      await waitFor(() => expect(title()).toHaveValue('کارشناس فروش'));
    });
  });
});
