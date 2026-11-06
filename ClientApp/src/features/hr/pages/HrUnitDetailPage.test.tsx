import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { stubApi } from '@/test/api';
import { antiforgeryRoute, companyId, employeeId, subtreeNode, unitDetail, unitId } from '../test/hrFixtures';
import { hrPermissionRoute, renderHrPage } from '../test/hrTestUtils';
import { HrUnitDetailPage } from './HrUnitDetailPage';

const onUnauthenticated = vi.fn((): boolean => false);
const base = `/api/v1/companies/${companyId}/org/units/${unitId}`;
const treePath = `/api/v1/companies/${companyId}/org/units?view=tree`;

function detail(overrides: Record<string, unknown> = {}) {
  return unitDetail(overrides);
}

function unitEmployees() {
  return {
    items: [
      {
        employeeId,
        employeeNumber: 'EMP-101',
        firstName: 'علی',
        lastName: 'محمدی',
        positionId: null,
        positionName: 'حسابدار',
        startOnUtc: '2026-11-01T07:00:00Z',
      },
    ],
    totalCount: 1,
    page: 1,
    pageSize: 25,
  };
}

function setup(routes: Record<string, unknown>, codes: readonly string[] = ['hr.organizational-units.update']) {
  const fetchMock = stubApi({ ...routes, 'GET /api/v1/me/permissions': hrPermissionRoute(codes), ...antiforgeryRoute } as never);
  renderHrPage(
    <HrUnitDetailPage onUnauthenticated={onUnauthenticated} companyId={companyId} />,
    { path: '/hr/org/units/:unitId', title: 'واحد' },
    `/hr/org/units/${unitId}`,
  );
  return fetchMock;
}

function defaultRoutes(overrides: Record<string, unknown> = {}) {
  return {
    [`GET ${base}`]: { status: 200, body: detail(overrides) },
    [`GET ${base}/subtree`]: {
      status: 200,
      body: subtreeNode({ id: unitId }, [subtreeNode({ id: 'b1b2c3d4-4444-4000-8000-00000000ef02', code: 'SALES', name: 'فروش', isActive: true })]),
    },
    [`GET ${base}/employees?page=1&pageSize=25`]: { status: 200, body: unitEmployees() },
    [`GET ${treePath}`]: { status: 200, body: { roots: [] } },
  };
}

describe('HrUnitDetailPage', () => {
  beforeEach(() => {
    onUnauthenticated.mockReset();
    onUnauthenticated.mockReturnValue(false);
  });

  it('shows the unit data, subtree and its employees with the assignment start', async () => {
    setup(defaultRoutes());
    expect(await screen.findByRole('heading', { level: 1, name: 'دفتر مرکزی' })).toBeInTheDocument();
    expect(screen.getByText('SALES')).toBeInTheDocument();
    const employeesTable = await screen.findByRole('table', { name: 'کارکنان واحد سازمانی' });
    expect(employeesTable).toHaveTextContent('علی محمدی');
    expect(employeesTable).toHaveTextContent('حسابدار');
  });

  it('sends the edit payload with rowVersion and the documented null semantics', async () => {
    const user = userEvent.setup();
    const fetchMock = setup({ ...defaultRoutes(), [`PUT ${base}`]: { status: 204 } });
    await user.click(await screen.findByRole('button', { name: 'ویرایش' }));
    const nameInput = screen.getByLabelText(/عنوان واحد/);
    await user.clear(nameInput);
    await user.type(nameInput, 'دفتر مرکزی جدید');
    await user.click(screen.getByRole('button', { name: 'ذخیرهٔ تغییرات' }));
    await waitFor(() => {
      const put = fetchMock.mock.calls.find(([url, init]) => String(url) === base && (init as RequestInit | undefined)?.method === 'PUT');
      expect(put).toBeTruthy();
    });
    const putBody = JSON.parse(
      (fetchMock.mock.calls.find(([url, init]) => String(url) === base && (init as RequestInit | undefined)?.method === 'PUT')?.[1] as RequestInit)
        .body as string,
    );
    expect(putBody).toEqual({
      name: 'دفتر مرکزی جدید',
      description: null,
      parentUnitId: null,
      isActive: true,
      validFromUtc: null, // untouched → keep
      validToUtc: null, // empty → clear
      rowVersion: 'AAAAAAAAB9I=',
    });
    expect(await screen.findByText('تغییرات ذخیره شد.')).toBeInTheDocument();
  });

  it('re-reads the unit after a 409 concurrency conflict and shows the server message', async () => {
    const user = userEvent.setup();
    const fresh = detail({ rowVersion: 'AAAAAAAACdE=' });
    let readCount = 0;
    setup({
      [`GET ${base}`]: () => {
        readCount += 1;
        return { status: 200, body: readCount === 1 ? detail() : fresh };
      },
      [`GET ${base}/subtree`]: { status: 200, body: subtreeNode({ id: unitId }) },
      [`GET ${base}/employees?page=1&pageSize=25`]: { status: 200, body: unitEmployees() },
      [`GET ${treePath}`]: { status: 200, body: { roots: [] } },
      [`PUT ${base}`]: { status: 409, body: { error: 'این واحد هم‌زمان توسط کاربر دیگری تغییر کرده است؛ اطلاعات را تازه‌سازی کنید.' } },
    } as never);
    await user.click(await screen.findByRole('button', { name: 'ویرایش' }));
    await user.click(screen.getByRole('button', { name: 'ذخیرهٔ تغییرات' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('این واحد هم‌زمان توسط کاربر دیگری تغییر کرده است');
    // The page re-read the unit; the form stays open and is refreshed with the server state
    // (fresh rowVersion), so the user can retry without losing the form.
    await waitFor(() => expect(readCount).toBeGreaterThanOrEqual(2));
    expect(screen.getByLabelText(/عنوان واحد/)).toHaveValue('دفتر مرکزی');
  });

  it('deactivates the unit through the confirm dialog', async () => {
    const user = userEvent.setup();
    const fetchMock = setup({ ...defaultRoutes(), [`POST ${base}/deactivate`]: { status: 204 } });
    await user.click(await screen.findByRole('button', { name: 'غیرفعال‌سازی واحد' }));
    const dialog = screen.getByRole('alertdialog');
    await user.click(within(dialog).getByRole('button', { name: 'غیرفعال‌سازی' }));
    await waitFor(() => {
      expect(fetchMock.mock.calls.some(([url]) => String(url) === `${base}/deactivate`)).toBe(true);
    });
  });

  it('shows the server guard message when active assignments block deactivation (409)', async () => {
    const user = userEvent.setup();
    setup({
      ...defaultRoutes(),
      [`POST ${base}/deactivate`]: { status: 409, body: { error: 'ابتدا انتساب‌های فعال این واحد را پایان دهید.' } },
    });
    await user.click(await screen.findByRole('button', { name: 'غیرفعال‌سازی واحد' }));
    const dialog = screen.getByRole('alertdialog');
    await user.click(within(dialog).getByRole('button', { name: 'غیرفعال‌سازی' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('ابتدا انتساب‌های فعال این واحد را پایان دهید.');
  });

  it('renders the not-found panel on 404 without leaking whether the unit exists elsewhere', async () => {
    stubApi({
      [`GET ${base}`]: { status: 404 },
      [`GET ${base}/subtree`]: { status: 404 },
      [`GET ${base}/employees?page=1&pageSize=25`]: { status: 404 },
      [`GET ${treePath}`]: { status: 200, body: { roots: [] } },
      'GET /api/v1/me/permissions': hrPermissionRoute([]),
    } as never);
    renderHrPage(
      <HrUnitDetailPage onUnauthenticated={onUnauthenticated} companyId={companyId} />,
      { path: '/hr/org/units/:unitId', title: 'واحد' },
      `/hr/org/units/${unitId}`,
    );
    expect(await screen.findByText('یافت نشد یا دسترسی ندارید')).toBeInTheDocument();
  });
});
