import { useState } from 'react';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import type { SelectedContext, Session } from '../api/types';
import { antiforgeryRoute, stubApi, type RouteHandler } from '../test/api';
import { AppShell } from './AppShell';
import { MyAccountPanel } from './MyAccountPanel';

const session: Session = { username: 'alice', fullName: 'آلیس مثالی', accountKind: 'User', isAdministrator: false };
const context: SelectedContext = {
  company: { id: 'company-1', name: 'شرکت نمونه', nationalId: null, roleName: 'کاربر' },
  fiscalPeriod: { id: 'period-1', title: 'سال مالی ۱۴۰۵', startDate: '2026-03-21', endDate: '2027-03-20', status: 'Open', isSelectable: true, accessMode: 'readWrite' },
};

const profileBody = {
  username: 'alice', firstName: 'آلیس', lastName: 'مثالی', emailAddress: 'alice@example.test', cellPhoneNumber: null,
  isEmailAddressVerified: true, isCellPhoneNumberVerified: false,
  lastLoginDateTime: '2026-11-02T08:00:00Z', insertDateTime: '2026-10-01T09:00:00Z',
};

function historyItems(count: number) {
  return Array.from({ length: count }, (_, index) => ({
    eventType: 'LoginSucceeded',
    occurredAtUtc: new Date(Date.UTC(2026, 10, 2, 8, 0, 0) - index * 60000).toISOString(),
    ipAddress: '203.0.113.7',
  }));
}

const currentOnly = [{ sessionId: 'only', isCurrent: true, createdAtUtc: '2026-11-03T08:00:00Z', lastSeenAtUtc: '2026-11-03T09:00:00Z' }];
const profileRoute: Record<string, RouteHandler> = {
  'GET /api/v1/me/profile': { status: 200, body: profileBody },
  // The security tab also lists the sessions; tests that do not care about them see only this device.
  'GET /api/v1/me/sessions': { status: 200, body: currentOnly },
};
const stableUnauthenticated = vi.fn(() => false);

function Host({ onPasswordChanged }: { onPasswordChanged: () => void }) {
  const [open, setOpen] = useState(false);
  return (
    <div>
      <button type="button" onClick={() => setOpen(true)}>باز کردن حساب</button>
      <MyAccountPanel open={open} onClose={() => setOpen(false)} onPasswordChanged={onPasswordChanged} onUnauthenticated={stableUnauthenticated} />
    </div>
  );
}

async function openPanel(onPasswordChanged: () => void = vi.fn()) {
  const user = userEvent.setup();
  render(<Host onPasswordChanged={onPasswordChanged} />);
  await user.click(screen.getByRole('button', { name: 'باز کردن حساب' }));
  await screen.findByRole('dialog', { name: 'حساب کاربری من' });
  return user;
}

function postCalls(fetchMock: ReturnType<typeof stubApi>) {
  return fetchMock.mock.calls.filter(([url, init]) => url === '/api/v1/me/password' && init?.method === 'POST');
}

const field = {
  current: () => screen.getByLabelText(/^رمز فعلی/),
  next: () => screen.getByLabelText(/^رمز جدید/),
  confirm: () => screen.getByLabelText(/^تکرار رمز جدید/),
};
const submitButton = () => screen.getByRole('button', { name: 'تغییر رمز و خروج' });

async function openSecurity() {
  const user = await openPanel();
  await user.click(screen.getByRole('tab', { name: 'امنیت حساب' }));
  return user;
}

describe('MyAccountPanel in the app shell', () => {
  it('shows only the authenticated profile and loads its own history on demand', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({ ...profileBody, emailAddress: null, isEmailAddressVerified: false }), { status: 200, headers: { 'Content-Type': 'application/json' } }))
      .mockResolvedValueOnce(new Response(JSON.stringify([]), { status: 200, headers: { 'Content-Type': 'application/json' } }));
    vi.stubGlobal('fetch', fetchMock);

    const trigger = render(<AppShell session={session} context={context} onChangeContext={vi.fn()} onLogout={vi.fn().mockResolvedValue(undefined)}><main>محتوای آزمایشی</main></AppShell>);
    fireEvent.click(screen.getByRole('button', { name: 'حساب کاربری من' }));

    const dialog = await screen.findByRole('dialog', { name: 'حساب کاربری من' });
    expect((await screen.findAllByText('آلیس مثالی')).length).toBeGreaterThan(0);
    fireEvent.click(screen.getByRole('tab', { name: 'سوابق ورود' }));
    expect(await screen.findByText('سابقه‌ای ثبت نشده است')).toBeInTheDocument();
    expect(fetchMock.mock.calls[1]?.[0]).toBe('/api/v1/me/login-history?take=50');

    fireEvent.keyDown(dialog, { key: 'Escape' });
    expect(screen.queryByRole('dialog')).toBeNull();
    expect(trigger.getByRole('button', { name: 'حساب کاربری من' })).toHaveAttribute('aria-expanded', 'false');
  });
});

describe('MyAccountPanel dialog behavior', () => {
  it('is a modal dialog, locks body scroll, focuses the close button, and restores everything on close', async () => {
    stubApi(profileRoute);
    const user = await openPanel();
    const dialog = screen.getByRole('dialog', { name: 'حساب کاربری من' });
    expect(dialog).toHaveAttribute('aria-modal', 'true');
    expect(document.body.style.overflow).toBe('hidden');
    expect(screen.getByRole('button', { name: 'بستن' })).toHaveFocus();

    await user.keyboard('{Escape}');
    expect(screen.queryByRole('dialog')).toBeNull();
    expect(document.body.style.overflow).not.toBe('hidden');
  });

  it('returns focus to the opener after Escape, the close button and the overlay', async () => {
    stubApi(profileRoute);
    const user = await openPanel();
    const opener = screen.getByRole('button', { name: 'باز کردن حساب' });

    await user.keyboard('{Escape}');
    expect(opener).toHaveFocus();

    await user.click(opener);
    await screen.findByRole('dialog');
    await user.click(screen.getByRole('button', { name: 'بستن' }));
    expect(screen.queryByRole('dialog')).toBeNull();
    expect(opener).toHaveFocus();

    await user.click(opener);
    await screen.findByRole('dialog');
    fireEvent.click(screen.getByRole('button', { name: 'بستن پنجره حساب کاربری' }));
    expect(screen.queryByRole('dialog')).toBeNull();
    expect(opener).toHaveFocus();
  });

  it('keeps Tab and Shift+Tab inside the drawer', async () => {
    stubApi(profileRoute);
    const user = await openPanel();
    const close = screen.getByRole('button', { name: 'بستن' });
    expect(close).toHaveFocus();

    await user.tab({ shift: true });
    const dialog = screen.getByRole('dialog');
    expect(dialog.contains(document.activeElement)).toBe(true);
    expect(document.activeElement).not.toBe(close);
    await user.tab();
    expect(close).toHaveFocus();
  });
});

describe('MyAccountPanel tabs and profile', () => {
  it('moves between tabs with arrow keys in RTL, Home and End', async () => {
    stubApi(profileRoute);
    const user = await openPanel();
    screen.getByRole('tab', { name: 'مشخصات من' }).focus();

    await user.keyboard('{ArrowLeft}');
    expect(screen.getByRole('tab', { name: 'امنیت حساب' })).toHaveFocus();
    expect(screen.getByRole('tab', { name: 'امنیت حساب' })).toHaveAttribute('aria-selected', 'true');
    await user.keyboard('{ArrowRight}');
    expect(screen.getByRole('tab', { name: 'مشخصات من' })).toHaveFocus();
    await user.keyboard('{End}');
    expect(screen.getByRole('tab', { name: 'سوابق ورود' })).toHaveFocus();
    await user.keyboard('{Home}');
    expect(screen.getByRole('tab', { name: 'مشخصات من' })).toHaveAttribute('aria-selected', 'true');
  });

  it('shows read-only profile fields with verification badges and no edit controls', async () => {
    stubApi(profileRoute);
    await openPanel();
    expect(await screen.findByText('alice@example.test')).toBeInTheDocument();
    expect(screen.getByText('تأیید شده')).toBeInTheDocument();
    expect(screen.getAllByText('ثبت نشده').length).toBeGreaterThan(0);
    expect(screen.getByText('فقط‌خواندنی')).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('تغییر ایمیل و شماره همراه در این صفحه در دسترس نیست.');
    const panel = screen.getByRole('tabpanel');
    expect(within(panel).queryAllByRole('textbox')).toHaveLength(0);
  });

  it('marks an unverified contact as not verified and offers retry when the profile fails', async () => {
    let calls = 0;
    stubApi({
      'GET /api/v1/me/profile': () => (++calls === 1 ? { status: 500 } : { status: 200, body: { ...profileBody, cellPhoneNumber: '09120000000' } }),
    });
    const user = await openPanel();
    expect(await screen.findByRole('alert')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'تلاش دوباره' }));
    expect(await screen.findByText('09120000000')).toBeInTheDocument();
    expect(screen.getByText('تأیید نشده')).toBeInTheDocument();
  });
});

describe('MyAccountPanel security tab', () => {
  it('validates before sending: each rule shows an error on the right field and sends nothing', async () => {
    const fetchMock = stubApi({ ...profileRoute, ...antiforgeryRoute, 'POST /api/v1/me/password': { status: 204 } });
    const user = await openSecurity();

    await user.click(submitButton());
    expect(screen.getByText('رمز فعلی را وارد کنید.')).toBeInTheDocument();
    expect(screen.getByText('رمز جدید را وارد کنید.')).toBeInTheDocument();
    expect(screen.getByText('تکرار رمز جدید را وارد کنید.')).toBeInTheDocument();
    expect(field.current()).toHaveFocus();
    expect(field.current()).toHaveAttribute('aria-invalid', 'true');

    await user.type(field.current(), 'OldPass#1-Long-2026!');
    await user.type(field.next(), 'short');
    await user.type(field.confirm(), 'short');
    await user.click(submitButton());
    expect(screen.getByText(/رمز جدید باید دست‌کم/)).toBeInTheDocument();
    expect(field.next()).toHaveFocus();

    await user.clear(field.next());
    await user.type(field.next(), 'OldPass#1-Long-2026!');
    await user.click(submitButton());
    expect(screen.getByText('رمز جدید نباید با رمز فعلی یکسان باشد.')).toBeInTheDocument();

    await user.clear(field.next());
    await user.type(field.next(), 'NewPass#1234567');
    await user.clear(field.confirm());
    await user.type(field.confirm(), 'Different#12345');
    await user.click(submitButton());
    expect(screen.getByText('تکرار رمز جدید با رمز جدید یکسان نیست.')).toBeInTheDocument();
    expect(field.confirm()).toHaveFocus();
    expect(field.confirm()).toHaveAccessibleDescription(/یکسان نیست/);

    expect(postCalls(fetchMock)).toHaveLength(0);
  });

  it('toggles each password field with a fixed label and aria-pressed', async () => {
    stubApi(profileRoute);
    const user = await openSecurity();
    const toggle = screen.getByRole('button', { name: 'نمایش رمز فعلی' });
    expect(toggle).toHaveAttribute('aria-pressed', 'false');
    expect(field.current()).toHaveAttribute('type', 'password');
    await user.click(toggle);
    expect(screen.getByRole('button', { name: 'نمایش رمز فعلی' })).toHaveAttribute('aria-pressed', 'true');
    expect(field.current()).toHaveAttribute('type', 'text');
    expect(field.next()).toHaveAttribute('type', 'password');
    expect(field.current()).toHaveAttribute('dir', 'ltr');
  });

  it('maps server field errors to the fields, focuses the first invalid one, and clears a rejected current password', async () => {
    stubApi({
      ...profileRoute,
      ...antiforgeryRoute,
      'POST /api/v1/me/password': {
        status: 400,
        body: { title: 'One or more validation errors occurred.', errors: { CurrentPassword: ['رمز فعلی نادرست است.'], NewPassword: ['این رمز بسیار رایج است.'] } },
      },
    });
    const user = await openSecurity();
    await user.type(field.current(), 'WrongOld#1');
    await user.type(field.next(), 'NewPass#1234567');
    await user.type(field.confirm(), 'NewPass#1234567');
    await user.click(submitButton());

    expect(await screen.findByText('رمز فعلی نادرست است.')).toBeInTheDocument();
    expect(screen.getByText('این رمز بسیار رایج است.')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).toBeNull();
    expect(field.current()).toHaveFocus();
    expect(field.current()).toHaveValue('');
    expect(field.next()).toHaveValue('NewPass#1234567');
    expect(submitButton()).toBeEnabled();
  });

  it('maps a confirm-field server error to the confirm field', async () => {
    stubApi({
      ...profileRoute,
      ...antiforgeryRoute,
      'POST /api/v1/me/password': { status: 400, body: { errors: { ConfirmNewPassword: ['تکرار رمز یکسان نیست.'] } } },
    });
    const user = await openSecurity();
    await user.type(field.current(), 'OldPass#1');
    await user.type(field.next(), 'NewPass#1234567');
    await user.type(field.confirm(), 'NewPass#1234567');
    await user.click(submitButton());
    expect(await screen.findByText('تکرار رمز یکسان نیست.')).toBeInTheDocument();
    expect(field.confirm()).toHaveAccessibleDescription(/یکسان نیست/);
    expect(field.confirm()).toHaveFocus();
  });

  it('shows non-field server errors in an alert and moves focus to it', async () => {
    stubApi({
      ...profileRoute,
      ...antiforgeryRoute,
      'POST /api/v1/me/password': { status: 429, body: { error: 'تعداد تلاش‌ها زیاد است.' } },
    });
    const user = await openSecurity();
    await user.type(field.current(), 'OldPass#1');
    await user.type(field.next(), 'NewPass#1234567');
    await user.type(field.confirm(), 'NewPass#1234567');
    await user.click(submitButton());
    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('تعداد تلاش‌ها زیاد است.');
    expect(alert).toHaveFocus();
    // Any non-401 API rejection may be a rejected current password, so it is never kept on screen.
    expect(field.current()).toHaveValue('');
    expect(field.next()).toHaveValue('NewPass#1234567');
  });

  it('pairs the toggle placement with the input padding in one LTR wrapper', async () => {
    stubApi(profileRoute);
    const user = await openSecurity();
    const input = field.current();
    const wrapper = input.parentElement!;
    const toggle = screen.getByRole('button', { name: 'نمایش رمز فعلی' });
    expect(wrapper).toHaveAttribute('dir', 'ltr');
    expect(input).toHaveAttribute('dir', 'ltr');
    expect(toggle.parentElement).toBe(wrapper);
    expect(toggle).toHaveClass('start-0');
    expect(input).toHaveClass('ps-11');
    expect(toggle).not.toHaveClass('end-0');
    expect(input).not.toHaveClass('pe-11');
    await user.click(toggle);
  });

  it('maps prefixed server field keys such as $.newPassword and request.confirmNewPassword', async () => {
    stubApi({
      ...profileRoute,
      ...antiforgeryRoute,
      'POST /api/v1/me/password': { status: 400, body: { errors: { '$.newPassword': ['رمز ضعیف است.'], 'request.confirmNewPassword': ['تکرار نادرست است.'] } } },
    });
    const user = await openSecurity();
    await user.type(field.current(), 'OldPass#1');
    await user.type(field.next(), 'NewPass#1234567');
    await user.type(field.confirm(), 'NewPass#1234567');
    await user.click(submitButton());
    expect(await screen.findByText('رمز ضعیف است.')).toBeInTheDocument();
    expect(field.next()).toHaveAccessibleDescription(/رمز ضعیف است/);
    expect(field.confirm()).toHaveAccessibleDescription(/تکرار نادرست است/);
    expect(screen.queryByRole('alert')).toBeNull();
    expect(field.next()).toHaveFocus();
  });

  it('clears the current password after a generic 4xx rejection', async () => {
    stubApi({ ...profileRoute, ...antiforgeryRoute, 'POST /api/v1/me/password': { status: 400, body: { error: 'درخواست نامعتبر است.' } } });
    const user = await openSecurity();
    await user.type(field.current(), 'OldPass#1');
    await user.type(field.next(), 'NewPass#1234567');
    await user.type(field.confirm(), 'NewPass#1234567');
    await user.click(submitButton());
    expect(await screen.findByRole('alert')).toHaveTextContent('درخواست نامعتبر است.');
    expect(field.current()).toHaveValue('');
  });

  it('blocks leaving the tab and a second submit while the request is in flight, and unlocks after a failure', async () => {
    let release: (response: Response) => void = () => {};
    const pending = new Promise<Response>((resolve) => { release = resolve; });
    const fetchMock = vi.fn((input: string, init?: RequestInit) => {
      if (input === '/api/v1/me/profile') return Promise.resolve(new Response(JSON.stringify(profileBody), { status: 200, headers: { 'Content-Type': 'application/json' } }));
      if (input === '/api/v1/antiforgery/token') return Promise.resolve(new Response(JSON.stringify({ headerName: 'X-XSRF-TOKEN', requestToken: 't' }), { status: 200, headers: { 'Content-Type': 'application/json' } }));
      if (input === '/api/v1/me/password' && init?.method === 'POST') return pending;
      return Promise.resolve(new Response(null, { status: 404 }));
    });
    vi.stubGlobal('fetch', fetchMock);
    const user = await openPanel();
    await user.click(screen.getByRole('tab', { name: 'امنیت حساب' }));
    await user.type(field.current(), 'OldPass#1');
    await user.type(field.next(), 'NewPass#1234567');
    await user.type(field.confirm(), 'NewPass#1234567');
    await user.click(submitButton());
    await screen.findByRole('button', { name: 'در حال ثبت…' });

    expect(screen.getByRole('tab', { name: 'مشخصات من' })).toBeDisabled();
    expect(screen.getByRole('tab', { name: 'سوابق ورود' })).toBeDisabled();
    await user.click(screen.getByRole('tab', { name: 'مشخصات من' }));
    expect(screen.getByRole('tab', { name: 'امنیت حساب' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('button', { name: 'در حال ثبت…' })).toBeDisabled();

    release(new Response(JSON.stringify({ error: 'خطا' }), { status: 400, headers: { 'Content-Type': 'application/json' } }));
    await waitFor(() => expect(screen.getByRole('tab', { name: 'مشخصات من' })).toBeEnabled());
    expect(submitButton()).toBeEnabled();
    expect(fetchMock.mock.calls.filter(([url, init]) => url === '/api/v1/me/password' && init?.method === 'POST')).toHaveLength(1);
  });

  it('starts on the profile tab after closing and reopening', async () => {
    stubApi(profileRoute);
    const user = await openSecurity();
    await user.keyboard('{Escape}');
    await user.click(screen.getByRole('button', { name: 'باز کردن حساب' }));
    expect(await screen.findByRole('tab', { name: 'مشخصات من' })).toHaveAttribute('aria-selected', 'true');
  });

  it('prevents a double submit while the request is pending', async () => {
    let release: (response: Response) => void = () => {};
    const pending = new Promise<Response>((resolve) => { release = resolve; });
    const fetchMock = vi.fn((input: string, init?: RequestInit) => {
      if (input === '/api/v1/me/profile') return Promise.resolve(new Response(JSON.stringify(profileBody), { status: 200, headers: { 'Content-Type': 'application/json' } }));
      if (input === '/api/v1/antiforgery/token') return Promise.resolve(new Response(JSON.stringify({ headerName: 'X-XSRF-TOKEN', requestToken: 't' }), { status: 200, headers: { 'Content-Type': 'application/json' } }));
      if (input === '/api/v1/me/password' && init?.method === 'POST') return pending;
      return Promise.resolve(new Response(null, { status: 404 }));
    });
    vi.stubGlobal('fetch', fetchMock);
    const onPasswordChanged = vi.fn();
    const user = await openPanel(onPasswordChanged);
    await user.click(screen.getByRole('tab', { name: 'امنیت حساب' }));
    await user.type(field.current(), 'OldPass#1');
    await user.type(field.next(), 'NewPass#1234567');
    await user.type(field.confirm(), 'NewPass#1234567');

    const form = submitButton().closest('form')!;
    fireEvent.submit(form);
    fireEvent.submit(form);
    await user.click(screen.getByRole('button', { name: 'در حال ثبت…' }));
    await waitFor(() => expect(fetchMock.mock.calls.filter(([url, init]) => url === '/api/v1/me/password' && init?.method === 'POST')).toHaveLength(1));
    expect(screen.getByRole('button', { name: 'در حال ثبت…' })).toBeDisabled();

    release(new Response(null, { status: 204 }));
    await waitFor(() => expect(onPasswordChanged).toHaveBeenCalledTimes(1));
  });

  it('sends the three values on success, clears the fields and calls the sign-in redirect once', async () => {
    const onPasswordChanged = vi.fn();
    const fetchMock = stubApi({ ...profileRoute, ...antiforgeryRoute, 'POST /api/v1/me/password': { status: 204 } });
    const user = await openPanel(onPasswordChanged);
    await user.click(screen.getByRole('tab', { name: 'امنیت حساب' }));
    await user.type(field.current(), 'OldPass#1');
    await user.type(field.next(), 'NewPass#1234567');
    await user.type(field.confirm(), 'NewPass#1234567');
    await user.click(submitButton());

    await waitFor(() => expect(onPasswordChanged).toHaveBeenCalledTimes(1));
    const [, init] = postCalls(fetchMock)[0]!;
    expect(JSON.parse(String(init?.body))).toEqual({ currentPassword: 'OldPass#1', newPassword: 'NewPass#1234567', confirmNewPassword: 'NewPass#1234567' });
    expect(field.current()).toHaveValue('');
    expect(field.next()).toHaveValue('');
    expect(field.confirm()).toHaveValue('');
    expect(localStorage.length).toBe(0);
    expect(sessionStorage.length).toBe(0);
  });

  it('clears the form with the clear button', async () => {
    stubApi(profileRoute);
    const user = await openSecurity();
    await user.type(field.current(), 'abc');
    await user.click(screen.getByRole('button', { name: 'پاک‌کردن' }));
    expect(field.current()).toHaveValue('');
  });
});

describe('MyAccountPanel active sessions', () => {
  const sessionRows = [
    { sessionId: 's-1', isCurrent: true, createdAtUtc: '2026-11-03T08:00:00Z', lastSeenAtUtc: '2026-11-03T09:00:00Z' },
    { sessionId: 's-2', isCurrent: false, createdAtUtc: '2026-11-02T08:00:00Z', lastSeenAtUtc: '2026-11-02T09:00:00Z' },
  ];

  it('lists the sessions in the security tab and leaves for login after ending this device', async () => {
    const fetchMock = stubApi({
      ...profileRoute,
      ...antiforgeryRoute,
      'GET /api/v1/me/sessions': { status: 200, body: sessionRows },
      'POST /api/v1/me/sessions/s-1/revoke': { status: 204 },
    });
    const onSignedOut = vi.fn();
    const user = await openPanel(onSignedOut);
    await user.click(screen.getByRole('tab', { name: 'امنیت حساب' }));
    expect(await screen.findByRole('list', { name: 'نشست‌های فعال' })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'خروج از این دستگاه' }));
    await user.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'خروج از این دستگاه' }));
    await waitFor(() => expect(onSignedOut).toHaveBeenCalledTimes(1));
    expect(fetchMock.mock.calls.some(([url, init]) => url === '/api/v1/me/sessions/s-1/revoke' && init?.method === 'POST')).toBe(true);
  });
});

describe('MyAccountPanel history tab', () => {
  it('shows a loading state, then the events with text status and left-to-right IP', async () => {
    let release: (response: Response) => void = () => {};
    const pending = new Promise<Response>((resolve) => { release = resolve; });
    vi.stubGlobal('fetch', vi.fn((input: string) => {
      if (input === '/api/v1/me/profile') return Promise.resolve(new Response(JSON.stringify(profileBody), { status: 200, headers: { 'Content-Type': 'application/json' } }));
      if (input === '/api/v1/me/login-history?take=50') return pending;
      return Promise.resolve(new Response(null, { status: 404 }));
    }));
    const user = await openPanel();
    await user.click(screen.getByRole('tab', { name: 'سوابق ورود' }));
    expect(screen.getByText('در حال دریافت سوابق…')).toBeInTheDocument();
    release(new Response(JSON.stringify([
      { eventType: 'LoginSucceeded', occurredAtUtc: '2026-11-02T08:00:00Z', ipAddress: '203.0.113.7' },
      { eventType: 'LoginFailed', occurredAtUtc: '2026-11-01T08:00:00Z', ipAddress: null },
      { eventType: 'LoginDeniedInactive', occurredAtUtc: '2026-10-31T08:00:00Z', ipAddress: null },
      { eventType: 'LoginDeniedEnrollmentPending', occurredAtUtc: '2026-10-30T08:00:00Z', ipAddress: null },
      { eventType: 'LogoutSucceeded', occurredAtUtc: '2026-10-29T08:00:00Z', ipAddress: null },
    ]), { status: 200, headers: { 'Content-Type': 'application/json' } }));
    const list = await screen.findByRole('list', { name: 'سوابق ورود و خروج' });
    const items = within(list).getAllByRole('listitem');
    expect(items).toHaveLength(5);
    for (const [index, label] of ['ورود موفق', 'ورود ناموفق', 'حساب غیرفعال', 'فعال‌سازی تکمیل نشده', 'خروج از حساب'].entries()) {
      expect(items[index]).toHaveTextContent(label);
    }
    expect(within(items[0]!).getByText('203.0.113.7')).toHaveAttribute('dir', 'ltr');
    expect(items[1]).toHaveTextContent('IP ثبت نشده');
    expect(screen.queryByRole('button', { name: 'نمایش بیشتر' })).toBeNull();
    expect(screen.queryByText('در حال دریافت سوابق…')).toBeNull();
  });

  it('shows the empty state', async () => {
    stubApi({ ...profileRoute, 'GET /api/v1/me/login-history?take=50': { status: 200, body: [] } });
    const user = await openPanel();
    await user.click(screen.getByRole('tab', { name: 'سوابق ورود' }));
    expect(await screen.findByText('سابقه‌ای ثبت نشده است')).toBeInTheDocument();
  });

  it('shows an error with retry and recovers', async () => {
    let calls = 0;
    stubApi({
      ...profileRoute,
      'GET /api/v1/me/login-history?take=50': () => (++calls === 1 ? { status: 500 } : { status: 200, body: historyItems(2) }),
    });
    const user = await openPanel();
    await user.click(screen.getByRole('tab', { name: 'سوابق ورود' }));
    expect(await screen.findByRole('alert')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'تلاش دوباره' }));
    expect(await screen.findAllByRole('listitem')).toHaveLength(2);
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('offers show more at 50 events, requests 100, then notes the cap', async () => {
    const fetchMock = stubApi({
      ...profileRoute,
      'GET /api/v1/me/login-history?take=50': { status: 200, body: historyItems(50) },
      'GET /api/v1/me/login-history?take=100': { status: 200, body: historyItems(100) },
    });
    const user = await openPanel();
    await user.click(screen.getByRole('tab', { name: 'سوابق ورود' }));
    expect(await screen.findAllByRole('listitem')).toHaveLength(50);
    expect(screen.queryByRole('note')).toBeNull();

    await user.click(screen.getByRole('button', { name: 'نمایش بیشتر' }));
    await waitFor(() => expect(screen.getAllByRole('listitem')).toHaveLength(100));
    expect(fetchMock.mock.calls.some(([url]) => url === '/api/v1/me/login-history?take=100')).toBe(true);
    expect(screen.queryByRole('button', { name: 'نمایش بیشتر' })).toBeNull();
    expect(screen.getByRole('note')).toHaveTextContent('حداکثر ۱۰۰ رویداد اخیر');
  });

  it('aborts the history request when the tab is left before it finishes', async () => {
    let historySignal: AbortSignal | null | undefined;
    const fetchMock = vi.fn((input: string, init?: RequestInit) => {
      if (input === '/api/v1/me/profile') return Promise.resolve(new Response(JSON.stringify(profileBody), { status: 200, headers: { 'Content-Type': 'application/json' } }));
      if (input.startsWith('/api/v1/me/login-history')) {
        historySignal = init?.signal;
        return new Promise<Response>(() => {});
      }
      return Promise.resolve(new Response(null, { status: 404 }));
    });
    vi.stubGlobal('fetch', fetchMock);
    const user = await openPanel();
    await user.click(screen.getByRole('tab', { name: 'سوابق ورود' }));
    await waitFor(() => expect(historySignal).toBeDefined());
    expect(historySignal?.aborted).toBe(false);
    await user.click(screen.getByRole('tab', { name: 'مشخصات من' }));
    expect(historySignal?.aborted).toBe(true);
  });
});
