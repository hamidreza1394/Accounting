import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent, { type UserEvent } from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AppShell } from '@/components/AppShell';
import { resetFormsForTests } from '@/lib/formGuard';
import type { AccessSnapshot } from '@/lib/permissions';
import type { SelectedContext, Session } from '@/api/types';
import { json, listBody, stubApi, userItem, type ApiCall } from '@/test/adminUsers';
import { UserCreatePage } from './UserCreatePage';
import { UsersListPage } from './UsersListPage';
import { clearUserFlash, clearUserListFlash } from './userFlash';
import { resetUserListReturnForTests } from './userListReturn';

const session: Session = { username: 'hamid', fullName: 'حمید رضایی', accountKind: 'User', isAdministrator: false };
const access: AccessSnapshot = { status: 'ready', isInstallationAdministrator: true, companyId: 'company-1', companyCodes: [] };
const context: SelectedContext = {
  company: { id: 'company-1', name: 'شرکت نمونه', nationalId: null, roleName: 'مدیر سیستم' },
  fiscalPeriod: { id: 'period-1', title: 'سال مالی ۱۴۰۵', startDate: '2026-03-21', endDate: '2027-03-20', status: 'Open', isSelectable: true, accessMode: 'readWrite' },
};
const onUnauthenticated = vi.fn((): boolean => false);
const routes = [
  { path: '/', element: () => <h1>داشبورد</h1>, title: 'داشبورد' },
  { path: '/admin/users', element: () => <UsersListPage onUnauthenticated={onUnauthenticated} canCreate />, title: 'مدیریت کاربران' },
  { path: '/admin/users/new', element: () => <UserCreatePage onUnauthenticated={onUnauthenticated} />, title: 'ایجاد کاربر' },
] as const;

function setup(at: string) {
  window.history.pushState(null, '', at);
  return render(
    <AppShell session={session} context={context} access={access} routes={routes} workspaceTabs onChangeContext={vi.fn()} onLogout={vi.fn().mockResolvedValue(undefined)}>
      <main>جایگزین</main>
    </AppShell>,
  );
}

const tabbar = () => screen.getByRole('navigation', { name: 'تب‌های کاری باز' });
const tabNames = () =>
  within(tabbar())
    .getAllByRole('button')
    .filter((button) => !button.className.includes('faranou-tab-close') && !button.className.includes('faranou-tabbar-new'))
    .map((button) => button.textContent?.trim());

const existing = [userItem(1), userItem(2)];
const created = userItem(9, { id: 'new-9', username: 'ali.r', firstName: 'علی', lastName: 'رضایی' });

/** A server whose list contains the created user (first) only after the POST succeeded. */
function server() {
  let createdYet = false;
  const api = stubApi((call: ApiCall) => {
    if (call.method === 'POST' && call.path === '/api/v1/admin/users') {
      createdYet = true;
      return json({ id: 'new-9' }, 201);
    }
    if (call.method === 'GET' && call.path === '/api/v1/admin/users') {
      return json(listBody(createdYet ? [created, ...existing] : existing));
    }
    return json({});
  });
  const listGets = () => api.calls.filter((call) => call.method === 'GET' && call.path === '/api/v1/admin/users');
  return { ...api, listGets };
}

async function fillCreate(user: UserEvent) {
  await user.type(screen.getByLabelText(/^نام\s*\*?$/), 'علی');
  await user.type(screen.getByLabelText(/^نام خانوادگی/), 'رضایی');
  await user.type(screen.getByLabelText(/^نام کاربری/), 'ali.r');
  await user.type(screen.getByLabelText(/^رمز عبور/), 'Abcd-1234');
  await user.type(screen.getByLabelText(/^تکرار رمز عبور/), 'Abcd-1234');
}

beforeEach(() => {
  onUnauthenticated.mockReset();
  onUnauthenticated.mockReturnValue(false);
  clearUserFlash();
  clearUserListFlash();
  resetUserListReturnForTests();
});

afterEach(() => {
  window.history.pushState(null, '', '/app/');
  resetFormsForTests();
  vi.unstubAllGlobals();
});

describe('user create → users list (workspace tabs)', () => {
  it('after a successful create: one POST, success message, the plain list, a refetch, the new user first, no leftover create tab', async () => {
    const user = userEvent.setup();
    const { calls, listGets } = server();
    setup('/app/admin/users?q=zzz&page=3');
    await waitFor(() => expect(listGets()).toHaveLength(1));

    await user.click(await screen.findByRole('link', { name: 'ایجاد کاربر' }));
    await screen.findByRole('heading', { name: 'ایجاد کاربر', level: 1 });
    await fillCreate(user);
    await user.click(screen.getByRole('button', { name: 'ایجاد کاربر' }));

    await waitFor(() => expect(window.location.pathname + window.location.search).toBe('/app/admin/users'));
    expect(calls.filter((call) => call.method === 'POST')).toHaveLength(1);
    // The message, the refetch (default newest-first query, page 1, no search) and the new row on top.
    expect(await screen.findByText('کاربر با موفقیت ایجاد شد.')).toBeInTheDocument();
    // The pane that was open with the old filters loads again; the LAST request is the plain first page.
    await waitFor(() => expect(listGets().length).toBeGreaterThanOrEqual(2));
    const refetch = listGets().at(-1)!.search!;
    expect(refetch.get('page')).toBe('1');
    expect(refetch.get('sort')).toBe('created');
    expect(refetch.get('descending')).toBe('true');
    expect(refetch.get('search')).toBeNull();
    const rows = await screen.findAllByRole('row');
    expect(within(rows[1]!).getByText('ali.r')).toBeInTheDocument();
    // The create tab is gone and the list tab exists exactly once.
    expect(tabNames().filter((name) => name?.includes('ایجاد کاربر'))).toHaveLength(0);
    expect(tabNames().filter((name) => name?.includes('مدیریت کاربران'))).toHaveLength(1);
  });

  it('cancel on a clean form returns to the plain list and refreshes it', async () => {
    const user = userEvent.setup();
    const { listGets } = server();
    setup('/app/admin/users/new');
    await screen.findByRole('heading', { name: 'ایجاد کاربر', level: 1 });
    await user.click(screen.getByRole('button', { name: 'انصراف' }));
    await waitFor(() => expect(window.location.pathname).toBe('/app/admin/users'));
    await waitFor(() => expect(listGets().length).toBeGreaterThanOrEqual(1));
    expect(tabNames().filter((name) => name?.includes('ایجاد کاربر'))).toHaveLength(0);
  });

  it('the back button goes to exactly /admin/users (not history.back) and asks before discarding typed values', async () => {
    const user = userEvent.setup();
    const back = vi.spyOn(window.history, 'back');
    const { listGets } = server();
    setup('/app/admin/users?q=old&page=2');
    await waitFor(() => expect(listGets()).toHaveLength(1));
    await user.click(await screen.findByRole('link', { name: 'ایجاد کاربر' }));
    await screen.findByRole('heading', { name: 'ایجاد کاربر', level: 1 });
    await user.type(screen.getByLabelText(/^نام کاربری/), 'draft');

    await user.click(screen.getByRole('button', { name: 'بازگشت به فهرست کاربران' }));
    // Dirty: the shared guard dialog appears first and nothing moved.
    expect(await screen.findByRole('alertdialog')).toHaveTextContent('تغییرات ذخیره‌نشده');
    expect(window.location.pathname).toBe('/app/admin/users/new');
    await user.click(screen.getByRole('button', { name: 'ادامه ویرایش' }));
    expect(window.location.pathname).toBe('/app/admin/users/new');
    expect(screen.getByLabelText(/^نام کاربری/)).toHaveValue('draft');

    await user.click(screen.getByRole('button', { name: 'بازگشت به فهرست کاربران' }));
    await user.click(await screen.findByRole('button', { name: 'خروج بدون ذخیره' }));
    await waitFor(() => expect(window.location.pathname + window.location.search).toBe('/app/admin/users'));
    expect(back).not.toHaveBeenCalled();
    await waitFor(() => expect(listGets().length).toBeGreaterThanOrEqual(2));
    expect(tabNames().filter((name) => name?.includes('ایجاد کاربر'))).toHaveLength(0);
    back.mockRestore();
  });

  it('a failed create stays on the form with the typed values and does not touch the list', async () => {
    const user = userEvent.setup();
    const { calls, listGets } = (() => {
      const api = stubApi((call) =>
        call.method === 'POST' ? json({ error: 'نام کاربری، ایمیل یا شماره همراه قبلاً ثبت شده است.' }, 409) : json(listBody(existing)),
      );
      return { ...api, listGets: () => api.calls.filter((call) => call.method === 'GET' && call.path === '/api/v1/admin/users') };
    })();
    setup('/app/admin/users');
    await waitFor(() => expect(listGets()).toHaveLength(1));
    await user.click(await screen.findByRole('link', { name: 'ایجاد کاربر' }));
    await fillCreate(user);
    await user.click(screen.getByRole('button', { name: 'ایجاد کاربر' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('قبلاً ثبت شده است');
    expect(window.location.pathname).toBe('/app/admin/users/new');
    expect(listGets()).toHaveLength(1);
    expect(calls.filter((call) => call.method === 'POST')).toHaveLength(1);
  });
});
