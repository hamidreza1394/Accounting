import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { stubApi } from '@/test/api';
import { antiforgeryRoute, companyId, employeeId, employeeListResponse, unitId, unitTreeNode } from '../test/hrFixtures';
import { hrInstallationAdminPermissionRoute, hrPermissionRoute, renderHrPage } from '../test/hrTestUtils';
import { HrEmployeesPage } from './HrEmployeesPage';
import { HrEmployeeCreatePage } from './HrEmployeeCreatePage';

const onUnauthenticated = vi.fn((): boolean => false);
const employeesPath = `/api/v1/companies/${companyId}/org/employees`;
const treePath = `/api/v1/companies/${companyId}/org/units?view=tree`;

function employee(overrides: Record<string, unknown> = {}) {
  return {
    id: employeeId,
    employeeNumber: 'EMP-101',
    firstName: 'علی',
    lastName: 'محمدی',
    employmentStatus: 1 as const,
    hiredOnUtc: '2026-01-10T08:30:00Z',
    terminatedOnUtc: null,
    hasLinkedUser: false,
    unitId,
    unitName: 'دفتر مرکزی',
    positionId: null,
    positionName: null,
    ...overrides,
  };
}

describe('HrEmployeesPage', () => {
  beforeEach(() => {
    onUnauthenticated.mockReset();
    onUnauthenticated.mockReturnValue(false);
  });

  it('lists employees with status, current unit/position, hire date and account flag', async () => {
    stubApi({
      [`GET ${employeesPath}?page=1&pageSize=25`]: {
        status: 200,
        body: employeeListResponse([employee(), employee({ id: 'dd000000-0000-4000-8000-00000000000d', employeeNumber: 'EMP-102', firstName: 'سارا', lastName: 'کریمی', hasLinkedUser: true, unitId: null, unitName: null })]),
      },
      [`GET ${treePath}`]: { status: 200, body: { roots: [unitTreeNode()] } },
      'GET /api/v1/me/permissions': hrPermissionRoute(['hr.employees.list']),
    } as never);
    renderHrPage(
      <HrEmployeesPage onUnauthenticated={onUnauthenticated} companyId={companyId} />,
      { path: '/hr/org/employees', title: 'کارکنان' },
      '/hr/org/employees',
    );
    const table = await screen.findByRole('table', { name: 'فهرست کارکنان' });
    expect(table).toHaveTextContent('علی محمدی');
    expect(table).toHaveTextContent('EMP-101');
    expect(table).toHaveTextContent('دفتر مرکزی');
    expect(table).toHaveTextContent('بدون حساب');
    expect(table).toHaveTextContent('متصل');
  });

  it('sends the documented filters (search, status, unitId) and resets to page 1', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi({
      [`GET ${employeesPath}?page=1&pageSize=25`]: { status: 200, body: employeeListResponse([]) },
      [`GET ${employeesPath}?search=%D8%B9%D9%84%DB%8C&page=1&pageSize=25`]: { status: 200, body: employeeListResponse([]) },
      [`GET ${employeesPath}?status=3&page=1&pageSize=25`]: { status: 200, body: employeeListResponse([]) },
      [`GET ${treePath}`]: { status: 200, body: { roots: [unitTreeNode({ id: unitId, name: 'دفتر مرکزی', code: 'HQ' })] } },
      'GET /api/v1/me/permissions': hrPermissionRoute(['hr.employees.list']),
    } as never);
    renderHrPage(
      <HrEmployeesPage onUnauthenticated={onUnauthenticated} companyId={companyId} />,
      { path: '/hr/org/employees', title: 'کارکنان' },
      '/hr/org/employees',
    );
    await screen.findByRole('table', { name: 'فهرست کارکنان' });
    await user.type(screen.getByLabelText('جستجو'), 'علی');
    await waitFor(() => expect(fetchMock.mock.calls.some(([url]) => String(url).includes('search='))).toBe(true));
    await user.selectOptions(screen.getByLabelText('وضعیت اشتغال'), '3');
    await waitFor(() => expect(fetchMock.mock.calls.some(([url]) => String(url).includes('status=3'))).toBe(true));
  });

  it('shows the create action to the installation administrator without any company code (Issue #444 stage 1)', async () => {
    stubApi({
      [`GET ${employeesPath}?page=1&pageSize=25`]: { status: 200, body: employeeListResponse([]) },
      [`GET ${treePath}`]: { status: 200, body: { roots: [] } },
      'GET /api/v1/me/permissions': hrInstallationAdminPermissionRoute(),
    } as never);
    renderHrPage(
      <HrEmployeesPage onUnauthenticated={onUnauthenticated} companyId={companyId} />,
      { path: '/hr/org/employees', title: 'کارکنان' },
      '/hr/org/employees',
    );
    expect(await screen.findByRole('link', { name: 'کارمند جدید' })).toBeInTheDocument();
  });

  it('shows the access-denied panel on 403 without the table', async () => {
    stubApi({
      [`GET ${employeesPath}?page=1&pageSize=25`]: { status: 403, body: { error: 'شما مجوز دسترسی به این بخش را ندارید.' } },
      [`GET ${treePath}`]: { status: 200, body: { roots: [] } },
      'GET /api/v1/me/permissions': hrPermissionRoute([]),
    } as never);
    renderHrPage(
      <HrEmployeesPage onUnauthenticated={onUnauthenticated} companyId={companyId} />,
      { path: '/hr/org/employees', title: 'کارکنان' },
      '/hr/org/employees',
    );
    expect(await screen.findByText('دسترسی ندارید')).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });
});

describe('HrEmployeeCreatePage', () => {
  beforeEach(() => {
    onUnauthenticated.mockReset();
    onUnauthenticated.mockReturnValue(false);
  });

  it('never asks for a user account and explains the optional association', async () => {
    stubApi({
      'GET /api/v1/me/permissions': hrPermissionRoute(['hr.employees.create']),
      ...antiforgeryRoute,
      [`POST ${employeesPath}`]: { status: 201, body: { id: employeeId } },
    } as never);
    renderHrPage(
      <HrEmployeeCreatePage onUnauthenticated={onUnauthenticated} companyId={companyId} />,
      { path: '/hr/org/employees/new', title: 'کارمند جدید' },
      '/hr/org/employees/new',
    );
    expect(await screen.findByText(/نیازی به ساخت حساب کاربری ندارد/)).toBeInTheDocument();
    expect(screen.queryByLabelText(/حساب کاربری/)).not.toBeInTheDocument();
  });

  it('posts the exact create DTO (hiredOnUtc omitted when no date picked) and navigates', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi({
      'GET /api/v1/me/permissions': hrPermissionRoute(['hr.employees.create']),
      ...antiforgeryRoute,
      [`POST ${employeesPath}`]: { status: 201, body: { id: employeeId } },
    } as never);
    renderHrPage(
      <HrEmployeeCreatePage onUnauthenticated={onUnauthenticated} companyId={companyId} />,
      { path: '/hr/org/employees/new', title: 'کارمند جدید' },
      '/hr/org/employees/new',
    );
    await user.type(await screen.findByLabelText(/شماره پرسنلی/), 'EMP-101');
    await user.type(screen.getByLabelText(/^نام\*$/), 'علی');
    await user.type(screen.getByLabelText(/نام خانوادگی/), 'محمدی');
    await user.click(screen.getByRole('button', { name: 'ثبت کارمند' }));
    await waitFor(() => {
      const post = fetchMock.mock.calls.find(([url, init]) => String(url) === employeesPath && (init as RequestInit | undefined)?.method === 'POST');
      expect(post).toBeTruthy();
    });
    const postBody = JSON.parse(
      (fetchMock.mock.calls.find(([url, init]) => String(url) === employeesPath && (init as RequestInit | undefined)?.method === 'POST')?.[1] as RequestInit)
        .body as string,
    );
    expect(postBody).toEqual({ employeeNumber: 'EMP-101', firstName: 'علی', lastName: 'محمدی' });
    await waitFor(() => expect(window.location.pathname).toBe(`/app/hr/org/employees/${employeeId}`));
  });

  it('validates the employee-number pattern client-side before any request', async () => {
    const user = userEvent.setup();
    stubApi({
      'GET /api/v1/me/permissions': hrPermissionRoute(['hr.employees.create']),
      ...antiforgeryRoute,
      [`POST ${employeesPath}`]: { status: 201, body: { id: employeeId } },
    } as never);
    renderHrPage(
      <HrEmployeeCreatePage onUnauthenticated={onUnauthenticated} companyId={companyId} />,
      { path: '/hr/org/employees/new', title: 'کارمند جدید' },
      '/hr/org/employees/new',
    );
    await user.type(await screen.findByLabelText(/شماره پرسنلی/), '-bad number!');
    await user.type(screen.getByLabelText(/^نام\*$/), 'علی');
    await user.type(screen.getByLabelText(/نام خانوادگی/), 'محمدی');
    await user.click(screen.getByRole('button', { name: 'ثبت کارمند' }));
    expect(await screen.findByText(/شماره پرسنلی باید با حرف یا رقم شروع شود/)).toBeInTheDocument();
  });

  it('shows the duplicate-number 409 message from the server', async () => {
    const user = userEvent.setup();
    stubApi({
      'GET /api/v1/me/permissions': hrPermissionRoute(['hr.employees.create']),
      ...antiforgeryRoute,
      [`POST ${employeesPath}`]: { status: 409, body: { error: 'شماره پرسنلی در این شرکت تکراری است.' } },
    } as never);
    renderHrPage(
      <HrEmployeeCreatePage onUnauthenticated={onUnauthenticated} companyId={companyId} />,
      { path: '/hr/org/employees/new', title: 'کارمند جدید' },
      '/hr/org/employees/new',
    );
    await user.type(await screen.findByLabelText(/شماره پرسنلی/), 'EMP-101');
    await user.type(screen.getByLabelText(/^نام\*$/), 'علی');
    await user.type(screen.getByLabelText(/نام خانوادگی/), 'محمدی');
    await user.click(screen.getByRole('button', { name: 'ثبت کارمند' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('شماره پرسنلی در این شرکت تکراری است.');
  });
});
