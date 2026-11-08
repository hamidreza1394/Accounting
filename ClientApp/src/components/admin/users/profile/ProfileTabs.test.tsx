import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { RouteView, RouterProvider, type Route } from '../../../../lib/router';
import { PageBreadcrumbBar, PageBreadcrumbProvider } from '@/components/shell/PageBreadcrumb';
import { stubApi, antiforgeryRoute } from '../../../../test/api';
import { userDetails } from '../../../../test/adminUsers';
import { UserDetailPage } from '../UserDetailPage';

const onUnauthenticated = vi.fn((): boolean => false);

function DetailRoute() {
  return <UserDetailPage onUnauthenticated={onUnauthenticated} />;
}
const routes: Route[] = [{ path: '/admin/users/:id', element: DetailRoute, title: 'جزئیات' }];

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

const loginHistoryBody = [
  { eventType: 'LoginSucceeded', occurredAtUtc: '2026-11-02T08:00:00Z', ipAddress: '10.0.0.8' },
  { eventType: 'LoginFailed', occurredAtUtc: '2026-11-01T07:00:00Z', ipAddress: '10.0.0.9' },
];

const auditBody = (total = 2) => ({
  items: [
    {
      id: 'e1',
      userId: 'user-2',
      actorUsername: 'admin1',
      actorDisplayName: 'مدیر نصب',
      companyId: null,
      companyName: null,
      eventType: 'UserUpdated',
      entityType: 'User',
      entityId: 'user-2',
      occurredAtUtc: '2026-11-03T10:00:00Z',
      correlationId: null,
    },
  ],
  totalCount: total,
  page: 1,
  pageSize: 20,
});

describe('UserDetailPage profile workspace (stage 3)', () => {
  beforeEach(() => {
    onUnauthenticated.mockReset();
    onUnauthenticated.mockReturnValue(false);
  });

  it('loads each tab lazily: an inactive tab never fetches and a revisited tab never re-requests', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi({
      'GET /api/v1/admin/users/user-2': { status: 200, body: userDetails(2, { memberships: [] }) },
      'GET /api/v1/admin/users/user-2/login-history?take=50': { status: 200, body: loginHistoryBody },
    });
    setup();
    await screen.findByRole('tablist');
    // Only the detail request so far — the six data tabs stayed inactive.
    expect(fetchMock).toHaveBeenCalledTimes(1);

    await user.click(screen.getByRole('tab', { name: 'سوابق ورود و خروج' }));
    expect(await screen.findByRole('table', { name: 'سوابق ورود و خروج کاربر' })).toBeInTheDocument();
    const afterFirstOpen = fetchMock.mock.calls.length;
    expect(afterFirstOpen).toBe(2);

    // Switch away and back: served from the cache, no new request.
    await user.click(screen.getByRole('tab', { name: 'شرکت‌ها و عضویت‌ها' }));
    await user.click(screen.getByRole('tab', { name: 'سوابق ورود و خروج' }));
    expect(screen.getByRole('table', { name: 'سوابق ورود و خروج کاربر' })).toBeInTheDocument();
    expect(fetchMock.mock.calls.length).toBe(afterFirstOpen);
    // The login history endpoint was called exactly once in total.
    expect(fetchMock.mock.calls.filter(([input]) => String(input).includes('/login-history')).length).toBe(1);
  });

  it('shows the login history with Persian labels, an LTR IP column and no secrets', async () => {
    const user = userEvent.setup();
    stubApi({
      'GET /api/v1/admin/users/user-2': { status: 200, body: userDetails(2, { memberships: [] }) },
      'GET /api/v1/admin/users/user-2/login-history?take=50': { status: 200, body: loginHistoryBody },
    });
    setup();
    await user.click(await screen.findByRole('tab', { name: 'سوابق ورود و خروج' }));
    const table = within(await screen.findByRole('table', { name: 'سوابق ورود و خروج کاربر' }));
    expect(table.getByText('ورود موفق')).toBeInTheDocument();
    expect(table.getByText('ورود ناموفق')).toBeInTheDocument();
    expect(table.getByText('10.0.0.8')).toHaveAttribute('dir', 'ltr');
    expect(document.body.textContent).not.toMatch(/password|token|hash/i);
  });

  it('shows a retryable error state when a tab request fails, and recovers', async () => {
    const user = userEvent.setup();
    let fail = true;
    stubApi({
      'GET /api/v1/admin/users/user-2': { status: 200, body: userDetails(2, { memberships: [] }) },
      'GET /api/v1/admin/users/user-2/login-history?take=50': () => ({
        status: fail ? 500 : 200,
        body: fail ? { error: 'خطای غیرمنتظره' } : loginHistoryBody,
      }),
    });
    setup();
    await user.click(await screen.findByRole('tab', { name: 'سوابق ورود و خروج' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('خطای غیرمنتظره');
    fail = false;
    await user.click(screen.getByRole('button', { name: 'تلاش دوباره' }));
    expect(await screen.findByRole('table', { name: 'سوابق ورود و خروج کاربر' })).toBeInTheDocument();
  });

  it('shows a permission error without retry for 403 tab data', async () => {
    const user = userEvent.setup();
    stubApi({
      'GET /api/v1/admin/users/user-2': { status: 200, body: userDetails(2, { memberships: [] }) },
      'GET /api/v1/admin/users/user-2/login-history?take=50': { status: 403, body: { error: 'فقط مدیر نصب سامانه مجاز به این عملیات است.' } },
    });
    setup();
    await user.click(await screen.findByRole('tab', { name: 'سوابق ورود و خروج' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('فقط مدیر نصب سامانه مجاز به این عملیات است.');
    expect(screen.queryByRole('button', { name: 'تلاش دوباره' })).toBeNull();
  });

  it('shows the linked personnel record per active membership and hides unauthorized companies', async () => {
    stubApi({
      'GET /api/v1/admin/users/user-2': { status: 200, body: userDetails(2) },
      'GET /api/v1/companies/c1/org/employees?userId=user-2&page=1&pageSize=5': {
        status: 200,
        body: {
          items: [{ id: '11111111-1111-1111-1111-111111111111', employeeNumber: 'EMP-100', firstName: 'نام2', lastName: 'خانوادگی2', employmentStatus: 1, hiredOnUtc: null, terminatedOnUtc: null, hasLinkedUser: true }],
          totalCount: 1,
          page: 1,
          pageSize: 5,
        },
      },
      // c2 has an inactive membership, so it is never requested; a fail-closed 403 for an
      // unauthorized company renders the honest "not visible" text instead of an error.
      'GET /api/v1/companies/c3/org/employees?userId=user-2&page=1&pageSize=5': { status: 403, body: { error: 'x' } },
    });
    setup();
    await screen.findByRole('tablist');
    const panel = within(screen.getByRole('tabpanel'));
    expect(await panel.findByText(/EMP-100/)).toBeInTheDocument();
    expect(panel.getByText(/شرکت نمونه/)).toBeInTheDocument();
  });

  it('renders the subsystem catalog with the honest entitlement note and never invents a status', async () => {
    const user = userEvent.setup();
    stubApi({
      'GET /api/v1/admin/users/user-2': { status: 200, body: userDetails(2, { memberships: [] }) },
      'GET /api/v1/admin/access/catalog/subsystems': {
        status: 200,
        body: [
          { code: 'finance', title: 'مالی', entitlementScope: 'company', entitlementLinked: true, entitlementCode: 'FINANCE' },
          { code: 'sales', title: 'فروش', entitlementScope: 'company', entitlementLinked: false, entitlementCode: null },
        ],
      },
    });
    setup();
    await user.click(await screen.findByRole('tab', { name: 'زیرسیستم‌ها و لایسنس' }));
    expect(await screen.findByText('مالی')).toBeInTheDocument();
    expect(screen.getByText('فروش')).toBeInTheDocument();
    expect(screen.getByText(/اتصال لایسنس \(Entitlement\) به کاربر هنوز پیاده‌سازی نشده است/)).toBeInTheDocument();
  });

  it('shows the management change history from the audit endpoint with pagination when needed', async () => {
    const user = userEvent.setup();
    stubApi({
      'GET /api/v1/admin/users/user-2': { status: 200, body: userDetails(2, { memberships: [] }) },
      'GET /api/v1/admin/security/audit-events?userId=user-2&page=1&pageSize=20': { status: 200, body: auditBody(41) },
    });
    setup();
    await user.click(await screen.findByRole('tab', { name: 'سوابق تغییرات مدیریتی' }));
    const table = within(await screen.findByRole('table', { name: 'سوابق تغییرات مدیریتی کاربر' }));
    expect(table.getByText('ویرایش کاربر')).toBeInTheDocument();
    expect(table.getByText('مدیر نصب')).toBeInTheDocument();
    expect(await screen.findByRole('navigation', { name: 'صفحه‌بندی سوابق تغییرات' })).toBeInTheDocument();
    expect(screen.getByText(/از 3/)).toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole('button', { name: 'بعدی' })).toBeEnabled());
  });

  describe('session revocation (D-073 §1 step-up)', () => {
    const sessionBody = (extra: boolean) => [
      { sessionId: 's-1', createdAtUtc: '2026-11-05T08:00:00Z', lastSeenAtUtc: '2026-11-06T09:00:00Z' },
      ...(extra ? [{ sessionId: 's-2', createdAtUtc: '2026-11-05T10:00:00Z', lastSeenAtUtc: '2026-11-06T11:00:00Z' }] : []),
    ];

    function revokeHarness(postOutcome: () => { status: number; body?: unknown }) {
      let sessions = 2;
      return stubApi({
        ...antiforgeryRoute,
        'GET /api/v1/admin/users/user-2': { status: 200, body: userDetails(2, { memberships: [] }) },
        'GET /api/v1/admin/users/user-2/sessions': () => {
          const extra = sessions === 2;
          return { status: 200, body: sessionBody(extra) };
        },
        'POST /api/v1/admin/users/user-2/sessions/s-1/revoke': () => {
          sessions = 1;
          return postOutcome();
        },
      });
    }

    it('revokes one session only after the step-up form is filled, then reloads the real list', async () => {
      const user = userEvent.setup();
      const fetchMock = revokeHarness(() => ({ status: 204 }));
      setup();
      await user.click(await screen.findByRole('tab', { name: 'حساب کاربری و امنیت' }));
      const table = within(await screen.findByRole('table', { name: 'نشست‌های فعال کاربر' }));
      expect(table.getAllByRole('button', { name: /^ابطال نشست ایجادشده/ })).toHaveLength(2);

      const firstRevoke = table.getAllByRole('button', { name: /^ابطال نشست ایجادشده/ })[0] as HTMLElement;
      await user.click(firstRevoke);
      const dialog = screen.getByRole('alertdialog', { name: 'ابطال این نشست' });

      // Nothing filled: the request must not even be attempted.
      await user.click(within(dialog).getByRole('button', { name: 'ابطال نشست' }));
      expect(within(dialog).getByText('برای تأیید هویت، رمز عبور خودتان لازم است.')).toBeInTheDocument();
      expect(within(dialog).getByText('انتخاب دلیل ابطال الزامی است.'));
      expect(fetchMock.mock.calls.filter(([input]) => String(input).includes('/revoke')).length).toBe(0);

      await user.type(within(dialog).getByLabelText(/رمز عبور خودتان/), 'admin-secret-1');
      await user.selectOptions(within(dialog).getByLabelText(/دلیل ابطال/), 'admin_revoked_suspected_compromise');
      await user.click(within(dialog).getByRole('button', { name: 'ابطال نشست' }));

      expect(await screen.findByRole('status')).toHaveTextContent('نشست کاربر پایان یافت.');
      expect(screen.queryByRole('alertdialog')).toBeNull();
      const revokeCalls = fetchMock.mock.calls.filter(([input]) => String(input).includes('/revoke'));
      expect(revokeCalls.length).toBe(1);
      const revokeBody = revokeCalls[0]?.[1]?.body;
      expect(typeof revokeBody).toBe('string');
      expect(JSON.parse(String(revokeBody))).toEqual({
        currentPassword: 'admin-secret-1',
        reasonCode: 'admin_revoked_suspected_compromise',
      });
      // The list was refetched and now shows the remaining session only.
      await waitFor(() =>
        expect(within(screen.getByRole('table', { name: 'نشست‌های فعال کاربر' })).getAllByRole('button', { name: /^ابطال نشست ایجادشده/ })).toHaveLength(1),
      );
      // The step-up secret never leaks into the document text.
      expect(document.body.textContent).not.toContain('admin-secret-1');
    });

    it('keeps the dialog open with the entered values and the real server message when re-authentication fails', async () => {
      const user = userEvent.setup();
      revokeHarness(() => ({ status: 403, body: { error: 'رمز عبور مدیر نادرست است.' } }));
      setup();
      await user.click(await screen.findByRole('tab', { name: 'حساب کاربری و امنیت' }));
      const table = await screen.findByRole('table', { name: 'نشست‌های فعال کاربر' });
      const revokeButtons = within(table).getAllByRole('button', { name: /^ابطال نشست ایجادشده/ });
      await user.click(revokeButtons[0] as HTMLElement);
      const dialog = screen.getByRole('alertdialog', { name: 'ابطال این نشست' });
      await user.type(within(dialog).getByLabelText(/رمز عبور خودتان/), 'wrong-password');
      await user.selectOptions(within(dialog).getByLabelText(/دلیل ابطال/), 'admin_revoked_administrative');
      await user.click(within(dialog).getByRole('button', { name: 'ابطال نشست' }));

      expect(await within(dialog).findByRole('alert')).toHaveTextContent('رمز عبور مدیر نادرست است.');
      // The values survive the failure so the retry is a correction, not a retyping.
      expect(within(dialog).getByLabelText(/رمز عبور خودتان/)).toHaveValue('wrong-password');
      expect(within(dialog).getByLabelText(/دلیل ابطال/)).toHaveValue('admin_revoked_administrative');
      expect(screen.getByRole('alertdialog', { name: 'ابطال این نشست' })).toBeInTheDocument();
    });

    it('revokes every active session with one step-up and reports the server count', async () => {
      const user = userEvent.setup();
      const fetchMock = stubApi({
        ...antiforgeryRoute,
        'GET /api/v1/admin/users/user-2': { status: 200, body: userDetails(2, { memberships: [] }) },
        'GET /api/v1/admin/users/user-2/sessions': { status: 200, body: sessionBody(true) },
        'POST /api/v1/admin/users/user-2/sessions/revoke-all': { status: 200, body: { revokedCount: 2 } },
      });
      setup();
      await user.click(await screen.findByRole('tab', { name: 'حساب کاربری و امنیت' }));
      await screen.findByRole('table', { name: 'نشست‌های فعال کاربر' });
      await user.click(screen.getByRole('button', { name: /ابطال همهٔ نشست‌های فعال/ }));
      const dialog = screen.getByRole('alertdialog', { name: 'ابطال همهٔ نشست‌های فعال' });
      await user.type(within(dialog).getByLabelText(/رمز عبور خودتان/), 'admin-secret-2');
      await user.selectOptions(within(dialog).getByLabelText(/دلیل ابطال/), 'admin_revoked_offboarding');
      await user.click(within(dialog).getByRole('button', { name: 'ابطال نشست' }));

      expect(await screen.findByRole('status')).toHaveTextContent('۲ نشست فعال این کاربر پایان یافت.');
      const revokeCalls = fetchMock.mock.calls.filter(([input]) => String(input).includes('/revoke-all'));
      expect(revokeCalls.length).toBe(1);
      const revokeBody = revokeCalls[0]?.[1]?.body;
      expect(typeof revokeBody).toBe('string');
      expect(JSON.parse(String(revokeBody))).toEqual({
        currentPassword: 'admin-secret-2',
        reasonCode: 'admin_revoked_offboarding',
      });
    });
  });
});
