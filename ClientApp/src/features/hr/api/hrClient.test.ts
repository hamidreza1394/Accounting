import { describe, expect, it } from 'vitest';
import { ApiError, UnauthenticatedError } from '@/api/client';
import { stubApi } from '@/test/api';
import {
  antiforgeryRoute,
  assignmentItem,
  companyId,
  employeeDetail,
  employeeId,
  employeeListResponse,
  employeeListItem,
  historyResponse,
  positionDetail,
  positionId,
  positionListResponse,
  positionItem,
  subtreeNode,
  unitDetail,
  unitId,
  unitListResponse,
  unitTreeNode,
} from '../test/hrFixtures';
import {
  changeHrAssignment,
  createHrEmployee,
  createHrPosition,
  createHrUnit,
  deactivateHrEmployee,
  endHrAssignment,
  getHrEmployee,
  getHrEmployeeHistory,
  getHrPosition,
  getHrUnit,
  getHrUnitSubtree,
  getHrUnitTree,
  linkHrEmployeeUser,
  listHrAssignments,
  listHrEmployees,
  listHrPositions,
  listHrUnitEmployees,
  listHrUnits,
  startHrAssignment,
  unlinkHrEmployeeUser,
  updateHrEmployee,
  updateHrPosition,
  updateHrUnit,
} from './hrClient';

const employeesPath = `/api/v1/companies/${companyId}/org/employees`;
const unitsPath = `/api/v1/companies/${companyId}/org/units`;
const positionsPath = `/api/v1/companies/${companyId}/org/positions`;

function lastBody(fetchMock: { mock: { calls: readonly unknown[][] } }): unknown {
  const call = fetchMock.mock.calls.at(-1)?.[1] as RequestInit | undefined;
  return typeof call?.body === 'string' ? JSON.parse(call.body) : undefined;
}

describe('hrClient — employees', () => {
  it('lists employees with the documented query parameters and parses the page', async () => {
    const fetchMock = stubApi({
      [`GET ${employeesPath}?search=%D8%B9%D9%84%DB%8C&status=1&unitId=${unitId}&page=2&pageSize=50`]: {
        status: 200,
        body: employeeListResponse([employeeListItem()], { page: 2, pageSize: 50, totalCount: 51 }),
      },
    });
    const result = await listHrEmployees(companyId, { search: 'علی', status: 1, unitId, page: 2, pageSize: 50 });
    expect(result.items).toHaveLength(1);
    expect(result.totalCount).toBe(51);
    expect(result.page).toBe(2);
    expect(fetchMock.mock.calls[0]?.[0]).toBe(
      `${employeesPath}?search=%D8%B9%D9%84%DB%8C&status=1&unitId=${unitId}&page=2&pageSize=50`,
    );
  });

  it('omits empty filters instead of sending empty strings', async () => {
    stubApi({ [`GET ${employeesPath}?page=1&pageSize=25`]: { status: 200, body: employeeListResponse([]) } });
    await listHrEmployees(companyId, { page: 1, pageSize: 25 });
  });

  it('throws UnauthenticatedError on 401', async () => {
    stubApi({ [`GET ${employeesPath}?page=1&pageSize=25`]: { status: 401 } });
    await expect(listHrEmployees(companyId, { page: 1, pageSize: 25 })).rejects.toBeInstanceOf(UnauthenticatedError);
  });

  it('maps the Persian `{ error }` body of 403, 404 and 409 onto ApiError', async () => {
    stubApi({
      [`GET ${employeesPath}?page=1&pageSize=25`]: { status: 403, body: { error: 'شما مجوز دسترسی به این بخش را ندارید.' } },
      [`GET ${employeesPath}/${employeeId}`]: { status: 404 },
      [`POST ${employeesPath}`]: { status: 409, body: { error: 'شماره پرسنلی در این شرکت تکراری است.' } },
      ...antiforgeryRoute,
    });
    await expect(listHrEmployees(companyId, { page: 1, pageSize: 25 })).rejects.toThrow('شما مجوز دسترسی به این بخش را ندارید.');
    await expect(getHrEmployee(companyId, employeeId)).rejects.toMatchObject({ status: 404 });
    await expect(
      createHrEmployee(companyId, { employeeNumber: 'EMP-1', firstName: 'علی', lastName: 'محمدی', hiredOnUtc: null }),
    ).rejects.toMatchObject({ status: 409, message: 'شماره پرسنلی در این شرکت تکراری است.' });
  });

  it('creates an employee with the exact create DTO and the anti-forgery header', async () => {
    const fetchMock = stubApi({
      ...antiforgeryRoute,
      [`POST ${employeesPath}`]: { status: 201, body: { id: employeeId } },
    });
    const created = await createHrEmployee(companyId, {
      employeeNumber: 'EMP-101',
      firstName: 'علی',
      lastName: 'محمدی',
      hiredOnUtc: '2026-01-10T00:00:00Z',
    });
    expect(created.id).toBe(employeeId);
    const request = fetchMock.mock.calls.at(-1)?.[1] as RequestInit;
    expect(request.method).toBe('POST');
    expect(request.headers).toMatchObject({ 'X-XSRF-TOKEN': 'hr-token' });
    expect(lastBody(fetchMock)).toEqual({
      employeeNumber: 'EMP-101',
      firstName: 'علی',
      lastName: 'محمدی',
      hiredOnUtc: '2026-01-10T00:00:00Z',
    });
  });

  it('sends the create body without hiredOnUtc when no date was picked', async () => {
    const fetchMock = stubApi({ ...antiforgeryRoute, [`POST ${employeesPath}`]: { status: 201, body: { id: employeeId } } });
    await createHrEmployee(companyId, { employeeNumber: 'EMP-2', firstName: 'سارا', lastName: 'کریمی', hiredOnUtc: null });
    expect(lastBody(fetchMock)).toEqual({ employeeNumber: 'EMP-2', firstName: 'سارا', lastName: 'کریمی' });
  });

  it('updates an employee with the mandatory rowVersion token', async () => {
    const fetchMock = stubApi({ ...antiforgeryRoute, [`PUT ${employeesPath}/${employeeId}`]: { status: 204 } });
    await updateHrEmployee(companyId, employeeId, {
      firstName: 'علی',
      lastName: 'محمدی',
      employmentStatus: 2,
      hiredOnUtc: null,
      rowVersion: 'AAAAAAAAB9I=',
    });
    expect(lastBody(fetchMock)).toEqual({
      firstName: 'علی',
      lastName: 'محمدی',
      employmentStatus: 2,
      hiredOnUtc: null,
      rowVersion: 'AAAAAAAAB9I=',
    });
  });

  it('parses the employee detail with its active assignment and rowVersion', async () => {
    stubApi({ [`GET ${employeesPath}/${employeeId}`]: { status: 200, body: employeeDetail() } });
    const detail = await getHrEmployee(companyId, employeeId);
    expect(detail.rowVersion).toBe('AAAAAAAAB9I=');
    expect(detail.activeAssignment?.unitName).toBe('دفتر مرکزی');
    expect(detail.activeAssignment?.status).toBe(1);
  });

  it('posts deactivate, link and unlink without a body but with the anti-forgery token', async () => {
    const fetchMock = stubApi({
      ...antiforgeryRoute,
      [`POST ${employeesPath}/${employeeId}/deactivate`]: { status: 204 },
      [`PUT ${employeesPath}/${employeeId}/user`]: { status: 204 },
      [`DELETE ${employeesPath}/${employeeId}/user`]: { status: 204 },
    });
    await deactivateHrEmployee(companyId, employeeId);
    await linkHrEmployeeUser(companyId, employeeId, 'e4e5f6a7-5555-4000-8000-00000000abcd');
    await unlinkHrEmployeeUser(companyId, employeeId);
    expect(lastBody(fetchMock)).toBeUndefined();
    const userCalls = fetchMock.mock.calls.filter(([url]) => String(url).endsWith('/user')) as unknown as [string, RequestInit][];
    expect(userCalls).toHaveLength(2); // PUT (link) and DELETE (unlink)
    expect(JSON.parse(userCalls[0]?.[1]?.body as string)).toEqual({ userId: 'e4e5f6a7-5555-4000-8000-00000000abcd' });
    expect(userCalls[1]?.[1]?.method).toBe('DELETE');
    expect(userCalls[1]?.[1]?.body).toBeUndefined();
  });

  it('sends the transfer (assignment-change) with null = keep semantics', async () => {
    const fetchMock = stubApi({
      ...antiforgeryRoute,
      [`POST ${employeesPath}/${employeeId}/assignment-change`]: { status: 200, body: { id: assignmentItem().id } },
    });
    await changeHrAssignment(companyId, employeeId, { organizationalUnitId: unitId, positionId: null, managerEmployeeId: null });
    expect(lastBody(fetchMock)).toEqual({ organizationalUnitId: unitId, positionId: null, managerEmployeeId: null });
  });

  it('fetches the paged employee history (the audit allowlist)', async () => {
    stubApi({
      [`GET ${employeesPath}/${employeeId}/history?page=1&pageSize=25`]: {
        status: 200,
        body: historyResponse([{ eventType: 'HrAssignmentStarted', entityType: 'CompanyEmployeeAssignment' }]),
      },
    });
    const history = await getHrEmployeeHistory(companyId, employeeId, 1, 25);
    expect(history.items[0]?.eventType).toBe('HrAssignmentStarted');
    expect(Object.keys(history.items[0] ?? {}).sort()).toEqual([
      'actorUserId',
      'correlationId',
      'entityId',
      'entityType',
      'eventType',
      'occurredAtUtc',
    ]);
  });
});

describe('hrClient — units', () => {
  it('lists the flat view with paging and the tree view without it', async () => {
    stubApi({
      [`GET ${unitsPath}?page=1&pageSize=25`]: { status: 200, body: unitListResponse([]) },
      'GET /api/v1/companies/0f3a1d2c-0000-4000-8000-000000000001/org/units?view=tree': {
        status: 200,
        body: { roots: [unitTreeNode()] },
      },
    });
    await listHrUnits(companyId, { view: 'flat', page: 1, pageSize: 25 });
    const tree = await getHrUnitTree(companyId);
    expect(tree.roots).toHaveLength(1);
  });

  it('searches the flat list by the documented `search` parameter', async () => {
    stubApi({ [`GET ${unitsPath}?search=HQ&page=1&pageSize=25`]: { status: 200, body: unitListResponse([]) } });
    await listHrUnits(companyId, { view: 'flat', search: 'HQ', page: 1, pageSize: 25 });
  });

  it('creates a unit with the exact create DTO', async () => {
    const fetchMock = stubApi({ ...antiforgeryRoute, [`POST ${unitsPath}`]: { status: 201, body: { id: unitId } } });
    await createHrUnit(companyId, {
      code: 'HQ',
      name: 'دفتر مرکزی',
      description: null,
      parentUnitId: null,
      validFromUtc: null,
      validToUtc: null,
    });
    expect(lastBody(fetchMock)).toEqual({
      code: 'HQ',
      name: 'دفتر مرکزی',
      description: null,
      parentUnitId: null,
      validFromUtc: null,
      validToUtc: null,
    });
  });

  it('updates a unit with the mandatory rowVersion and the documented null semantics', async () => {
    const fetchMock = stubApi({ ...antiforgeryRoute, [`PUT ${unitsPath}/${unitId}`]: { status: 204 } });
    await updateHrUnit(companyId, unitId, {
      name: 'دفتر مرکزی',
      description: null,
      parentUnitId: null,
      isActive: false,
      validFromUtc: null,
      validToUtc: null,
      rowVersion: 'AAAAAAAAB9I=',
    });
    expect(lastBody(fetchMock)).toEqual({
      name: 'دفتر مرکزی',
      description: null,
      parentUnitId: null,
      isActive: false,
      validFromUtc: null,
      validToUtc: null,
      rowVersion: 'AAAAAAAAB9I=',
    });
  });

  it('reads unit details, subtree and unit employees', async () => {
    stubApi({
      [`GET ${unitsPath}/${unitId}`]: { status: 200, body: unitDetail() },
      [`GET ${unitsPath}/${unitId}/subtree`]: { status: 200, body: subtreeNode() },
      [`GET ${unitsPath}/${unitId}/employees?page=1&pageSize=25`]: {
        status: 200,
        body: {
          items: [
            {
              employeeId,
              employeeNumber: 'EMP-101',
              firstName: 'علی',
              lastName: 'محمدی',
              positionId,
              positionName: 'حسابدار',
              startOnUtc: '2026-11-01T07:00:00Z',
            },
          ],
          totalCount: 1,
          page: 1,
          pageSize: 25,
        },
      },
    });
    const detail = await getHrUnit(companyId, unitId);
    expect(detail.activeAssignmentCount).toBe(1);
    const subtree = await getHrUnitSubtree(companyId, unitId);
    expect(subtree.activeAssignmentCount).toBe(1);
    const employees = await listHrUnitEmployees(companyId, unitId, 1, 25);
    expect(employees.items[0]?.employeeId).toBe(employeeId);
  });
});

describe('hrClient — positions', () => {
  it('lists, creates and updates positions with the real DTOs', async () => {
    const fetchMock = stubApi({
      [`GET ${positionsPath}?page=1&pageSize=25`]: { status: 200, body: positionListResponse([positionItem()]) },
      [`GET ${positionsPath}/${positionId}`]: { status: 200, body: positionDetail() },
      ...antiforgeryRoute,
      [`POST ${positionsPath}`]: { status: 201, body: { id: positionId } },
      [`PUT ${positionsPath}/${positionId}`]: { status: 204 },
    });
    const list = await listHrPositions(companyId, { page: 1, pageSize: 25 });
    expect(list.items[0]?.code).toBe('ACC');
    const detail = await getHrPosition(companyId, positionId);
    expect(detail.rowVersion).toBe('AAAAAAAAB9I=');
    await createHrPosition(companyId, { code: 'ACC', name: 'حسابدار', description: null, rank: null });
    expect(lastBody(fetchMock)).toEqual({ code: 'ACC', name: 'حسابدار', description: null, rank: null });
    await updateHrPosition(companyId, positionId, { name: 'حسابدار', description: null, rank: 3, isActive: true, rowVersion: 'AAAAAAAAB9I=' });
    expect(lastBody(fetchMock)).toEqual({ name: 'حسابدار', description: null, rank: 3, isActive: true, rowVersion: 'AAAAAAAAB9I=' });
  });
});

describe('hrClient — assignments', () => {
  it('starts an assignment with the exact request DTO', async () => {
    const fetchMock = stubApi({
      ...antiforgeryRoute,
      [`POST /api/v1/companies/${companyId}/org/assignments`]: { status: 201, body: { id: assignmentItem().id } },
    });
    await startHrAssignment(companyId, {
      employeeId,
      organizationalUnitId: unitId,
      positionId,
      managerEmployeeId: null,
      startOnUtc: '2026-11-05T07:00:00Z',
      endActive: false,
    });
    expect(lastBody(fetchMock)).toEqual({
      employeeId,
      organizationalUnitId: unitId,
      positionId,
      managerEmployeeId: null,
      startOnUtc: '2026-11-05T07:00:00Z',
      endActive: false,
    });
  });

  it('ends an assignment now (empty body) or at an explicit instant', async () => {
    const fetchMock = stubApi({
      ...antiforgeryRoute,
      [`POST /api/v1/companies/${companyId}/org/assignments/${assignmentItem().id}/end`]: { status: 204 },
    });
    const path = `/api/v1/companies/${companyId}/org/assignments/${assignmentItem().id}/end`;
    await endHrAssignment(companyId, assignmentItem().id, null);
    expect(lastBody(fetchMock)).toEqual({});
    await endHrAssignment(companyId, assignmentItem().id, '2026-11-05T10:00:00Z');
    expect(lastBody(fetchMock)).toEqual({ endOnUtc: '2026-11-05T10:00:00Z' });
    expect(path).toBeTruthy();
  });

  it('lists assignments by employee (the server requires at least one subject)', async () => {
    stubApi({
      [`GET /api/v1/companies/${companyId}/org/assignments?employeeId=${employeeId}&page=1&pageSize=25`]: {
        status: 200,
        body: { items: [assignmentItem()], totalCount: 1, page: 1, pageSize: 25 },
      },
    });
    const list = await listHrAssignments(companyId, { employeeId }, 1, 25);
    expect(list.items[0]?.status).toBe(1);
  });

  it('surfaces a validation 400 with its Persian message', async () => {
    stubApi({
      ...antiforgeryRoute,
      [`POST /api/v1/companies/${companyId}/org/assignments`]: {
        status: 400,
        body: { error: 'انتساب آینده‌نگر پشتیبانی نمی‌شود؛ تاریخ شروع باید امروز یا گذشته باشد.' },
      },
    });
    await expect(
      startHrAssignment(companyId, {
        employeeId,
        organizationalUnitId: null,
        positionId: null,
        managerEmployeeId: null,
        startOnUtc: '2030-01-01T00:00:00Z',
        endActive: false,
      }),
    ).rejects.toBeInstanceOf(ApiError);
  });
});
