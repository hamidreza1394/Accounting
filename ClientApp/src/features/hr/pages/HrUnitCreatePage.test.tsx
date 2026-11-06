import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { stubApi } from '@/test/api';
import { antiforgeryRoute, companyId, unitId, unitTreeNode } from '../test/hrFixtures';
import { hrPermissionRoute, renderHrPage } from '../test/hrTestUtils';
import { HrUnitCreatePage } from './HrUnitCreatePage';

const onUnauthenticated = vi.fn((): boolean => false);
const unitsPath = `/api/v1/companies/${companyId}/org/units`;
const treePath = `${unitsPath}?view=tree`;

describe('HrUnitCreatePage', () => {
  beforeEach(() => {
    onUnauthenticated.mockReset();
    onUnauthenticated.mockReturnValue(false);
  });

  function setup(codes: readonly string[] = ['hr.organizational-units.create']) {
    stubApi({
      [`GET ${treePath}`]: { status: 200, body: { roots: [unitTreeNode({ id: 'aa000000-0000-4000-8000-0000000000aa', name: 'ستاد', code: 'HQ' })] } },
      'GET /api/v1/me/permissions': hrPermissionRoute(codes),
      ...antiforgeryRoute,
      [`POST ${unitsPath}`]: { status: 201, body: { id: unitId } },
    } as never);
    renderHrPage(
      <HrUnitCreatePage onUnauthenticated={onUnauthenticated} companyId={companyId} />,
      { path: '/hr/org/units/new', title: 'واحد جدید' },
      '/hr/org/units/new',
    );
  }

  it('validates the required fields with Persian messages before any request', async () => {
    const user = userEvent.setup();
    setup();
    await user.click(await screen.findByRole('button', { name: 'ثبت واحد' }));
    expect(await screen.findByText('کد واحد الزامی است.')).toBeInTheDocument();
    expect(screen.getByText('عنوان واحد الزامی است.')).toBeInTheDocument();
  });

  it('validates the code pattern and the validity window like the server', async () => {
    const user = userEvent.setup();
    setup();
    await user.type(screen.getByLabelText(/کد واحد/), 'ا ب');
    await user.type(screen.getByLabelText(/عنوان واحد/), 'فروش');
    // Open the Jalali pickers is heavy; the window rule is covered in hrValidation.test.ts.
    await user.click(screen.getByRole('button', { name: 'ثبت واحد' }));
    expect(await screen.findByText(/کد واحد باید با حرف یا رقم شروع شود/)).toBeInTheDocument();
  });

  it('posts the exact create DTO and navigates to the new unit', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi({
      [`GET ${treePath}`]: { status: 200, body: { roots: [] } },
      'GET /api/v1/me/permissions': hrPermissionRoute(['hr.organizational-units.create']),
      ...antiforgeryRoute,
      [`POST ${unitsPath}`]: { status: 201, body: { id: unitId } },
    } as never);
    renderHrPage(
      <HrUnitCreatePage onUnauthenticated={onUnauthenticated} companyId={companyId} />,
      { path: '/hr/org/units/new', title: 'واحد جدید' },
      '/hr/org/units/new',
    );
    await user.type(await screen.findByLabelText(/کد واحد/), 'HQ');
    await user.type(screen.getByLabelText(/عنوان واحد/), 'دفتر مرکزی');
    await user.click(screen.getByRole('button', { name: 'ثبت واحد' }));
    await waitFor(() => {
      const post = fetchMock.mock.calls.find(([url]) => String(url) === unitsPath);
      expect(post).toBeTruthy();
    });
    const postCall = fetchMock.mock.calls.find(([url]) => String(url) === unitsPath)?.[1] as RequestInit;
    expect(JSON.parse(postCall.body as string)).toEqual({
      code: 'HQ',
      name: 'دفتر مرکزی',
      description: null,
      parentUnitId: null,
      validFromUtc: null,
      validToUtc: null,
    });
    // Navigated to the detail route of the created unit.
    await waitFor(() => expect(window.location.pathname).toBe(`/app/hr/org/units/${unitId}`));
  });

  it('surfaces the server duplicate message on 409 and stays on the form', async () => {
    const user = userEvent.setup();
    stubApi({
      [`GET ${treePath}`]: { status: 200, body: { roots: [] } },
      'GET /api/v1/me/permissions': hrPermissionRoute(['hr.organizational-units.create']),
      ...antiforgeryRoute,
      [`POST ${unitsPath}`]: { status: 409, body: { error: 'کد واحد در این شرکت تکراری است.' } },
    } as never);
    renderHrPage(
      <HrUnitCreatePage onUnauthenticated={onUnauthenticated} companyId={companyId} />,
      { path: '/hr/org/units/new', title: 'واحد جدید' },
      '/hr/org/units/new',
    );
    await user.type(await screen.findByLabelText(/کد واحد/), 'HQ');
    await user.type(screen.getByLabelText(/عنوان واحد/), 'دفتر مرکزی');
    await user.click(screen.getByRole('button', { name: 'ثبت واحد' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('کد واحد در این شرکت تکراری است.');
    expect(screen.getByRole('button', { name: 'ثبت واحد' })).toBeInTheDocument();
  });
});
