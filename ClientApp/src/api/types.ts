import { asUtcInstant } from '../lib/time';
// Contracts of the Faranou JSON API v1 (Server/Api/ApiEndpoints.cs).
// Every response is checked at runtime before it is used, so a contract change on the
// server surfaces as a clear error instead of `undefined` deep inside a component.

export type Session = {
  username: string;
  fullName: string;
  accountKind: string;
  isAdministrator: boolean;
};

export type CompanySummary = {
  id: string;
  name: string;
  nationalId: string | null;
  roleName: string;
};

export type FiscalPeriodStatus = 'Open' | 'Closed' | 'Archived';

export type FiscalPeriod = {
  id: string;
  title: string;
  /** Calendar date as stored by the server: ISO `yyyy-MM-dd` (Gregorian). */
  startDate: string;
  /** Calendar date as stored by the server: ISO `yyyy-MM-dd` (Gregorian). */
  endDate: string;
  status: FiscalPeriodStatus;
};

export type CompanyOverview = {
  company: CompanySummary;
  fiscalPeriods: FiscalPeriod[];
};

/** What a working context allows: an open period is read-write, a closed one is read-only. */
export type AccessMode = 'readWrite' | 'readOnly';

/**
 * A fiscal period as offered for selection; `isSelectable` and `accessMode` are decided by the
 * server (`accessMode` is `null` for a period that cannot be entered).
 */
export type FiscalPeriodChoice = FiscalPeriod & { isSelectable: boolean; accessMode: AccessMode | null };

export type CompanyRelationshipType = 'parent' | 'subsidiary' | 'associate' | 'jointVenture' | 'jointVenturer' | 'investor' | 'investment' | 'groupMember' | 'independent' | 'unknown';

export type CompanyChoice = CompanySummary & { fiscalPeriods: FiscalPeriodChoice[]; relationshipTypes: CompanyRelationshipType[] };

export type SelectedContext = {
  company: CompanySummary;
  fiscalPeriod: FiscalPeriodChoice;
};

export type EntryMode = 'directEntry' | 'selectionRequired' | 'noCompany' | 'noEligiblePeriod';

export type DirectSelection = { companyId: string; fiscalPeriodId: string };

/**
 * The working context of the sign-in: the validated current choice (if any), the choices, and
 * what to do next. Every decision is made by the server; the client only presents it.
 */
export type WorkContext = {
  current: SelectedContext | null;
  entry: EntryMode;
  directSelection: DirectSelection | null;
  defaultCompanyId: string | null;
  companies: CompanyChoice[];
};

export type AntiforgeryToken = {
  headerName: string;
  requestToken: string;
};

export class ContractError extends Error {
  constructor(what: string) {
    super(`پاسخ سرور با قرارداد API هم‌خوان نیست (${what}).`);
    this.name = 'ContractError';
  }
}

type JsonObject = Record<string, unknown>;

function isObject(value: unknown): value is JsonObject {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function readString(source: JsonObject, key: string, what: string): string {
  const value = source[key];
  if (typeof value !== 'string') {
    throw new ContractError(`${what}.${key}`);
  }
  return value;
}

function readNullableString(source: JsonObject, key: string, what: string): string | null {
  const value = source[key];
  if (value === null || value === undefined) {
    return null;
  }
  if (typeof value !== 'string') {
    throw new ContractError(`${what}.${key}`);
  }
  return value;
}

const isoDate = /^\d{4}-\d{2}-\d{2}$/;

function readIsoDate(source: JsonObject, key: string, what: string): string {
  const value = readString(source, key, what);
  if (!isoDate.test(value)) {
    throw new ContractError(`${what}.${key}`);
  }
  return value;
}

const statuses: readonly FiscalPeriodStatus[] = ['Open', 'Closed', 'Archived'];

function isFiscalPeriodStatus(value: string): value is FiscalPeriodStatus {
  return (statuses as readonly string[]).includes(value);
}

export function parseSession(value: unknown): Session {
  if (!isObject(value) || typeof value['isAdministrator'] !== 'boolean') {
    throw new ContractError('session');
  }
  return {
    username: readString(value, 'username', 'session'),
    fullName: readString(value, 'fullName', 'session'),
    accountKind: readString(value, 'accountKind', 'session'),
    isAdministrator: value['isAdministrator'],
  };
}

export function parseCompanySummary(value: unknown): CompanySummary {
  if (!isObject(value)) {
    throw new ContractError('company');
  }
  return {
    id: readString(value, 'id', 'company'),
    name: readString(value, 'name', 'company'),
    nationalId: readNullableString(value, 'nationalId', 'company'),
    roleName: readString(value, 'roleName', 'company'),
  };
}

export function parseCompanies(value: unknown): CompanySummary[] {
  if (!Array.isArray(value)) {
    throw new ContractError('companies');
  }
  return value.map(parseCompanySummary);
}

export function parseFiscalPeriod(value: unknown): FiscalPeriod {
  if (!isObject(value)) {
    throw new ContractError('fiscalPeriod');
  }
  const status = readString(value, 'status', 'fiscalPeriod');
  if (!isFiscalPeriodStatus(status)) {
    throw new ContractError('fiscalPeriod.status');
  }
  return {
    id: readString(value, 'id', 'fiscalPeriod'),
    title: readString(value, 'title', 'fiscalPeriod'),
    startDate: readIsoDate(value, 'startDate', 'fiscalPeriod'),
    endDate: readIsoDate(value, 'endDate', 'fiscalPeriod'),
    status,
  };
}

function readBoolean(source: JsonObject, key: string, what: string): boolean {
  const value = source[key];
  if (typeof value !== 'boolean') {
    throw new ContractError(`${what}.${key}`);
  }
  return value;
}

export function parseFiscalPeriodChoice(value: unknown): FiscalPeriodChoice {
  if (!isObject(value)) {
    throw new ContractError('fiscalPeriod');
  }
  const mode = value['accessMode'];
  if (mode !== null && mode !== 'readWrite' && mode !== 'readOnly') {
    throw new ContractError('fiscalPeriod');
  }
  return { ...parseFiscalPeriod(value), isSelectable: readBoolean(value, 'isSelectable', 'fiscalPeriod'), accessMode: mode };
}

const companyRelationshipTypes: readonly CompanyRelationshipType[] = ['parent', 'subsidiary', 'associate', 'jointVenture', 'jointVenturer', 'investor', 'investment', 'groupMember', 'independent', 'unknown'];

function parseCompanyRelationshipTypes(value: unknown): CompanyRelationshipType[] {
  if (!Array.isArray(value) || value.some((item) => typeof item !== 'string' || !(companyRelationshipTypes as readonly string[]).includes(item))) {
    throw new ContractError('companyChoice.relationshipTypes');
  }
  return value as CompanyRelationshipType[];
}

function parseCompanyChoice(value: unknown): CompanyChoice {
  if (!isObject(value) || !Array.isArray(value['fiscalPeriods'])) {
    throw new ContractError('companyChoice');
  }
  return {
    ...parseCompanySummary(value),
    relationshipTypes: parseCompanyRelationshipTypes(value['relationshipTypes']),
    fiscalPeriods: value['fiscalPeriods'].map(parseFiscalPeriodChoice),
  };
}

function parseSelectedContext(value: unknown): SelectedContext {
  if (!isObject(value)) {
    throw new ContractError('context');
  }
  return {
    company: parseCompanySummary(value['company']),
    fiscalPeriod: parseFiscalPeriodChoice(value['fiscalPeriod']),
  };
}

const entryModes: readonly EntryMode[] = ['directEntry', 'selectionRequired', 'noCompany', 'noEligiblePeriod'];

export function parseSelectedContextResponse(value: unknown): SelectedContext {
  return parseSelectedContext(value);
}

export function parseWorkContext(value: unknown): WorkContext {
  if (!isObject(value) || !Array.isArray(value['companies'])) {
    throw new ContractError('workContext');
  }
  const entry = readString(value, 'entry', 'workContext');
  if (!(entryModes as readonly string[]).includes(entry)) {
    throw new ContractError('workContext.entry');
  }
  const direct = value['directSelection'];
  let directSelection: DirectSelection | null = null;
  if (direct !== null && direct !== undefined) {
    if (!isObject(direct)) {
      throw new ContractError('workContext.directSelection');
    }
    directSelection = {
      companyId: readString(direct, 'companyId', 'directSelection'),
      fiscalPeriodId: readString(direct, 'fiscalPeriodId', 'directSelection'),
    };
  }
  const current = value['current'];
  return {
    current: current === null || current === undefined ? null : parseSelectedContext(current),
    entry: entry as EntryMode,
    directSelection,
    defaultCompanyId: readNullableString(value, 'defaultCompanyId', 'workContext'),
    companies: value['companies'].map(parseCompanyChoice),
  };
}

export function parseCompanyOverview(value: unknown): CompanyOverview {
  if (!isObject(value) || !Array.isArray(value['fiscalPeriods'])) {
    throw new ContractError('overview');
  }
  return {
    company: parseCompanySummary(value['company']),
    fiscalPeriods: value['fiscalPeriods'].map(parseFiscalPeriod),
  };
}

export function parseAntiforgeryToken(value: unknown): AntiforgeryToken {
  if (!isObject(value)) {
    throw new ContractError('antiforgery');
  }
  return {
    headerName: readString(value, 'headerName', 'antiforgery'),
    requestToken: readString(value, 'requestToken', 'antiforgery'),
  };
}

export type MyProfile = {
  username: string;
  firstName: string | null;
  lastName: string | null;
  emailAddress: string | null;
  cellPhoneNumber: string | null;
  isEmailAddressVerified: boolean;
  isCellPhoneNumberVerified: boolean;
  lastLoginDateTime: string | null;
  insertDateTime: string;
};

export type LoginHistoryEventType =
  | 'LoginSucceeded'
  | 'LoginFailed'
  | 'LoginDeniedInactive'
  | 'LoginDeniedEnrollmentPending'
  | 'LogoutSucceeded';

export type LoginHistoryItem = {
  eventType: LoginHistoryEventType;
  occurredAtUtc: string;
  ipAddress: string | null;
};

export type ChangeMyPasswordRequest = {
  currentPassword: string;
  newPassword: string;
  confirmNewPassword: string;
};

const loginHistoryEventTypes: readonly LoginHistoryEventType[] = [
  'LoginSucceeded',
  'LoginFailed',
  'LoginDeniedInactive',
  'LoginDeniedEnrollmentPending',
  'LogoutSucceeded',
];

function readIsoDateTime(source: JsonObject, key: string, what: string, nullable = false): string | null {
  const value = readNullableString(source, key, what);
  if (value === null) {
    if (nullable) return null;
    throw new ContractError(`${what}.${key}`);
  }
  if (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})?$/.test(value) || !Number.isFinite(Date.parse(value))) {
    throw new ContractError(`${what}.${key}`);
  }
  return value;
}

/** A `*Utc` instant: UTC by contract; an offset-less value (EF `Kind=Unspecified`) gets `Z` (see lib/time.ts). */
function readUtcDateTime(source: JsonObject, key: string, what: string, nullable = false): string | null {
  const value = readIsoDateTime(source, key, what, nullable);
  return value === null ? null : asUtcInstant(value);
}

/**
 * A legacy server-local wall-clock time (`DateTime.Now` on the server: InsertDateTime, UpdateDateTime,
 * LastLoginDateTime). It is NOT UTC and is returned unchanged, so it displays as the server wrote it.
 */
function readServerLocalDateTime(source: JsonObject, key: string, what: string, nullable = false): string | null {
  return readIsoDateTime(source, key, what, nullable);
}

export function parseMyProfile(value: unknown): MyProfile {
  if (!isObject(value)) throw new ContractError('myProfile');
  return {
    username: readString(value, 'username', 'myProfile'),
    firstName: readNullableString(value, 'firstName', 'myProfile'),
    lastName: readNullableString(value, 'lastName', 'myProfile'),
    emailAddress: readNullableString(value, 'emailAddress', 'myProfile'),
    cellPhoneNumber: readNullableString(value, 'cellPhoneNumber', 'myProfile'),
    isEmailAddressVerified: readBoolean(value, 'isEmailAddressVerified', 'myProfile'),
    isCellPhoneNumberVerified: readBoolean(value, 'isCellPhoneNumberVerified', 'myProfile'),
    lastLoginDateTime: readServerLocalDateTime(value, 'lastLoginDateTime', 'myProfile', true),
    insertDateTime: readServerLocalDateTime(value, 'insertDateTime', 'myProfile')!,
  };
}

export function parseLoginHistory(value: unknown): LoginHistoryItem[] {
  if (!Array.isArray(value)) throw new ContractError('loginHistory');
  return value.map((item) => {
    if (!isObject(item)) throw new ContractError('loginHistory.item');
    const eventType = readString(item, 'eventType', 'loginHistory.item');
    if (!(loginHistoryEventTypes as readonly string[]).includes(eventType)) {
      throw new ContractError('loginHistory.item.eventType');
    }
    return {
      eventType: eventType as LoginHistoryEventType,
      occurredAtUtc: readUtcDateTime(item, 'occurredAtUtc', 'loginHistory.item')!,
      ipAddress: readNullableString(item, 'ipAddress', 'loginHistory.item'),
    };
  });
}

export type AdminUserListItem = {
  id: string;
  username: string | null;
  firstName: string | null;
  lastName: string | null;
  emailAddress: string;
  cellPhoneNumber: string | null;
  isActive: boolean;
  lastLoginDateTime: string | null;
  insertDateTime: string;
  activeCompanyCount: number;
};

export type AdminUserListResponse = {
  items: AdminUserListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
};

export type AdminUserMembership = {
  companyId: string;
  companyName: string;
  roleCode: string;
  roleName: string;
  isActive: boolean;
  createdAtUtc: string;
};

export type AdminUserDetails = AdminUserListItem & {
  isEmailAddressVerified: boolean;
  isCellPhoneNumberVerified: boolean;
  updateDateTime: string;
  legacyRoleName: string | null;
  memberships: AdminUserMembership[];
};

export function parseAdminUserList(value: unknown): AdminUserListResponse {
  if (!isObject(value) || !Array.isArray(value['items'])) throw new ContractError('adminUsers');
  const number = (key: string): number => {
    const v = value[key];
    if (typeof v !== 'number' || !Number.isInteger(v) || v < 0) throw new ContractError('adminUsers.' + key);
    return v;
  };
  const items = value['items'].map((item): AdminUserListItem => {
    if (!isObject(item)) throw new ContractError('adminUsers.item');
    const activeCompanyCount = item['activeCompanyCount'];
    if (typeof activeCompanyCount !== 'number' || !Number.isInteger(activeCompanyCount) || activeCompanyCount < 0) {
      throw new ContractError('adminUsers.item.activeCompanyCount');
    }
    return {
      id: readString(item, 'id', 'adminUsers.item'),
      username: readNullableString(item, 'username', 'adminUsers.item'),
      firstName: readNullableString(item, 'firstName', 'adminUsers.item'),
      lastName: readNullableString(item, 'lastName', 'adminUsers.item'),
      emailAddress: readString(item, 'emailAddress', 'adminUsers.item'),
      cellPhoneNumber: readNullableString(item, 'cellPhoneNumber', 'adminUsers.item'),
      isActive: readBoolean(item, 'isActive', 'adminUsers.item'),
      lastLoginDateTime: readServerLocalDateTime(item, 'lastLoginDateTime', 'adminUsers.item', true),
      insertDateTime: readServerLocalDateTime(item, 'insertDateTime', 'adminUsers.item')!,
      activeCompanyCount,
    };
  });
  const page = number('page');
  const pageSize = number('pageSize');
  if (page < 1 || pageSize < 1 || pageSize > 100) throw new ContractError('adminUsers.pagination');
  return { items, totalCount: number('totalCount'), page, pageSize };
}

export function parseAdminUserDetails(value: unknown): AdminUserDetails {
  if (!isObject(value) || !Array.isArray(value['memberships'])) throw new ContractError('adminUser');
  const memberships = value['memberships'].map((item): AdminUserMembership => {
    if (!isObject(item)) throw new ContractError('adminUser.membership');
    return {
      companyId: readString(item, 'companyId', 'adminUser.membership'),
      companyName: readString(item, 'companyName', 'adminUser.membership'),
      roleCode: readString(item, 'roleCode', 'adminUser.membership'),
      roleName: readString(item, 'roleName', 'adminUser.membership'),
      isActive: readBoolean(item, 'isActive', 'adminUser.membership'),
      createdAtUtc: readUtcDateTime(item, 'createdAtUtc', 'adminUser.membership')!,
    };
  });
  const base: AdminUserListItem = {
    id: readString(value, 'id', 'adminUser'),
    username: readNullableString(value, 'username', 'adminUser'),
    firstName: readNullableString(value, 'firstName', 'adminUser'),
    lastName: readNullableString(value, 'lastName', 'adminUser'),
    emailAddress: readString(value, 'emailAddress', 'adminUser'),
    cellPhoneNumber: readNullableString(value, 'cellPhoneNumber', 'adminUser'),
    isActive: readBoolean(value, 'isActive', 'adminUser'),
    lastLoginDateTime: readServerLocalDateTime(value, 'lastLoginDateTime', 'adminUser', true),
    insertDateTime: readServerLocalDateTime(value, 'insertDateTime', 'adminUser')!,
    activeCompanyCount: memberships.filter(x => x.isActive).length,
  };
  return {
    ...base,
    isEmailAddressVerified: readBoolean(value, 'isEmailAddressVerified', 'adminUser'),
    isCellPhoneNumberVerified: readBoolean(value, 'isCellPhoneNumberVerified', 'adminUser'),
    updateDateTime: readServerLocalDateTime(value, 'updateDateTime', 'adminUser')!,
    legacyRoleName: readNullableString(value, 'legacyRoleName', 'adminUser'),
    memberships,
  };
}

/** Body of `POST /admin/users`; the server normalizes text and validates every value again. */
export type CreateAdminUserRequest = {
  username: string;
  firstName: string | null;
  lastName: string | null;
  emailAddress: string;
  cellPhoneNumber: string | null;
  password: string;
  confirmPassword: string;
  isActive: boolean;
};

/** Body of `PUT /admin/users/{id}`: always the full set of editable fields (no partial update). */
export type UpdateAdminUserRequest = Omit<CreateAdminUserRequest, 'password' | 'confirmPassword'>;

export function parseCreatedAdminUser(value: unknown): { id: string } {
  if (!isObject(value)) throw new ContractError('createdUser');
  return { id: readString(value, 'id', 'createdUser') };
}

// --- Company roles and the permission catalog (PR #459, `/admin/companies/{id}/roles`) ---

/** A permission as registered on the server. `subsystemCode` is the first segment of `code`. */
export type PermissionCatalogItem = {
  id: string;
  code: string;
  name: string;
  description: string | null;
  subsystemCode: string;
};

/** A server-defined starting selection of permissions for a new role. */
export type PermissionTemplate = {
  code: string;
  name: string;
  permissionCodes: string[];
};

export type CompanyRole = {
  id: string;
  companyId: string;
  code: string;
  name: string;
  isSystemRole: boolean;
  isActive: boolean;
  permissionCount: number;
  activeMemberCount: number;
  permissionCodes: string[];
};

/**
 * Body of `POST /admin/companies/{id}/roles`. The server prefers `permissionCodes` over
 * `templateCode`, so the client applies a template to the selection and sends the codes only.
 */
export type CreateCompanyRoleRequest = {
  code: string;
  name: string;
  permissionCodes: string[];
};

/** Body of `PUT /admin/companies/{id}/roles/{roleId}`: always the full editable state. */
export type UpdateCompanyRoleRequest = {
  name: string;
  isActive: boolean;
  permissionCodes: string[];
};

function readStringArray(source: JsonObject, key: string, what: string): string[] {
  const value = source[key];
  if (!Array.isArray(value) || !value.every((entry): entry is string => typeof entry === 'string')) {
    throw new ContractError(`${what}.${key}`);
  }
  return value;
}

function readCount(source: JsonObject, key: string, what: string): number {
  const value = source[key];
  if (typeof value !== 'number' || !Number.isInteger(value) || value < 0) throw new ContractError(`${what}.${key}`);
  return value;
}

/** One active member of a company role (minimal identity only; no e-mail, phone or hash). */
export type CompanyRoleMember = {
  userId: string;
  username: string | null;
  firstName: string | null;
  lastName: string | null;
  isUserActive: boolean;
  memberSinceUtc: string;
};

export type CompanyRoleMemberPage = { items: CompanyRoleMember[]; totalCount: number; page: number; pageSize: number };

export function parseCompanyRoleMembers(value: unknown): CompanyRoleMemberPage {
  const what = 'roleMembers';
  if (!isObject(value)) throw new ContractError(what);
  const rows = value['items'];
  if (!Array.isArray(rows)) throw new ContractError(`${what}.items`);
  const page = readCount(value, 'page', what);
  const pageSize = readCount(value, 'pageSize', what);
  if (page < 1 || pageSize < 1) throw new ContractError(`${what}.page`);
  return {
    items: rows.map((row): CompanyRoleMember => {
      if (!isObject(row)) throw new ContractError(`${what}.item`);
      const since = readUtcDateTime(row, 'memberSinceUtc', `${what}.item`);
      if (since === null) throw new ContractError(`${what}.item.memberSinceUtc`);
      return {
        userId: readString(row, 'userId', `${what}.item`),
        username: readNullableString(row, 'username', `${what}.item`),
        firstName: readNullableString(row, 'firstName', `${what}.item`),
        lastName: readNullableString(row, 'lastName', `${what}.item`),
        isUserActive: readBoolean(row, 'isUserActive', `${what}.item`),
        memberSinceUtc: since,
      };
    }),
    totalCount: readCount(value, 'totalCount', what),
    page,
    pageSize,
  };
}

export function parseTransferredMemberCount(value: unknown): number {
  if (!isObject(value)) throw new ContractError('transferResult');
  return readCount(value, 'movedCount', 'transferResult');
}

export function parsePermissionCatalog(value: unknown): PermissionCatalogItem[] {
  if (!Array.isArray(value)) throw new ContractError('permissionCatalog');
  return value.map((item): PermissionCatalogItem => {
    if (!isObject(item)) throw new ContractError('permissionCatalog.item');
    return {
      id: readString(item, 'id', 'permissionCatalog.item'),
      code: readString(item, 'code', 'permissionCatalog.item'),
      name: readString(item, 'name', 'permissionCatalog.item'),
      description: readNullableString(item, 'description', 'permissionCatalog.item'),
      subsystemCode: readString(item, 'subsystemCode', 'permissionCatalog.item'),
    };
  });
}

export function parsePermissionTemplates(value: unknown): PermissionTemplate[] {
  if (!Array.isArray(value)) throw new ContractError('permissionTemplates');
  return value.map((item): PermissionTemplate => {
    if (!isObject(item)) throw new ContractError('permissionTemplates.item');
    return {
      code: readString(item, 'code', 'permissionTemplates.item'),
      name: readString(item, 'name', 'permissionTemplates.item'),
      permissionCodes: readStringArray(item, 'permissionCodes', 'permissionTemplates.item'),
    };
  });
}

export function parseCompanyRoles(value: unknown): CompanyRole[] {
  if (!Array.isArray(value)) throw new ContractError('companyRoles');
  return value.map((item): CompanyRole => {
    if (!isObject(item)) throw new ContractError('companyRoles.item');
    const what = 'companyRoles.item';
    return {
      id: readString(item, 'id', what),
      companyId: readString(item, 'companyId', what),
      code: readString(item, 'code', what),
      name: readString(item, 'name', what),
      isSystemRole: readBoolean(item, 'isSystemRole', what),
      isActive: readBoolean(item, 'isActive', what),
      permissionCount: readCount(item, 'permissionCount', what),
      activeMemberCount: readCount(item, 'activeMemberCount', what),
      permissionCodes: readStringArray(item, 'permissionCodes', what),
    };
  });
}

export function parseCreatedCompanyRole(value: unknown): { id: string } {
  if (!isObject(value)) throw new ContractError('createdRole');
  return { id: readString(value, 'id', 'createdRole') };
}

// --- Cross-company membership requests (backend: CompanyMembershipRequestEndpoints, D-065) ---

export type MembershipRequestStatus = 'Pending' | 'Approved' | 'Rejected';

const membershipRequestStatuses: readonly MembershipRequestStatus[] = ['Pending', 'Approved', 'Rejected'];

/** One request the signed-in user made to join another company. */
export type MyMembershipRequest = {
  id: string;
  destinationCompanyId: string;
  destinationCompanyName: string;
  status: MembershipRequestStatus;
  requestedAtUtc: string;
  decidedAtUtc: string | null;
  decisionReason: string | null;
  approvedRoleCode: string | null;
  approvedRoleName: string | null;
};

/** One request waiting for the destination company's approver (minimal requester identity only). */
export type PendingMembershipRequest = {
  id: string;
  userId: string;
  requesterDisplayName: string;
  username: string | null;
  requestedAtUtc: string;
  isRequesterActive: boolean;
};

export type MembershipRequestPage<T> = { items: T[]; page: number; pageSize: number; totalCount: number };

function readMembershipRequestStatus(source: JsonObject, what: string): MembershipRequestStatus {
  const value = readString(source, 'status', what);
  const status = membershipRequestStatuses.find((candidate) => candidate === value);
  if (!status) throw new ContractError(`${what}.status`);
  return status;
}

function readRequiredDateTime(source: JsonObject, key: string, what: string): string {
  const value = readUtcDateTime(source, key, what);
  if (value === null) throw new ContractError(`${what}.${key}`);
  return value;
}

function parseMembershipRequestPage<T>(
  value: unknown,
  what: string,
  parseItem: (item: JsonObject, itemWhat: string) => T,
): MembershipRequestPage<T> {
  if (!isObject(value)) throw new ContractError(what);
  const rows = value['items'];
  if (!Array.isArray(rows)) throw new ContractError(`${what}.items`);
  const page = readCount(value, 'page', what);
  const pageSize = readCount(value, 'pageSize', what);
  if (page < 1 || pageSize < 1) throw new ContractError(`${what}.page`);
  return {
    items: rows.map((row) => {
      if (!isObject(row)) throw new ContractError(`${what}.item`);
      return parseItem(row, `${what}.item`);
    }),
    page,
    pageSize,
    totalCount: readCount(value, 'totalCount', what),
  };
}

export function parseMyMembershipRequests(value: unknown): MembershipRequestPage<MyMembershipRequest> {
  return parseMembershipRequestPage(value, 'myMembershipRequests', (item, what): MyMembershipRequest => ({
    id: readString(item, 'id', what),
    destinationCompanyId: readString(item, 'destinationCompanyId', what),
    destinationCompanyName: readString(item, 'destinationCompanyName', what),
    status: readMembershipRequestStatus(item, what),
    requestedAtUtc: readRequiredDateTime(item, 'requestedAtUtc', what),
    decidedAtUtc: readUtcDateTime(item, 'decidedAtUtc', what, true),
    decisionReason: readNullableString(item, 'decisionReason', what),
    approvedRoleCode: readNullableString(item, 'approvedRoleCode', what),
    approvedRoleName: readNullableString(item, 'approvedRoleName', what),
  }));
}

export function parsePendingMembershipRequests(value: unknown): MembershipRequestPage<PendingMembershipRequest> {
  return parseMembershipRequestPage(value, 'pendingMembershipRequests', (item, what): PendingMembershipRequest => ({
    id: readString(item, 'id', what),
    userId: readString(item, 'userId', what),
    requesterDisplayName: readString(item, 'requesterDisplayName', what),
    username: readNullableString(item, 'username', what),
    requestedAtUtc: readRequiredDateTime(item, 'requestedAtUtc', what),
    isRequesterActive: readBoolean(item, 'isRequesterActive', what),
  }));
}

// --- Installation-wide security audit report (backend: SecurityAuditAdministrationEndpoints) ---

/** One audit event. The server deliberately omits the IP address and the event payload. */
export type SecurityAuditEvent = {
  id: string;
  userId: string | null;
  actorUsername: string | null;
  actorDisplayName: string | null;
  companyId: string | null;
  companyName: string | null;
  eventType: string;
  entityType: string | null;
  entityId: string | null;
  occurredAtUtc: string;
  correlationId: string | null;
};

export type SecurityAuditPage = { items: SecurityAuditEvent[]; totalCount: number; page: number; pageSize: number };

export function parseSecurityAuditEvents(value: unknown): SecurityAuditPage {
  const what = 'securityAudit';
  if (!isObject(value)) throw new ContractError(what);
  const rows = value['items'];
  if (!Array.isArray(rows)) throw new ContractError(`${what}.items`);
  const page = readCount(value, 'page', what);
  const pageSize = readCount(value, 'pageSize', what);
  if (page < 1 || pageSize < 1) throw new ContractError(`${what}.page`);
  return {
    items: rows.map((row): SecurityAuditEvent => {
      if (!isObject(row)) throw new ContractError(`${what}.item`);
      const at = `${what}.item`;
      const occurred = readUtcDateTime(row, 'occurredAtUtc', at);
      if (occurred === null) throw new ContractError(`${at}.occurredAtUtc`);
      return {
        id: readString(row, 'id', at),
        userId: readNullableString(row, 'userId', at),
        actorUsername: readNullableString(row, 'actorUsername', at),
        actorDisplayName: readNullableString(row, 'actorDisplayName', at),
        companyId: readNullableString(row, 'companyId', at),
        companyName: readNullableString(row, 'companyName', at),
        eventType: readString(row, 'eventType', at),
        entityType: readNullableString(row, 'entityType', at),
        entityId: readNullableString(row, 'entityId', at),
        occurredAtUtc: occurred,
        correlationId: readNullableString(row, 'correlationId', at),
      };
    }),
    totalCount: readCount(value, 'totalCount', what),
    page,
    pageSize,
  };
}
