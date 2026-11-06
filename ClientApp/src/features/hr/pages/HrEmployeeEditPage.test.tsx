import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { stubApi } from '@/test/api';
import { antiforgeryRoute, companyId, employeeDetail, employeeId } from '../test/hrFixtures';
import { hrPermissionRoute, renderHrPage } from '../test/hrTestUtils';
import { HrEmployeeEditPage } from './HrEmployeeEditPage';

const onUnauthenticated = vi.fn((): boolean => false);
const employeePath = `/api/v1/companies/${companyId}/org/employees/${employeeId}`;

describe('HrEmployeeEditPage', () => {
  beforeEach(() => {
    onUnauthenticated.mockReset();
    onUnauthenticated.mockReturnValue(false);
  });

  it('sends the update DTO with rowVersion and keep-semantics for the hire date', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi({
      [`GET ${employeePath}`]: { status: 200, body: employeeDetail() },
      [`PUT ${employeePath}`]: { status: 204 },
      'GET /api/v1/me/permissions': hrPermissionRoute(['hr.employees.update']),
      ...antiforgeryRoute,
    } as never);
    renderHrPage(
      <HrEmployeeEditPage onUnauthenticated={onUnauthenticated} companyId={companyId} />,
      { path: '/hr/org/employees/:employeeId/edit', title: 'ویرایش' },
      `/hr/org/employees/${employeeId}/edit`,
    );
    const nameInput = await screen.findByLabelText(/^نام\*$/);
    await user.clear(nameInput);
    await user.type(nameInput, 'علی‌رضا');
    // Clearing the (prefilled) hire date sends `null` = keep — the documented keep-semantics.
    await user.click(screen.getByRole('button', { name: 'پاک‌کردن تاریخ' }));
    await user.click(screen.getByRole('button', { name: 'ذخیرهٔ تغییرات' }));
    await waitFor(() => {
      const put = fetchMock.mock.calls.find(([url, init]) => String(url) === employeePath && (init as RequestInit | undefined)?.method === 'PUT');
      expect(put).toBeTruthy();
    });
    const putBody = JSON.parse(
      (fetchMock.mock.calls.find(([url, init]) => String(url) === employeePath && (init as RequestInit | undefined)?.method === 'PUT')?.[1] as RequestInit)
        .body as string,
    );
    expect(putBody).toEqual({
      firstName: 'علی‌رضا',
      lastName: 'محمدی',
      employmentStatus: 1,
      hiredOnUtc: null, // cleared field → keep the stored value (clearing is impossible by contract)
      rowVersion: 'AAAAAAAAB9I=',
    });
  });

  it('states that a terminated employee cannot be edited and sends nothing', async () => {
    const fetchMock = stubApi({
      [`GET ${employeePath}`]: {
        status: 200,
        body: employeeDetail({ employmentStatus: 3, terminatedOnUtc: '2026-11-04T10:00:00Z', activeAssignment: null }),
      },
      [`PUT ${employeePath}`]: { status: 409, body: { error: 'کارمند خاتمه‌همکاری قابل فعال‌سازی مجدد نیست؛ رکورد پرسنلی جدید ثبت کنید.' } },
      'GET /api/v1/me/permissions': hrPermissionRoute(['hr.employees.update']),
      ...antiforgeryRoute,
    } as never);
    renderHrPage(
      <HrEmployeeEditPage onUnauthenticated={onUnauthenticated} companyId={companyId} />,
      { path: '/hr/org/employees/:employeeId/edit', title: 'ویرایش' },
      `/hr/org/employees/${employeeId}/edit`,
    );
    expect(await screen.findByText(/قابل ویرایش یا فعال‌سازی مجدد نیست/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'ذخیرهٔ تغییرات' })).not.toBeInTheDocument();
    expect(fetchMock.mock.calls.every(([, init]) => (init as RequestInit | undefined)?.method !== 'PUT')).toBe(true);
  });

  it('shows the stale-rowVersion 409 message and re-reads the employee', async () => {
    const user = userEvent.setup();
    let readCount = 0;
    stubApi({
      [`GET ${employeePath}`]: () => {
        readCount += 1;
        return { status: 200, body: readCount === 1 ? employeeDetail() : employeeDetail({ rowVersion: 'AAAAAAAACdE=' }) };
      },
      [`PUT ${employeePath}`]: { status: 409, body: { error: 'این کارمند هم‌زمان توسط کاربر دیگری تغییر کرده است؛ اطلاعات را تازه‌سازی کنید.' } },
      'GET /api/v1/me/permissions': hrPermissionRoute(['hr.employees.update']),
      ...antiforgeryRoute,
    } as never);
    renderHrPage(
      <HrEmployeeEditPage onUnauthenticated={onUnauthenticated} companyId={companyId} />,
      { path: '/hr/org/employees/:employeeId/edit', title: 'ویرایش' },
      `/hr/org/employees/${employeeId}/edit`,
    );
    await screen.findByLabelText(/^نام\*$/);
    await user.click(screen.getByRole('button', { name: 'ذخیرهٔ تغییرات' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('این کارمند هم‌زمان توسط کاربر دیگری تغییر کرده است');
    await waitFor(() => expect(readCount).toBeGreaterThanOrEqual(2));
  });
});
