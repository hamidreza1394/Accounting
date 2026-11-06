import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { stubApi } from '@/test/api';
import { companyId, unitId, unitItem, unitListResponse, unitTreeNode } from '../test/hrFixtures';
import { hrPermissionRoute, renderHrPage } from '../test/hrTestUtils';
import { HrUnitsPage } from './HrUnitsPage';

const onUnauthenticated = vi.fn((): boolean => false);
const unitsPath = `/api/v1/companies/${companyId}/org/units`;
const treePath = `${unitsPath}?view=tree`;

function unitNode(overrides: Record<string, unknown> = {}) {
  return unitTreeNode({ id: unitId, ...overrides });
}

function api(routes: Record<string, unknown>) {
  return stubApi(routes as never);
}

describe('HrUnitsPage', () => {
  beforeEach(() => {
    onUnauthenticated.mockReset();
    onUnauthenticated.mockReturnValue(false);
  });

  it('shows the tree by default and switches to the paged flat list', async () => {
    api({
      [`GET ${treePath}`]: { status: 200, body: { roots: [unitNode()] } },
      [`GET ${unitsPath}?page=1&pageSize=25`]: { status: 200, body: unitListResponse([unitItem()]) },
      'GET /api/v1/me/permissions': hrPermissionRoute(['hr.organizational-units.list']),
    });
    renderHrPage(<HrUnitsPage onUnauthenticated={onUnauthenticated} companyId={companyId} />, { path: '/hr/org/units', title: 'واحدها' }, '/hr/org/units');
    expect(await screen.findByRole('heading', { level: 1, name: 'واحدهای سازمانی' })).toBeInTheDocument();
    expect(await screen.findByRole('link', { name: /دفتر مرکزی/ })).toBeInTheDocument();

    await userEvent.setup().selectOptions(screen.getByLabelText('نحوهٔ نمایش'), 'flat');
    const table = await screen.findByRole('table');
    expect(table).toHaveTextContent('دفتر مرکزی');
  });

  it('lists flat units with code, status and validity window', async () => {
    api({
      [`GET ${unitsPath}?page=1&pageSize=25`]: {
        status: 200,
        body: unitListResponse([unitItem({ name: 'فروش', code: 'SALES' })]),
      },
      [`GET ${treePath}`]: { status: 200, body: { roots: [] } },
      'GET /api/v1/me/permissions': hrPermissionRoute(['hr.organizational-units.list']),
    });
    renderHrPage(<HrUnitsPage onUnauthenticated={onUnauthenticated} companyId={companyId} />, { path: '/hr/org/units', title: 'واحدها' }, '/hr/org/units');
    await userEvent.setup().selectOptions(await screen.findByLabelText('نحوهٔ نمایش'), 'flat');
    const table = await screen.findByRole('table');
    expect(table).toHaveTextContent('فروش');
    expect(table).toHaveTextContent('SALES');
  });

  it('searches the flat list with the documented `search` parameter from page 1', async () => {
    const fetchMock = api({
      [`GET ${treePath}`]: { status: 200, body: { roots: [] } },
      [`GET ${unitsPath}?page=1&pageSize=25`]: { status: 200, body: unitListResponse([]) },
      [`GET ${unitsPath}?search=HQ&page=1&pageSize=25`]: { status: 200, body: unitListResponse([]) },
      'GET /api/v1/me/permissions': hrPermissionRoute(['hr.organizational-units.list']),
    });
    renderHrPage(<HrUnitsPage onUnauthenticated={onUnauthenticated} companyId={companyId} />, { path: '/hr/org/units', title: 'واحدها' }, '/hr/org/units');
    await userEvent.setup().selectOptions(await screen.findByLabelText('نحوهٔ نمایش'), 'flat');
    await screen.findByRole('table');
    await userEvent.setup().type(screen.getByLabelText('جستجو'), 'HQ');
    await waitFor(() => expect(fetchMock.mock.calls.some(([url]) => String(url).includes('search=HQ'))).toBe(true));
  });

  it('shows the standard access-denied panel on 403', async () => {
    api({
      [`GET ${treePath}`]: { status: 403, body: { error: 'شما مجوز دسترسی به این بخش را ندارید.' } },
      'GET /api/v1/me/permissions': hrPermissionRoute([]),
    });
    renderHrPage(<HrUnitsPage onUnauthenticated={onUnauthenticated} companyId={companyId} />, { path: '/hr/org/units', title: 'واحدها' }, '/hr/org/units');
    expect(await screen.findByText('دسترسی ندارید')).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('shows the create action only with the create permission hint', async () => {
    api({
      [`GET ${treePath}`]: { status: 200, body: { roots: [unitNode()] } },
      'GET /api/v1/me/permissions': hrPermissionRoute(['hr.organizational-units.create']),
    });
    renderHrPage(<HrUnitsPage onUnauthenticated={onUnauthenticated} companyId={companyId} />, { path: '/hr/org/units', title: 'واحدها' }, '/hr/org/units');
    expect(await screen.findByRole('link', { name: 'واحد جدید' })).toBeInTheDocument();
  });

  it('hides the create action without the permission hint (the server still decides)', async () => {
    api({
      [`GET ${treePath}`]: { status: 200, body: { roots: [unitNode()] } },
      'GET /api/v1/me/permissions': hrPermissionRoute(['hr.organizational-units.list']),
    });
    renderHrPage(<HrUnitsPage onUnauthenticated={onUnauthenticated} companyId={companyId} />, { path: '/hr/org/units', title: 'واحدها' }, '/hr/org/units');
    await screen.findByRole('link', { name: /دفتر مرکزی/ });
    expect(screen.queryByRole('link', { name: 'واحد جدید' })).not.toBeInTheDocument();
  });

  it('renders an empty tree with guidance', async () => {
    api({
      [`GET ${treePath}`]: { status: 200, body: { roots: [] } },
      'GET /api/v1/me/permissions': hrPermissionRoute([]),
    });
    renderHrPage(<HrUnitsPage onUnauthenticated={onUnauthenticated} companyId={companyId} />, { path: '/hr/org/units', title: 'واحدها' }, '/hr/org/units');
    expect(await screen.findByText('هنوز واحدی ثبت نشده است')).toBeInTheDocument();
  });
});
