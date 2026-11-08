import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { RouteView, RouterProvider, type Route } from '../../../lib/router';
import { json, stubApi, type ApiCall } from '../../../test/adminUsers';
import { catalogItems, companiesBody, templates } from '../../../test/roles';
import { clearRoleFlash, peekRoleFlash } from './roleFlash';
import { RoleCreatePage } from './RoleCreatePage';

const onUnauthenticated = vi.fn((): boolean => false);
const routes: Route[] = [
  { path: '/admin/roles', element: () => <h1>فهرست گروه‌ها</h1>, title: 'فهرست' },
  { path: '/admin/roles/new', element: () => <RoleCreatePage onUnauthenticated={onUnauthenticated} defaultCompanyId="a" />, title: 'ایجاد' },
  { path: '/admin/roles/:roleId', element: () => <h1>جزئیات گروه</h1>, title: 'جزئیات' },
];

function setup(search = '?company=a') {
  window.history.pushState(null, '', `/app/admin/roles/new${search}`);
  return render(
    <RouterProvider routes={routes}>
      <RouteView />
    </RouterProvider>,
  );
}

const posts = (calls: ApiCall[]) => calls.filter((call) => call.method === 'POST');
const code = () => screen.getByLabelText(/^کد گروه/);
const title = () => screen.getByLabelText(/^عنوان گروه/);
const template = () => screen.getByLabelText(/^الگوی مجوز/);
const submit = () => screen.getByRole('button', { name: 'ایجاد گروه دسترسی' });
const leaf = (name: string) => screen.getByRole('checkbox', { name });

function api(
  onPost: (call: ApiCall) => Response | Promise<Response> = () => json({ id: 'new-id' }, 201),
  catalog: Response = json(catalogItems),
  roles: Response | null = null,
) {
  return stubApi((call) => {
    if (call.method === 'POST') return onPost(call);
    if (call.path === '/api/v1/companies') return json(companiesBody);
    if (call.path === '/api/v1/admin/permission-catalog') return catalog.clone();
    if (call.path === '/api/v1/admin/permission-templates') return json(templates);
    if (call.path === '/api/v1/admin/companies/a/roles') return roles ?? new Response(null, { status: 404 });
    return new Response(null, { status: 404 });
  });
}

async function ready() {
  await screen.findByRole('form', { name: 'فرم ایجاد گروه دسترسی' });
  await waitFor(() => expect(template()).toBeEnabled());
}

describe('RoleCreatePage', () => {
  beforeEach(() => {
    onUnauthenticated.mockReset();
    onUnauthenticated.mockReturnValue(false);
    clearRoleFlash();
  });

  it('renders the form with the company, labelled fields and the permission tree', async () => {
    api();
    setup();
    await ready();
    expect(screen.getByRole('heading', { level: 1, name: 'ایجاد گروه دسترسی' })).toBeInTheDocument();
    expect(await screen.findByText(/شرکت «شرکت الف»/)).toBeInTheDocument();
    expect(code()).toHaveAttribute('dir', 'ltr');
    expect(screen.getByRole('heading', { level: 2, name: 'مجوزها' })).toBeInTheDocument();
    expect(screen.getByRole('group', { name: 'مجوزهای گروه' })).toBeInTheDocument();
    expect(within(template()).getAllByRole('option').map((option) => option.textContent)).toEqual(['بدون الگو', 'بازرگانی — مشاهده', 'بازرگانی — مدیر']);
  });

  it('validates the code and title without sending anything and focuses the first invalid field', async () => {
    const user = userEvent.setup();
    const { calls } = api();
    setup();
    await ready();
    await user.click(submit());
    expect(await screen.findByText('کد گروه الزامی است.')).toBeInTheDocument();
    expect(screen.getByText('عنوان گروه الزامی است.')).toBeInTheDocument();
    expect(code()).toHaveFocus();
    await user.type(code(), 'a b');
    await user.tab();
    expect(screen.getByText(/کد باید ۲ تا ۸۰ نویسه/)).toBeInTheDocument();
    await user.clear(code());
    await user.type(code(), 'x');
    await user.tab();
    expect(screen.getByText(/کد باید ۲ تا ۸۰ نویسه/)).toBeInTheDocument();
    expect(posts(calls)).toHaveLength(0);
  });

  it('upper-cases the code on blur and sends the exact body, then opens the new role', async () => {
    const user = userEvent.setup();
    const { calls } = api();
    setup();
    await ready();
    await user.type(code(), ' sales.rep ');
    await user.tab();
    expect(code()).toHaveValue('SALES.REP');
    await user.type(title(), '  کارشناس   فروش ');
    await user.click(leaf('ایجاد سفارش'));
    await user.click(leaf('مشاهدهٔ سفارش‌ها'));
    await user.click(submit());
    await waitFor(() => expect(window.location.pathname).toBe('/app/admin/roles/new-id'));
    expect(window.location.search).toBe('?company=a');
    expect(posts(calls)).toHaveLength(1);
    expect(posts(calls)[0]).toMatchObject({
      path: '/api/v1/admin/companies/a/roles',
      body: { code: 'SALES.REP', name: 'کارشناس فروش', permissionCodes: ['trade.orders.create', 'trade.orders.view'] },
    });
    expect(Object.keys(posts(calls)[0]?.body as object).sort()).toEqual(['code', 'name', 'permissionCodes']);
    expect(posts(calls)[0]?.headers['X-XSRF-TOKEN']).toBe('tok');
    expect(peekRoleFlash('new-id')).toBe('created');
  });

  it('applies a template directly while the selection is untouched, and asks before replacing edits', async () => {
    const user = userEvent.setup();
    api();
    setup();
    await ready();
    await user.selectOptions(template(), 'trade.viewer');
    expect(leaf('مشاهدهٔ سفارش‌ها')).toBeChecked();
    expect(leaf('گزارش شرکتی')).toBeChecked();
    expect(leaf('ایجاد سفارش')).not.toBeChecked();
    expect(screen.queryByRole('dialog')).toBeNull();

    await user.click(leaf('ایجاد سفارش'));
    await user.selectOptions(template(), 'trade.manager');
    const dialog = await screen.findByRole('dialog', { name: 'جایگزینی مجوزهای انتخاب‌شده' });
    expect(dialog).toHaveTextContent('بازرگانی — مدیر');
    // Cancel keeps the user's selection and the previous template.
    await user.click(within(dialog).getByRole('button', { name: 'نگه‌داشتن انتخاب فعلی' }));
    expect(leaf('ایجاد سفارش')).toBeChecked();
    expect(leaf('حذف سفارش')).not.toBeChecked();
    // The selection no longer equals the template, so the select does not claim it.
    expect(template()).toHaveValue('');

    await user.selectOptions(template(), 'trade.manager');
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'جایگزین کن' }));
    expect(leaf('حذف سفارش')).toBeChecked();
    expect(leaf('گزارش شرکتی')).not.toBeChecked();
    expect(template()).toHaveValue('trade.manager');
  });

  it('prefills the form from a copy source and skips codes outside the actor catalog', async () => {
    const user = userEvent.setup();
    const source = {
      id: 'role-src',
      companyId: 'a',
      code: 'HR',
      name: 'منابع انسانی',
      isSystemRole: false,
      isActive: true,
      permissionCount: 3,
      activeMemberCount: 2,
      permissionCodes: ['trade.orders.view', 'trade.orders.create', 'hr.employees.list'],
    };
    const { calls } = api(undefined, json(catalogItems), json([source]));
    setup('?company=a&copyFrom=role-src');
    await ready();

    // The prefill is applied only after the copy-source fetch resolves; its completion notice
    // is the reliable readiness signal (asserting the values before it raced on slow runners).
    const notice = await screen.findByText(/کپی از گروه «منابع انسانی»/);

    // Code and title are prefilled as an editable starting point.
    expect(code()).toHaveValue('HR-COPY');
    expect(title()).toHaveValue('کپی منابع انسانی');
    expect(leaf('مشاهدهٔ سفارش‌ها')).toBeChecked();
    expect(leaf('ایجاد سفارش')).toBeChecked();
    // hr.employees.list is not in this actor's catalog: left out and reported, never smuggled.
    expect(screen.queryByRole('checkbox', { name: 'فهرست کارکنان شرکت' })).toBeNull();
    expect(notice).toHaveTextContent('۱ مجوز خارج از کاتالوگ/سقف شما در این کپی لحاظ نشد');

    // The creation still goes through the normal guarded POST with the carried codes only.
    await user.click(submit());
    await waitFor(() => expect(posts(calls)).toHaveLength(1));
    expect(posts(calls)[0]?.body).toMatchObject({ code: 'HR-COPY', name: 'کپی منابع انسانی' });
    expect((posts(calls)[0]?.body as { permissionCodes: string[] }).permissionCodes.sort()).toEqual(['trade.orders.create', 'trade.orders.view'].sort());
  });

  it('reports a missing copy source honestly instead of prefilling anything', async () => {
    api(undefined, json(catalogItems), json([]));
    setup('?company=a&copyFrom=missing-role');
    await ready();
    expect(await screen.findByText(/بارگذاری اطلاعات گروه مبدأ برای کپی ممکن نشد/)).toBeInTheDocument();
    expect(code()).toHaveValue('');
    expect(title()).toHaveValue('');
  });

  it('never prefills from a system role, even through a direct copyFrom URL', async () => {
    const systemSource = {
      id: 'role-sys',
      companyId: 'a',
      code: 'company-admin',
      name: 'مدیر شرکت',
      isSystemRole: true,
      isActive: true,
      permissionCount: 2,
      activeMemberCount: 1,
      permissionCodes: ['trade.orders.view', 'trade.orders.create'],
    };
    api(undefined, json(catalogItems), json([systemSource]));
    setup('?company=a&copyFrom=role-sys');
    await ready();
    expect(await screen.findByText(/گروه سیستمی قابل کپی نیست/)).toBeInTheDocument();
    expect(code()).toHaveValue('');
    expect(title()).toHaveValue('');
    expect(leaf('مشاهدهٔ سفارش‌ها')).not.toBeChecked();
  });

  it('sends the template selection as permissionCodes (the server prefers explicit codes)', async () => {
    const user = userEvent.setup();
    const { calls } = api();
    setup();
    await ready();
    await user.type(code(), 'VIEWER');
    await user.type(title(), 'مشاهده‌گر');
    await user.selectOptions(template(), 'trade.viewer');
    await user.click(submit());
    await waitFor(() => expect(posts(calls)).toHaveLength(1));
    expect(posts(calls)[0]?.body).toEqual({ code: 'VIEWER', name: 'مشاهده‌گر', permissionCodes: ['trade.orders.view', 'trade.reports.company.view'] });
  });

  it('shows a duplicate code (409) at the code field and a 400 in the message box', async () => {
    const user = userEvent.setup();
    let status = 409;
    api(() => json({ error: status === 409 ? 'کد گروه دسترسی در این شرکت تکراری است.' : 'الگوی انتخاب‌شده نامعتبر است یا مجوزی در کاتالوگ وجود ندارد.' }, status));
    setup();
    await ready();
    await user.type(code(), 'SALES');
    await user.type(title(), 'فروش');
    await user.click(submit());
    expect(await screen.findByText('کد گروه دسترسی در این شرکت تکراری است.')).toBeInTheDocument();
    expect(code()).toHaveAttribute('aria-invalid', 'true');
    expect(code()).toHaveFocus();
    expect(window.location.pathname).toBe('/app/admin/roles/new');
    status = 400;
    await user.type(code(), '2');
    await user.click(submit());
    expect(await screen.findByRole('alert')).toHaveTextContent('الگوی انتخاب‌شده نامعتبر است');
  });

  it('sends one request for a double submit and locks the tree meanwhile', async () => {
    const user = userEvent.setup();
    let release: (response: Response) => void = () => undefined;
    const { calls } = api(() => new Promise<Response>((resolve) => { release = resolve; }));
    setup();
    await ready();
    await user.type(code(), 'SALES');
    await user.type(title(), 'فروش');
    await user.dblClick(submit());
    await waitFor(() => expect(posts(calls)).toHaveLength(1));
    expect(screen.getByRole('button', { name: 'در حال ذخیره…' })).toHaveAttribute('aria-busy', 'true');
    for (const box of screen.getAllByRole('checkbox')) expect(box).toBeDisabled();
    release(json({ id: 'new-id' }, 201));
    await waitFor(() => expect(window.location.pathname).toBe('/app/admin/roles/new-id'));
    expect(posts(calls)).toHaveLength(1);
  });

  it('guards unsaved changes on cancel and on page unload', async () => {
    const user = userEvent.setup();
    api();
    setup();
    await ready();
    const clean = new Event('beforeunload', { cancelable: true });
    window.dispatchEvent(clean);
    expect(clean.defaultPrevented).toBe(false);

    await user.type(title(), 'چیزی');
    const dirty = new Event('beforeunload', { cancelable: true });
    window.dispatchEvent(dirty);
    expect(dirty.defaultPrevented).toBe(true);

    await user.click(screen.getByRole('button', { name: 'انصراف' }));
    const dialog = await screen.findByRole('alertdialog', { name: 'تغییرات ذخیره‌نشده' });
    await user.click(within(dialog).getByRole('button', { name: 'ادامه ویرایش' }));
    expect(window.location.pathname).toBe('/app/admin/roles/new');
    await user.click(screen.getByRole('button', { name: 'انصراف' }));
    await user.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'خروج بدون ذخیره' }));
    expect(window.location.pathname).toBe('/app/admin/roles');
    expect(window.location.search).toBe('?company=a');
  });

  it('resets the template select when the selection diverges from the template', async () => {
    const user = userEvent.setup();
    api();
    setup();
    await ready();
    await user.selectOptions(template(), 'trade.viewer');
    expect(template()).toHaveValue('trade.viewer');
    await user.click(leaf('ایجاد سفارش'));
    expect(template()).toHaveValue('');
    await user.click(leaf('ایجاد سفارش'));
    expect(template()).toHaveValue('trade.viewer');
  });

  it('explains up front when ?company= is not among the user companies and offers the selector', async () => {
    const user = userEvent.setup();
    const { calls } = api();
    setup('?company=zzz');
    expect(await screen.findByRole('alert')).toHaveTextContent('شرکت انتخاب‌شده در فهرست شرکت‌های عضو شما نیست');
    expect(screen.queryByRole('form', { name: 'فرم ایجاد گروه دسترسی' })).toBeNull();
    await user.selectOptions(screen.getByLabelText('شرکت', { exact: true }), 'b');
    await screen.findByRole('form', { name: 'فرم ایجاد گروه دسترسی' });
    expect(window.location.search).toBe('?company=b');
    expect(posts(calls)).toHaveLength(0);
  });

  it('shows the D-071 delegation-ceiling note to a company administrator', async () => {
    api();
    setup();
    await ready();
    expect(screen.getByText(/سقف تفویض \(D-071\)/)).toBeInTheDocument();
    expect(screen.queryByText(/به‌عنوان مدیر نصب سقف تفویض ندارید/)).not.toBeInTheDocument();
  });

  it('leaves a pristine form without asking', async () => {
    const user = userEvent.setup();
    api();
    setup();
    await ready();
    await user.click(screen.getByRole('button', { name: 'انصراف' }));
    expect(window.location.pathname).toBe('/app/admin/roles');
  });

  it('falls back to the work-context company without ?company= and tolerates failing templates', async () => {
    stubApi((call) => {
      if (call.path === '/api/v1/admin/permission-catalog') return json(catalogItems);
      return new Response(null, { status: call.method === 'POST' ? 201 : 500 });
    });
    setup('');
    await screen.findByRole('form', { name: 'فرم ایجاد گروه دسترسی' });
    expect(await screen.findByText(/دریافت الگوها انجام نشد/)).toBeInTheDocument();
    expect(template()).toBeDisabled();
    expect(screen.getByRole('link', { name: 'بازگشت به فهرست' })).toHaveAttribute('href', '/app/admin/roles?company=a');
  });

  it('shows the catalog error with retry and the forbidden message without it', async () => {
    const user = userEvent.setup();
    let fail = true;
    stubApi((call) => {
      if (call.path === '/api/v1/admin/permission-catalog') return fail ? json({ error: 'x' }, 500) : json(catalogItems);
      if (call.path === '/api/v1/admin/permission-templates') return json(templates);
      return json(companiesBody);
    });
    const view = setup();
    await screen.findByText('خطای غیرمنتظره‌ای در سرور رخ داد. لطفاً دوباره تلاش کنید.');
    fail = false;
    await user.click(screen.getByRole('button', { name: 'تلاش دوباره' }));
    await screen.findByRole('form', { name: 'فرم ایجاد گروه دسترسی' });
    view.unmount();

    api(undefined, new Response(null, { status: 403 }));
    setup();
    expect(await screen.findByRole('alert')).toHaveTextContent('شما مجوز ایجاد گروه دسترسی را ندارید.');
    expect(screen.queryByRole('button', { name: 'تلاش دوباره' })).toBeNull();
  });
});
