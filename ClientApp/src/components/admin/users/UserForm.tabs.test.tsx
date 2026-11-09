import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent, { type UserEvent } from '@testing-library/user-event';
import { useState } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { RouteView, RouterProvider, type Route } from '../../../lib/router';
import type { Permission } from '../../../lib/permissions';
import { PageBreadcrumbProvider } from '@/components/shell/PageBreadcrumb';
import { employeeDetail, employeeId, employeeListItem, employeeListResponse, managerEmployeeId } from '@/features/hr/test/hrFixtures';
import { json, stubApi, userDetails, type ApiCall } from '../../../test/adminUsers';
import { roleItem } from '../../../test/roles';
import { UserCreatePage } from './UserCreatePage';
import { UserDetailPage } from './UserDetailPage';
import { UserEditPage } from './UserEditPage';
import { accessManagementPath, type UserFormContext } from './form/userFormShared';
import { clearUserFlash } from './userFlash';

const onUnauthenticated = vi.fn((): boolean => false);

const context = (...allowed: Permission[]): UserFormContext => ({
  activeCompanyId: 'c1',
  activeCompanyName: 'شرکت نمونه',
  can: (permission) => allowed.includes(permission),
});

function setup(path: string, workContext: UserFormContext) {
  const routes: Route[] = [
    { path: '/admin/users', element: () => <h1>فهرست</h1>, title: 'فهرست' },
    { path: '/admin/users/new', element: () => <UserCreatePage onUnauthenticated={onUnauthenticated} workContext={workContext} />, title: 'ایجاد کاربر' },
    { path: '/admin/users/:id', element: () => <UserDetailPage onUnauthenticated={onUnauthenticated} />, title: 'جزئیات' },
    { path: '/admin/users/:id/edit', element: () => <UserEditPage onUnauthenticated={onUnauthenticated} workContext={workContext} />, title: 'ویرایش' },
    { path: '/admin/access', element: () => <h1>مجوزهای دسترسی</h1>, title: 'مجوزهای دسترسی' },
  ];
  window.history.pushState(null, '', `/app${path}`);
  return render(
    <RouterProvider routes={routes}>
      <PageBreadcrumbProvider fallback={[{ label: 'فرم' }]}>
        <RouteView />
      </PageBreadcrumbProvider>
    </RouterProvider>,
  );
}

const tab = (name: RegExp) => screen.getByRole('tab', { name });
const field = {
  firstName: () => screen.getByLabelText(/^نام\s*\*?$/),
  lastName: () => screen.getByLabelText(/^نام خانوادگی\s*\*?$/),
  username: () => screen.getByLabelText(/^نام کاربری/),
  password: () => screen.getByLabelText(/^رمز عبور/),
  confirm: () => screen.getByLabelText(/^تکرار رمز عبور/),
  employeeNumber: () => screen.getByLabelText(/^شماره پرسنلی$/),
  email: () => screen.getByLabelText(/^ایمیل/),
};
const createButton = () => screen.getByRole('button', { name: 'ایجاد کاربر' });
const countOf = (calls: ApiCall[], method: string, path: string) => calls.filter((call) => call.method === method && call.path === path).length;

const nextNumberPath = '/api/v1/companies/c1/org/employees/next-number';
const employeesPath = '/api/v1/companies/c1/org/employees';

async function fillCreate(user: UserEvent) {
  await user.type(field.firstName(), 'علی');
  await user.type(field.lastName(), 'رضایی');
  await user.type(field.username(), 'ali.r');
  await user.type(field.password(), 'Str0ng-pass-phrase!');
  await user.type(field.confirm(), 'Str0ng-pass-phrase!');
  await user.click(tab(/^اطلاعات تماس/));
  await user.type(field.email(), 'ali@example.test');
  await user.click(tab(/^اطلاعات اصلی/));
}

describe('UserForm create — personnel number and the employee step', () => {
  beforeEach(() => {
    onUnauthenticated.mockReset();
    onUnauthenticated.mockReturnValue(false);
    clearUserFlash();
    window.localStorage.clear();
    window.sessionStorage.clear();
  });

  /** Handler for the create workflow; `onEmployee` answers `POST .../org/employees`. */
  function createApi(onEmployee: (call: ApiCall) => Response, suggestions: string[] = ['0042']) {
    let served = 0;
    return stubApi((call) => {
      if (call.method === 'GET' && call.path === nextNumberPath) {
        const value = suggestions[Math.min(served, suggestions.length - 1)];
        served += 1;
        return json({ suggestedEmployeeNumber: value });
      }
      if (call.method === 'POST' && call.path === '/api/v1/admin/users') return json({ id: 'new-1' }, 201);
      if (call.method === 'POST' && call.path === employeesPath) return onEmployee(call);
      if (call.method === 'GET' && call.path === employeesPath) return json(employeeListResponse([]));
      if (call.method === 'GET' && call.path === '/api/v1/admin/users/new-1') return json(userDetails(2, { id: 'new-1' }));
      return json({}, 404);
    });
  }

  it('suggests the next number for the work-context company once, and the suggestion alone is not a change', async () => {
    const user = userEvent.setup();
    const { calls } = createApi(() => json({ id: employeeId }, 201));
    setup('/admin/users/new', context('hr.employees.create'));
    await waitFor(() => expect(field.employeeNumber()).toHaveValue('0042'));
    expect(field.employeeNumber()).toBeEnabled();
    expect(field.employeeNumber()).toHaveAttribute('dir', 'ltr');
    expect(screen.getByText(/شمارهٔ پیشنهادی «شرکت نمونه» قابل ویرایش است/)).toBeInTheDocument();
    // Switching tabs back and forth never asks again.
    await user.click(tab(/^اطلاعات تماس/));
    await user.click(tab(/^اطلاعات اصلی/));
    expect(countOf(calls, 'GET', nextNumberPath)).toBe(1);
    // An untouched suggestion needs no "discard changes" question.
    await user.click(screen.getByRole('button', { name: 'انصراف' }));
    expect(screen.queryByRole('alertdialog')).toBeNull();
    expect(window.location.pathname).toBe('/app/admin/users');
  });

  it('disables the field and asks nothing without hr.employees.create in the work-context company', async () => {
    const { calls } = createApi(() => json({ id: employeeId }, 201));
    setup('/admin/users/new', context('hr.employees.view'));
    expect(field.employeeNumber()).toBeDisabled();
    expect(field.employeeNumber()).toHaveValue('');
    expect(screen.getByText(/مجوز «ایجاد کارمند» در «شرکت نمونه» لازم است/)).toBeInTheDocument();
    await Promise.resolve();
    expect(countOf(calls, 'GET', nextNumberPath)).toBe(0);
  });

  it('keeps the field empty and editable when the suggestion cannot be fetched', async () => {
    stubApi((call) => (call.path === nextNumberPath ? json({ error: 'پر است.' }, 409) : json({}, 404)));
    setup('/admin/users/new', context('hr.employees.create'));
    expect(await screen.findByText(/دریافت شمارهٔ پیشنهادی انجام نشد/)).toBeInTheDocument();
    expect(field.employeeNumber()).toBeEnabled();
    expect(field.employeeNumber()).toHaveValue('');
  });

  it('creates the account, then an UNLINKED personnel record with the form names, and reports both', async () => {
    const user = userEvent.setup();
    const { calls } = createApi(() => json({ id: employeeId }, 201));
    setup('/admin/users/new', context('hr.employees.create'));
    await waitFor(() => expect(field.employeeNumber()).toHaveValue('0042'));
    await fillCreate(user);
    await user.click(createButton());
    expect(await screen.findByText(/کاربر و پرونده پرسنلی او با موفقیت ایجاد شدند/)).toBeInTheDocument();
    const posts = calls.filter((call) => call.method === 'POST');
    expect(posts.map((call) => call.path)).toEqual(['/api/v1/admin/users', employeesPath]);
    // The personnel number never travels with the user request, and the record is not linked.
    expect(Object.keys(posts[0]?.body as object)).not.toContain('employeeNumber');
    expect(posts[1]?.body).toEqual({ employeeNumber: '0042', firstName: 'علی', lastName: 'رضایی' });
    expect(calls.some((call) => call.path.endsWith('/user'))).toBe(false);
    expect(posts[1]?.headers['X-XSRF-TOKEN']).toBe('tok');
    expect(window.location.pathname).toBe('/app/admin/users/new-1');
    expect(document.body.textContent).not.toContain('Str0ng');
  });

  it('never writes the password to the console while creating, failing or retrying', async () => {
    const user = userEvent.setup();
    const spies = (['log', 'info', 'warn', 'error', 'debug'] as const).map((method) => vi.spyOn(console, method).mockImplementation(() => undefined));
    createApi(() => json({}, 500));
    setup('/admin/users/new', context('hr.employees.create'));
    await waitFor(() => expect(field.employeeNumber()).toHaveValue('0042'));
    await fillCreate(user);
    await user.click(createButton());
    await screen.findByText(/ثبت پرونده پرسنلی انجام نشد/);
    for (const spy of spies) {
      expect(JSON.stringify(spy.mock.calls)).not.toContain('Str0ng');
      spy.mockRestore();
    }
  });

  it('creates only the account when the personnel number is cleared', async () => {
    const user = userEvent.setup();
    const { calls } = createApi(() => json({ id: employeeId }, 201));
    setup('/admin/users/new', context('hr.employees.create'));
    await waitFor(() => expect(field.employeeNumber()).toHaveValue('0042'));
    await user.clear(field.employeeNumber());
    await fillCreate(user);
    await user.click(createButton());
    expect(await screen.findByText('کاربر با موفقیت ایجاد شد.')).toBeInTheDocument();
    expect(countOf(calls, 'POST', employeesPath)).toBe(0);
  });

  it('sends a manually edited number as typed', async () => {
    const user = userEvent.setup();
    const { calls } = createApi(() => json({ id: employeeId }, 201));
    setup('/admin/users/new', context('hr.employees.create'));
    await waitFor(() => expect(field.employeeNumber()).toHaveValue('0042'));
    await user.clear(field.employeeNumber());
    await user.type(field.employeeNumber(), 'EMP-7');
    await fillCreate(user);
    await user.click(createButton());
    await screen.findByText(/پرونده پرسنلی او/);
    expect(calls.find((call) => call.method === 'POST' && call.path === employeesPath)?.body).toMatchObject({ employeeNumber: 'EMP-7' });
  });

  it('rejects a malformed number on its tab before anything is sent', async () => {
    const user = userEvent.setup();
    const { calls } = createApi(() => json({ id: employeeId }, 201));
    setup('/admin/users/new', context('hr.employees.create'));
    await waitFor(() => expect(field.employeeNumber()).toHaveValue('0042'));
    await user.clear(field.employeeNumber());
    await user.type(field.employeeNumber(), 'bad number');
    await fillCreate(user);
    await user.click(createButton());
    expect(await screen.findByText(/شماره پرسنلی فقط شامل حروف لاتین/)).toBeInTheDocument();
    expect(field.employeeNumber()).toHaveFocus();
    expect(calls.filter((call) => call.method === 'POST')).toHaveLength(0);
  });

  it('on 409 for an untouched suggestion asks for a fresh one and retries', async () => {
    const user = userEvent.setup();
    let attempts = 0;
    const { calls } = createApi(() => {
      attempts += 1;
      return attempts === 1 ? json({ error: 'شماره پرسنلی در این شرکت تکراری است.' }, 409) : json({ id: employeeId }, 201);
    }, ['0042', '0043']);
    setup('/admin/users/new', context('hr.employees.create'));
    await waitFor(() => expect(field.employeeNumber()).toHaveValue('0042'));
    await fillCreate(user);
    await user.click(createButton());
    expect(await screen.findByText(/کاربر و پرونده پرسنلی او با موفقیت ایجاد شدند/)).toBeInTheDocument();
    const employeePosts = calls.filter((call) => call.method === 'POST' && call.path === employeesPath);
    expect(employeePosts.map((call) => (call.body as { employeeNumber: string }).employeeNumber)).toEqual(['0042', '0043']);
    expect(countOf(calls, 'POST', '/api/v1/admin/users')).toBe(1);
  });

  it('on 409 for a typed number keeps the created account and warns, without replacing the number', async () => {
    const user = userEvent.setup();
    const { calls } = createApi(() => json({ error: 'شماره پرسنلی در این شرکت تکراری است.' }, 409));
    setup('/admin/users/new', context('hr.employees.create'));
    await waitFor(() => expect(field.employeeNumber()).toHaveValue('0042'));
    await user.clear(field.employeeNumber());
    await user.type(field.employeeNumber(), 'EMP-1');
    await fillCreate(user);
    await user.click(createButton());
    const warning = await screen.findByText(/پرونده پرسنلی ساخته نشد، چون شماره پرسنلی در این شرکت تکراری بود/);
    expect(warning.closest('[role="status"]')).not.toBeNull();
    expect(window.location.pathname).toBe('/app/admin/users/new-1');
    expect(countOf(calls, 'POST', employeesPath)).toBe(1);
    expect(countOf(calls, 'POST', '/api/v1/admin/users')).toBe(1);
  });

  it('shows a Persian warning on the created account when the second step fails', async () => {
    const user = userEvent.setup();
    createApi(() => json({}, 500));
    setup('/admin/users/new', context('hr.employees.create'));
    await waitFor(() => expect(field.employeeNumber()).toHaveValue('0042'));
    await fillCreate(user);
    await user.click(createButton());
    expect(await screen.findByText(/حساب کاربر ایجاد شد، اما ثبت پرونده پرسنلی انجام نشد/)).toBeInTheDocument();
    expect(window.location.pathname).toBe('/app/admin/users/new-1');
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('hands a 401 of the second step to the unauthenticated flow without hiding the created account', async () => {
    const user = userEvent.setup();
    onUnauthenticated.mockReturnValue(true);
    createApi(() => new Response(null, { status: 401 }));
    setup('/admin/users/new', context('hr.employees.create'));
    await waitFor(() => expect(field.employeeNumber()).toHaveValue('0042'));
    await fillCreate(user);
    await user.click(createButton());
    await waitFor(() => expect(onUnauthenticated).toHaveBeenCalled());
    // The shell is already leaving for the sign-in; the form does not navigate on its own.
    expect(window.location.pathname).toBe('/app/admin/users/new');
  });

  it('replaces an untouched suggestion when the work-context company changes, but keeps a typed number', async () => {
    const user = userEvent.setup();
    stubApi((call) => {
      if (call.method === 'GET' && call.path === nextNumberPath) return json({ suggestedEmployeeNumber: '0042' });
      if (call.method === 'GET' && call.path === '/api/v1/companies/c2/org/employees/next-number') return json({ suggestedEmployeeNumber: '0777' });
      return json({}, 404);
    });
    let setContext: (next: UserFormContext) => void = () => undefined;
    function Host() {
      const [workContext, set] = useState<UserFormContext>(context('hr.employees.create'));
      setContext = set;
      return <UserCreatePage onUnauthenticated={onUnauthenticated} workContext={workContext} />;
    }
    window.history.pushState(null, '', '/app/admin/users/new');
    render(
      <RouterProvider routes={[{ path: '/admin/users/new', element: () => <Host />, title: 'ایجاد کاربر' }]}>
        <PageBreadcrumbProvider fallback={[{ label: 'فرم' }]}>
          <RouteView />
        </PageBreadcrumbProvider>
      </RouterProvider>,
    );
    await waitFor(() => expect(field.employeeNumber()).toHaveValue('0042'));
    const second: UserFormContext = { ...context('hr.employees.create'), activeCompanyId: 'c2' };
    act(() => setContext(second));
    await waitFor(() => expect(field.employeeNumber()).toHaveValue('0777'));
    await user.clear(field.employeeNumber());
    await user.type(field.employeeNumber(), '9001');
    act(() => setContext({ ...second, activeCompanyId: 'c1' }));
    await waitFor(() => expect(field.employeeNumber()).toHaveValue('9001'));
  });

  it('does not send the personnel step when the user request itself fails, and keeps the typed values', async () => {
    const user = userEvent.setup();
    const { calls } = stubApi((call) => {
      if (call.path === nextNumberPath) return json({ suggestedEmployeeNumber: '0042' });
      if (call.method === 'POST') return json({ error: 'نام کاربری، ایمیل یا شماره همراه قبلاً ثبت شده است.' }, 409);
      return json({}, 404);
    });
    setup('/admin/users/new', context('hr.employees.create'));
    await waitFor(() => expect(field.employeeNumber()).toHaveValue('0042'));
    await fillCreate(user);
    await user.click(createButton());
    expect(await screen.findByRole('alert')).toHaveTextContent('قبلاً ثبت شده است');
    expect(field.employeeNumber()).toHaveValue('0042');
    expect(field.username()).toHaveValue('ali.r');
    expect(calls.filter((call) => call.method === 'POST' && call.path === employeesPath)).toHaveLength(0);
    expect(window.location.pathname).toBe('/app/admin/users/new');
  });

  it('shows honest empty states on the HR, roles and history tabs without requesting anything', async () => {
    const user = userEvent.setup();
    const { calls } = stubApi(() => json({}, 404));
    setup('/admin/users/new', context());
    await user.click(tab(/^اطلاعات استخدامی/));
    expect(screen.getByText('اطلاعات استخدامی پس از ایجاد حساب و عضویت در شرکت قابل تنظیم است.')).toBeInTheDocument();
    await user.click(tab(/^سمت‌ها و نقش‌ها/));
    expect(screen.getByText(/پس از ایجاد حساب و عضویت در شرکت قابل تنظیم است/)).toBeInTheDocument();
    await user.click(tab(/^سوابق/));
    expect(screen.getByText('سوابق پس از ایجاد حساب نمایش داده می‌شود.')).toBeInTheDocument();
    expect(calls).toHaveLength(0);
  });

  it('exposes a labelled tablist; in RTL ArrowLeft moves to the next tab, Home/End jump', async () => {
    const user = userEvent.setup();
    stubApi(() => json({}, 404));
    setup('/admin/users/new', context());
    const list = screen.getByRole('tablist', { name: 'بخش‌های فرم کاربر' });
    expect(within(list).getAllByRole('tab')).toHaveLength(5);
    expect(tab(/^اطلاعات اصلی/)).toHaveAttribute('aria-selected', 'true');
    tab(/^اطلاعات اصلی/).focus();
    await user.keyboard('{ArrowLeft}');
    expect(tab(/^اطلاعات تماس/)).toHaveAttribute('aria-selected', 'true');
    expect(tab(/^اطلاعات تماس/)).toHaveFocus();
    await user.keyboard('{ArrowRight}');
    expect(tab(/^اطلاعات اصلی/)).toHaveAttribute('aria-selected', 'true');
    await user.keyboard('{End}');
    expect(tab(/^سوابق/)).toHaveAttribute('aria-selected', 'true');
    await user.keyboard('{Home}');
    expect(tab(/^اطلاعات اصلی/)).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('tabpanel')).toHaveAccessibleName(/اطلاعات اصلی/);
  });

  it('keeps what was typed on every tab while switching, and never leaks the password', async () => {
    const user = userEvent.setup();
    stubApi(() => json({}, 404));
    setup('/admin/users/new', context());
    await fillCreate(user);
    for (const name of [/^اطلاعات استخدامی/, /^سوابق/, /^اطلاعات تماس/]) await user.click(tab(name));
    expect(field.email()).toHaveValue('ali@example.test');
    await user.click(tab(/^اطلاعات اصلی/));
    expect(field.username()).toHaveValue('ali.r');
    expect(field.password()).toHaveValue('Str0ng-pass-phrase!');
    expect(field.password()).toHaveAttribute('type', 'password');
    expect(window.location.href).not.toContain('Str0ng');
    expect(JSON.stringify({ ...window.localStorage, ...window.sessionStorage })).not.toContain('Str0ng');
  });

  it('does not treat a submit bubbling from a nested form as the form submit', async () => {
    const { calls } = createApi(() => json({ id: employeeId }, 201));
    setup('/admin/users/new', context());
    const inner = document.createElement('form');
    screen.getByRole('form', { name: 'فرم ایجاد کاربر' }).appendChild(inner);
    fireEvent.submit(inner);
    expect(calls.filter((call) => call.method === 'POST')).toHaveLength(0);
    expect(screen.queryByText('نام کاربری را وارد کنید.')).toBeNull();
  });
});

describe('UserForm edit — HR, roles and history tabs', () => {
  beforeEach(() => {
    onUnauthenticated.mockReset();
    onUnauthenticated.mockReturnValue(false);
    clearUserFlash();
  });

  const hrAll = context('hr.employees.view', 'admin.roles.assign', 'admin.security.view');

  /** The linked employee exists until an unlink is answered; a user object can be swapped to simulate a reload. */
  function editApi(options: { linked?: boolean; onAssign?: (call: ApiCall) => Response; user?: () => unknown } = {}) {
    let linked = options.linked ?? true;
    const api = stubApi((call) => {
      const { method, path } = call;
      if (method === 'GET' && path === '/api/v1/admin/users/user-2') return json(options.user ? options.user() : userDetails(2));
      if (method === 'GET' && path === employeesPath) {
        return json(employeeListResponse(linked ? [employeeListItem({ hasLinkedUser: true })] : []));
      }
      if (method === 'GET' && path === `${employeesPath}/${employeeId}`) {
        return json(employeeDetail({ activeAssignment: { ...employeeDetail().activeAssignment, managerEmployeeId } }));
      }
      if (method === 'GET' && path === `${employeesPath}/${managerEmployeeId}`) {
        return json(employeeDetail({ id: managerEmployeeId, firstName: 'سارا', lastName: 'کریمی' }));
      }
      if (method === 'DELETE' && path === `${employeesPath}/${employeeId}/user`) {
        linked = false;
        return new Response(null, { status: 204 });
      }
      if (method === 'GET' && path === '/api/v1/admin/companies/c1/roles') {
        return json([roleItem({ id: 'r-acc', code: 'ACCOUNTANT', name: 'حسابدار' }), roleItem({ id: 'r-sales', code: 'SALES', name: 'کارشناس فروش' })]);
      }
      if (method === 'POST' && path === '/api/v1/admin/companies/c1/roles/r-sales/members/user-2') {
        return options.onAssign ? options.onAssign(call) : new Response(null, { status: 204 });
      }
      if (method === 'GET' && path === '/api/v1/admin/users/user-2/login-history') return json([]);
      if (method === 'GET' && path === '/api/v1/admin/security/audit-events') return json({ items: [], totalCount: 0, page: 1, pageSize: 20 });
      return json({}, 404);
    });
    return api;
  }

  it('shows the linked personnel number read-only on the first tab and never a password or editable number', async () => {
    editApi();
    setup('/admin/users/user-2/edit', hrAll);
    expect(await screen.findByText('شماره پرسنلی (فقط‌خواندنی)')).toBeInTheDocument();
    expect(screen.getByText('EMP-101')).toBeInTheDocument();
    expect(screen.queryByLabelText(/^شماره پرسنلی$/)).toBeNull();
    expect(screen.queryByLabelText(/رمز عبور/)).toBeNull();
    expect(screen.queryByRole('textbox', { name: /شماره پرسنلی/ })).toBeNull();
  });

  it('loads the employment tab lazily from the real HR endpoints and never re-requests on revisit', async () => {
    const user = userEvent.setup();
    const { calls } = editApi();
    setup('/admin/users/user-2/edit', hrAll);
    await screen.findByText('EMP-101');
    expect(countOf(calls, 'GET', `${employeesPath}/${employeeId}`)).toBe(0);
    await user.click(tab(/^اطلاعات استخدامی/));
    expect(await screen.findByText('سارا کریمی')).toBeInTheDocument();
    expect(screen.getByText('دفتر مرکزی')).toBeInTheDocument();
    expect(screen.getByText('حسابدار')).toBeInTheDocument();
    expect(screen.getByText('فعال')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'باز کردن پروندهٔ کارمند' })).toHaveAttribute('href', `/app/hr/org/employees/${employeeId}`);
    const listBefore = countOf(calls, 'GET', employeesPath);
    await user.click(tab(/^اطلاعات تماس/));
    await user.click(tab(/^اطلاعات استخدامی/));
    expect(await screen.findByText('سارا کریمی')).toBeInTheDocument();
    expect(countOf(calls, 'GET', employeesPath)).toBe(listBefore);
    expect(countOf(calls, 'GET', `${employeesPath}/${employeeId}`)).toBe(1);
    expect(countOf(calls, 'GET', `${employeesPath}/${managerEmployeeId}`)).toBe(1);
  });

  it('asks the HR endpoint nothing without the HR view hint and says so', async () => {
    const user = userEvent.setup();
    const { calls } = editApi();
    setup('/admin/users/user-2/edit', context());
    await screen.findByLabelText(/^نام$/);
    await user.click(tab(/^اطلاعات استخدامی/));
    expect(screen.getByText('نمایش اطلاعات استخدامی این شرکت برای شما مجاز نیست.')).toBeInTheDocument();
    expect(countOf(calls, 'GET', employeesPath)).toBe(0);
  });

  it('disconnects the personnel record after confirmation and reloads the section from the server', async () => {
    const user = userEvent.setup();
    const { calls } = editApi();
    setup('/admin/users/user-2/edit', hrAll);
    await user.click(await screen.findByRole('tab', { name: /^اطلاعات استخدامی/ }));
    await user.click(await screen.findByRole('button', { name: 'قطع اتصال پرونده پرسنلی در شرکت نمونه' }));
    const dialog = screen.getByRole('alertdialog');
    await user.click(within(dialog).getByRole('button', { name: 'قطع اتصال' }));
    expect(await screen.findByText('اتصال پرونده پرسنلی قطع شد.')).toBeInTheDocument();
    expect(countOf(calls, 'DELETE', `${employeesPath}/${employeeId}/user`)).toBe(1);
    expect(await screen.findByText('برای این کاربر در این شرکت پروندهٔ پرسنلی متصل نشده است.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'اتصال پرونده پرسنلی در شرکت نمونه' })).toBeInTheDocument();
  });

  it('opens the link dialog for an unlinked user and does not submit the user form from it', async () => {
    const user = userEvent.setup();
    const { calls } = editApi({ linked: false });
    setup('/admin/users/user-2/edit', hrAll);
    await user.click(await screen.findByRole('tab', { name: /^اطلاعات استخدامی/ }));
    await user.click(await screen.findByRole('button', { name: 'اتصال پرونده پرسنلی در شرکت نمونه' }));
    const dialog = screen.getByRole('dialog');
    expect(dialog).toBeInTheDocument();
    await user.type(within(dialog).getByRole('searchbox', { name: 'جست‌وجوی کارمند' }), '{Enter}');
    await waitFor(() => expect(calls.filter((call) => call.method === 'GET' && call.path === employeesPath).length).toBeGreaterThan(1));
    expect(calls.filter((call) => call.method === 'PUT')).toHaveLength(0);
  });

  it('lists the real memberships and links to the access management constant', async () => {
    const user = userEvent.setup();
    editApi();
    setup('/admin/users/user-2/edit', hrAll);
    await user.click(await screen.findByRole('tab', { name: /^سمت‌ها و نقش‌ها/ }));
    const table = await screen.findByRole('table', { name: 'نقش نرم‌افزاری کاربر در هر عضویت' });
    expect(within(table).getByText('شرکت دوم')).toBeInTheDocument();
    expect(screen.getByText(/سمت سازمانی به‌تنهایی هیچ مجوز نرم‌افزاری ایجاد نمی‌کند/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'مدیریت مجوزهای دسترسی' })).toHaveAttribute('href', `/app${accessManagementPath}`);
    expect(await screen.findByText('سمت سازمانی:')).toBeInTheDocument();
  });

  it('assigns a role only after confirmation, to the real role of the member company, and reloads the user', async () => {
    const user = userEvent.setup();
    let reloads = 0;
    const { calls } = editApi({
      user: () => {
        reloads += 1;
        const base = userDetails(2);
        if (reloads < 2) return base;
        return { ...base, memberships: base.memberships.map((m) => (m.companyId === 'c1' ? { ...m, roleCode: 'SALES', roleName: 'کارشناس فروش' } : m)) };
      },
    });
    setup('/admin/users/user-2/edit', hrAll);
    await user.click(await screen.findByRole('tab', { name: /^سمت‌ها و نقش‌ها/ }));
    const select = await screen.findByLabelText('نقش نرم‌افزاری جدید');
    // The current role cannot be re-picked.
    expect(within(select).getByRole('option', { name: /حسابدار \(نقش فعلی\)/ })).toBeDisabled();
    const assign = screen.getByRole('button', { name: 'تخصیص نقش' });
    expect(assign).toBeDisabled();
    await user.selectOptions(select, 'r-sales');
    await user.click(assign);
    const dialog = screen.getByRole('dialog', { name: 'تخصیص نقش' });
    expect(countOf(calls, 'POST', '/api/v1/admin/companies/c1/roles/r-sales/members/user-2')).toBe(0);
    await user.click(within(dialog).getByRole('button', { name: 'تخصیص نقش' }));
    expect(await screen.findByText(/نقش «کارشناس فروش» برای این کاربر در «شرکت نمونه» تخصیص داده شد/)).toBeInTheDocument();
    const post = calls.find((call) => call.method === 'POST');
    expect(post?.headers['X-XSRF-TOKEN']).toBe('tok');
    await waitFor(() => expect(reloads).toBeGreaterThanOrEqual(2));
    const table = await screen.findByRole('table', { name: 'نقش نرم‌افزاری کاربر در هر عضویت' });
    await waitFor(() => expect(within(table).getByText('کارشناس فروش')).toBeInTheDocument());
  });

  it.each([
    [403, 'شما مجوز انجام این عملیات را ندارید.'],
    [409, 'این عضویت فعال نیست یا نقش سیستمی است.'],
  ])('shows the server message of a %s role assignment and keeps the form data', async (status, message) => {
    const user = userEvent.setup();
    editApi({ onAssign: () => json({ error: message }, status) });
    setup('/admin/users/user-2/edit', hrAll);
    await user.type(await screen.findByLabelText(/^نام$/), 'ی');
    await user.click(tab(/^سمت‌ها و نقش‌ها/));
    await user.selectOptions(await screen.findByLabelText('نقش نرم‌افزاری جدید'), 'r-sales');
    await user.click(screen.getByRole('button', { name: 'تخصیص نقش' }));
    await user.click(within(screen.getByRole('dialog', { name: 'تخصیص نقش' })).getByRole('button', { name: 'تخصیص نقش' }));
    expect(await screen.findByText(message)).toBeInTheDocument();
    await user.click(tab(/^اطلاعات اصلی/));
    expect(screen.getByLabelText(/^نام$/)).toHaveValue('نام2ی');
  });

  it('hides role assignment without the roles.assign hint', async () => {
    const user = userEvent.setup();
    const { calls } = editApi();
    setup('/admin/users/user-2/edit', context('hr.employees.view'));
    await user.click(await screen.findByRole('tab', { name: /^سمت‌ها و نقش‌ها/ }));
    expect(await screen.findByText('تخصیص نقش برای شما مجاز نیست.')).toBeInTheDocument();
    expect(countOf(calls, 'GET', '/api/v1/admin/companies/c1/roles')).toBe(0);
  });

  it('shows dates, login history once and the audit trail only with the security hint', async () => {
    const user = userEvent.setup();
    const { calls } = editApi();
    setup('/admin/users/user-2/edit', hrAll);
    await user.click(await screen.findByRole('tab', { name: /^سوابق/ }));
    expect(await screen.findByText('تاریخ ایجاد حساب')).toBeInTheDocument();
    expect(screen.getByText('آخرین تغییر')).toBeInTheDocument();
    expect(await screen.findByText('سابقه‌ای از ورود و خروج برای این حساب ثبت نشده است.')).toBeInTheDocument();
    expect(await screen.findByText('تغییر مدیریتی ثبت‌شده‌ای برای این حساب وجود ندارد.')).toBeInTheDocument();
    await user.click(tab(/^اطلاعات اصلی/));
    await user.click(tab(/^سوابق/));
    expect(countOf(calls, 'GET', '/api/v1/admin/users/user-2/login-history')).toBe(1);
    expect(countOf(calls, 'GET', '/api/v1/admin/security/audit-events')).toBe(1);
  });

  it('does not request the audit trail without the security hint', async () => {
    const user = userEvent.setup();
    const { calls } = editApi();
    setup('/admin/users/user-2/edit', context('hr.employees.view'));
    await user.click(await screen.findByRole('tab', { name: /^سوابق/ }));
    expect(await screen.findByText('نمایش سوابق تغییرات مدیریتی برای شما مجاز نیست.')).toBeInTheDocument();
    expect(countOf(calls, 'GET', '/api/v1/admin/security/audit-events')).toBe(0);
  });

  it('sends no request on the tabs the user never opens', async () => {
    const { calls } = editApi();
    setup('/admin/users/user-2/edit', hrAll);
    await screen.findByText('EMP-101');
    expect(countOf(calls, 'GET', '/api/v1/admin/companies/c1/roles')).toBe(0);
    expect(countOf(calls, 'GET', '/api/v1/admin/users/user-2/login-history')).toBe(0);
    expect(countOf(calls, 'GET', `${employeesPath}/${employeeId}`)).toBe(0);
  });

  it('opens the tab that holds a client error when saving from another tab', async () => {
    const user = userEvent.setup();
    const { calls } = editApi();
    setup('/admin/users/user-2/edit', hrAll);
    await user.click(await screen.findByRole('tab', { name: /^اطلاعات تماس/ }));
    await user.type(screen.getByLabelText(/^شماره موبایل/), '1');
    await user.click(tab(/^سوابق/));
    await user.click(screen.getByRole('button', { name: 'ذخیرهٔ تغییرات' }));
    // The mobile rule fails (12 digits): the contact tab opens and is flagged.
    expect(await screen.findByText(/شماره موبایل باید ۱۱ رقم لاتین/)).toBeInTheDocument();
    expect(tab(/^اطلاعات تماس/)).toHaveAttribute('aria-selected', 'true');
    expect(calls.filter((call) => call.method === 'PUT')).toHaveLength(0);
  });
});
