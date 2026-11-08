import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { RouteView, RouterProvider, type Route } from '../../../../lib/router';
import { PageBreadcrumbBar, PageBreadcrumbProvider } from '@/components/shell/PageBreadcrumb';
import { stubApi, antiforgeryRoute, type Route as StubRoute } from '../../../../test/api';
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
      'GET /api/v1/admin/users/user-2/membership-requests?page=1&pageSize=10': {
        status: 200,
        body: { items: [], totalCount: 0, page: 1, pageSize: 10 },
      },
    });
    setup();
    await screen.findByRole('tablist');
    // Only the detail request so far — the data tabs stayed inactive.
    expect(fetchMock).toHaveBeenCalledTimes(1);

    await user.click(screen.getByRole('tab', { name: 'سوابق ورود و خروج' }));
    expect(await screen.findByRole('table', { name: 'سوابق ورود و خروج کاربر' })).toBeInTheDocument();
    const afterFirstOpen = fetchMock.mock.calls.length;
    expect(afterFirstOpen).toBe(2);

    // The memberships tab fetches its request section once, then serves it from the cache.
    await user.click(screen.getByRole('tab', { name: 'شرکت‌ها و عضویت‌ها' }));
    await screen.findByText('درخواست عضویتی برای این حساب ثبت نشده است.');
    const afterMemberships = fetchMock.mock.calls.length;
    expect(afterMemberships).toBe(3);
    expect(fetchMock.mock.calls.filter(([input]) => String(input).includes('/membership-requests')).length).toBe(1);

    await user.click(screen.getByRole('tab', { name: 'سوابق ورود و خروج' }));
    expect(screen.getByRole('table', { name: 'سوابق ورود و خروج کاربر' })).toBeInTheDocument();
    expect(fetchMock.mock.calls.length).toBe(afterMemberships);
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

  describe('personnel record link management (PR 3-C2)', () => {
    const hrEmployee = {
      id: '22222222-2222-2222-2222-222222222222',
      employeeNumber: 'EMP-2',
      firstName: 'آزاد',
      lastName: 'کارمند',
      employmentStatus: 1,
      hiredOnUtc: null,
      terminatedOnUtc: null,
      hasLinkedUser: false,
    };
    const hrPage = (items: unknown[]) => ({ items, totalCount: items.length, page: 1, pageSize: 5 });

    const freeEmployeePath = '/org/employees/22222222-2222-2222-2222-222222222222/user';

    function hrHarness(
      connected: { status: number; body?: unknown } | (() => { status: number; body?: unknown }),
      mutations: { put?: StubRoute | (() => StubRoute); del?: StubRoute | (() => StubRoute) } = {},
    ) {
      return stubApi({
        ...antiforgeryRoute,
        'GET /api/v1/admin/users/user-2': { status: 200, body: userDetails(2) },
        'GET /api/v1/companies/c1/org/employees?userId=user-2&page=1&pageSize=5': () =>
          typeof connected === 'function' ? connected() : connected,
        ...(mutations.put ? { [`PUT /api/v1/companies/c1${freeEmployeePath}`]: mutations.put } : {}),
        ...(mutations.del ? { [`DELETE /api/v1/companies/c1${freeEmployeePath}`]: mutations.del } : {}),
        // The picker search: one already-connected employee must never be offered here.
        'GET /api/v1/companies/c1/org/employees?search=EMP&page=1&pageSize=25': {
          status: 200,
          body: {
            items: [
              hrEmployee,
              { ...hrEmployee, id: '33333333-3333-3333-3333-333333333333', employeeNumber: 'EMP-9', hasLinkedUser: true },
            ],
            totalCount: 2,
            page: 1,
            pageSize: 25,
          },
        },
      });
    }

    it('connects an employee without a linked account through the search picker, then reloads the section', async () => {
      const user = userEvent.setup();
      let linked = false;
      const fetchMock = hrHarness(
        () => ({ status: 200, body: hrPage(linked ? [{ ...hrEmployee, hasLinkedUser: true }] : []) }),
        { put: () => { linked = true; return { status: 204 }; } },
      );
      setup();
      await screen.findByRole('tablist');
      const panel = within(screen.getByRole('tabpanel'));
      expect(await panel.findByText('پرونده پرسنلی متصل نشده است.')).toBeInTheDocument();
      await user.click(panel.getByRole('button', { name: 'اتصال پرونده پرسنلی در شرکت نمونه' }));

      const dialog = screen.getByRole('dialog', { name: 'اتصال پرونده پرسنلی — شرکت نمونه' });
      // Confirming without a pick must not send anything.
      await user.click(within(dialog).getByRole('button', { name: 'اتصال' }));
      expect(fetchMock.mock.calls.filter(([input, init]) => init?.method === 'PUT' && String(input).endsWith('/user')).length).toBe(0);

      await user.type(within(dialog).getByLabelText('جست‌وجوی کارمند'), 'EMP');
      await user.click(within(dialog).getByRole('button', { name: 'جست‌وجو' }));
      const results = within(dialog).getByRole('list', { name: 'کارمندان بدون حساب متصل' });
      // The already-connected employee is filtered out; only the free one is offered.
      expect(within(results).getAllByRole('listitem')).toHaveLength(1);
      await user.click(within(results).getByRole('button', { name: 'انتخاب آزاد کارمند' }));

      await user.click(within(dialog).getByRole('button', { name: 'اتصال آزاد کارمند' }));
      expect(await screen.findByRole('status')).toHaveTextContent('پرونده پرسنلی «آزاد کارمند» به این حساب متصل شد.');
      const linkCall = fetchMock.mock.calls.find(([input]) => String(input).endsWith(freeEmployeePath));
      expect(linkCall).toBeTruthy();
      expect(JSON.parse(String(linkCall?.[1]?.body))).toEqual({ userId: 'user-2' });
      // The section was refetched and now shows the connected record with its unlink action.
      expect(await panel.findByText(/EMP-2/)).toBeInTheDocument();
      expect(panel.getByRole('button', { name: 'قطع اتصال پرونده پرسنلی در شرکت نمونه' })).toBeInTheDocument();
    });

    it('disconnects the linked personnel record after an explicit confirmation', async () => {
      const user = userEvent.setup();
      let linked = true;
      const fetchMock = hrHarness(
        () => ({ status: 200, body: hrPage(linked ? [{ ...hrEmployee, hasLinkedUser: true }] : []) }),
        { del: () => { linked = false; return { status: 204 }; } },
      );
      setup();
      await screen.findByRole('tablist');
      const panel = within(screen.getByRole('tabpanel'));
      expect(await panel.findByText(/EMP-2/)).toBeInTheDocument();
      await user.click(panel.getByRole('button', { name: 'قطع اتصال پرونده پرسنلی در شرکت نمونه' }));
      const dialog = screen.getByRole('alertdialog', { name: 'قطع اتصال — شرکت نمونه' });
      await user.click(within(dialog).getByRole('button', { name: 'قطع اتصال' }));

      expect(await screen.findByRole('status')).toHaveTextContent('اتصال پرونده پرسنلی قطع شد.');
      expect(fetchMock.mock.calls.some(([input, init]) => String(input).endsWith(freeEmployeePath) && init?.method === 'DELETE')).toBe(true);
      await waitFor(() => expect(panel.getByText('پرونده پرسنلی متصل نشده است.')).toBeInTheDocument());
    });

    it('keeps the link dialog open with the real server rule when the anchor conflicts', async () => {
      const user = userEvent.setup();
      hrHarness({ status: 200, body: hrPage([]) }, {
        put: { status: 409, body: { error: 'این عضویت قبلاً به کارمند دیگری متصل شده است.' } },
      });
      setup();
      await screen.findByRole('tablist');
      const panel = within(screen.getByRole('tabpanel'));
      await panel.findByRole('button', { name: 'اتصال پرونده پرسنلی در شرکت نمونه' });
      await user.click(panel.getByRole('button', { name: 'اتصال پرونده پرسنلی در شرکت نمونه' }));
      const dialog = screen.getByRole('dialog', { name: 'اتصال پرونده پرسنلی — شرکت نمونه' });
      await user.type(within(dialog).getByLabelText('جست‌وجوی کارمند'), 'EMP');
      await user.click(within(dialog).getByRole('button', { name: 'جست‌وجو' }));
      await user.click(within(dialog).getByRole('button', { name: 'انتخاب آزاد کارمند' }));
      await user.click(within(dialog).getByRole('button', { name: 'اتصال آزاد کارمند' }));

      expect(await within(dialog).findByRole('alert')).toHaveTextContent('این عضویت قبلاً به کارمند دیگری متصل شده است.');
      expect(screen.getByRole('dialog', { name: 'اتصال پرونده پرسنلی — شرکت نمونه' })).toBeInTheDocument();
    });

    it('offers no link actions for a company whose HR records are not visible to the viewer', async () => {
      hrHarness({ status: 403, body: { error: 'شما مجاز به این عملیات در این شرکت نیستید.' } });
      setup();
      await screen.findByRole('tablist');
      const panel = within(screen.getByRole('tabpanel'));
      expect(await panel.findByText('نمایش این بخش برای شما مجاز نیست.')).toBeInTheDocument();
      expect(panel.queryByRole('button', { name: /اتصال پرونده پرسنلی/ })).toBeNull();
      expect(panel.queryByRole('button', { name: /قطع اتصال/ })).toBeNull();
    });
  });

<<<<<<< HEAD
  describe('inter-company membership request states (PR 3-D2)', () => {
    const requestItem = (overrides: Record<string, unknown> = {}) => ({
      id: 'req-1',
      destinationCompanyId: 'c9',
      destinationCompanyName: 'شرکت مقصد',
      status: 'Pending',
      requestedAtUtc: '2026-11-05T08:00:00Z',
      decidedAtUtc: null,
      decidedByDisplayName: null,
      approvedRoleName: null,
      decisionReason: null,
      ...overrides,
    });

    it('loads the request states lazily with Persian status labels, decision details and the rejection reason', async () => {
      const user = userEvent.setup();
      const fetchMock = stubApi({
        'GET /api/v1/admin/users/user-2': { status: 200, body: userDetails(2) },
        'GET /api/v1/admin/users/user-2/membership-requests?page=1&pageSize=10': {
          status: 200,
          body: {
            items: [
              requestItem(),
              requestItem({
                id: 'req-2',
                destinationCompanyName: 'شرکت ردشده',
                status: 'Rejected',
                decidedAtUtc: '2026-11-06T09:00:00Z',
                decidedByDisplayName: 'مدیر مقصد',
                decisionReason: 'در دورهٔ بسته بود',
              }),
            ],
            totalCount: 2,
            page: 1,
            pageSize: 10,
          },
        },
      });
      setup();
      await screen.findByRole('tablist');
      // The request section belongs to the memberships tab and stays idle until it opens.
      expect(fetchMock.mock.calls.filter(([input]) => String(input).includes('/membership-requests')).length).toBe(0);
      await user.click(screen.getByRole('tab', { name: 'شرکت‌ها و عضویت‌ها' }));

      const table = within(await screen.findByRole('table', { name: 'درخواست‌های عضویت کاربر' }));
      expect(table.getByText('در انتظار تأیید')).toBeInTheDocument();
      expect(table.getByText('رد شده')).toBeInTheDocument();
      expect(table.getByText('شرکت مقصد')).toBeInTheDocument();
      expect(table.getByText('در دورهٔ بسته بود')).toBeInTheDocument();
      expect(table.getByText('مدیر مقصد')).toBeInTheDocument();
      expect(screen.getByRole('link', { name: 'رفتن به صف تأیید مقصد' })).toHaveAttribute('href', '/app/admin/membership-requests');
    });

    it('filters the requests by the status allowlist without reloading the whole tab', async () => {
      const user = userEvent.setup();
      const fetchMock = stubApi({
        'GET /api/v1/admin/users/user-2': { status: 200, body: userDetails(2) },
        'GET /api/v1/admin/users/user-2/membership-requests?page=1&pageSize=10': {
          status: 200,
          body: { items: [requestItem()], totalCount: 1, page: 1, pageSize: 10 },
        },
        'GET /api/v1/admin/users/user-2/membership-requests?page=1&pageSize=10&status=Pending': {
          status: 200,
          body: { items: [requestItem()], totalCount: 1, page: 1, pageSize: 10 },
        },
      });
      setup();
      await user.click(await screen.findByRole('tab', { name: 'شرکت‌ها و عضویت‌ها' }));
      await screen.findByRole('table', { name: 'درخواست‌های عضویت کاربر' });
      const before = fetchMock.mock.calls.length;

      await user.selectOptions(screen.getByLabelText('فیلتر وضعیت'), 'Pending');
      await screen.findByRole('table', { name: 'درخواست‌های عضویت کاربر' });
      expect(fetchMock.mock.calls.filter(([input]) => String(input).includes('status=Pending')).length).toBe(1);
      expect(fetchMock.mock.calls.length).toBe(before + 1);
    });

    it('shows an honest empty state when the user has never requested a membership', async () => {
      const user = userEvent.setup();
      stubApi({
        'GET /api/v1/admin/users/user-2': { status: 200, body: userDetails(2) },
        'GET /api/v1/admin/users/user-2/membership-requests?page=1&pageSize=10': {
          status: 200,
          body: { items: [], totalCount: 0, page: 1, pageSize: 10 },
        },
      });
      setup();
      await user.click(await screen.findByRole('tab', { name: 'شرکت‌ها و عضویت‌ها' }));
      expect(await screen.findByText('درخواست عضویتی برای این حساب ثبت نشده است.')).toBeInTheDocument();
    });
  });

=======
>>>>>>> origin/main
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
