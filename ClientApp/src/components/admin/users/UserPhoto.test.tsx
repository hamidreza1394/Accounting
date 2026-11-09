import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent, { type UserEvent } from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { RouteView, RouterProvider, type Route } from '../../../lib/router';
import type { Permission } from '../../../lib/permissions';
import { PageBreadcrumbProvider } from '@/components/shell/PageBreadcrumb';
import { employeeId, employeeListItem, employeeListResponse } from '@/features/hr/test/hrFixtures';
import { json, stubApi, userDetails, type ApiCall } from '../../../test/adminUsers';
import { UserCreatePage } from './UserCreatePage';
import { UserEditPage } from './UserEditPage';
import { UserListFlashBanner } from './UserListFlashBanner';
import type { UserFormContext } from './form/userFormShared';
import { clearUserFlash, clearUserListFlash } from './userFlash';
import { photoMaxBytes } from '@/lib/employeePhoto';

const onUnauthenticated = vi.fn((): boolean => false);
const context = (...allowed: Permission[]): UserFormContext => ({
  activeCompanyId: 'c1',
  activeCompanyName: 'شرکت نمونه',
  can: (permission) => allowed.includes(permission),
});
const employeesPath = '/api/v1/companies/c1/org/employees';
const photoPath = `${employeesPath}/${employeeId}/photo`;

function setup(path: string, workContext: UserFormContext) {
  const routes: Route[] = [
    { path: '/admin/users', element: () => (<><h1>فهرست</h1><UserListFlashBanner /></>), title: 'فهرست' },
    { path: '/admin/users/new', element: () => <UserCreatePage onUnauthenticated={onUnauthenticated} workContext={workContext} />, title: 'ایجاد کاربر' },
    { path: '/admin/users/:id', element: () => <h1>جزئیات</h1>, title: 'جزئیات' },
    { path: '/admin/users/:id/edit', element: () => <UserEditPage onUnauthenticated={onUnauthenticated} workContext={workContext} />, title: 'ویرایش' },
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

const png = (name = 'me.png', size = 12) => new File([new Uint8Array(size).fill(1)], name, { type: 'image/png' });
const fileInput = () => screen.getByLabelText('انتخاب فایل عکس پرسنلی');
const tab = (name: RegExp) => screen.getByRole('tab', { name });
const field = {
  firstName: () => screen.getByLabelText(/^نام\s*\*?$/),
  lastName: () => screen.getByLabelText(/^نام خانوادگی/),
  username: () => screen.getByLabelText(/^نام کاربری/),
  password: () => screen.getByLabelText(/^رمز عبور/),
  confirm: () => screen.getByLabelText(/^تکرار رمز عبور/),
  employeeNumber: () => screen.getByLabelText(/^شماره پرسنلی$/),
};

async function fillCreate(user: UserEvent) {
  await user.type(field.firstName(), 'علی');
  await user.type(field.lastName(), 'رضایی');
  await user.type(field.username(), 'ali.r');
  await user.type(field.password(), 'Abcd-1234');
  await user.type(field.confirm(), 'Abcd-1234');
}

beforeEach(() => {
  onUnauthenticated.mockReset();
  onUnauthenticated.mockReturnValue(false);
  clearUserFlash();
  clearUserListFlash();
});

describe('personnel photo — create form', () => {
  function createApi(onPhoto: (call: ApiCall) => Response = () => new Response(null, { status: 204 })) {
    return stubApi((call) => {
      if (call.method === 'GET' && call.path === `${employeesPath}/next-number`) return json({ suggestedEmployeeNumber: '0042' });
      if (call.method === 'POST' && call.path === '/api/v1/admin/users') return json({ id: 'new-1' }, 201);
      if (call.method === 'POST' && call.path === employeesPath) return json({ id: employeeId }, 201);
      if (call.method === 'PUT' && call.path === photoPath) return onPhoto(call);
      return json({}, 404);
    });
  }

  it('shows the default initials avatar and a pick button before any photo is chosen', async () => {
    const user = userEvent.setup();
    createApi();
    setup('/admin/users/new', context('hr.employees.create', 'hr.employees.update'));
    expect(screen.getByRole('button', { name: 'انتخاب عکس' })).toBeEnabled();
    expect(screen.queryByRole('button', { name: 'حذف عکس' })).toBeNull();
    await user.type(field.firstName(), 'علی');
    await user.type(field.lastName(), 'رضایی');
    expect(screen.getByTestId('user-photo-field')).toHaveTextContent('عر');
  });

  it('previews a valid image, lets the user change or remove it, and keeps it across tab switches', async () => {
    const user = userEvent.setup();
    createApi();
    setup('/admin/users/new', context('hr.employees.create', 'hr.employees.update'));
    await user.upload(fileInput(), png());
    const image = await screen.findByAltText('عکس پرسنلی');
    expect(image.getAttribute('src')).toMatch(/^data:image\/png;base64,/);
    expect(screen.getByRole('button', { name: 'تغییر عکس' })).toBeInTheDocument();

    await user.click(tab(/^اطلاعات تماس/));
    await user.click(tab(/^اطلاعات اصلی/));
    expect(screen.getByAltText('عکس پرسنلی').getAttribute('src')).toMatch(/^data:image\/png/);

    await user.click(screen.getByRole('button', { name: 'حذف عکس' }));
    expect(screen.queryByAltText('عکس پرسنلی')).toBeNull();
    expect(screen.getByRole('button', { name: 'انتخاب عکس' })).toBeInTheDocument();
  });

  it('refuses a file of the wrong type or above 2 MB and shows the reason', async () => {
    const user = userEvent.setup({ applyAccept: false });
    createApi();
    setup('/admin/users/new', context('hr.employees.create', 'hr.employees.update'));
    await user.upload(fileInput(), new File(['<svg/>'], 'x.svg', { type: 'image/svg+xml' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('فقط عکس با قالب JPEG، PNG یا WebP پذیرفته می‌شود.');
    expect(screen.queryByAltText('عکس پرسنلی')).toBeNull();

    await user.upload(fileInput(), png('big.png', photoMaxBytes + 1));
    expect(await screen.findByRole('alert')).toHaveTextContent('حجم عکس نباید بیشتر از ۲ مگابایت باشد.');
    expect(screen.queryByAltText('عکس پرسنلی')).toBeNull();

    // Exactly 2 MB is accepted.
    await user.upload(fileInput(), png('edge.png', photoMaxBytes));
    expect(await screen.findByAltText('عکس پرسنلی')).toBeInTheDocument();
  });

  it('is unavailable (and says why) without a work company or the create-employee permission', () => {
    createApi();
    setup('/admin/users/new', context('hr.employees.view'));
    expect(screen.getByRole('button', { name: 'انتخاب عکس' })).toBeDisabled();
    expect(screen.getByText(/مجوزهای «ایجاد کارمند» و «ویرایش کارمند» در شرکت فعال لازم است/)).toBeInTheDocument();
  });

  it('blocks the save while a photo is chosen but no personnel number would be created', async () => {
    const user = userEvent.setup();
    const { calls } = createApi();
    setup('/admin/users/new', context('hr.employees.create', 'hr.employees.update'));
    await waitFor(() => expect(field.employeeNumber()).toHaveValue('0042'));
    await user.upload(fileInput(), png());
    await user.clear(field.employeeNumber());
    expect(await screen.findByRole('alert')).toHaveTextContent('عکس بدون شماره پرسنلی ذخیره نمی‌شود');
    await fillCreate(user);
    await user.click(screen.getByRole('button', { name: 'ایجاد کاربر' }));
    expect(calls.filter((call) => call.method === 'POST')).toHaveLength(0);
    expect(window.location.pathname).toBe('/app/admin/users/new');
  });

  it('creates the account, then the personnel record, then uploads the photo to THAT record (once)', async () => {
    const user = userEvent.setup();
    const { calls } = createApi();
    setup('/admin/users/new', context('hr.employees.create', 'hr.employees.update'));
    await waitFor(() => expect(field.employeeNumber()).toHaveValue('0042'));
    await user.upload(fileInput(), png('secret-name.png'));
    await fillCreate(user);
    await user.click(screen.getByRole('button', { name: 'ایجاد کاربر' }));
    await waitFor(() => expect(window.location.pathname).toBe('/app/admin/users'));
    const writes = calls.filter((call) => call.method !== 'GET');
    expect(writes.map((call) => `${call.method} ${call.path}`)).toEqual([
      'POST /api/v1/admin/users',
      `POST ${employeesPath}`,
      `PUT ${photoPath}`,
    ]);
    const put = writes[2]!;
    expect(put.headers['X-XSRF-TOKEN']).toBe('tok');
    expect(put.headers['Content-Type']).toBe('image/png');
    // The body is the file itself; the user's file name travels nowhere.
    expect(put.rawBody).toBeInstanceOf(File);
    expect(JSON.stringify(writes)).not.toContain('secret-name');
    expect(await screen.findByText(/کاربر و پرونده پرسنلی او با موفقیت ایجاد شدند/)).toBeInTheDocument();
  });

  it('reports a photo failure as a warning on the list while the account and record stay created', async () => {
    const user = userEvent.setup();
    createApi(() => json({ error: 'فقط عکس با قالب JPEG، PNG یا WebP پذیرفته می‌شود.' }, 400));
    setup('/admin/users/new', context('hr.employees.create', 'hr.employees.update'));
    await waitFor(() => expect(field.employeeNumber()).toHaveValue('0042'));
    await user.upload(fileInput(), png());
    await fillCreate(user);
    await user.click(screen.getByRole('button', { name: 'ایجاد کاربر' }));
    await waitFor(() => expect(window.location.pathname).toBe('/app/admin/users'));
    expect(await screen.findByText(/ذخیرهٔ عکس پرسنلی انجام نشد/)).toBeInTheDocument();
  });

  it('does not upload anything when no photo was chosen', async () => {
    const user = userEvent.setup();
    const { calls } = createApi();
    setup('/admin/users/new', context('hr.employees.create', 'hr.employees.update'));
    await waitFor(() => expect(field.employeeNumber()).toHaveValue('0042'));
    await fillCreate(user);
    await user.click(screen.getByRole('button', { name: 'ایجاد کاربر' }));
    await waitFor(() => expect(window.location.pathname).toBe('/app/admin/users'));
    expect(calls.some((call) => call.method === 'PUT')).toBe(false);
  });
});

describe('personnel photo — edit form', () => {
  function editApi(options: { linked?: boolean; onPut?: (call: ApiCall) => Response } = {}) {
    return stubApi((call) => {
      const { method, path } = call;
      if (method === 'GET' && path === '/api/v1/admin/users/user-2') return json(userDetails(2));
      if (method === 'GET' && path === employeesPath) {
        return json(employeeListResponse((options.linked ?? true) ? [employeeListItem({ hasLinkedUser: true })] : []));
      }
      if (method === 'PUT' && path === photoPath) return options.onPut ? options.onPut(call) : new Response(null, { status: 204 });
      if (method === 'DELETE' && path === photoPath) return new Response(null, { status: 204 });
      return json({}, 404);
    });
  }
  const editContext = context('hr.employees.view', 'hr.employees.update');

  it('shows the saved photo from the protected endpoint of the linked personnel record', async () => {
    editApi();
    setup('/admin/users/user-2/edit', editContext);
    const image = await screen.findByAltText('عکس پرسنلی');
    expect(image.getAttribute('src')).toMatch(new RegExp(`^${photoPath}\\?v=\\d+$`));
    expect(photoPath).not.toContain('wwwroot');
  });

  it('falls back to the default initials avatar when the record has no photo (404)', async () => {
    editApi();
    setup('/admin/users/user-2/edit', editContext);
    const image = await screen.findByAltText('عکس پرسنلی');
    fireEvent.error(image);
    await waitFor(() => expect(screen.queryByAltText('عکس پرسنلی')).toBeNull());
    expect(screen.getByRole('button', { name: 'انتخاب عکس' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'حذف عکس' })).toBeNull();
  });

  it('uploads a replacement immediately (anti-forgery header, file body) and previews it', async () => {
    const user = userEvent.setup();
    const { calls } = editApi();
    setup('/admin/users/user-2/edit', editContext);
    await screen.findByAltText('عکس پرسنلی');
    await user.upload(fileInput(), png());
    await waitFor(() => expect(calls.some((call) => call.method === 'PUT' && call.path === photoPath)).toBe(true));
    const put = calls.find((call) => call.method === 'PUT')!;
    expect(put.headers['X-XSRF-TOKEN']).toBe('tok');
    expect(put.rawBody).toBeInstanceOf(File);
    await waitFor(() => expect(screen.getByRole('button', { name: 'تغییر عکس' })).toBeEnabled());
  });

  it('shows the server message and restores the saved photo when the upload fails', async () => {
    const user = userEvent.setup();
    editApi({ onPut: () => json({ error: 'حجم عکس نباید بیشتر از ۲ مگابایت باشد.' }, 413) });
    setup('/admin/users/user-2/edit', editContext);
    await screen.findByAltText('عکس پرسنلی');
    await user.upload(fileInput(), png());
    expect(await screen.findByRole('alert')).toHaveTextContent('حجم عکس نباید بیشتر از ۲ مگابایت باشد.');
    expect(screen.getByAltText('عکس پرسنلی').getAttribute('src')).toMatch(/^\/api\/v1\//);
  });

  it('removes the photo with a DELETE and shows the default avatar', async () => {
    const user = userEvent.setup();
    const { calls } = editApi();
    setup('/admin/users/user-2/edit', editContext);
    await screen.findByAltText('عکس پرسنلی');
    await user.click(screen.getByRole('button', { name: 'حذف عکس' }));
    await waitFor(() => expect(calls.some((call) => call.method === 'DELETE' && call.path === photoPath)).toBe(true));
    await waitFor(() => expect(screen.queryByAltText('عکس پرسنلی')).toBeNull());
  });

  it('is read-only without hr.employees.update', async () => {
    editApi();
    setup('/admin/users/user-2/edit', context('hr.employees.view'));
    await screen.findByAltText('عکس پرسنلی');
    expect(screen.getByRole('button', { name: 'تغییر عکس' })).toBeDisabled();
    expect(screen.getByText(/مجوز «ویرایش کارمند» در شرکت فعال لازم است/)).toBeInTheDocument();
  });

  it('says honestly that there is no photo to manage when no personnel record is linked', async () => {
    editApi({ linked: false });
    setup('/admin/users/user-2/edit', editContext);
    expect((await screen.findAllByText(/پرونده پرسنلی متصل نشده است/)).length).toBeGreaterThan(0);
    expect(screen.queryByLabelText('انتخاب فایل عکس پرسنلی')).toBeNull();
  });
});
