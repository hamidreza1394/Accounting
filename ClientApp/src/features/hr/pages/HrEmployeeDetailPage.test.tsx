import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { stubApi } from '@/test/api';
import {
  antiforgeryRoute,
  assignmentItem,
  companyId,
  employeeDetail,
  employeeId,
  historyResponse,
  positionId,
  unitId,
} from '../test/hrFixtures';
import { hrPermissionRoute, renderHrPage } from '../test/hrTestUtils';
import { HrEmployeeDetailPage } from './HrEmployeeDetailPage';

const onUnauthenticated = vi.fn((): boolean => false);
const employeePath = `/api/v1/companies/${companyId}/org/employees/${employeeId}`;
const assignmentsPath = `/api/v1/companies/${companyId}/org/assignments?employeeId=${employeeId}&page=1&pageSize=25`;
const historyPath = `${employeePath}/history?page=1&pageSize=25`;
const treePath = `/api/v1/companies/${companyId}/org/units?view=tree`;
const positionsPath = `/api/v1/companies/${companyId}/org/positions?page=1&pageSize=25`;

function detail(overrides: Record<string, unknown> = {}) {
  return employeeDetail(overrides);
}

function baseRoutes(overrides: Record<string, unknown> = {}) {
  return {
    [`GET ${employeePath}`]: { status: 200, body: detail(overrides) },
    [`GET ${assignmentsPath}`]: {
      status: 200,
      body: {
        items: [assignmentItem(), assignmentItem({ id: 'aa000000-0000-4000-8000-000000000002', status: 2, endOnUtc: '2026-11-04T10:00:00Z' })],
        totalCount: 2,
        page: 1,
        pageSize: 25,
      },
    },
    [`GET ${historyPath}`]: {
      status: 200,
      body: historyResponse([{ eventType: 'HrAssignmentStarted', entityType: 'CompanyEmployeeAssignment' }]),
    },
    [`GET ${treePath}`]: { status: 200, body: { roots: [] } },
    [`GET ${positionsPath}`]: { status: 200, body: { items: [], totalCount: 0, page: 1, pageSize: 25 } },
  };
}

function setup(routes: Record<string, unknown>, codes: readonly string[] = ['hr.employees.update', 'hr.assignments.manage']) {
  return stubApi({ ...routes, 'GET /api/v1/me/permissions': hrPermissionRoute(codes), ...antiforgeryRoute } as never);
}

describe('HrEmployeeDetailPage', () => {
  beforeEach(() => {
    onUnauthenticated.mockReset();
    onUnauthenticated.mockReturnValue(false);
  });

  it('opens on the structure tab: active assignment card plus the assignment history', async () => {
    setup(baseRoutes());
    renderHrPage(
      <HrEmployeeDetailPage onUnauthenticated={onUnauthenticated} companyId={companyId} />,
      { path: '/hr/org/employees/:employeeId', title: 'کارمند' },
      `/hr/org/employees/${employeeId}`,
    );
    expect(await screen.findByRole('heading', { level: 1, name: 'علی محمدی' })).toBeInTheDocument();
    expect(screen.getByText('انتساب جاری')).toBeInTheDocument();
    const history = await screen.findByRole('table', { name: 'تاریخچه انتساب‌های کارمند' });
    // The table shell renders before the history rows arrive; wait for the rows instead of racing them (slow CI runners).
    await waitFor(() => expect(history).toHaveTextContent('دفتر مرکزی'));
    expect(history).toHaveTextContent('پایان‌یافته');
  });

  it('transfers the employee with null = keep semantics and reloads the file', async () => {
    const user = userEvent.setup();
    const fetchMock = setup({
      ...baseRoutes(),
      [`POST ${employeePath}/assignment-change`]: { status: 200, body: { id: assignmentItem().id } },
    });
    renderHrPage(
      <HrEmployeeDetailPage onUnauthenticated={onUnauthenticated} companyId={companyId} />,
      { path: '/hr/org/employees/:employeeId', title: 'کارمند' },
      `/hr/org/employees/${employeeId}`,
    );
    await user.click(await screen.findByRole('button', { name: 'انتقال کارمند' }));
    await user.click(screen.getByRole('button', { name: 'ثبت انتقال' }));
    await waitFor(() => {
      const post = fetchMock.mock.calls.find(
        ([url, init]) => String(url) === `${employeePath}/assignment-change` && (init as RequestInit | undefined)?.method === 'POST',
      );
      expect(post).toBeTruthy();
    });
    const postBody = JSON.parse(
      (
        fetchMock.mock.calls.find(
          ([url, init]) => String(url) === `${employeePath}/assignment-change` && (init as RequestInit | undefined)?.method === 'POST',
        )?.[1] as RequestInit
      ).body as string,
    );
    expect(postBody).toEqual({ organizationalUnitId: null, positionId: null, managerEmployeeId: null });
    expect(await screen.findByText(/انتقال کارمند انجام شد/)).toBeInTheDocument();
  });

  it('starts the first assignment for an employee without one, sending the exact DTO', async () => {
    const user = userEvent.setup();
    const fetchMock = setup({
      ...baseRoutes({ activeAssignment: null }),
      [`GET ${treePath}`]: {
        status: 200,
        body: { roots: [{ id: unitId, code: 'HQ', name: 'دفتر مرکزی', isActive: true, children: [] }] },
      },
      [`GET ${positionsPath}`]: {
        status: 200,
        body: { items: [{ id: positionId, code: 'ACC', name: 'حسابدار', description: null, rank: null, isActive: true }], totalCount: 1, page: 1, pageSize: 25 },
      },
      'POST /api/v1/companies/0f3a1d2c-0000-4000-8000-000000000001/org/assignments': { status: 201, body: { id: assignmentItem().id } },
    });
    renderHrPage(
      <HrEmployeeDetailPage onUnauthenticated={onUnauthenticated} companyId={companyId} />,
      { path: '/hr/org/employees/:employeeId', title: 'کارمند' },
      `/hr/org/employees/${employeeId}`,
    );
    expect(await screen.findByText('این کارمند هنوز انتساب فعالی ندارد.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'شروع انتساب' }));
    // Pick unit and position through the searchable selects.
    await user.selectOptions(await screen.findByRole('combobox', { name: 'جستجوی واحد سازمانی' }), unitId);
    await user.selectOptions(screen.getByRole('combobox', { name: 'جستجوی سمت' }), positionId);
    // The JalaliDateField is a popover; the start date rule is validated in hrValidation and by
    // the server — submit without a date would be blocked client-side, so a day is picked there:
    await user.click(screen.getByRole('button', { name: 'تاریخ شروع' }));
    const grid = await screen.findByRole('grid');
    const dayButton = within(grid)
      .getAllByRole('gridcell')
      .map((cell) => cell.querySelector('button:not([disabled])'))
      .find(Boolean);
    expect(dayButton).toBeTruthy();
    await user.click(dayButton as HTMLElement);
    await user.click(screen.getByRole('button', { name: 'ثبت انتساب' }));
    await waitFor(() => {
      const post = fetchMock.mock.calls.find(
        ([url, init]) =>
          String(url) === `/api/v1/companies/${companyId}/org/assignments` && (init as RequestInit | undefined)?.method === 'POST',
      );
      expect(post).toBeTruthy();
    });
    const postBody = JSON.parse(
      (
        fetchMock.mock.calls.find(
          ([url, init]) =>
            String(url) === `/api/v1/companies/${companyId}/org/assignments` && (init as RequestInit | undefined)?.method === 'POST',
        )?.[1] as RequestInit
      ).body as string,
    );
    expect(postBody).toMatchObject({
      employeeId,
      organizationalUnitId: unitId,
      positionId,
      managerEmployeeId: null,
      endActive: false,
    });
    expect(new Date(postBody.startOnUtc as string).getTime()).not.toBeNaN();
  });

  it('shows the server date-rule message when the start is invalid (400)', async () => {
    const user = userEvent.setup();
    setup({
      ...baseRoutes({ activeAssignment: null }),
      'POST /api/v1/companies/0f3a1d2c-0000-4000-8000-000000000001/org/assignments': {
        status: 400,
        body: { error: 'شروع انتساب نمی‌تواند قبل از تاریخ شروع همکاری باشد.' },
      },
    });
    renderHrPage(
      <HrEmployeeDetailPage onUnauthenticated={onUnauthenticated} companyId={companyId} />,
      { path: '/hr/org/employees/:employeeId', title: 'کارمند' },
      `/hr/org/employees/${employeeId}`,
    );
    await user.click(await screen.findByRole('button', { name: 'شروع انتساب' }));
    await user.click(screen.getByRole('button', { name: 'تاریخ شروع' }));
    const grid = await screen.findByRole('grid');
    const dayButton = within(grid)
      .getAllByRole('gridcell')
      .map((cell) => cell.querySelector('button:not([disabled])'))
      .find(Boolean);
    expect(dayButton).toBeTruthy();
    await user.click(dayButton as HTMLElement);
    await user.click(screen.getByRole('button', { name: 'ثبت انتساب' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('شروع انتساب نمی‌تواند قبل از تاریخ شروع همکاری باشد.');
  });

  it('ends the active assignment from the danger dialog (empty date = end now)', async () => {
    const user = userEvent.setup();
    const fetchMock = setup({ ...baseRoutes(), [`POST /api/v1/companies/${companyId}/org/assignments/${assignmentItem().id}/end`]: { status: 204 } });
    renderHrPage(
      <HrEmployeeDetailPage onUnauthenticated={onUnauthenticated} companyId={companyId} />,
      { path: '/hr/org/employees/:employeeId', title: 'کارمند' },
      `/hr/org/employees/${employeeId}`,
    );
    await user.click(await screen.findByRole('button', { name: 'پایان انتساب جاری' }));
    const dialog = screen.getByRole('alertdialog');
    await user.click(within(dialog).getByRole('button', { name: 'پایان انتساب' }));
    await waitFor(() => {
      const post = fetchMock.mock.calls.find(
        ([url, init]) =>
          String(url) === `/api/v1/companies/${companyId}/org/assignments/${assignmentItem().id}/end` &&
          (init as RequestInit | undefined)?.method === 'POST',
      );
      expect(post).toBeTruthy();
    });
    const postBody = JSON.parse(
      (
        fetchMock.mock.calls.find(
          ([url, init]) =>
            String(url) === `/api/v1/companies/${companyId}/org/assignments/${assignmentItem().id}/end` &&
            (init as RequestInit | undefined)?.method === 'POST',
        )?.[1] as RequestInit
      ).body as string,
    );
    expect(postBody).toEqual({});
  });

  it('terminates the employee through the irreversible dialog', async () => {
    const user = userEvent.setup();
    const fetchMock = setup({ ...baseRoutes(), [`POST ${employeePath}/deactivate`]: { status: 204 } });
    renderHrPage(
      <HrEmployeeDetailPage onUnauthenticated={onUnauthenticated} companyId={companyId} />,
      { path: '/hr/org/employees/:employeeId', title: 'کارمند' },
      `/hr/org/employees/${employeeId}`,
    );
    await user.click(await screen.findByRole('button', { name: 'خاتمه همکاری' }));
    const dialog = screen.getByRole('alertdialog');
    await user.click(within(dialog).getByRole('button', { name: 'خاتمه همکاری' }));
    await waitFor(() => {
      expect(fetchMock.mock.calls.some(([url, init]) => String(url) === `${employeePath}/deactivate` && (init as RequestInit | undefined)?.method === 'POST')).toBe(true);
    });
  });

  it('shows the read-only notice and hides edit/terminate for a terminated employee', async () => {
    setup(baseRoutes({ employmentStatus: 3, terminatedOnUtc: '2026-11-04T10:00:00Z', activeAssignment: null }));
    renderHrPage(
      <HrEmployeeDetailPage onUnauthenticated={onUnauthenticated} companyId={companyId} />,
      { path: '/hr/org/employees/:employeeId', title: 'کارمند' },
      `/hr/org/employees/${employeeId}`,
    );
    expect(await screen.findByText(/همکاری این کارمند خاتمه شده است/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'خاتمه همکاری' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'ویرایش اطلاعات' })).not.toBeInTheDocument();
  });

  it('renders the audit history tab with only the allowlisted fields', async () => {
    const user = userEvent.setup();
    setup(baseRoutes());
    renderHrPage(
      <HrEmployeeDetailPage onUnauthenticated={onUnauthenticated} companyId={companyId} />,
      { path: '/hr/org/employees/:employeeId', title: 'کارمند' },
      `/hr/org/employees/${employeeId}`,
    );
    await user.click(await screen.findByRole('tab', { name: 'تاریخچه رخدادها' }));
    const table = await screen.findByRole('table', { name: 'تاریخچه رخدادهای کارمند' });
    expect(table).toHaveTextContent('شروع انتساب');
    expect(table).toHaveTextContent('CompanyEmployeeAssignment');
  });
});

describe('HrUserLinkSection (inside the employee page)', () => {
  beforeEach(() => {
    onUnauthenticated.mockReset();
    onUnauthenticated.mockReturnValue(false);
  });

  function userLinkRoutes(overrides: Record<string, unknown> = {}) {
    return {
      ...baseRoutes(overrides),
      'GET /api/v1/companies/0f3a1d2c-0000-4000-8000-000000000001/users?membershipActive=true&page=1&pageSize=25': {
        status: 200,
        body: {
          companyId,
          companyName: 'شرکت نمونه',
          items: [{ userId: 'e4e5f6a7-5555-4000-8000-00000000abcd', username: 'alice', firstName: 'آلیس', lastName: 'احمدی', userIsActive: true, membershipIsActive: true, roleId: 'r', roleCode: 'R', roleName: 'حسابدار', roleIsActive: true, membershipCreatedAtUtc: '2026-01-01T00:00:00Z' }],
          totalCount: 1,
          page: 1,
          pageSize: 25,
        },
      },
      [`PUT ${employeePath}/user`]: { status: 204 },
      [`DELETE ${employeePath}/user`]: { status: 204 },
      ...antiforgeryRoute,
    };
  }

  it('links an existing member without creating an account', async () => {
    const user = userEvent.setup();
    const fetchMock = setup(
      userLinkRoutes({ activeAssignment: null, linkedUserId: null, userCompanyMembershipId: null }),
      ['hr.employees.user-link.manage', 'users.list'],
    );
    renderHrPage(
      <HrEmployeeDetailPage onUnauthenticated={onUnauthenticated} companyId={companyId} />,
      { path: '/hr/org/employees/:employeeId', title: 'کارمند' },
      `/hr/org/employees/${employeeId}`,
    );
    await user.click(await screen.findByRole('tab', { name: 'حساب کاربری' }));
    expect(await screen.findByText('بدون حساب کاربری')).toBeInTheDocument();
    await user.selectOptions(await screen.findByRole('combobox', { name: 'انتخاب کاربر (جستجو)' }), 'e4e5f6a7-5555-4000-8000-00000000abcd');
    await user.click(screen.getByRole('button', { name: 'اتصال حساب' }));
    await waitFor(() => {
      const put = fetchMock.mock.calls.find(([url, init]) => String(url) === `${employeePath}/user` && (init as RequestInit | undefined)?.method === 'PUT');
      expect(put).toBeTruthy();
    });
    const putBody = JSON.parse(
      (fetchMock.mock.calls.find(([url, init]) => String(url) === `${employeePath}/user` && (init as RequestInit | undefined)?.method === 'PUT')?.[1] as RequestInit)
        .body as string,
    );
    expect(putBody).toEqual({ userId: 'e4e5f6a7-5555-4000-8000-00000000abcd' });
    expect(await screen.findByText('حساب کاربری به کارمند متصل شد.')).toBeInTheDocument();
  });

  it('unlinks the account through the danger dialog', async () => {
    const user = userEvent.setup();
    const fetchMock = setup(
      userLinkRoutes({ linkedUserId: 'e4e5f6a7-5555-4000-8000-00000000abcd', userCompanyMembershipId: 'aa000000-0000-4000-8000-0000000000aa' }),
      ['hr.employees.user-link.manage', 'users.list'],
    );
    renderHrPage(
      <HrEmployeeDetailPage onUnauthenticated={onUnauthenticated} companyId={companyId} />,
      { path: '/hr/org/employees/:employeeId', title: 'کارمند' },
      `/hr/org/employees/${employeeId}`,
    );
    await user.click(await screen.findByRole('tab', { name: 'حساب کاربری' }));
    await user.click(await screen.findByRole('button', { name: 'جداسازی حساب کاربری' }));
    const dialog = screen.getByRole('alertdialog');
    await user.click(within(dialog).getByRole('button', { name: 'جداسازی' }));
    await waitFor(() => {
      expect(fetchMock.mock.calls.some(([url, init]) => String(url) === `${employeePath}/user` && (init as RequestInit | undefined)?.method === 'DELETE')).toBe(true);
    });
    expect(await screen.findByText('اتصال حساب کاربری جدا شد.')).toBeInTheDocument();
  });

  it('documents the missing lookup instead of inventing one when users.list is absent', async () => {
    setup(userLinkRoutes({ linkedUserId: null, userCompanyMembershipId: null }), ['hr.employees.user-link.manage']);
    renderHrPage(
      <HrEmployeeDetailPage onUnauthenticated={onUnauthenticated} companyId={companyId} />,
      { path: '/hr/org/employees/:employeeId', title: 'کارمند' },
      `/hr/org/employees/${employeeId}`,
    );
    await userEvent.setup().click(await screen.findByRole('tab', { name: 'حساب کاربری' }));
    expect(await screen.findByText(/برای جستجوی کاربران جهت اتصال/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'اتصال حساب' })).not.toBeInTheDocument();
  });
});
