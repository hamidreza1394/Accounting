import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { CanHint } from '../../../lib/permissions';
import { RouteView, RouterProvider, type Route } from '../../../lib/router';
import { PageBreadcrumbProvider } from '@/components/shell/PageBreadcrumb';
import { json, stubApi, type ApiCall } from '../../../test/adminUsers';
import { companiesBody, perm, roleItem, systemRole } from '../../../test/roles';
import { AccessPermissionsPage } from './AccessPermissionsPage';

const onUnauthenticated = vi.fn((): boolean => false);
const onForbidden = vi.fn();

const catalog = [
  perm('hr.employees.list', 'فهرست کارکنان'),
  perm('hr.employees.view', 'مشاهده کارمند'),
  perm('hr.employees.create', 'ایجاد کارمند'),
  perm('hr.positions.list', 'فهرست سمت‌ها'),
  perm('roles.view', 'مشاهده نقش‌ها'),
  perm('roles.update', 'ویرایش نقش‌ها'),
  perm('trade.form.party.view', 'مشاهده طرف حساب'),
  perm('trade.form.party.create', 'ایجاد طرف حساب'),
  perm('trade.form.party.delete', 'حذف طرف حساب'),
];

const subsystemsBody = [
  { code: 'access-admin', title: 'دسترسی و نقش‌ها', entitlementScope: 'none', entitlementLinked: false },
  { code: 'hr', title: 'منابع انسانی', entitlementScope: 'none', entitlementLinked: false },
  { code: 'finance', title: 'حسابداری', entitlementScope: 'installation', entitlementLinked: true, entitlementCode: 'finance' },
  { code: 'TRADE_IMPORT', title: 'بازرگانی خارجی', entitlementScope: 'company', entitlementLinked: true, entitlementCode: 'TRADE_IMPORT' },
];

const hrRole = roleItem({ id: 'role-1', code: 'HR', name: 'کارشناس منابع انسانی', activeMemberCount: 1, permissionCodes: ['hr.employees.list', 'hr.employees.view'] });
const sharedRole = roleItem({ id: 'role-2', code: 'SHARED', name: 'مدیر مشترک', activeMemberCount: 3, permissionCodes: ['hr.employees.list', 'hr.employees.view', 'roles.view'] });
const viewerRole = roleItem({ id: 'role-3', code: 'VIEWER', name: 'ناظر', activeMemberCount: 0, permissionCodes: ['roles.view'] });
const sysRole = { ...systemRole, permissionCodes: ['hr.employees.list', 'roles.view'] };

const directoryUser = (overrides: Record<string, unknown> = {}) => ({
  userId: 'user-1',
  username: 'ali',
  firstName: 'علی',
  lastName: 'رضایی',
  userIsActive: true,
  membershipIsActive: true,
  roleId: 'role-1',
  roleCode: 'HR',
  roleName: 'کارشناس منابع انسانی',
  roleIsActive: true,
  membershipCreatedAtUtc: '2026-10-01T09:00:00Z',
  ...overrides,
});

const directory = (items: unknown[]) => ({ companyId: 'a', companyName: 'شرکت الف', items, totalCount: items.length, page: 1, pageSize: 25 });

type Handlers = {
  roles?: () => Response;
  subsystems?: () => Response;
  users?: () => Response;
  effective?: () => Response;
  preview?: (call: ApiCall) => Response;
  put?: (call: ApiCall) => Response;
  assign?: (call: ApiCall) => Response;
};

const defaultPreview = (count: number) => (call: ApiCall) => {
  void call;
  return json({
    roleId: 'x',
    roleName: 'x',
    activeMemberCount: count,
    currentPermissionCodes: [],
    addedPermissionCodes: [],
    removedPermissionCodes: [],
    sensitiveChangedPermissionCodes: [],
    ceilingViolatedPermissionCodes: [],
  });
};

function api(handlers: Handlers = {}) {
  return stubApi((call) => {
    if (call.method === 'PUT') return handlers.put?.(call) ?? new Response(null, { status: 204 });
    if (call.method === 'POST') return handlers.assign?.(call) ?? new Response(null, { status: 204 });
    if (call.path === '/api/v1/companies') return json(companiesBody);
    if (call.path === '/api/v1/admin/permission-catalog') return json(catalog);
    if (call.path === '/api/v1/admin/access/catalog/subsystems') return handlers.subsystems?.() ?? json(subsystemsBody);
    if (call.path === '/api/v1/admin/companies/a/roles') return handlers.roles?.() ?? json([hrRole, sharedRole, viewerRole, sysRole]);
    if (call.path === '/api/v1/companies/a/users') return handlers.users?.() ?? json(directory([directoryUser()]));
    if (call.path === '/api/v1/admin/companies/a/effective-permissions/user-1') {
      return (
        handlers.effective?.() ??
        json({ userId: 'user-1', companyId: 'a', roleNames: ['کارشناس منابع انسانی'], permissionCodes: ['hr.employees.list', 'hr.employees.view'], permissions: [] })
      );
    }
    if (call.path.endsWith('/permissions/preview')) return (handlers.preview ?? defaultPreview(1))(call);
    return new Response(null, { status: 404 });
  });
}

function setup(options: { can?: CanHint; installationAdmin?: boolean } = {}) {
  const can: CanHint = options.can ?? (() => true);
  const routes: Route[] = [
    {
      path: '/admin/access',
      element: () => (
        <AccessPermissionsPage
          onUnauthenticated={onUnauthenticated}
          defaultCompanyId="a"
          can={can}
          isInstallationAdministrator={options.installationAdmin ?? true}
          onForbidden={onForbidden}
        />
      ),
      title: 'مجوزهای دسترسی',
    },
    { path: '/admin/roles/new', element: () => <h1>ایجاد نقش</h1>, title: 'ایجاد' },
  ];
  window.history.pushState(null, '', '/app/admin/access');
  return render(
    <RouterProvider routes={routes}>
      <PageBreadcrumbProvider fallback={[{ label: 'x' }]}>
        <RouteView />
      </PageBreadcrumbProvider>
    </RouterProvider>,
  );
}

const method = (calls: ApiCall[], name: string) => calls.filter((call) => call.method === name);
const gets = (calls: ApiCall[], path: string) => calls.filter((call) => call.method === 'GET' && call.path === path);
const toggle = (title: string) => screen.getByRole('checkbox', { name: `فعال بودن زیرسیستم ${title}` });
const roleSelect = () => screen.getByRole('combobox', { name: 'نقش' });
const saveButton = () => screen.getByRole('button', { name: 'بررسی و ذخیرهٔ مجوزهای نقش' });

async function chooseRole(user: ReturnType<typeof userEvent.setup>, name: string) {
  await screen.findByRole('option', { name: new RegExp(name) });
  await user.selectOptions(roleSelect(), name);
}

async function pickUser(user: ReturnType<typeof userEvent.setup>) {
  await user.click(screen.getByRole('button', { name: 'جستجو' }));
  await user.click(await screen.findByRole('button', { name: 'انتخاب علی رضایی' }));
}

beforeEach(() => {
  onUnauthenticated.mockClear();
  onForbidden.mockClear();
});

describe('AccessPermissionsPage', () => {
  it('shows the two main tabs and keeps the subsystem selector inside the second tab', async () => {
    const user = userEvent.setup();
    api();
    setup();
    await chooseRole(user, 'کارشناس منابع انسانی');
    expect(screen.getByRole('heading', { level: 1, name: 'مجوزهای دسترسی' })).toBeInTheDocument();
    const tabs = screen.getAllByRole('tab').map((tab) => tab.textContent);
    expect(tabs).toEqual(['زیرسیستم‌ها', 'دسترسی فرم‌ها']);
    expect(screen.getByRole('tab', { name: 'زیرسیستم‌ها' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.queryByRole('combobox', { name: 'زیرسیستم' })).not.toBeInTheDocument();
    await user.click(screen.getByRole('tab', { name: 'دسترسی فرم‌ها' }));
    expect(screen.getByRole('combobox', { name: 'زیرسیستم' })).toBeInTheDocument();
  });

  it('searches the company users, shows membership status and current role, and preselects the user role', async () => {
    const user = userEvent.setup();
    api();
    setup();
    await screen.findByRole('option', { name: /کارشناس منابع انسانی/ });
    await user.type(screen.getByRole('textbox', { name: 'جستجوی کاربر' }), 'علی');
    await pickUser(user);
    const card = await screen.findByRole('group', { name: 'کاربر انتخاب‌شده' });
    expect(within(card).getByText('علی رضایی')).toBeInTheDocument();
    expect(within(card).getByText('ali')).toBeInTheDocument();
    expect(within(card).getByText(/عضویت فعال در شرکت الف/)).toBeInTheDocument();
    expect(within(card).getByText('حساب فعال')).toBeInTheDocument();
    expect(await within(card).findByText(/دسترسی مؤثر: ۲ مجوز از نقش‌های کارشناس منابع انسانی/)).toBeInTheDocument();
    expect(roleSelect()).toHaveValue('role-1');
    expect(toggle('منابع انسانی')).toBeChecked();
  });

  it('treats an inactive membership as no access: warns, requests no effective permissions and blocks assignment', async () => {
    const user = userEvent.setup();
    const { calls } = api({ users: () => json(directory([directoryUser({ membershipIsActive: false })])) });
    setup();
    await screen.findByRole('option', { name: /ناظر/ });
    await pickUser(user);
    const card = await screen.findByRole('group', { name: 'کاربر انتخاب‌شده' });
    expect(within(card).getByText('عضویت غیرفعال', { selector: 'span' })).toBeInTheDocument();
    expect(within(card).getByText(/دسترسی عملیاتی ایجاد نمی‌کند/)).toBeInTheDocument();
    expect(gets(calls, '/api/v1/admin/companies/a/effective-permissions/user-1')).toHaveLength(0);
    await user.selectOptions(roleSelect(), 'ناظر');
    expect(screen.getByRole('button', { name: 'تخصیص این نقش به کاربر' })).toBeDisabled();
    expect(screen.getByText(/عضویت کاربر در این شرکت فعال نیست/)).toBeInTheDocument();
  });

  it('assigns a picked real role only after confirmation, with the company and user only in the URL', async () => {
    const user = userEvent.setup();
    const { calls } = api();
    setup();
    await screen.findByRole('option', { name: /ناظر/ });
    await pickUser(user);
    await screen.findByText(/دسترسی مؤثر: ۲ مجوز/);
    await user.selectOptions(roleSelect(), 'ناظر');
    await user.click(screen.getByRole('button', { name: 'تخصیص این نقش به کاربر' }));
    const dialog = await screen.findByRole('dialog', { name: 'تخصیص نقش به کاربر' });
    expect(method(calls, 'POST')).toHaveLength(0);
    await user.click(within(dialog).getByRole('button', { name: 'تأیید و تخصیص' }));
    await waitFor(() => expect(method(calls, 'POST')).toHaveLength(1));
    const post = method(calls, 'POST')[0]!;
    expect(post.path).toBe('/api/v1/admin/companies/a/roles/role-3/members/user-1');
    expect(post.body).toBeUndefined();
    expect(await screen.findByText(/نقش «ناظر» به علی رضایی داده شد/)).toBeInTheDocument();
    expect(within(screen.getByRole('group', { name: 'کاربر انتخاب‌شده' })).getByText('ناظر')).toBeInTheDocument();
  });

  it('shows the server message and asks for fresh permissions when the assignment is refused (403)', async () => {
    const user = userEvent.setup();
    api({ assign: () => json({ error: 'تخصیص فراتر از سقف مجاز است.' }, 403) });
    setup();
    await screen.findByRole('option', { name: /ناظر/ });
    await pickUser(user);
    await user.selectOptions(roleSelect(), 'ناظر');
    await user.click(screen.getByRole('button', { name: 'تخصیص این نقش به کاربر' }));
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'تأیید و تخصیص' }));
    expect(await screen.findByText('تخصیص فراتر از سقف مجاز است.')).toBeInTheDocument();
    expect(onForbidden).toHaveBeenCalledTimes(1);
  });

  it('offers no assignment for a system role and links to the existing role creation flow', async () => {
    const user = userEvent.setup();
    api();
    setup();
    await screen.findByRole('option', { name: /ناظر/ });
    await pickUser(user);
    await user.selectOptions(roleSelect(), 'role-sys');
    expect(screen.getByRole('button', { name: 'تخصیص این نقش به کاربر' })).toBeDisabled();
    expect(screen.getByText('تخصیص نقش سیستمی مجاز نیست.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'ایجاد نقش اختصاصی' })).toHaveAttribute('href', '/app/admin/roles/new?company=a');
  });

  it('derives the subsystem state strictly from the role permission codes and says so', async () => {
    const user = userEvent.setup();
    api();
    setup();
    await chooseRole(user, 'کارشناس منابع انسانی');
    expect(screen.getByText(/پرچم مستقلی برای «فعال‌بودن زیرسیستم» ندارد/)).toBeInTheDocument();
    expect(toggle('منابع انسانی')).toBeChecked();
    expect(toggle('دسترسی و نقش‌ها')).not.toBeChecked();
    expect(toggle('بازرگانی خارجی')).not.toBeChecked();
    // A subsystem with no real permission code cannot be switched on and says why (nothing is invented).
    expect(toggle('حسابداری')).toBeDisabled();
    expect(screen.getByText('برای این زیرسیستم هنوز مجوزی در کاتالوگ سرور تعریف نشده است.')).toBeInTheDocument();
    const hrRow = toggle('منابع انسانی').closest('tr') as HTMLElement;
    expect(within(hrRow).getByText('فعال')).toBeInTheDocument();
    expect(within(hrRow).getAllByText('۴')[0]).toBeInTheDocument();
  });

  it('warns before disabling a subsystem, removes only its codes, and cancel restores the saved state', async () => {
    const user = userEvent.setup();
    api();
    setup();
    await user.selectOptions(await screen.findByRole('combobox', { name: 'نقش' }), 'مدیر مشترک');
    await user.click(toggle('منابع انسانی'));
    const dialog = await screen.findByRole('alertdialog', { name: 'غیرفعال‌کردن زیرسیستم در نقش' });
    expect(within(dialog).getByText(/۲ مجوز این زیرسیستم از نقش «مدیر مشترک» حذف می‌شود/)).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: 'غیرفعال‌کردن' }));
    expect(toggle('منابع انسانی')).not.toBeChecked();
    expect(toggle('دسترسی و نقش‌ها')).toBeChecked();
    expect(screen.getByText('تغییرات ذخیره‌نشده')).toBeInTheDocument();
    expect(saveButton()).toBeEnabled();
    await user.click(screen.getByRole('button', { name: 'انصراف از تغییرات' }));
    expect(toggle('منابع انسانی')).toBeChecked();
    expect(saveButton()).toBeDisabled();
    expect(screen.queryByText('تغییرات ذخیره‌نشده')).not.toBeInTheDocument();
  });

  it('keeps everything when the disable warning is dismissed', async () => {
    const user = userEvent.setup();
    api();
    setup();
    await chooseRole(user, 'کارشناس منابع انسانی');
    await user.click(toggle('منابع انسانی'));
    await user.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'انصراف' }));
    expect(toggle('منابع انسانی')).toBeChecked();
    expect(saveButton()).toBeDisabled();
  });

  it('enabling a subsystem adds only its view and list codes, and the save sends the full replacement', async () => {
    const user = userEvent.setup();
    const { calls } = api();
    setup();
    await chooseRole(user, 'ناظر');
    await user.click(toggle('منابع انسانی'));
    await user.click(saveButton());
    const dialog = await screen.findByRole('dialog', { name: 'پیش‌نمایش تغییرات نقش' });
    expect(within(dialog).queryByRole('alert')).not.toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: 'تأیید و ذخیره' }));
    await waitFor(() => expect(method(calls, 'PUT')).toHaveLength(1));
    const put = method(calls, 'PUT')[0]!;
    expect(put.path).toBe('/api/v1/admin/companies/a/roles/role-3');
    const body = put.body as { name: string; isActive: boolean; permissionCodes: string[] };
    expect(body.name).toBe('ناظر');
    expect(body.isActive).toBe(true);
    expect([...body.permissionCodes].sort()).toEqual(['hr.employees.list', 'hr.employees.view', 'hr.positions.list', 'roles.view']);
    expect(Object.keys(put.body as object).sort()).toEqual(['isActive', 'name', 'permissionCodes']);
    expect(JSON.stringify(put.body)).not.toMatch(/tenant|companyId/i);
    expect(await screen.findByText(/مجوزهای نقش ذخیره شد/)).toBeInTheDocument();
    await waitFor(() => expect(saveButton()).toBeDisabled());
  });

  it('shows only the real forms and operations of an enabled subsystem and never invents permissions', async () => {
    const user = userEvent.setup();
    api();
    setup();
    await chooseRole(user, 'کارشناس منابع انسانی');
    await user.click(screen.getByRole('tab', { name: 'دسترسی فرم‌ها' }));
    const select = screen.getByRole('combobox', { name: 'زیرسیستم' });
    expect(select).toHaveValue('hr');
    expect(screen.getByRole('option', { name: /بازرگانی خارجی \(غیرفعال\)/ })).toBeDisabled();
    expect(screen.getByRole('option', { name: /دسترسی و نقش‌ها \(غیرفعال\)/ })).toBeDisabled();
    const table = screen.getByRole('table', { name: 'مجوزهای فرم‌های زیرسیستم منابع انسانی' });
    expect(within(table).getAllByRole('columnheader').map((cell) => cell.textContent)).toEqual(['فرم', 'فهرست', 'مشاهده', 'ایجاد']);
    expect(within(table).getByRole('checkbox', { name: 'فهرست کارکنان' })).toBeChecked();
    expect(within(table).getByRole('checkbox', { name: 'مشاهده کارمند' })).toBeChecked();
    expect(within(table).getByRole('checkbox', { name: 'ایجاد کارمند' })).not.toBeChecked();
    expect(within(table).getAllByRole('checkbox')).toHaveLength(4);
    for (const invented of [/حسابدار/, /انبار/, /فروش/]) expect(within(table).queryByText(invented)).not.toBeInTheDocument();
    expect(within(table).getAllByText('تعریف نشده').length).toBeGreaterThan(0);
  });

  it('edits a single operation, marks the draft dirty and saves it', async () => {
    const user = userEvent.setup();
    const { calls } = api();
    setup();
    await chooseRole(user, 'کارشناس منابع انسانی');
    await user.click(screen.getByRole('tab', { name: 'دسترسی فرم‌ها' }));
    await user.click(screen.getByRole('checkbox', { name: 'ایجاد کارمند' }));
    expect(screen.getByText('تغییرات ذخیره‌نشده')).toBeInTheDocument();
    // The draft is shared with the subsystem tab.
    await user.click(screen.getByRole('tab', { name: 'زیرسیستم‌ها' }));
    expect(screen.getByText('تغییرات ذخیره‌نشده')).toBeInTheDocument();
    await user.click(saveButton());
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'تأیید و ذخیره' }));
    await waitFor(() => expect(method(calls, 'PUT')).toHaveLength(1));
    expect(((method(calls, 'PUT')[0]!.body as { permissionCodes: string[] }).permissionCodes).sort()).toEqual(['hr.employees.create', 'hr.employees.list', 'hr.employees.view']);
  });

  it('gives a subsystem without permission codes an honest note instead of a fake form list', async () => {
    const user = userEvent.setup();
    api();
    setup();
    await chooseRole(user, 'کارشناس منابع انسانی');
    await user.click(screen.getByRole('tab', { name: 'دسترسی فرم‌ها' }));
    await user.selectOptions(screen.getByRole('combobox', { name: 'زیرسیستم' }), 'finance');
    expect(screen.getByText(/برای زیرسیستم «حسابداری» هنوز هیچ مجوز فرم یا عملیاتی در کاتالوگ سرور تعریف نشده است/)).toBeInTheDocument();
    expect(screen.queryByRole('table', { name: /حسابداری/ })).not.toBeInTheDocument();
  });

  it('warns about a shared role with the affected user count and needs an explicit confirmation before saving', async () => {
    const user = userEvent.setup();
    const { calls } = api({ preview: defaultPreview(3) });
    setup();
    await chooseRole(user, 'مدیر مشترک');
    expect(screen.getByText('۳ عضو فعال')).toBeInTheDocument();
    await user.click(screen.getByRole('tab', { name: 'دسترسی فرم‌ها' }));
    await user.selectOptions(screen.getByRole('combobox', { name: 'زیرسیستم' }), 'hr');
    await user.click(screen.getByRole('checkbox', { name: 'ایجاد کارمند' }));
    await user.click(saveButton());
    const dialog = await screen.findByRole('alertdialog', { name: 'تغییر نقش مشترک' });
    expect(within(dialog).getByText(/نقش «مدیر مشترک» مشترک است: این تغییر بلافاصله دسترسی ۳ کاربر فعال را تغییر می‌دهد/)).toBeInTheDocument();
    expect(within(dialog).getByText(/ایجاد کارمند/)).toBeInTheDocument();
    expect(gets(calls, '/api/v1/admin/companies/a/roles/role-2/permissions/preview')).toHaveLength(1);
    expect(method(calls, 'PUT')).toHaveLength(0);
    // Backing out sends nothing and keeps the draft.
    await user.click(within(dialog).getByRole('button', { name: 'بازگشت به ویرایش' }));
    expect(method(calls, 'PUT')).toHaveLength(0);
    expect(screen.getByRole('checkbox', { name: 'ایجاد کارمند' })).toBeChecked();
    await user.click(saveButton());
    await user.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'تأیید و اعمال برای ۳ کاربر' }));
    await waitFor(() => expect(method(calls, 'PUT')).toHaveLength(1));
  });

  it('does not show the shared-role warning for a role with a single active member', async () => {
    const user = userEvent.setup();
    api({ preview: defaultPreview(1) });
    setup();
    await chooseRole(user, 'کارشناس منابع انسانی');
    await user.click(screen.getByRole('tab', { name: 'دسترسی فرم‌ها' }));
    await user.click(screen.getByRole('checkbox', { name: 'ایجاد کارمند' }));
    await user.click(saveButton());
    const dialog = await screen.findByRole('dialog', { name: 'پیش‌نمایش تغییرات نقش' });
    expect(within(dialog).queryByText(/مشترک است/)).not.toBeInTheDocument();
    expect(within(dialog).getByText(/اعضای فعال تحت تأثیر: ۱ نفر/)).toBeInTheDocument();
  });

  it('falls back to the listed member count when the advisory preview fails (500) and still requires confirmation', async () => {
    const user = userEvent.setup();
    const { calls } = api({ preview: () => json({ error: 'x' }, 500) });
    setup();
    await chooseRole(user, 'مدیر مشترک');
    await user.click(screen.getByRole('tab', { name: 'دسترسی فرم‌ها' }));
    await user.selectOptions(screen.getByRole('combobox', { name: 'زیرسیستم' }), 'hr');
    await user.click(screen.getByRole('checkbox', { name: 'ایجاد کارمند' }));
    await user.click(saveButton());
    const dialog = await screen.findByRole('alertdialog', { name: 'تغییر نقش مشترک' });
    expect(within(dialog).getByText(/دسترسی ۳ کاربر فعال/)).toBeInTheDocument();
    expect(method(calls, 'PUT')).toHaveLength(0);
  });

  it('shows the sensitive and ceiling findings of the server preview', async () => {
    const user = userEvent.setup();
    api({
      preview: () =>
        json({
          roleId: 'role-1',
          roleName: 'x',
          activeMemberCount: 1,
          currentPermissionCodes: [],
          addedPermissionCodes: ['hr.employees.create'],
          removedPermissionCodes: [],
          sensitiveChangedPermissionCodes: ['hr.employees.create'],
          ceilingViolatedPermissionCodes: ['hr.employees.create'],
        }),
    });
    setup();
    await chooseRole(user, 'کارشناس منابع انسانی');
    await user.click(screen.getByRole('tab', { name: 'دسترسی فرم‌ها' }));
    await user.click(screen.getByRole('checkbox', { name: 'ایجاد کارمند' }));
    await user.click(saveButton());
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText(/مجوزهای حساس تغییرکرده: ایجاد کارمند/)).toBeInTheDocument();
    expect(within(dialog).getByText(/خارج از سقف تفویض شما است/)).toBeInTheDocument();
  });

  it('shows a system role read-only with the reason and without any save control', async () => {
    const user = userEvent.setup();
    const { calls } = api();
    setup();
    await user.selectOptions(await screen.findByRole('combobox', { name: 'نقش' }), 'role-sys');
    expect(screen.getByText(/این نقش سیستمی است و قابل ویرایش نیست/)).toBeInTheDocument();
    expect(toggle('منابع انسانی')).toBeChecked();
    expect(toggle('منابع انسانی')).toBeDisabled();
    expect(screen.queryByRole('button', { name: 'بررسی و ذخیرهٔ مجوزهای نقش' })).not.toBeInTheDocument();
    await user.click(screen.getByRole('tab', { name: 'دسترسی فرم‌ها' }));
    for (const box of within(screen.getByRole('table')).getAllByRole('checkbox')) expect(box).toBeDisabled();
    expect(method(calls, 'PUT')).toHaveLength(0);
  });

  it('is read-only without the update hint and for a company manager whose role exceeds their ceiling', async () => {
    const user = userEvent.setup();
    api();
    const { unmount } = setup({ can: (permission) => permission !== 'admin.roles.update' });
    await chooseRole(user, 'کارشناس منابع انسانی');
    expect(screen.getByText(/مجوز ویرایش نقش‌ها را ندارید/)).toBeInTheDocument();
    expect(toggle('منابع انسانی')).toBeDisabled();
    unmount();

    // The catalog of a company manager lacks `hr.employees.view`: the role holds a code beyond their ceiling.
    stubApi((call) => {
      if (call.path === '/api/v1/companies') return json(companiesBody);
      if (call.path === '/api/v1/admin/permission-catalog') return json(catalog.filter((item) => item.code !== 'hr.employees.view'));
      if (call.path === '/api/v1/admin/access/catalog/subsystems') return json({ error: 'فقط مدیر نصب' }, 403);
      if (call.path === '/api/v1/admin/companies/a/roles') return json([hrRole]);
      return new Response(null, { status: 404 });
    });
    setup({ installationAdmin: false });
    await user.selectOptions(await screen.findByRole('combobox', { name: 'نقش' }), 'role-1');
    expect(screen.getByText(/خارج از سقف مجوزهای شما است/)).toBeInTheDocument();
    expect(screen.getByText(/۱ مجوز این نقش در فهرست قابل‌نمایش شما نیست/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'بررسی و ذخیرهٔ مجوزهای نقش' })).not.toBeInTheDocument();
  });

  it('degrades honestly when the subsystem catalog answers 403: limited list from real codes, no fake titles', async () => {
    const user = userEvent.setup();
    api({ subsystems: () => json({ error: 'فقط مدیر نصب سامانه مجاز به این عملیات است.' }, 403) });
    setup();
    await chooseRole(user, 'کارشناس منابع انسانی');
    expect(await screen.findByText(/کاتالوگ زیرسیستم‌ها فقط برای مدیر نصب در دسترس است/)).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: 'فعال بودن زیرسیستم hr' })).toBeChecked();
    expect(screen.getByRole('checkbox', { name: 'فعال بودن زیرسیستم TRADE_IMPORT' })).not.toBeChecked();
    expect(screen.queryByRole('checkbox', { name: /حسابداری/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'تلاش دوباره' })).not.toBeInTheDocument();
  });

  it('shows the server message on a save refusal (403), refreshes the hints and keeps the draft', async () => {
    const user = userEvent.setup();
    api({ put: () => json({ error: 'مجوز ویرایش نقش را ندارید.' }, 403) });
    setup();
    await chooseRole(user, 'کارشناس منابع انسانی');
    await user.click(screen.getByRole('tab', { name: 'دسترسی فرم‌ها' }));
    await user.click(screen.getByRole('checkbox', { name: 'ایجاد کارمند' }));
    await user.click(saveButton());
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'تأیید و ذخیره' }));
    expect(await screen.findByText('مجوز ویرایش نقش را ندارید.')).toBeInTheDocument();
    expect(onForbidden).toHaveBeenCalledTimes(1);
    expect(screen.getByRole('checkbox', { name: 'ایجاد کارمند' })).toBeChecked();
  });

  it('shows the concurrency conflict (409) and reloads the real role on request, dropping the draft', async () => {
    const user = userEvent.setup();
    let reloads = 0;
    const { calls } = api({
      put: () => json({ error: 'نقش هم‌زمان تغییر کرده است؛ اطلاعات را دوباره دریافت کنید.' }, 409),
      roles: () => {
        reloads += 1;
        return json([reloads > 1 ? { ...hrRole, permissionCodes: ['hr.employees.list'] } : hrRole]);
      },
    });
    setup();
    await chooseRole(user, 'کارشناس منابع انسانی');
    await user.click(screen.getByRole('tab', { name: 'دسترسی فرم‌ها' }));
    await user.click(screen.getByRole('checkbox', { name: 'ایجاد کارمند' }));
    await user.click(saveButton());
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'تأیید و ذخیره' }));
    expect(await screen.findByText('نقش هم‌زمان تغییر کرده است؛ اطلاعات را دوباره دریافت کنید.')).toBeInTheDocument();
    expect(gets(calls, '/api/v1/admin/companies/a/roles')).toHaveLength(1);
    await user.click(screen.getByRole('button', { name: /دریافت آخرین اطلاعات/ }));
    await waitFor(() => expect(gets(calls, '/api/v1/admin/companies/a/roles')).toHaveLength(2));
    await user.click(await screen.findByRole('tab', { name: 'دسترسی فرم‌ها' }));
    await waitFor(() => expect(screen.getByRole('checkbox', { name: 'مشاهده کارمند' })).not.toBeChecked());
    expect(screen.getByRole('checkbox', { name: 'ایجاد کارمند' })).not.toBeChecked();
    expect(screen.queryByText('نقش هم‌زمان تغییر کرده است؛ اطلاعات را دوباره دریافت کنید.')).not.toBeInTheDocument();
  });

  it('stops at the preview when the server refuses it (403) and never sends the save', async () => {
    const user = userEvent.setup();
    const { calls } = api({ preview: () => json({ error: 'فقط با مجوز ویرایش نقش.' }, 403) });
    setup();
    await chooseRole(user, 'کارشناس منابع انسانی');
    await user.click(screen.getByRole('tab', { name: 'دسترسی فرم‌ها' }));
    await user.click(screen.getByRole('checkbox', { name: 'ایجاد کارمند' }));
    await user.click(saveButton());
    expect(await screen.findByText('فقط با مجوز ویرایش نقش.')).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(method(calls, 'PUT')).toHaveLength(0);
    expect(onForbidden).toHaveBeenCalledTimes(1);
  });

  it('explains a 403 on the roles list and a 403 on the user search without breaking the page', async () => {
    const user = userEvent.setup();
    api({ roles: () => json({ error: 'x' }, 403), users: () => json({ error: 'x' }, 403) });
    setup();
    expect(await screen.findByText('شما مجوز مشاهدهٔ نقش‌های این شرکت را ندارید.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'جستجو' }));
    expect(await screen.findByText(/به مجوز «مشاهده کاربران شرکت» نیاز است/)).toBeInTheDocument();
    expect(screen.queryByRole('tab')).not.toBeInTheDocument();
  });

  it('guards the unsaved draft when another role or company is chosen, and lets the user stay', async () => {
    const user = userEvent.setup();
    const { calls } = api();
    setup();
    await chooseRole(user, 'کارشناس منابع انسانی');
    await user.click(screen.getByRole('tab', { name: 'دسترسی فرم‌ها' }));
    await user.click(screen.getByRole('checkbox', { name: 'ایجاد کارمند' }));
    await user.selectOptions(roleSelect(), 'ناظر');
    const dialog = await screen.findByRole('alertdialog', { name: 'تغییرات ذخیره‌نشده' });
    await user.click(within(dialog).getByRole('button', { name: 'ماندن و ادامه ویرایش' }));
    expect(roleSelect()).toHaveValue('role-1');
    expect(screen.getByRole('checkbox', { name: 'ایجاد کارمند' })).toBeChecked();
    await user.selectOptions(screen.getByRole('combobox', { name: 'شرکت' }), 'b');
    await user.click(within(await screen.findByRole('alertdialog', { name: 'تغییرات ذخیره‌نشده' })).getByRole('button', { name: 'ماندن و ادامه ویرایش' }));
    expect(gets(calls, '/api/v1/admin/companies/b/roles')).toHaveLength(0);
    // Leaving on purpose drops the draft.
    await user.selectOptions(roleSelect(), 'ناظر');
    await user.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'ادامه بدون ذخیره' }));
    expect(roleSelect()).toHaveValue('role-3');
    expect(screen.queryByText('تغییرات ذخیره‌نشده')).not.toBeInTheDocument();
  });

  it('loads another company by id in the URL only and drops the chosen user and role', async () => {
    const user = userEvent.setup();
    const { calls } = api();
    setup();
    await screen.findByRole('option', { name: /ناظر/ });
    await pickUser(user);
    await user.selectOptions(roleSelect(), 'ناظر');
    await user.selectOptions(screen.getByRole('combobox', { name: 'شرکت' }), 'b');
    await waitFor(() => expect(gets(calls, '/api/v1/admin/companies/b/roles')).toHaveLength(1));
    expect(window.location.search).toBe('?company=b');
    expect(screen.queryByRole('group', { name: 'کاربر انتخاب‌شده' })).not.toBeInTheDocument();
    for (const call of calls) expect(JSON.stringify(call.body ?? '')).not.toMatch(/companyId|tenant/i);
  });

  it('does not issue duplicate requests: one load per source and a single preview and save for a double click', async () => {
    const user = userEvent.setup();
    const { calls } = api();
    setup();
    await chooseRole(user, 'کارشناس منابع انسانی');
    expect(gets(calls, '/api/v1/admin/permission-catalog')).toHaveLength(1);
    expect(gets(calls, '/api/v1/admin/access/catalog/subsystems')).toHaveLength(1);
    expect(gets(calls, '/api/v1/admin/companies/a/roles')).toHaveLength(1);
    expect(gets(calls, '/api/v1/companies')).toHaveLength(1);
    await user.click(screen.getByRole('tab', { name: 'دسترسی فرم‌ها' }));
    await user.click(screen.getByRole('tab', { name: 'زیرسیستم‌ها' }));
    expect(gets(calls, '/api/v1/admin/permission-catalog')).toHaveLength(1);
    await user.click(toggle('منابع انسانی'));
    await user.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'غیرفعال‌کردن' }));
    await user.dblClick(saveButton());
    const dialog = await screen.findByRole('dialog');
    expect(gets(calls, '/api/v1/admin/companies/a/roles/role-1/permissions/preview')).toHaveLength(1);
    await user.dblClick(within(dialog).getByRole('button', { name: /تأیید/ }));
    await waitFor(() => expect(method(calls, 'PUT')).toHaveLength(1));
    expect(method(calls, 'PUT')).toHaveLength(1);
  });
});
