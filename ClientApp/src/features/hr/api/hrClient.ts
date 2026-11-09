// HR organizational structure API client (S2, D-072). Every function maps 1:1 onto a real
// endpoint of `Server/Api/HrOrganizationalStructureEndpoints.cs` — no invented route, field or
// status code. Reads use a small same-origin GET helper with the same conventions as the shared
// client (`credentials: 'same-origin'`, `no-store`, 401 → UnauthenticatedError, Persian error
// mapping through `normalizeApiError`); writes reuse the shared `sendJson`, which fetches the
// anti-forgery token and sends `X-XSRF-TOKEN`. The company id is always the validated work-context
// company and only ever travels in the route, never as an authorization input (D-013).

import { apiUrl, normalizeApiError, sendFile, sendJson, UnauthenticatedError } from '@/api/client';
import {
  parseCreatedId,
  parseHrAssignmentListResponse,
  parseHrEmployeeDetail,
  parseHrEmployeeHistoryResponse,
  parseHrEmployeeListResponse,
  parseHrNextEmployeeNumber,
  parseHrPositionDetail,
  parseHrPositionEmployeeListResponse,
  parseHrPositionListResponse,
  parseHrUnitDetail,
  parseHrUnitEmployeeListResponse,
  parseHrUnitListResponse,
  parseHrUnitSubtree,
  parseHrUnitTreeResponse,
  type HrAssignmentListResponse,
  type HrEmployeeDetail,
  type HrEmployeeHistoryResponse,
  type HrEmployeeListResponse,
  type HrEmploymentStatus,
  type HrNextEmployeeNumber,
  type HrPositionDetail,
  type HrPositionEmployeeListResponse,
  type HrPositionListResponse,
  type HrUnitDetail,
  type HrUnitEmployeeListResponse,
  type HrUnitListResponse,
  type HrUnitSubtreeNode,
  type HrUnitTreeResponse,
} from './hrTypes';

function hrPath(companyId: string, ...segments: readonly string[]): string {
  return `/companies/${encodeURIComponent(companyId)}/org/${segments.map(encodeURIComponent).join('/')}`;
}

async function hrGet(path: string, signal?: AbortSignal): Promise<unknown> {
  const response = await fetch(`/api/v1${path}`, {
    method: 'GET',
    credentials: 'same-origin',
    headers: { Accept: 'application/json' },
    cache: 'no-store',
    ...(signal ? { signal } : {}),
  });
  if (response.status === 401) throw new UnauthenticatedError();
  if (!response.ok) {
    throw normalizeApiError(response.status, response.status < 500 ? await response.json().catch(() => undefined) : undefined);
  }
  return response.json();
}

function query(params: Record<string, string | number | undefined>): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== '') search.set(key, String(value));
  }
  const text = search.toString();
  return text ? `?${text}` : '';
}

// ---------------------------------------------------------------- employees

/** Query of `GET /org/employees`; the server clamps page/pageSize and validates `status`. */
export type HrEmployeeQuery = {
  search?: string;
  status?: HrEmploymentStatus;
  unitId?: string;
  /** Stage 3 profile: only the employee linked to a membership of this user (fail-closed). */
  userId?: string;
  page: number;
  pageSize: number;
};

export async function listHrEmployees(companyId: string, queryValues: HrEmployeeQuery, signal?: AbortSignal): Promise<HrEmployeeListResponse> {
  const path = `${hrPath(companyId, 'employees')}${query({
    search: queryValues.search,
    status: queryValues.status,
    unitId: queryValues.unitId,
    userId: queryValues.userId,
    page: queryValues.page,
    pageSize: queryValues.pageSize,
  })}`;
  return parseHrEmployeeListResponse(await hrGet(path, signal));
}

export async function getHrEmployee(companyId: string, employeeId: string, signal?: AbortSignal): Promise<HrEmployeeDetail> {
  return parseHrEmployeeDetail(await hrGet(hrPath(companyId, 'employees', employeeId), signal));
}

/**
 * `GET /org/employees/next-number` → 200 `{ suggestedEmployeeNumber }` (`hr.employees.create`).
 * A suggestion only: nothing is reserved, so the create call may still answer 409.
 */
export async function getNextEmployeeNumber(companyId: string, signal?: AbortSignal): Promise<HrNextEmployeeNumber> {
  return parseHrNextEmployeeNumber(await hrGet(hrPath(companyId, 'employees', 'next-number'), signal));
}

/**
 * The URL an `<img>` loads the personnel photo from (`GET /org/employees/{id}/photo`): same-origin,
 * served only after the server checked the company scope and the `hr.employees.view` permission.
 * `version` changes after an upload so the browser never shows the replaced picture.
 */
export function employeePhotoUrl(companyId: string, employeeId: string, version: number): string {
  return `${apiUrl(hrPath(companyId, 'employees', employeeId, 'photo'))}?v=${version}`;
}

/** `PUT /org/employees/{id}/photo` → 204. The body is the image file itself (JPEG, PNG or WebP, up to 2 MiB). */
export async function putEmployeePhoto(companyId: string, employeeId: string, file: File): Promise<void> {
  await sendFile('PUT', hrPath(companyId, 'employees', employeeId, 'photo'), file, file.type || 'application/octet-stream');
}

/** `DELETE /org/employees/{id}/photo` → 204 (also when there is no photo). */
export async function deleteEmployeePhoto(companyId: string, employeeId: string): Promise<void> {
  await sendJson('DELETE', hrPath(companyId, 'employees', employeeId, 'photo'));
}

/** `POST /org/employees` → 201 `{ id }`. Duplicate employee number answers 409. */
export async function createHrEmployee(
  companyId: string,
  request: { employeeNumber: string; firstName: string; lastName: string; hiredOnUtc: string | null },
): Promise<{ id: string }> {
  const response = await sendJson('POST', hrPath(companyId, 'employees'), {
    employeeNumber: request.employeeNumber,
    firstName: request.firstName,
    lastName: request.lastName,
    ...(request.hiredOnUtc === null ? {} : { hiredOnUtc: request.hiredOnUtc }),
  });
  return parseCreatedId(await response.json());
}

/**
 * `PUT /org/employees/{id}` → 204. `rowVersion` is mandatory (400 without, 409 stale).
 * `employmentStatus: 3` (terminated) is rejected by the server; termination goes through
 * `POST .../deactivate` only. `hiredOnUtc: null` means "keep the current value".
 */
export async function updateHrEmployee(
  companyId: string,
  employeeId: string,
  request: { firstName: string; lastName: string; employmentStatus: HrEmploymentStatus; hiredOnUtc: string | null; rowVersion: string },
): Promise<void> {
  await sendJson('PUT', hrPath(companyId, 'employees', employeeId), {
    firstName: request.firstName,
    lastName: request.lastName,
    employmentStatus: request.employmentStatus,
    hiredOnUtc: request.hiredOnUtc,
    rowVersion: request.rowVersion,
  });
}

/** `POST /org/employees/{id}/deactivate` → 204. Irreversible; also ends the active assignment. */
export async function deactivateHrEmployee(companyId: string, employeeId: string): Promise<void> {
  await sendJson('POST', hrPath(companyId, 'employees', employeeId, 'deactivate'));
}

/** `PUT /org/employees/{id}/user` → 204. The id resolves to an active membership of this company. */
export async function linkHrEmployeeUser(companyId: string, employeeId: string, userId: string): Promise<void> {
  await sendJson('PUT', hrPath(companyId, 'employees', employeeId, 'user'), { userId });
}

/** `DELETE /org/employees/{id}/user` → 204. */
export async function unlinkHrEmployeeUser(companyId: string, employeeId: string): Promise<void> {
  await sendJson('DELETE', hrPath(companyId, 'employees', employeeId, 'user'));
}

/**
 * `POST /org/employees/{id}/assignment-change` → 200 `{ id }`. Atomic transfer: the current
 * assignment ends and the new one starts in one transaction. A `null` field keeps the previous
 * value; there is no `startOnUtc` — the new assignment starts now.
 */
export async function changeHrAssignment(
  companyId: string,
  employeeId: string,
  request: { organizationalUnitId: string | null; positionId: string | null; managerEmployeeId: string | null },
): Promise<{ id: string }> {
  const response = await sendJson('POST', hrPath(companyId, 'employees', employeeId, 'assignment-change'), {
    organizationalUnitId: request.organizationalUnitId,
    positionId: request.positionId,
    managerEmployeeId: request.managerEmployeeId,
  });
  return parseCreatedId(await response.json());
}

/** `GET /org/employees/{id}/history` — the audit allowlist, newest first, paged. */
export async function getHrEmployeeHistory(
  companyId: string,
  employeeId: string,
  page: number,
  pageSize: number,
  signal?: AbortSignal,
): Promise<HrEmployeeHistoryResponse> {
  return parseHrEmployeeHistoryResponse(
    await hrGet(`${hrPath(companyId, 'employees', employeeId, 'history')}${query({ page, pageSize })}`, signal),
  );
}

// ---------------------------------------------------------------- units

export async function listHrUnits(
  companyId: string,
  options: { view: 'flat' | 'tree'; search?: string; page: number; pageSize: number },
  signal?: AbortSignal,
): Promise<HrUnitListResponse> {
  return parseHrUnitListResponse(
    await hrGet(
      `${hrPath(companyId, 'units')}${query({
        view: options.view === 'tree' ? 'tree' : undefined,
        search: options.search,
        ...(options.view === 'flat' ? { page: options.page, pageSize: options.pageSize } : {}),
      })}`,
      signal,
    ),
  );
}

export async function getHrUnitTree(companyId: string, signal?: AbortSignal): Promise<HrUnitTreeResponse> {
  return parseHrUnitTreeResponse(await hrGet(`${hrPath(companyId, 'units')}?view=tree`, signal));
}

export async function getHrUnit(companyId: string, unitId: string, signal?: AbortSignal): Promise<HrUnitDetail> {
  return parseHrUnitDetail(await hrGet(hrPath(companyId, 'units', unitId), signal));
}

/** `POST /org/units` → 201 `{ id }`. */
export async function createHrUnit(
  companyId: string,
  request: {
    code: string;
    name: string;
    description: string | null;
    parentUnitId: string | null;
    validFromUtc: string | null;
    validToUtc: string | null;
  },
): Promise<{ id: string }> {
  const response = await sendJson('POST', hrPath(companyId, 'units'), {
    code: request.code,
    name: request.name,
    description: request.description,
    parentUnitId: request.parentUnitId,
    validFromUtc: request.validFromUtc,
    validToUtc: request.validToUtc,
  });
  return parseCreatedId(await response.json());
}

/**
 * `PUT /org/units/{id}` → 204. Documented patch semantics of the backend: `parentUnitId: null`
 * makes the unit a root, `validFromUtc: null` KEEPS the current value (it cannot be cleared here),
 * `validToUtc: null` CLEARS it, and `rowVersion` is mandatory.
 */
export async function updateHrUnit(
  companyId: string,
  unitId: string,
  request: {
    name: string;
    description: string | null;
    parentUnitId: string | null;
    isActive: boolean;
    validFromUtc: string | null;
    validToUtc: string | null;
    rowVersion: string;
  },
): Promise<void> {
  await sendJson('PUT', hrPath(companyId, 'units', unitId), {
    name: request.name,
    description: request.description,
    parentUnitId: request.parentUnitId,
    isActive: request.isActive,
    validFromUtc: request.validFromUtc,
    validToUtc: request.validToUtc,
    rowVersion: request.rowVersion,
  });
}

/** `POST /org/units/{id}/deactivate` → 204; blocked with 409 by active children or assignments. */
export async function deactivateHrUnit(companyId: string, unitId: string): Promise<void> {
  await sendJson('POST', hrPath(companyId, 'units', unitId, 'deactivate'));
}

/** `GET /org/units/{id}/subtree` — the bounded, cycle-safe subtree with assignment counts. */
export async function getHrUnitSubtree(companyId: string, unitId: string, signal?: AbortSignal): Promise<HrUnitSubtreeNode> {
  return parseHrUnitSubtree(await hrGet(hrPath(companyId, 'units', unitId, 'subtree'), signal));
}

export async function listHrUnitEmployees(
  companyId: string,
  unitId: string,
  page: number,
  pageSize: number,
  signal?: AbortSignal,
): Promise<HrUnitEmployeeListResponse> {
  return parseHrUnitEmployeeListResponse(
    await hrGet(`${hrPath(companyId, 'units', unitId, 'employees')}${query({ page, pageSize })}`, signal),
  );
}

// ---------------------------------------------------------------- positions

export async function listHrPositions(
  companyId: string,
  options: { search?: string; page: number; pageSize: number },
  signal?: AbortSignal,
): Promise<HrPositionListResponse> {
  return parseHrPositionListResponse(
    await hrGet(`${hrPath(companyId, 'positions')}${query({ search: options.search, page: options.page, pageSize: options.pageSize })}`, signal),
  );
}

export async function getHrPosition(companyId: string, positionId: string, signal?: AbortSignal): Promise<HrPositionDetail> {
  return parseHrPositionDetail(await hrGet(hrPath(companyId, 'positions', positionId), signal));
}

export async function createHrPosition(
  companyId: string,
  request: { code: string; name: string; description: string | null; rank: number | null },
): Promise<{ id: string }> {
  const response = await sendJson('POST', hrPath(companyId, 'positions'), {
    code: request.code,
    name: request.name,
    description: request.description,
    rank: request.rank,
  });
  return parseCreatedId(await response.json());
}

/** `PUT /org/positions/{id}` → 204; `rank: null` clears the rank, `rowVersion` is mandatory. */
export async function updateHrPosition(
  companyId: string,
  positionId: string,
  request: { name: string; description: string | null; rank: number | null; isActive: boolean; rowVersion: string },
): Promise<void> {
  await sendJson('PUT', hrPath(companyId, 'positions', positionId), {
    name: request.name,
    description: request.description,
    rank: request.rank,
    isActive: request.isActive,
    rowVersion: request.rowVersion,
  });
}

/** `POST /org/positions/{id}/deactivate` → 204; blocked with 409 by active assignments. */
export async function deactivateHrPosition(companyId: string, positionId: string): Promise<void> {
  await sendJson('POST', hrPath(companyId, 'positions', positionId, 'deactivate'));
}

export async function listHrPositionEmployees(
  companyId: string,
  positionId: string,
  page: number,
  pageSize: number,
  signal?: AbortSignal,
): Promise<HrPositionEmployeeListResponse> {
  return parseHrPositionEmployeeListResponse(
    await hrGet(`${hrPath(companyId, 'positions', positionId, 'employees')}${query({ page, pageSize })}`, signal),
  );
}

// ---------------------------------------------------------------- assignments

/**
 * `GET /org/assignments` — at least one of employeeId/unitId/positionId is REQUIRED by the
 * server (400 otherwise). The UI only calls it with an explicit subject.
 */
export async function listHrAssignments(
  companyId: string,
  subject: { employeeId?: string; unitId?: string; positionId?: string },
  page: number,
  pageSize: number,
  signal?: AbortSignal,
): Promise<HrAssignmentListResponse> {
  return parseHrAssignmentListResponse(
    await hrGet(
      `${hrPath(companyId, 'assignments')}${query({ ...subject, page, pageSize })}`,
      signal,
    ),
  );
}

/**
 * `POST /org/assignments` → 201 `{ id }`. Future starts and starts before `hiredOnUtc` are
 * rejected (400); an existing active assignment needs `endActive: true` (otherwise 409).
 */
export async function startHrAssignment(
  companyId: string,
  request: {
    employeeId: string;
    organizationalUnitId: string | null;
    positionId: string | null;
    managerEmployeeId: string | null;
    startOnUtc: string;
    endActive: boolean;
  },
): Promise<{ id: string }> {
  const response = await sendJson('POST', hrPath(companyId, 'assignments'), {
    employeeId: request.employeeId,
    organizationalUnitId: request.organizationalUnitId,
    positionId: request.positionId,
    managerEmployeeId: request.managerEmployeeId,
    startOnUtc: request.startOnUtc,
    endActive: request.endActive,
  });
  return parseCreatedId(await response.json());
}

/** `POST /org/assignments/{id}/end` → 204. `endOnUtc: null` (or no body) ends it now. */
export async function endHrAssignment(companyId: string, assignmentId: string, endOnUtc: string | null): Promise<void> {
  await sendJson('POST', hrPath(companyId, 'assignments', assignmentId, 'end'), endOnUtc === null ? {} : { endOnUtc });
}
