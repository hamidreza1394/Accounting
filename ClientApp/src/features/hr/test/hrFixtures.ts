// HR test fixtures: builders for the real backend DTO shapes (see hrTypes.ts). They mirror
// `docs/HR_FRONTEND_API_CONTRACT.md` samples, so a test failure means the client or the
// documented contract drifted — never a made-up shape.

import type {
  HrAssignmentItem,
  HrEmployeeDetail,
  HrEmployeeHistoryResponse,
  HrEmployeeListItem,
  HrEmployeeListResponse,
  HrPositionDetail,
  HrPositionItem,
  HrPositionListResponse,
  HrUnitDetail,
  HrUnitItem,
  HrUnitListResponse,
  HrUnitSubtreeNode,
  HrUnitTreeNode,
} from '../api/hrTypes';

export const companyId = '0f3a1d2c-0000-4000-8000-000000000001';
export const employeeId = '8c5b7e2a-1111-4000-8000-00000000abcd';
export const unitId = '5d2e9f10-2222-4000-8000-00000000ef01';
export const positionId = '7a1c3b40-3333-4000-8000-00000000beef';
export const managerEmployeeId = '9d6a8f3b-4444-4000-8000-00000000cafe';

export function employeeListItem(overrides: Record<string, unknown> = {}): HrEmployeeListItem {
  return {
    id: employeeId,
    employeeNumber: 'EMP-101',
    firstName: 'علی',
    lastName: 'محمدی',
    employmentStatus: 1,
    hiredOnUtc: '2026-01-10T08:30:00Z',
    terminatedOnUtc: null,
    hasLinkedUser: false,
    unitId,
    unitName: 'دفتر مرکزی',
    positionId,
    positionName: 'حسابدار',
    ...overrides,
  };
}

export function employeeListResponse(items: readonly HrEmployeeListItem[], overrides: Record<string, unknown> = {}): HrEmployeeListResponse {
  return { items, totalCount: items.length, page: 1, pageSize: 25, ...overrides };
}

export function assignmentItem(overrides: Record<string, unknown> = {}): HrAssignmentItem {
  return {
    id: 'aa000000-0000-4000-8000-000000000001',
    employeeId,
    organizationalUnitId: unitId,
    unitCode: 'HQ',
    unitName: 'دفتر مرکزی',
    positionId,
    positionCode: 'ACC',
    positionName: 'حسابدار',
    managerEmployeeId: null,
    startOnUtc: '2026-11-01T07:00:00Z',
    endOnUtc: null,
    status: 1,
    ...overrides,
  };
}

export function employeeDetail(overrides: Record<string, unknown> = {}): HrEmployeeDetail {
  return {
    id: employeeId,
    employeeNumber: 'EMP-101',
    firstName: 'علی',
    lastName: 'محمدی',
    employmentStatus: 1,
    hiredOnUtc: '2026-01-10T08:30:00Z',
    terminatedOnUtc: null,
    userCompanyMembershipId: null,
    linkedUserId: null,
    createdAtUtc: '2026-11-01T06:59:58.100Z',
    rowVersion: 'AAAAAAAAB9I=',
    activeAssignment: assignmentItem(),
    ...overrides,
  };
}

export function unitItem(overrides: Record<string, unknown> = {}): HrUnitItem {
  return {
    id: unitId,
    code: 'HQ',
    name: 'دفتر مرکزی',
    description: null,
    parentUnitId: null,
    isActive: true,
    validFromUtc: null,
    validToUtc: null,
    ...overrides,
  };
}

export function unitListResponse(items: readonly HrUnitItem[], overrides: Record<string, unknown> = {}): HrUnitListResponse {
  return { items, totalCount: items.length, page: 1, pageSize: 25, ...overrides };
}

export function unitDetail(overrides: Record<string, unknown> = {}): HrUnitDetail {
  return { ...unitItem(), activeChildCount: 0, activeAssignmentCount: 1, rowVersion: 'AAAAAAAAB9I=', ...overrides };
}

export function unitTreeNode(overrides: Record<string, unknown> = {}, children: readonly HrUnitTreeNode[] = []): HrUnitTreeNode {
  return { ...unitItem(overrides), children };
}

export function subtreeNode(overrides: Record<string, unknown> = {}, children: readonly HrUnitSubtreeNode[] = []): HrUnitSubtreeNode {
  return {
    id: unitId,
    code: 'HQ',
    name: 'دفتر مرکزی',
    isActive: true,
    parentUnitId: null,
    activeAssignmentCount: 1,
    children,
    validFromUtc: null,
    validToUtc: null,
    ...overrides,
  };
}

export function positionItem(overrides: Record<string, unknown> = {}): HrPositionItem {
  return {
    id: positionId,
    code: 'ACC',
    name: 'حسابدار',
    description: null,
    rank: null,
    isActive: true,
    ...overrides,
  };
}

export function positionListResponse(items: readonly HrPositionItem[], overrides: Record<string, unknown> = {}): HrPositionListResponse {
  return { items, totalCount: items.length, page: 1, pageSize: 25, ...overrides };
}

export function positionDetail(overrides: Record<string, unknown> = {}): HrPositionDetail {
  return { ...positionItem(), activeAssignmentCount: 1, rowVersion: 'AAAAAAAAB9I=', ...overrides };
}

export function historyResponse(items: readonly Record<string, unknown>[], overrides: Record<string, unknown> = {}): HrEmployeeHistoryResponse {
  return {
    items: items.map((item) => ({
      eventType: 'HrEmployeeCreated',
      entityType: 'CompanyEmployee',
      entityId: employeeId,
      occurredAtUtc: '2026-11-05T06:59:58.100Z',
      actorUserId: 'e4e5f6a7-5555-4000-8000-00000000abcd',
      correlationId: '00-abc-01',
      ...item,
    })),
    totalCount: items.length,
    page: 1,
    pageSize: 25,
    ...overrides,
  };
}

export const antiforgeryRoute = {
  'GET /api/v1/antiforgery/token': { status: 200, body: { headerName: 'X-XSRF-TOKEN', requestToken: 'hr-token' } },
};
