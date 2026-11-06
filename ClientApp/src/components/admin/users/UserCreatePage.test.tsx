import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent, { type UserEvent } from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { RouteView, RouterProvider, type Route } from '../../../lib/router';
import { json, stubApi, userDetails } from '../../../test/adminUsers';
import { UserCreatePage } from './UserCreatePage';
import { UserDetailPage } from './UserDetailPage';
import { clearUserFlash, peekUserFlash, setUserFlash } from './userFlash';

const onUnauthenticated = vi.fn((): boolean => false);
const routes: Route[] = [
  { path: '/admin/users', element: () => <h1>فهرست</h1>, title: 'فهرست' },
  { path: '/admin/users/new', element: () => <UserCreatePage onUnauthenticated={onUnauthenticated} />, title: 'ایجاد کاربر' },
  { path: '/admin/users/:id', element: () => <UserDetailPage onUnauthenticated={onUnauthenticated} />, title: 'جزئیات' },
];

function setup() {
  window.history.pushState(null, '', '/app/admin/users/new');
  return render(
    <RouterProvider routes={routes}>
      <RouteView />
    </RouterProvider>,
  );
}

const field = {
  firstName: () => screen.getByLabelText(/^نام$/),
  lastName: () => screen.getByLabelText(/^نام خانوادگی$/),
  username: () => screen.getByLabelText(/^نام کاربری/),
  password: () => screen.getByLabelText(/^رمز عبور/),
  confirm: () => screen.getByLabelText(/^تکرار رمز عبور/),
  email: () => screen.getByLabelText(/^ایمیل/),
  mobile: () => screen.getByLabelText(/^شماره موبایل/),
};
const submit = () => screen.getByRole('button', { name: 'ایجاد کاربر' });

async function fillValid(user: UserEvent) {
  await user.type(field.firstName(), 'علی');
  await user.type(field.lastName(), 'رضایی');
  await user.type(field.username(), 'ali.r');
  await user.type(field.password(), 'Str0ng-pass-phrase!');
  await user.type(field.confirm(), 'Str0ng-pass-phrase!');
  await user.type(field.email(), 'ali@example.test');
  await user.type(field.mobile(), '09123456789');
}

describe('UserCreatePage', () => {
  beforeEach(() => {
    onUnauthenticated.mockReset();
    onUnauthenticated.mockReturnValue(false);
    clearUserFlash();
    window.localStorage.clear();
    window.sessionStorage.clear();
  });

  it('renders the four sections, a breadcrumb and an h1 with the right field attributes', () => {
    stubApi(() => json({}));
    setup();
    expect(screen.getByRole('heading', { level: 1, name: 'ایجاد کاربر' })).toBeInTheDocument();
    const crumbs = within(screen.getByRole('navigation', { name: 'مسیر صفحه' }));
    expect(crumbs.getByRole('link', { name: 'کاربران' })).toHaveAttribute('href', '/app/admin/users');
    for (const title of ['اطلاعات هویتی', 'اطلاعات ورود', 'اطلاعات تماس', 'وضعیت']) {
      expect(screen.getByRole('region', { name: title })).toBeInTheDocument();
    }
    expect(field.username()).toHaveAttribute('dir', 'ltr');
    expect(field.email()).toHaveAttribute('dir', 'ltr');
    expect(field.mobile()).toHaveAttribute('dir', 'ltr');
    expect(field.password()).toHaveAttribute('autocomplete', 'new-password');
    expect(field.confirm()).toHaveAttribute('autocomplete', 'new-password');
    expect(field.password()).toHaveAttribute('type', 'password');
    expect(screen.getByRole('checkbox', { name: 'حساب فعال باشد' })).toBeChecked();
    // Only fields the API supports.
    expect(screen.queryByLabelText(/کد ملی|نقش|شرکت|یادداشت/)).toBeNull();
  });

  it('keeps the password toggle on one side with the text padding, and turns off spelling aids', () => {
    stubApi(() => json({}));
    setup();
    for (const input of [field.password(), field.confirm()]) {
      const wrapper = input.parentElement as HTMLElement;
      expect(wrapper).toHaveAttribute('dir', 'ltr');
      expect(input).toHaveClass('pe-11', 'text-start');
      expect(within(wrapper).getByRole('button')).toHaveClass('end-0');
      expect(input).toHaveAttribute('spellcheck', 'false');
      expect(input).toHaveAttribute('autocapitalize', 'none');
      expect(input).toHaveAttribute('autocorrect', 'off');
    }
  });

  it('keeps the outline continuous: h1 then h2 sections, no h3', () => {
    stubApi(() => json({}));
    setup();
    expect(screen.getByRole('heading', { level: 1 })).toBeInTheDocument();
    expect(screen.getAllByRole('heading', { level: 2 }).map((h) => h.textContent)).toEqual([
      'اطلاعات هویتی',
      'اطلاعات ورود',
      'اطلاعات تماس',
      'وضعیت',
    ]);
    expect(screen.queryByRole('heading', { level: 3 })).toBeNull();
  });

  it('drops a stale success message when the create form opens', () => {
    setUserFlash('user-9', 'created');
    stubApi(() => json({}));
    setup();
    expect(peekUserFlash('user-9')).toBeNull();
  });

  it('derives the hints from the validation limits', () => {
    stubApi(() => json({}));
    setup();
    expect(screen.getByText(/۳ تا ۲۰ نویسه/)).toBeInTheDocument();
    expect(screen.getByText(/دست‌کم ۱۵ نویسه/)).toBeInTheDocument();
    expect(screen.getByText(/۱۱ رقم لاتین، مانند/)).toBeInTheDocument();
  });

  it('toggles password visibility with an accessible, keyboard-operable button', async () => {
    const user = userEvent.setup();
    stubApi(() => json({}));
    setup();
    const toggle = screen.getByRole('button', { name: 'نمایش رمز عبور' });
    expect(toggle).toHaveAttribute('aria-pressed', 'false');
    toggle.focus();
    await user.keyboard('{Enter}');
    expect(field.password()).toHaveAttribute('type', 'text');
    // The shared PasswordInput keeps one fixed label; the state is only in aria-pressed.
    expect(screen.getByRole('button', { name: 'نمایش رمز عبور' })).toHaveAttribute('aria-pressed', 'true');
    expect(field.confirm()).toHaveAttribute('type', 'password');
  });

  it('shows required-field errors on submit, focuses the first invalid field and sends nothing', async () => {
    const user = userEvent.setup();
    const { calls } = stubApi(() => json({}));
    setup();
    await user.click(submit());
    expect(await screen.findByText('نام کاربری را وارد کنید.')).toBeInTheDocument();
    expect(screen.getByText('ایمیل را وارد کنید.')).toBeInTheDocument();
    expect(screen.getByText('رمز عبور را وارد کنید.')).toBeInTheDocument();
    expect(screen.getByText('تکرار رمز عبور را وارد کنید.')).toBeInTheDocument();
    expect(field.username()).toHaveAttribute('aria-invalid', 'true');
    expect(field.username()).toHaveFocus();
    expect(field.username()).toHaveAccessibleDescription(/نام کاربری را وارد کنید/);
    expect(calls).toHaveLength(0);
  });

  it('validates a field on blur, not while untouched, and clears the error when fixed', async () => {
    const user = userEvent.setup();
    stubApi(() => json({}));
    setup();
    await user.type(field.username(), 'ab');
    expect(screen.queryByText(/دست‌کم ۳ نویسه/)).toBeNull();
    await user.tab();
    expect(screen.getByText('نام کاربری باید دست‌کم ۳ نویسه باشد.')).toBeInTheDocument();
    await user.type(field.username(), 'c');
    expect(screen.queryByText(/دست‌کم ۳ نویسه/)).toBeNull();
  });

  it.each([
    ['email', 'plain', 'قالب ایمیل معتبر نیست.'],
    ['mobile', '0912', 'شماره موبایل باید ۱۱ رقم لاتین باشد'],
    ['password', 'short1', 'رمز عبور باید دست‌کم ۱۵ نویسه باشد.'],
  ] as const)('shows the %s rule on blur', async (name, value, message) => {
    const user = userEvent.setup();
    stubApi(() => json({}));
    setup();
    await user.type(field[name](), value);
    await user.tab();
    expect(screen.getByText(new RegExp(message))).toBeInTheDocument();
  });

  it('flags a password confirmation mismatch', async () => {
    const user = userEvent.setup();
    stubApi(() => json({}));
    setup();
    await user.type(field.password(), 'Str0ng-pass-phrase!');
    await user.type(field.confirm(), 'other-pass-1');
    await user.tab();
    expect(screen.getByText('تکرار رمز عبور با رمز عبور یکسان نیست.')).toBeInTheDocument();
  });

  it('flags too-long names and username', async () => {
    const user = userEvent.setup();
    stubApi(() => json({}));
    setup();
    fireEvent.change(field.firstName(), { target: { value: 'x'.repeat(51) } });
    fireEvent.change(field.username(), { target: { value: 'u'.repeat(21) } });
    await user.click(submit());
    expect(screen.getByText(/نام نباید بیش از ۵۰ نویسه باشد/)).toBeInTheDocument();
    expect(screen.getByText(/نام کاربری نباید بیش از ۲۰ نویسه باشد/)).toBeInTheDocument();
  });

  it('posts exactly the supported fields with the anti-forgery header and opens the new user with a success message', async () => {
    const user = userEvent.setup();
    const { calls } = stubApi((call) =>
      call.method === 'POST' ? json({ id: 'new-1' }, 201) : json(userDetails(2, { id: 'new-1' })),
    );
    setup();
    await fillValid(user);
    await user.click(submit());
    const post = calls.find((call) => call.method === 'POST');
    expect(post?.path).toBe('/api/v1/admin/users');
    expect(post?.body).toEqual({
      username: 'ali.r',
      firstName: 'علی',
      lastName: 'رضایی',
      emailAddress: 'ali@example.test',
      cellPhoneNumber: '09123456789',
      password: 'Str0ng-pass-phrase!',
      confirmPassword: 'Str0ng-pass-phrase!',
      isActive: true,
    });
    expect(Object.keys(post?.body as object).sort()).toEqual(
      ['cellPhoneNumber', 'confirmPassword', 'emailAddress', 'firstName', 'isActive', 'lastName', 'password', 'username'],
    );
    expect(post?.headers['X-XSRF-TOKEN']).toBe('tok');
    expect(await screen.findByText('کاربر با موفقیت ایجاد شد.')).toBeInTheDocument();
    expect(window.location.pathname).toBe('/app/admin/users/new-1');
    // The password lives nowhere after the save.
    expect(window.location.href).not.toContain('Str0ng');
    expect(window.localStorage.length).toBe(0);
    expect(window.sessionStorage.length).toBe(0);
    expect(document.body.textContent).not.toContain('Str0ng');
  });

  it('sends null for empty optional fields and respects an inactive account', async () => {
    const user = userEvent.setup();
    const { calls } = stubApi((call) => (call.method === 'POST' ? json({ id: 'n2' }, 201) : json(userDetails(2))));
    setup();
    await user.type(field.username(), 'minimal');
    await user.type(field.email(), 'min@example.test');
    await user.type(field.password(), 'Str0ng-pass-phrase!');
    await user.type(field.confirm(), 'Str0ng-pass-phrase!');
    await user.click(screen.getByRole('checkbox', { name: 'حساب فعال باشد' }));
    expect(screen.getByText(/حساب غیرفعال است/)).toBeInTheDocument();
    await user.click(submit());
    await waitFor(() => expect(calls.some((call) => call.method === 'POST')).toBe(true));
    expect(calls.find((call) => call.method === 'POST')?.body).toMatchObject({ firstName: null, lastName: null, cellPhoneNumber: null, isActive: false });
  });

  it('shows the 409 duplicate message in an alert and keeps every typed value', async () => {
    const user = userEvent.setup();
    stubApi(() => json({ error: 'نام کاربری، ایمیل یا شماره همراه قبلاً ثبت شده است.' }, 409));
    setup();
    await fillValid(user);
    await user.click(submit());
    expect(await screen.findByRole('alert')).toHaveTextContent('نام کاربری، ایمیل یا شماره همراه قبلاً ثبت شده است.');
    expect(screen.getByRole('alert')).toHaveFocus();
    expect(field.username()).toHaveValue('ali.r');
    expect(field.password()).toHaveValue('Str0ng-pass-phrase!');
    expect(field.email()).toHaveValue('ali@example.test');
    expect(window.location.pathname).toBe('/app/admin/users/new');
    expect(submit()).toBeEnabled();
  });

  it('shows the generic 400 policy message (e.g. a common password) in an alert', async () => {
    const user = userEvent.setup();
    stubApi(() => json({ error: 'اطلاعات کاربر یا سیاست رمز عبور معتبر نیست.' }, 400));
    setup();
    await fillValid(user);
    await user.click(submit());
    expect(await screen.findByRole('alert')).toHaveTextContent('سیاست رمز عبور معتبر نیست');
  });

  it('maps server field errors (ProblemDetails) to their fields and clears one when edited', async () => {
    const user = userEvent.setup();
    stubApi(() =>
      json(
        {
          title: 'One or more validation errors occurred.',
          errors: { EmailAddress: ['این ایمیل مجاز نیست.'], Password: ['رمز عبور ضعیف است.'], Other: ['خطای عمومی.'] },
        },
        400,
      ),
    );
    setup();
    await fillValid(user);
    await user.click(submit());
    expect(await screen.findByText('این ایمیل مجاز نیست.')).toBeInTheDocument();
    expect(screen.getByText('رمز عبور ضعیف است.')).toBeInTheDocument();
    expect(screen.getByRole('alert')).toHaveTextContent('خطای عمومی.');
    expect(field.email()).toHaveAttribute('aria-invalid', 'true');
    // Focus goes to the first invalid control in DOM order (the password precedes the e-mail).
    expect(field.password()).toHaveFocus();
    await user.type(field.email(), 'x');
    expect(screen.queryByText('این ایمیل مجاز نیست.')).toBeNull();
  });

  it('shows a generic message when the network fails', async () => {
    const user = userEvent.setup();
    stubApi(() => {
      throw new TypeError('network');
    });
    setup();
    await fillValid(user);
    await user.click(submit());
    expect(await screen.findByRole('alert')).toHaveTextContent('ذخیرهٔ اطلاعات انجام نشد');
    expect(field.username()).toHaveValue('ali.r');
  });

  it('hands a 401 to the unauthenticated flow without showing an error', async () => {
    const user = userEvent.setup();
    onUnauthenticated.mockReturnValue(true);
    stubApi(() => new Response(null, { status: 401 }));
    setup();
    await fillValid(user);
    await user.click(submit());
    await waitFor(() => expect(onUnauthenticated).toHaveBeenCalled());
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('prevents a double submit while the request is in flight', async () => {
    const user = userEvent.setup();
    let release: (response: Response) => void = () => undefined;
    const { calls } = stubApi(() => new Promise<Response>((resolve) => (release = resolve)));
    setup();
    await fillValid(user);
    await user.dblClick(submit());
    fireEvent.submit(screen.getByRole('form', { name: 'فرم ایجاد کاربر' }));
    expect(await screen.findByRole('button', { name: 'در حال ذخیره…' })).toBeDisabled();
    expect(calls.filter((call) => call.method === 'POST')).toHaveLength(1);
    release(json({ id: 'n3' }, 201));
    await waitFor(() => expect(window.location.pathname).toBe('/app/admin/users/n3'));
    expect(calls.filter((call) => call.method === 'POST')).toHaveLength(1);
  });

  it('submits with Enter from a field (keyboard only)', async () => {
    const user = userEvent.setup();
    const { calls } = stubApi((call) => (call.method === 'POST' ? json({ id: 'n4' }, 201) : json(userDetails(2))));
    setup();
    await fillValid(user);
    await user.type(field.mobile(), '{Enter}');
    await waitFor(() => expect(calls.some((call) => call.method === 'POST')).toBe(true));
  });

  it('leaves a clean form with Cancel without asking', async () => {
    const user = userEvent.setup();
    stubApi(() => json({}));
    setup();
    await user.click(screen.getByRole('button', { name: 'انصراف' }));
    expect(screen.queryByRole('alertdialog')).toBeNull();
    expect(window.location.pathname).toBe('/app/admin/users');
  });

  it('asks before discarding a dirty form: stay keeps the values, leave goes to the list', async () => {
    const user = userEvent.setup();
    stubApi(() => json({}));
    setup();
    await user.type(field.username(), 'abc');
    await user.click(screen.getByRole('button', { name: 'انصراف' }));
    const dialog = screen.getByRole('alertdialog', { name: 'تغییرات ذخیره‌نشده' });
    await user.click(within(dialog).getByRole('button', { name: 'ادامه ویرایش' }));
    expect(screen.queryByRole('alertdialog')).toBeNull();
    expect(field.username()).toHaveValue('abc');
    expect(window.location.pathname).toBe('/app/admin/users/new');
    await user.click(screen.getByRole('button', { name: 'انصراف' }));
    await user.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'خروج بدون ذخیره' }));
    expect(window.location.pathname).toBe('/app/admin/users');
  });

  it('registers a beforeunload warning only while the form is dirty', async () => {
    const user = userEvent.setup();
    stubApi(() => json({}));
    setup();
    const clean = new Event('beforeunload', { cancelable: true });
    window.dispatchEvent(clean);
    expect(clean.defaultPrevented).toBe(false);
    await user.type(field.username(), 'a');
    const dirty = new Event('beforeunload', { cancelable: true });
    window.dispatchEvent(dirty);
    expect(dirty.defaultPrevented).toBe(true);
  });

  it('removes the beforeunload warning after the form is left', async () => {
    const user = userEvent.setup();
    stubApi(() => json({}));
    const view = setup();
    await user.type(field.username(), 'a');
    view.unmount();
    const event = new Event('beforeunload', { cancelable: true });
    window.dispatchEvent(event);
    expect(event.defaultPrevented).toBe(false);
  });

  it('never writes the password to storage, the URL or a field value other than its own', async () => {
    const user = userEvent.setup();
    stubApi(() => json({ error: 'نام کاربری، ایمیل یا شماره همراه قبلاً ثبت شده است.' }, 409));
    setup();
    await fillValid(user);
    await user.click(submit());
    await screen.findByRole('alert');
    expect(JSON.stringify({ ...window.localStorage })).not.toContain('Str0ng');
    expect(JSON.stringify({ ...window.sessionStorage })).not.toContain('Str0ng');
    expect(window.location.href).not.toContain('Str0ng');
  });
});
