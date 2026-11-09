// Real DTOs of the HR organizational structure API (S2, D-072), copied field by field from
// `Server/Api/HrOrganizationalStructureEndpoints.cs` and documented in
// `docs/HR_FRONTEND_API_CONTRACT.md`. No field is invented: if the backend grows, this file
// grows with it. Enums arrive as numbers (`employmentStatus: 1`); instants are UTC by contract
// and often reach the client without the `Z` (see docs/TIME_CONTRACT.md), so `asUtcInstant`
// normalizes them for display only — the wire keeps the server's own text.

/** Thrown when a response does not match the documented contract; the page shows an error state. */
export class HrContractError extends Error {
  constructor(part: string) {
    super(`HR API contract mismatch: ${part}`);
    this.name = 'HrContractError';
  }
}

/** `CompanyEmployeeEmploymentStatus` of the domain: 1=Active, 2=Suspended, 3=Terminated. */
export type HrEmploymentStatus = 1 | 2 | 3;

/** `CompanyEmployeeAssignmentStatus`: 1=Active, 2=Ended. */
export type HrAssignmentStatus = 1 | 2;

export type HrPage = { totalCount: number; page: number; pageSize: number };

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function readString(value: Record<string, unknown>, key: string, what: string): string {
  const field = value[key];
  if (typeof field !== 'string' || field === '') throw new HrContractError(`${what}.${key}`);
  return field;
}

function readOptionalString(value: Record<string, unknown>, key: string, what: string): string | null {
  const field = value[key];
  if (field === null || field === undefined) return null;
  if (typeof field !== 'string') throw new HrContractError(`${what}.${key}`);
  return field;
}

const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function readGuid(value: Record<string, unknown>, key: string, what: string): string {
  const field = readString(value, key, what);
  if (!guidPattern.test(field)) throw new HrContractError(`${what}.${key}`);
  return field;
}

function readOptionalGuid(value: Record<string, unknown>, key: string, what: string): string | null {
  const field = value[key];
  if (field === null || field === undefined) return null;
  if (typeof field !== 'string' || !guidPattern.test(field)) throw new HrContractError(`${what}.${key}`);
  return field;
}

function readBoolean(value: Record<string, unknown>, key: string, what: string): boolean {
  const field = value[key];
  if (typeof field !== 'boolean') throw new HrContractError(`${what}.${key}`);
  return field;
}

function readInt(value: Record<string, unknown>, key: string, what: string): number {
  const field = value[key];
  if (typeof field !== 'number' || !Number.isInteger(field)) throw new HrContractError(`${what}.${key}`);
  return field;
}

function readOptionalInt(value: Record<string, unknown>, key: string, what: string): number | null {
  const field = value[key];
  if (field === null || field === undefined) return null;
  if (typeof field !== 'number' || !Number.isInteger(field)) throw new HrContractError(`${what}.${key}`);
  return field;
}

/** A `*Utc` instant: raw text as sent (display normalizes with `asUtcInstant`). */
function readInstant(value: Record<string, unknown>, key: string, what: string): string {
  const field = readString(value, key, what);
  if (Number.isNaN(new Date(field).getTime())) throw new HrContractError(`${what}.${key}`);
  return field;
}

function readOptionalInstant(value: Record<string, unknown>, key: string, what: string): string | null {
  const field = value[key];
  if (field === null || field === undefined) return null;
  if (typeof field !== 'string' || Number.isNaN(new Date(field).getTime())) throw new HrContractError(`${what}.${key}`);
  return field;
}

function readStatus<T extends number>(value: Record<string, unknown>, key: string, allowed: readonly T[], what: string): T {
  const field = readInt(value, key, what);
  if (!(allowed as readonly number[]).includes(field)) throw new HrContractError(`${what}.${key}`);
  return field as T;
}

function readArray(value: Record<string, unknown>, key: string, what: string): readonly unknown[] {
  const field = value[key];
  if (!Array.isArray(field)) throw new HrContractError(`${what}.${key}`);
  return field;
}

function readRowVersion(value: Record<string, unknown>, what: string): string {
  // Base64 rowversion token (e.g. `AAAAAAAAB9I=`); opaque for the client, but its shape is checked.
  const field = readString(value, 'rowVersion', what);
  if (!/^[A-Za-z0-9+/]+={0,2}$/.test(field)) throw new HrContractError(`${what}.rowVersion`);
  return field;
}

// ---------------------------------------------------------------- employees

/** One row of `GET /org/employees`. Unit/position come from the employee's ACTIVE assignment. */
export type HrEmployeeListItem = {
  id: string;
  employeeNumber: string;
  firstName: string;
  lastName: string;
  employmentStatus: HrEmploymentStatus;
  hiredOnUtc: string | null;
  terminatedOnUtc: string | null;
  hasLinkedUser: boolean;
  unitId: string | null;
  unitName: string | null;
  positionId: string | null;
  positionName: string | null;
};

/** `HrAssignmentItem` — shared by employee details, the assignment list and history views. */
export type HrAssignmentItem = {
  id: string;
  employeeId: string;
  organizationalUnitId: string | null;
  unitCode: string | null;
  unitName: string | null;
  positionId: string | null;
  positionCode: string | null;
  positionName: string | null;
  managerEmployeeId: string | null;
  startOnUtc: string;
  endOnUtc: string | null;
  status: HrAssignmentStatus;
};

/** `GET /org/employees/{id}` — the only place that exposes `rowVersion` for an employee. */
export type HrEmployeeDetail = {
  id: string;
  employeeNumber: string;
  firstName: string;
  lastName: string;
  employmentStatus: HrEmploymentStatus;
  hiredOnUtc: string | null;
  terminatedOnUtc: string | null;
  userCompanyMembershipId: string | null;
  linkedUserId: string | null;
  createdAtUtc: string;
  rowVersion: string;
  activeAssignment: HrAssignmentItem | null;
};

export type HrEmployeeListResponse = HrPage & { items: readonly HrEmployeeListItem[] };

function parseAssignmentItem(value: unknown, what: string): HrAssignmentItem {
  if (!isObject(value)) throw new HrContractError(what);
  return {
    id: readGuid(value, 'id', what),
    employeeId: readGuid(value, 'employeeId', what),
    organizationalUnitId: readOptionalGuid(value, 'organizationalUnitId', what),
    unitCode: readOptionalString(value, 'unitCode', what),
    unitName: readOptionalString(value, 'unitName', what),
    positionId: readOptionalGuid(value, 'positionId', what),
    positionCode: readOptionalString(value, 'positionCode', what),
    positionName: readOptionalString(value, 'positionName', what),
    managerEmployeeId: readOptionalGuid(value, 'managerEmployeeId', what),
    startOnUtc: readInstant(value, 'startOnUtc', what),
    endOnUtc: readOptionalInstant(value, 'endOnUtc', what),
    status: readStatus(value, 'status', [1, 2] as const, what),
  };
}

function parseEmployeeListItem(value: unknown): HrEmployeeListItem {
  const what = 'hrEmployeeListItem';
  if (!isObject(value)) throw new HrContractError(what);
  return {
    id: readGuid(value, 'id', what),
    employeeNumber: readString(value, 'employeeNumber', what),
    firstName: readString(value, 'firstName', what),
    lastName: readString(value, 'lastName', what),
    employmentStatus: readStatus(value, 'employmentStatus', [1, 2, 3] as const, what),
    hiredOnUtc: readOptionalInstant(value, 'hiredOnUtc', what),
    terminatedOnUtc: readOptionalInstant(value, 'terminatedOnUtc', what),
    hasLinkedUser: readBoolean(value, 'hasLinkedUser', what),
    unitId: readOptionalGuid(value, 'unitId', what),
    unitName: readOptionalString(value, 'unitName', what),
    positionId: readOptionalGuid(value, 'positionId', what),
    positionName: readOptionalString(value, 'positionName', what),
  };
}

export function parseHrEmployeeListResponse(value: unknown): HrEmployeeListResponse {
  const what = 'hrEmployeeListResponse';
  if (!isObject(value)) throw new HrContractError(what);
  return {
    items: readArray(value, 'items', what).map(parseEmployeeListItem),
    totalCount: readInt(value, 'totalCount', what),
    page: readInt(value, 'page', what),
    pageSize: readInt(value, 'pageSize', what),
  };
}

export function parseHrEmployeeDetail(value: unknown): HrEmployeeDetail {
  const what = 'hrEmployeeDetail';
  if (!isObject(value)) throw new HrContractError(what);
  const activeAssignment = value['activeAssignment'];
  return {
    id: readGuid(value, 'id', what),
    employeeNumber: readString(value, 'employeeNumber', what),
    firstName: readString(value, 'firstName', what),
    lastName: readString(value, 'lastName', what),
    employmentStatus: readStatus(value, 'employmentStatus', [1, 2, 3] as const, what),
    hiredOnUtc: readOptionalInstant(value, 'hiredOnUtc', what),
    terminatedOnUtc: readOptionalInstant(value, 'terminatedOnUtc', what),
    userCompanyMembershipId: readOptionalGuid(value, 'userCompanyMembershipId', what),
    linkedUserId: readOptionalGuid(value, 'linkedUserId', what),
    createdAtUtc: readInstant(value, 'createdAtUtc', what),
    rowVersion: readRowVersion(value, what),
    activeAssignment:
      activeAssignment === null || activeAssignment === undefined
        ? null
        : parseAssignmentItem(activeAssignment, `${what}.activeAssignment`),
  };
}

/** `GET /org/employees/next-number` — a non-reserving suggestion; uniqueness is decided by the create call (409). */
export type HrNextEmployeeNumber = { suggestedEmployeeNumber: string };

export function parseHrNextEmployeeNumber(value: unknown): HrNextEmployeeNumber {
  const what = 'hrNextEmployeeNumber';
  if (!isObject(value)) throw new HrContractError(what);
  const suggestedEmployeeNumber = readString(value, 'suggestedEmployeeNumber', what);
  if (suggestedEmployeeNumber === '') throw new HrContractError(`${what}.suggestedEmployeeNumber`);
  return { suggestedEmployeeNumber };
}

export function parseCreatedId(value: unknown): { id: string } {
  const what = 'hrCreatedResponse';
  if (!isObject(value)) throw new HrContractError(what);
  return { id: readGuid(value, 'id', what) };
}

// ---------------------------------------------------------------- units

/** `HrUnitItem` — one row of the flat unit list (active units only). */
export type HrUnitItem = {
  id: string;
  code: string;
  name: string;
  description: string | null;
  parentUnitId: string | null;
  isActive: boolean;
  validFromUtc: string | null;
  validToUtc: string | null;
};

export type HrUnitDetail = HrUnitItem & {
  activeChildCount: number;
  activeAssignmentCount: number;
  rowVersion: string;
};

/** `HrUnitTreeNode` — the full tree, including inactive units. */
export type HrUnitTreeNode = HrUnitItem & { children: readonly HrUnitTreeNode[] };

export type HrUnitListResponse = HrPage & { items: readonly HrUnitItem[] };
export type HrUnitTreeResponse = { roots: readonly HrUnitTreeNode[] };

/** `HrUnitSubtreeNode` — the bounded subtree view with active assignment counts per node. */
export type HrUnitSubtreeNode = {
  id: string;
  code: string;
  name: string;
  isActive: boolean;
  parentUnitId: string | null;
  activeAssignmentCount: number;
  children: readonly HrUnitSubtreeNode[];
  validFromUtc: string | null;
  validToUtc: string | null;
};

function parseUnitItem(value: unknown): HrUnitItem {
  const what = 'hrUnitItem';
  if (!isObject(value)) throw new HrContractError(what);
  return {
    id: readGuid(value, 'id', what),
    code: readString(value, 'code', what),
    name: readString(value, 'name', what),
    description: readOptionalString(value, 'description', what),
    parentUnitId: readOptionalGuid(value, 'parentUnitId', what),
    isActive: readBoolean(value, 'isActive', what),
    validFromUtc: readOptionalInstant(value, 'validFromUtc', what),
    validToUtc: readOptionalInstant(value, 'validToUtc', what),
  };
}

function parseUnitTreeNode(value: unknown): HrUnitTreeNode {
  const item = parseUnitItem(value);
  const what = 'hrUnitTreeNode';
  if (!isObject(value)) throw new HrContractError(what);
  return { ...item, children: readArray(value, 'children', what).map(parseUnitTreeNode) };
}

export function parseHrUnitListResponse(value: unknown): HrUnitListResponse {
  const what = 'hrUnitListResponse';
  if (!isObject(value)) throw new HrContractError(what);
  return {
    items: readArray(value, 'items', what).map(parseUnitItem),
    totalCount: readInt(value, 'totalCount', what),
    page: readInt(value, 'page', what),
    pageSize: readInt(value, 'pageSize', what),
  };
}

export function parseHrUnitTreeResponse(value: unknown): HrUnitTreeResponse {
  const what = 'hrUnitTreeResponse';
  if (!isObject(value)) throw new HrContractError(what);
  return { roots: readArray(value, 'roots', what).map(parseUnitTreeNode) };
}

export function parseHrUnitDetail(value: unknown): HrUnitDetail {
  const what = 'hrUnitDetail';
  if (!isObject(value)) throw new HrContractError(what);
  return {
    ...parseUnitItem(value),
    activeChildCount: readInt(value, 'activeChildCount', what),
    activeAssignmentCount: readInt(value, 'activeAssignmentCount', what),
    rowVersion: readRowVersion(value, what),
  };
}

export function parseHrUnitSubtree(value: unknown): HrUnitSubtreeNode {
  const what = 'hrUnitSubtreeNode';
  if (!isObject(value)) throw new HrContractError(what);
  return {
    id: readGuid(value, 'id', what),
    code: readString(value, 'code', what),
    name: readString(value, 'name', what),
    isActive: readBoolean(value, 'isActive', what),
    parentUnitId: readOptionalGuid(value, 'parentUnitId', what),
    activeAssignmentCount: readInt(value, 'activeAssignmentCount', what),
    children: readArray(value, 'children', what).map(parseHrUnitSubtree),
    validFromUtc: readOptionalInstant(value, 'validFromUtc', what),
    validToUtc: readOptionalInstant(value, 'validToUtc', what),
  };
}

// ---------------------------------------------------------------- positions

export type HrPositionItem = {
  id: string;
  code: string;
  name: string;
  description: string | null;
  rank: number | null;
  isActive: boolean;
};

export type HrPositionDetail = HrPositionItem & {
  activeAssignmentCount: number;
  rowVersion: string;
};

export type HrPositionListResponse = HrPage & { items: readonly HrPositionItem[] };

function parsePositionItem(value: unknown): HrPositionItem {
  const what = 'hrPositionItem';
  if (!isObject(value)) throw new HrContractError(what);
  return {
    id: readGuid(value, 'id', what),
    code: readString(value, 'code', what),
    name: readString(value, 'name', what),
    description: readOptionalString(value, 'description', what),
    rank: readOptionalInt(value, 'rank', what),
    isActive: readBoolean(value, 'isActive', what),
  };
}

export function parseHrPositionListResponse(value: unknown): HrPositionListResponse {
  const what = 'hrPositionListResponse';
  if (!isObject(value)) throw new HrContractError(what);
  return {
    items: readArray(value, 'items', what).map(parsePositionItem),
    totalCount: readInt(value, 'totalCount', what),
    page: readInt(value, 'page', what),
    pageSize: readInt(value, 'pageSize', what),
  };
}

export function parseHrPositionDetail(value: unknown): HrPositionDetail {
  const what = 'hrPositionDetail';
  if (!isObject(value)) throw new HrContractError(what);
  return {
    ...parsePositionItem(value),
    activeAssignmentCount: readInt(value, 'activeAssignmentCount', what),
    rowVersion: readRowVersion(value, what),
  };
}

// ---------------------------------------------------------------- workflows (Phase 4)

/** One employee of a unit (`GET /org/units/{unitId}/employees`), with their active assignment. */
export type HrUnitEmployeeItem = {
  employeeId: string;
  employeeNumber: string;
  firstName: string;
  lastName: string;
  positionId: string | null;
  positionName: string | null;
  startOnUtc: string;
};

export type HrUnitEmployeeListResponse = HrPage & { items: readonly HrUnitEmployeeItem[] };

/** One holder of a position (`GET /org/positions/{positionId}/employees`). */
export type HrPositionEmployeeItem = {
  employeeId: string;
  employeeNumber: string;
  firstName: string;
  lastName: string;
  organizationalUnitId: string | null;
  organizationalUnitName: string | null;
  startOnUtc: string;
};

export type HrPositionEmployeeListResponse = HrPage & { items: readonly HrPositionEmployeeItem[] };

/**
 * `HrEmployeeHistoryItem` — the strict audit allowlist. No payload, IP, token or details JSON ever
 * reaches the UI; these six fields are everything the backend exposes.
 */
export type HrEmployeeHistoryItem = {
  eventType: string;
  entityType: string;
  entityId: string;
  occurredAtUtc: string;
  actorUserId: string | null;
  correlationId: string | null;
};

export type HrEmployeeHistoryResponse = HrPage & { items: readonly HrEmployeeHistoryItem[] };

export type HrAssignmentListResponse = HrPage & { items: readonly HrAssignmentItem[] };

function parsePage(value: unknown, what: string): HrPage {
  if (!isObject(value)) throw new HrContractError(what);
  return {
    totalCount: readInt(value, 'totalCount', what),
    page: readInt(value, 'page', what),
    pageSize: readInt(value, 'pageSize', what),
  };
}

export function parseHrUnitEmployeeListResponse(value: unknown): HrUnitEmployeeListResponse {
  const what = 'hrUnitEmployeeListResponse';
  if (!isObject(value)) throw new HrContractError(what);
  return {
    items: readArray(value, 'items', what).map((item): HrUnitEmployeeItem => {
      if (!isObject(item)) throw new HrContractError(what);
      return {
        employeeId: readGuid(item, 'employeeId', what),
        employeeNumber: readString(item, 'employeeNumber', what),
        firstName: readString(item, 'firstName', what),
        lastName: readString(item, 'lastName', what),
        positionId: readOptionalGuid(item, 'positionId', what),
        positionName: readOptionalString(item, 'positionName', what),
        startOnUtc: readInstant(item, 'startOnUtc', what),
      };
    }),
    ...parsePage(value, what),
  };
}

export function parseHrPositionEmployeeListResponse(value: unknown): HrPositionEmployeeListResponse {
  const what = 'hrPositionEmployeeListResponse';
  if (!isObject(value)) throw new HrContractError(what);
  return {
    items: readArray(value, 'items', what).map((item): HrPositionEmployeeItem => {
      if (!isObject(item)) throw new HrContractError(what);
      return {
        employeeId: readGuid(item, 'employeeId', what),
        employeeNumber: readString(item, 'employeeNumber', what),
        firstName: readString(item, 'firstName', what),
        lastName: readString(item, 'lastName', what),
        organizationalUnitId: readOptionalGuid(item, 'organizationalUnitId', what),
        organizationalUnitName: readOptionalString(item, 'organizationalUnitName', what),
        startOnUtc: readInstant(item, 'startOnUtc', what),
      };
    }),
    ...parsePage(value, what),
  };
}

export function parseHrEmployeeHistoryResponse(value: unknown): HrEmployeeHistoryResponse {
  const what = 'hrEmployeeHistoryResponse';
  if (!isObject(value)) throw new HrContractError(what);
  return {
    items: readArray(value, 'items', what).map((item): HrEmployeeHistoryItem => {
      if (!isObject(item)) throw new HrContractError(what);
      return {
        eventType: readString(item, 'eventType', what),
        entityType: readString(item, 'entityType', what),
        entityId: readString(item, 'entityId', what),
        occurredAtUtc: readInstant(item, 'occurredAtUtc', what),
        actorUserId: readOptionalGuid(item, 'actorUserId', what),
        correlationId: readOptionalString(item, 'correlationId', what),
      };
    }),
    ...parsePage(value, what),
  };
}

export function parseHrAssignmentListResponse(value: unknown): HrAssignmentListResponse {
  const what = 'hrAssignmentListResponse';
  if (!isObject(value)) throw new HrContractError(what);
  return {
    items: readArray(value, 'items', what).map((item) => parseAssignmentItem(item, what)),
    ...parsePage(value, what),
  };
}
