import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { stubApi } from '@/test/api';
import { antiforgeryRoute, companyId, employeeId, positionDetail, positionId, positionItem, positionListResponse } from '../test/hrFixtures';
import { hrInstallationAdminPermissionRoute, hrPermissionRoute, renderHrPage } from '../test/hrTestUtils';
import { HrPositionsPage } from './HrPositionsPage';
import { HrPositionCreatePage } from './HrPositionCreatePage';
import { HrPositionDetailPage } from './HrPositionDetailPage';

const onUnauthenticated = vi.fn((): boolean => false);
const positionsPath = `/api/v1/companies/${companyId}/org/positions`;
const positionPath = `${positionsPath}/${positionId}`;

function holders() {
  return {
    items: [
      {
        employeeId,
        employeeNumber: 'EMP-101',
        firstName: 'علی',
        lastName: 'محمدی',
        organizationalUnitId: null,
        organizationalUnitName: 'دفتر مرکزی',
        startOnUtc: '2026-11-01T07:00:00Z',
      },
    ],
    totalCount: 1,
    page: 1,
    pageSize: 25,
  };
}

describe('HrPositionsPage', () => {
  beforeEach(() => {
    onUnauthenticated.mockReset();
    onUnauthenticated.mockReturnValue(false);
  });

  it('lists positions with code, rank and status, and links rows to the detail page', async () => {
    stubApi({
      [`GET ${positionsPath}?page=1&pageSize=25`]: {
        status: 200,
        body: positionListResponse([positionItem({ name: 'حسابدار', code: 'ACC', rank: 2 })]),
      },
      'GET /api/v1/me/permissions': hrPermissionRoute(['hr.positions.list']),
    } as never);
    renderHrPage(
      <HrPositionsPage onUnauthenticated={onUnauthenticated} companyId={companyId} />,
      { path: '/hr/org/positions', title: 'سمت‌ها' },
      '/hr/org/positions',
    );
    const table = await screen.findByRole('table', { name: 'فهرست سمت‌های سازمانی' });
    expect(table).toHaveTextContent('حسابدار');
    expect(table).toHaveTextContent('ACC');
    expect(table).toHaveTextContent('۲');
  });

  it('shows the create action to the installation administrator without any company code (Issue #444 stage 1)', async () => {
    stubApi({
      [`GET ${positionsPath}?page=1&pageSize=25`]: { status: 200, body: positionListResponse([]) },
      'GET /api/v1/me/permissions': hrInstallationAdminPermissionRoute(),
    } as never);
    renderHrPage(
      <HrPositionsPage onUnauthenticated={onUnauthenticated} companyId={companyId} />,
      { path: '/hr/org/positions', title: 'سمت‌ها' },
      '/hr/org/positions',
    );
    expect(await screen.findByRole('button', { name: 'سمت جدید' })).toBeInTheDocument();
  });

  it('states that a position is not a security role', async () => {
    stubApi({
      [`GET ${positionsPath}?page=1&pageSize=25`]: { status: 200, body: positionListResponse([]) },
      'GET /api/v1/me/permissions': hrPermissionRoute([]),
    } as never);
    renderHrPage(
      <HrPositionsPage onUnauthenticated={onUnauthenticated} companyId={companyId} />,
      { path: '/hr/org/positions', title: 'سمت‌ها' },
      '/hr/org/positions',
    );
    expect(await screen.findByText(/سمت هیچ مجوز دسترسی‌ای به سیستم نمی‌دهد/)).toBeInTheDocument();
  });

  it('shows the access-denied panel on 404/403', async () => {
    stubApi({
      [`GET ${positionsPath}?page=1&pageSize=25`]: { status: 404 },
      'GET /api/v1/me/permissions': hrPermissionRoute([]),
    } as never);
    renderHrPage(
      <HrPositionsPage onUnauthenticated={onUnauthenticated} companyId={companyId} />,
      { path: '/hr/org/positions', title: 'سمت‌ها' },
      '/hr/org/positions',
    );
    expect(await screen.findByText('دسترسی ندارید')).toBeInTheDocument();
  });
});

describe('HrPositionCreatePage', () => {
  beforeEach(() => {
    onUnauthenticated.mockReset();
    onUnauthenticated.mockReturnValue(false);
  });

  it('posts the exact create DTO including the optional rank', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi({
      'GET /api/v1/me/permissions': hrPermissionRoute(['hr.positions.create']),
      ...antiforgeryRoute,
      [`POST ${positionsPath}`]: { status: 201, body: { id: positionId } },
    } as never);
    renderHrPage(
      <HrPositionCreatePage onUnauthenticated={onUnauthenticated} companyId={companyId} />,
      { path: '/hr/org/positions/new', title: 'سمت جدید' },
      '/hr/org/positions/new',
    );
    await user.type(await screen.findByLabelText(/کد سمت/), 'ACC');
    await user.type(screen.getByLabelText(/عنوان سمت/), 'حسابدار');
    await user.type(screen.getByLabelText(/رتبه/), '3');
    await user.click(screen.getByRole('button', { name: 'ثبت سمت' }));
    await waitFor(() => {
      const post = fetchMock.mock.calls.find(([url, init]) => String(url) === positionsPath && (init as RequestInit | undefined)?.method === 'POST');
      expect(post).toBeTruthy();
    });
    const postBody = JSON.parse(
      (fetchMock.mock.calls.find(([url, init]) => String(url) === positionsPath && (init as RequestInit | undefined)?.method === 'POST')?.[1] as RequestInit)
        .body as string,
    );
    expect(postBody).toEqual({ code: 'ACC', name: 'حسابدار', description: null, rank: 3 });
    await waitFor(() => expect(window.location.pathname).toBe(`/app/hr/org/positions/${positionId}`));
  });

  it('rejects an invalid rank shape before any request', async () => {
    const user = userEvent.setup();
    stubApi({
      'GET /api/v1/me/permissions': hrPermissionRoute(['hr.positions.create']),
      ...antiforgeryRoute,
      [`POST ${positionsPath}`]: { status: 201, body: { id: positionId } },
    } as never);
    renderHrPage(
      <HrPositionCreatePage onUnauthenticated={onUnauthenticated} companyId={companyId} />,
      { path: '/hr/org/positions/new', title: 'سمت جدید' },
      '/hr/org/positions/new',
    );
    await user.type(await screen.findByLabelText(/کد سمت/), 'ACC');
    await user.type(screen.getByLabelText(/عنوان سمت/), 'حسابدار');
    await user.type(screen.getByLabelText(/رتبه/), '3.5');
    await user.click(screen.getByRole('button', { name: 'ثبت سمت' }));
    expect(await screen.findByText('رتبه باید یک عدد صحیح باشد.')).toBeInTheDocument();
  });
});

describe('HrPositionDetailPage', () => {
  beforeEach(() => {
    onUnauthenticated.mockReset();
    onUnauthenticated.mockReturnValue(false);
  });

  function setup(routes: Record<string, unknown>, codes: readonly string[] = ['hr.positions.update']) {
    return stubApi({ ...routes, 'GET /api/v1/me/permissions': hrPermissionRoute(codes), ...antiforgeryRoute } as never);
  }

  const routes = () => ({
    [`GET ${positionPath}`]: { status: 200, body: positionDetail() },
    [`GET ${positionPath}/employees?page=1&pageSize=25`]: { status: 200, body: holders() },
  });

  it('shows the position, its holders and links them to the employee pages', async () => {
    setup(routes());
    renderHrPage(
      <HrPositionDetailPage onUnauthenticated={onUnauthenticated} companyId={companyId} />,
      { path: '/hr/org/positions/:positionId', title: 'سمت' },
      `/hr/org/positions/${positionId}`,
    );
    expect(await screen.findByRole('heading', { level: 1, name: 'حسابدار' })).toBeInTheDocument();
    const table = await screen.findByRole('table', { name: 'کارکنان دارای این سمت' });
    expect(table).toHaveTextContent('علی محمدی');
    expect(table).toHaveTextContent('دفتر مرکزی');
  });

  it('updates with the mandatory rowVersion and re-reads after a conflict', async () => {
    const user = userEvent.setup();
    let readCount = 0;
    const fetchMock = setup({
      [`GET ${positionPath}`]: () => {
        readCount += 1;
        return { status: 200, body: readCount === 1 ? positionDetail() : positionDetail({ rowVersion: 'AAAAAAAACdE=', name: 'حسابدار ارشد' }) };
      },
      [`GET ${positionPath}/employees?page=1&pageSize=25`]: { status: 200, body: holders() },
      [`PUT ${positionPath}`]: { status: 409, body: { error: 'این سمت هم‌زمان توسط کاربر دیگری تغییر کرده است؛ اطلاعات را تازه‌سازی کنید.' } },
    } as never);
    renderHrPage(
      <HrPositionDetailPage onUnauthenticated={onUnauthenticated} companyId={companyId} />,
      { path: '/hr/org/positions/:positionId', title: 'سمت' },
      `/hr/org/positions/${positionId}`,
    );
    await user.click(await screen.findByRole('button', { name: 'ویرایش' }));
    const nameInput = screen.getByLabelText(/عنوان سمت/);
    await user.clear(nameInput);
    await user.type(nameInput, 'حسابدار ارشد');
    await user.click(screen.getByRole('button', { name: 'ذخیرهٔ تغییرات' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('این سمت هم‌زمان توسط کاربر دیگری تغییر کرده است');
    await waitFor(() => expect(readCount).toBeGreaterThanOrEqual(2));
    expect(screen.getByLabelText(/عنوان سمت/)).toHaveValue('حسابدار ارشد');
    expect(fetchMock).toBeTruthy();
  });

  it('sends rank: null to clear the rank and isActive on save', async () => {
    const user = userEvent.setup();
    const fetchMock = setup({ ...routes(), [`PUT ${positionPath}`]: { status: 204 } });
    renderHrPage(
      <HrPositionDetailPage onUnauthenticated={onUnauthenticated} companyId={companyId} />,
      { path: '/hr/org/positions/:positionId', title: 'سمت' },
      `/hr/org/positions/${positionId}`,
    );
    await user.click(await screen.findByRole('button', { name: 'ویرایش' }));
    await user.clear(screen.getByLabelText(/رتبه/));
    await user.click(screen.getByRole('button', { name: 'ذخیرهٔ تغییرات' }));
    await waitFor(() => {
      const put = fetchMock.mock.calls.find(([url, init]) => String(url) === positionPath && (init as RequestInit | undefined)?.method === 'PUT');
      expect(put).toBeTruthy();
    });
    const putBody = JSON.parse(
      (fetchMock.mock.calls.find(([url, init]) => String(url) === positionPath && (init as RequestInit | undefined)?.method === 'PUT')?.[1] as RequestInit)
        .body as string,
    );
    expect(putBody).toEqual({ name: 'حسابدار', description: null, rank: null, isActive: true, rowVersion: 'AAAAAAAAB9I=' });
  });

  it('deactivates via the confirm dialog and shows the server guard on 409', async () => {
    const user = userEvent.setup();
    setup({
      ...routes(),
      [`POST ${positionPath}/deactivate`]: { status: 409, body: { error: 'ابتدا انتساب‌های فعال این سمت را پایان دهید.' } },
    });
    renderHrPage(
      <HrPositionDetailPage onUnauthenticated={onUnauthenticated} companyId={companyId} />,
      { path: '/hr/org/positions/:positionId', title: 'سمت' },
      `/hr/org/positions/${positionId}`,
    );
    await user.click(await screen.findByRole('button', { name: 'غیرفعال‌سازی سمت' }));
    const dialog = screen.getByRole('alertdialog');
    await user.click(within(dialog).getByRole('button', { name: 'غیرفعال‌سازی' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('ابتدا انتساب‌های فعال این سمت را پایان دهید.');
  });
});
