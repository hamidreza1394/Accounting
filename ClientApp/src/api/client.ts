import {
  parseAntiforgeryToken,
  parseCurrentPermissions,
  parseRolePermissionPreview,
  type RolePermissionPreview,
  parseMembershipDestinations,
  parseAssignableRoles,
  parseMyProfile,
  parseLoginHistory,
  parseAdminUserList,
  parseAdminUserDetails,
  parseCreatedAdminUser,
  parseCompanyRoles,
  parseMyMembershipRequests,
  parsePendingMembershipRequests,
  parseCompanyDirectory,
  parseSecurityAuditEvents,
  parseSecurityAuditEventTypes,
  parseAdminCompanies,
  parseCreatedCompanyRole,
  parsePermissionCatalog,
  parseCompanyRoleMembers,
  parseEffectivePermissions,
  parseTransferredMemberCount,
  parseMySessions,
  parseRevokedSessionCount,
  parseAdminUserSessions,
  parseAccessSubsystems,
  parsePermissionTemplates,
  parseCompanies,
  parseCompanyOverview,
  parseSelectedContextResponse,
  parseSession,
  parseWorkContext,
  type MyProfile,
  type LoginHistoryItem,
  type AdminUserListResponse,
  type AdminUserDetails,
  type CreateAdminUserRequest,
  type UpdateAdminUserRequest,
  type ChangeMyPasswordRequest,
  type CompanyOverview,
  type CompanyRole,
  type CreateCompanyRoleRequest,
  type PermissionCatalogItem,
  type PermissionTemplate,
  type UpdateCompanyRoleRequest,
  type CompanyRoleMemberPage,
  type MySession,
  type AdminUserSession,
  type AccessSubsystem,
  type CompanySummary,
  type SelectedContext,
  type Session,
  type WorkContext,
  type MembershipRequestPage,
  type MyMembershipRequest,
  type PendingMembershipRequest,
  type CompanyDirectoryPage,
  type SecurityAuditPage,
  type SecurityAuditEventType,
  type AdminCompanyPage,
  type EffectivePermissions,
} from './types';

// The API lives on the same origin and is authenticated by the HttpOnly cookie of the
// Razor sign-in. JavaScript never sees a token of the session; only the anti-forgery
// request token is read, and only to send it back in a header for state changes.

const apiBase = '/api/v1';

export const loginPath = '/Account/Login';

/** Thrown for 401: the session is missing or expired. */
export class UnauthenticatedError extends Error {
  constructor() {
    super('نشست شما پایان یافته است. لطفاً دوباره وارد شوید.');
    this.name = 'UnauthenticatedError';
  }
}

/** Thrown for every other unsuccessful answer; `message` is safe to show to the user. */
export class ApiError extends Error {
  readonly status: number;
  /** Validation messages per field (RFC 7807 `errors`), when the server sent any. */
  readonly fieldErrors?: Record<string, string[]>;

  constructor(status: number, message: string, fieldErrors?: Record<string, string[]>) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    if (fieldErrors && Object.keys(fieldErrors).length > 0) {
      this.fieldErrors = fieldErrors;
    }
  }
}

function messageForStatus(status: number): string {
  switch (status) {
    case 403:
      return 'شما مجوز دسترسی به این بخش را ندارید.';
    case 404:
      return 'اطلاعات درخواست‌شده پیدا نشد یا به آن دسترسی ندارید.';
    default:
      return status >= 500
        ? 'خطای غیرمنتظره‌ای در سرور رخ داد. لطفاً دوباره تلاش کنید.'
        : 'درخواست با خطا مواجه شد.';
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function nonEmptyString(value: unknown): string | null {
  return typeof value === 'string' && value.trim() !== '' ? value : null;
}

const defaultValidationTitle = 'One or more validation errors occurred.';

/** Field keys arrive as model property names (`Email`); the client uses the JSON names (`email`). */
function toFieldKey(key: string): string {
  return key.length > 0 ? key.charAt(0).toLowerCase() + key.slice(1) : key;
}

function readFieldErrors(value: unknown): Record<string, string[]> {
  const result: Record<string, string[]> = {};
  if (!isRecord(value)) return result;
  for (const [key, messages] of Object.entries(value)) {
    const list = (Array.isArray(messages) ? messages : [messages]).filter(
      (message): message is string => typeof message === 'string' && message.trim() !== '',
    );
    if (list.length > 0) result[toFieldKey(key)] = list;
  }
  return result;
}

/**
 * Turns an unsuccessful answer into an {@link ApiError}. Understands both RFC 7807 ProblemDetails
 * (`title`, `errors` dictionary) and the ad-hoc `{ error: string }` bodies. Server errors
 * (5xx) never show body text: details stay in the server log.
 */
export function normalizeApiError(status: number, body?: unknown): ApiError {
  if (status >= 500 || !isRecord(body)) {
    return new ApiError(status, messageForStatus(status));
  }
  const fieldErrors = readFieldErrors(body['errors']);
  // `detail` is never shown (it may carry internal text). The ASP.NET default validation title is
  // English; with field errors present the Persian status message is shown instead.
  const title = nonEmptyString(body['title']);
  const useTitle = title !== null && !(Object.keys(fieldErrors).length > 0 && title === defaultValidationTitle);
  const message = (useTitle ? title : null) ?? nonEmptyString(body['error']) ?? messageForStatus(status);
  return new ApiError(status, message, fieldErrors);
}

async function readJsonBody(response: Response): Promise<unknown> {
  const contentType = response.headers.get('Content-Type') ?? '';
  if (!contentType.includes('json')) {
    return undefined;
  }
  try {
    return await response.json();
  } catch {
    // A malformed error body still produces the status-based message.
    return undefined;
  }
}

async function ensureSuccess(response: Response): Promise<void> {
  if (response.status === 401) {
    throw new UnauthenticatedError();
  }
  if (!response.ok) {
    throw normalizeApiError(response.status, response.status < 500 ? await readJsonBody(response) : undefined);
  }
}

async function getJson(path: string, signal?: AbortSignal): Promise<unknown> {
  const response = await fetch(`${apiBase}${path}`, {
    method: 'GET',
    credentials: 'same-origin',
    headers: { Accept: 'application/json' },
    cache: 'no-store',
    ...(signal ? { signal } : {}),
  });
  await ensureSuccess(response);
  return response.json();
}

async function post(path: string): Promise<void> {
  const token = parseAntiforgeryToken(await getJson('/antiforgery/token'));
  const response = await fetch(`${apiBase}${path}`, {
    method: 'POST',
    credentials: 'same-origin',
    headers: { Accept: 'application/json', [token.headerName]: token.requestToken },
  });
  await ensureSuccess(response);
}

/** A state-changing request: carries the anti-forgery request token and, optionally, a JSON body. */
export async function sendJson(method: 'POST' | 'PUT' | 'PATCH' | 'DELETE', path: string, body?: unknown): Promise<Response> {
  const token = parseAntiforgeryToken(await getJson('/antiforgery/token'));
  const response = await fetch(`${apiBase}${path}`, {
    method,
    credentials: 'same-origin',
    headers: {
      Accept: 'application/json',
      [token.headerName]: token.requestToken,
      ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
    },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  });
  await ensureSuccess(response);
  return response;
}


/** An anti-forgery token as issued by the server. */
export type AntiforgeryToken = { headerName: string; requestToken: string };

/** The raw outcome of a request made without the normal error mapping (see below). */
export type RawApiResult = { ok: boolean; status: number; body: unknown };

async function toRawResult(response: Response): Promise<RawApiResult> {
  // A missing or malformed body is `null`; the caller decides what the status means.
  const body: unknown = await response.json().catch(() => null);
  return { ok: response.ok, status: response.status, body };
}

/**
 * Same-origin GET for the pre-sign-in provisioning pages, which have no session and map statuses
 * to their own messages. It does not throw for an unsuccessful status (only for a network failure).
 */
export async function apiGetRaw(path: string): Promise<RawApiResult> {
  return toRawResult(await fetch(`${apiBase}${path}`, { credentials: 'same-origin', cache: 'no-store' }));
}

/**
 * Same-origin JSON POST for the provisioning pages. The page already holds the anti-forgery token
 * (from its own endpoint) and sends it back in the header. Unlike {@link sendJson} it does not
 * throw for an unsuccessful status: those pages show status-specific messages themselves.
 */
export async function apiPostJson(path: string, body: unknown, antiforgery: AntiforgeryToken): Promise<RawApiResult> {
  const response = await fetch(`${apiBase}${path}`, {
    method: 'POST',
    credentials: 'same-origin',
    cache: 'no-store',
    headers: { 'Content-Type': 'application/json', [antiforgery.headerName]: antiforgery.requestToken },
    body: JSON.stringify(body),
  });
  return toRawResult(response);
}

/** Retrieves only the authenticated user's safe profile fields. */
export async function getMyProfile(signal?: AbortSignal): Promise<MyProfile> {
  return parseMyProfile(await getJson('/me/profile', signal));
}

/** Retrieves authentication events belonging only to the current user. */
export async function getMyLoginHistory(signal?: AbortSignal, take = 50): Promise<LoginHistoryItem[]> {
  const pageSize = Math.max(1, Math.min(100, Math.trunc(take)));
  return parseLoginHistory(await getJson(`/me/login-history?take=${pageSize}`, signal));
}

/** Query of the administrator's user list; the server validates and clamps every value. */
export type AdminUserQuery = {
  search?: string;
  status: 'all' | 'active' | 'inactive';
  page: number;
  pageSize: number;
  sort: 'created' | 'lastLogin' | 'name' | 'username';
  descending: boolean;
};

export async function getAdminUsers(query: AdminUserQuery, signal?: AbortSignal): Promise<AdminUserListResponse> {
  const params = new URLSearchParams({
    status: query.status,
    page: String(query.page),
    pageSize: String(query.pageSize),
    sort: query.sort,
    descending: String(query.descending),
  });
  if (query.search?.trim()) params.set('search', query.search.trim());
  return parseAdminUserList(await getJson(`/admin/users?${params.toString()}`, signal));
}

export async function getAdminUser(userId: string, signal?: AbortSignal): Promise<AdminUserDetails> {
  return parseAdminUserDetails(await getJson(`/admin/users/${encodeURIComponent(userId)}`, signal));
}

/** Creates a user (201 `{ id }`). A duplicate answers 409 and invalid data 400, both as {@link ApiError}. */
export async function createAdminUser(request: CreateAdminUserRequest): Promise<{ id: string }> {
  const response = await sendJson('POST', '/admin/users', request);
  return parseCreatedAdminUser(await response.json());
}

/** Replaces the editable fields of a user (204). There is no concurrency token: the last write wins. */
export async function updateAdminUser(userId: string, request: UpdateAdminUserRequest): Promise<void> {
  await sendJson('PUT', `/admin/users/${encodeURIComponent(userId)}`, request);
}

/** The access groups (roles) of one company, with their permission codes. Installation administrators only. */
export async function getCompanyRoles(companyId: string, signal?: AbortSignal): Promise<CompanyRole[]> {
  return parseCompanyRoles(await getJson(`/admin/companies/${encodeURIComponent(companyId)}/roles`, signal));
}

/** Creates a role (201 `{ id }`). A duplicate code answers 409 and an invalid code or permission 400. */
export async function createCompanyRole(companyId: string, request: CreateCompanyRoleRequest): Promise<{ id: string }> {
  const response = await sendJson('POST', `/admin/companies/${encodeURIComponent(companyId)}/roles`, request);
  return parseCreatedCompanyRole(await response.json());
}

/** Replaces name, status and permissions of a non-system role (204). System roles answer 409. */
export async function updateCompanyRole(companyId: string, roleId: string, request: UpdateCompanyRoleRequest): Promise<void> {
  await sendJson('PUT', `/admin/companies/${encodeURIComponent(companyId)}/roles/${encodeURIComponent(roleId)}`, request);
}

/**
 * Server-computed impact preview of a role permission change: added/removed codes, the number of
 * active members affected, the sensitive changed codes and — for a company manager — the candidate
 * codes outside their delegation ceiling (D-071). Read-only: nothing is saved and nothing is
 * audited; the save path re-runs every security control on its own.
 */
export async function previewCompanyRolePermissions(
  companyId: string,
  roleId: string,
  permissionCodes: readonly string[],
  signal?: AbortSignal,
): Promise<RolePermissionPreview> {
  const params = new URLSearchParams();
  for (const code of permissionCodes) params.append('permissionCodes', code);
  const query = params.size > 0 ? `?${params.toString()}` : '';
  return parseRolePermissionPreview(
    await getJson(`/admin/companies/${encodeURIComponent(companyId)}/roles/${encodeURIComponent(roleId)}/permissions/preview${query}`, signal),
  );
}

export type CompanyRoleMemberQuery = { page: number; pageSize: number; search?: string };

/** The active members of one role, paged and server-side searchable (`GET .../roles/{roleId}/members`). */
export async function getCompanyRoleMembers(
  companyId: string,
  roleId: string,
  query: CompanyRoleMemberQuery,
  signal?: AbortSignal,
): Promise<CompanyRoleMemberPage> {
  const params = new URLSearchParams({ page: String(query.page), pageSize: String(query.pageSize) });
  if (query.search?.trim()) params.set('search', query.search.trim());
  return parseCompanyRoleMembers(
    await getJson(`/admin/companies/${encodeURIComponent(companyId)}/roles/${encodeURIComponent(roleId)}/members?${params.toString()}`, signal),
  );
}

/**
 * The effective permissions of one active member of the company: the union of the codes granted by
 * the active roles of their active memberships, plus the role names. Installation administrators
 * and members holding `roles.view` may read it; users without an active membership in the company
 * answer 404 without disclosing the account (D-064).
 */
export async function getEffectivePermissions(
  companyId: string,
  userId: string,
  signal?: AbortSignal,
): Promise<EffectivePermissions> {
  return parseEffectivePermissions(
    await getJson(
      `/admin/companies/${encodeURIComponent(companyId)}/effective-permissions/${encodeURIComponent(userId)}`,
      signal,
    ),
  );
}

/**
 * Moves every active member of a role to another active role of the same company in one atomic
 * step and returns how many were moved. It never creates or reactivates a membership. System roles
 * and more than 500 members answer 409; a role of another company answers 404.
 */
export async function transferCompanyRoleMembers(companyId: string, roleId: string, targetRoleId: string): Promise<number> {
  const response = await sendJson(
    'POST',
    `/admin/companies/${encodeURIComponent(companyId)}/roles/${encodeURIComponent(roleId)}/members/transfer`,
    { targetRoleId },
  );
  return parseTransferredMemberCount(await response.json());
}

/**
 * Gives one existing, active member of the company the role (`POST .../roles/{roleId}/members/{userId}`, 204).
 * The server refuses a system role (409), a new or inactive membership (409), the caller's own membership
 * and anything beyond the caller's delegation ceiling (403), and answers 404 for anything it will not reveal.
 */
export async function assignCompanyRoleMember(companyId: string, roleId: string, userId: string): Promise<void> {
  await sendJson(
    'POST',
    `/admin/companies/${encodeURIComponent(companyId)}/roles/${encodeURIComponent(roleId)}/members/${encodeURIComponent(userId)}`,
  );
}

/** Every registered permission (unpaged, a few hundred at most). */
export async function getPermissionCatalog(signal?: AbortSignal): Promise<PermissionCatalogItem[]> {
  return parsePermissionCatalog(await getJson('/admin/permission-catalog', signal));
}

export async function getPermissionTemplates(signal?: AbortSignal): Promise<PermissionTemplate[]> {
  return parsePermissionTemplates(await getJson('/admin/permission-templates', signal));
}

/** The signed-in user's own membership requests, newest first (`GET /me/membership-requests`). */
export async function getMyMembershipRequests(
  page: number,
  pageSize: number,
  signal?: AbortSignal,
): Promise<MembershipRequestPage<MyMembershipRequest>> {
  return parseMyMembershipRequests(await getJson(`/me/membership-requests?page=${page}&pageSize=${pageSize}`, signal));
}

/**
 * Requests waiting for this company's approver, oldest first. The server answers 404 (not 403) to
 * a user without the approval permission in that company, so a company's queue is not revealed.
 */
export async function getPendingMembershipRequests(
  companyId: string,
  page: number,
  pageSize: number,
  signal?: AbortSignal,
): Promise<MembershipRequestPage<PendingMembershipRequest>> {
  return parsePendingMembershipRequests(
    await getJson(`/admin/companies/${encodeURIComponent(companyId)}/membership-requests/pending?page=${page}&pageSize=${pageSize}`, signal),
  );
}

/** Approves a pending request into one active role of the same company (204). Self-approval answers 403. */
export async function approveMembershipRequest(companyId: string, requestId: string, roleId: string): Promise<void> {
  await sendJson(
    'POST',
    `/admin/companies/${encodeURIComponent(companyId)}/membership-requests/${encodeURIComponent(requestId)}/approve`,
    { roleId },
  );
}

/** Rejects a pending request; the reason is optional and at most 500 characters (204). */
export async function rejectMembershipRequest(companyId: string, requestId: string, reason: string | null): Promise<void> {
  await sendJson(
    'POST',
    `/admin/companies/${encodeURIComponent(companyId)}/membership-requests/${encodeURIComponent(requestId)}/reject`,
    { reason },
  );
}

/** Filters of the security audit report; every filter is an exact match and optional. */
export type SecurityAuditQuery = {
  eventType?: string;
  userId?: string;
  companyId?: string;
  /** ISO instants (UTC). The server answers 400 when `fromUtc` is after `toUtc`. */
  fromUtc?: string;
  toUtc?: string;
  /** Server allowlist: only `occurredAt` and `eventType` exist; anything else answers 400. */
  sort?: 'occurredAt' | 'eventType';
  /** The server defaults to `occurredAt` descending when these are omitted. */
  descending?: boolean;
  page: number;
  pageSize: number;
};

/** The installation-wide security audit events. Sorting happens on the server, never on the page. */
export async function getSecurityAuditEvents(query: SecurityAuditQuery, signal?: AbortSignal): Promise<SecurityAuditPage> {
  const params = new URLSearchParams();
  if (query.eventType) params.set('eventType', query.eventType);
  if (query.userId) params.set('userId', query.userId);
  if (query.companyId) params.set('companyId', query.companyId);
  if (query.fromUtc) params.set('fromUtc', query.fromUtc);
  if (query.toUtc) params.set('toUtc', query.toUtc);
  if (query.sort) params.set('sort', query.sort);
  if (query.descending !== undefined) params.set('descending', String(query.descending));
  params.set('page', String(query.page));
  params.set('pageSize', String(query.pageSize));
  return parseSecurityAuditEvents(await getJson(`/admin/security/audit-events?${params.toString()}`, signal));
}

/**
 * The audit event-type registry of the server, unioned with every code observed in the database
 * (`GET /admin/security/audit-event-types`). Unknown codes arrive with the code as their label and
 * are never dropped, so a newly written event shows up without a client change.
 */
export async function getSecurityAuditEventTypes(signal?: AbortSignal): Promise<SecurityAuditEventType[]> {
  return parseSecurityAuditEventTypes(await getJson('/admin/security/audit-event-types', signal));
}

/** Query of the installation-wide company list of the security report; the server clamps every value. */
export type AdminCompanyQuery = { search?: string; page: number; pageSize: number };

/**
 * EVERY company of the installation, paged and searched on the server
 * (`GET /admin/companies`). Installation administrators only; unlike `GET /companies` this is not
 * limited to the caller's own memberships, so historical events of inactive companies stay filterable.
 */
export async function getAdminCompanies(query: AdminCompanyQuery, signal?: AbortSignal): Promise<AdminCompanyPage> {
  const params = new URLSearchParams({ page: String(query.page), pageSize: String(query.pageSize) });
  if (query.search?.trim()) params.set('search', query.search.trim());
  return parseAdminCompanies(await getJson(`/admin/companies?${params.toString()}`, signal));
}

/** The body of the audit CSV export; the server re-validates the range (31 days) and row cap (10,000). */
export type SecurityAuditExportRequest = {
  eventType?: string;
  userId?: string;
  companyId?: string;
  fromUtc: string;
  toUtc: string;
};

/** The `filename*`/`filename` of a Content-Disposition header, or `null` when absent or unsafe. */
export function fileNameFromDisposition(header: string | null): string | null {
  const safe = (name: string): string | null =>
    name !== '' && name !== '.' && name !== '..' && !name.includes('/') && !name.includes('\\') && !/[\u0000-\u001f]/.test(name) ? name : null;
  if (!header) return null;
  const star = /filename\*=UTF-8''([^;]+)/i.exec(header);
  if (star) {
    try {
      const raw = star[1];
      return raw ? safe(decodeURIComponent(raw).trim()) : null;
    } catch {
      return null;
    }
  }
  const plain = /filename\s*=\s*"?([^";]+)"?/i.exec(header);
  const name = plain?.[1]?.trim();
  return name ? safe(name) : null;
}

/**
 * Downloads the audit CSV export (`POST /admin/security/audit-events/export`) as a file. The filters
 * travel in the POST body, never in a URL or log; errors surface as {@link ApiError} with the
 * server's Persian messages (400 bad range, 403 not an administrator, 413 over the row cap).
 */
export async function exportSecurityAuditEventsCsv(request: SecurityAuditExportRequest): Promise<void> {
  const response = await sendJson('POST', '/admin/security/audit-events/export', request);
  const blob = await response.blob();
  const name = fileNameFromDisposition(response.headers.get('Content-Disposition')) ?? 'security-audit-events.csv';
  const url = URL.createObjectURL(blob);
  try {
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = name;
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
  } finally {
    URL.revokeObjectURL(url);
  }
}

/** Filters of the company user directory; the server validates and clamps every value. */
export type CompanyDirectoryQuery = {
  search?: string;
  /** `undefined` = every membership, `true` = active only, `false` = inactive only. */
  membershipActive?: boolean;
  page: number;
  pageSize: number;
};

/**
 * The users who have a membership in one company (`GET /companies/{id}/users`). The server answers 404 for a
 * company the user cannot access and 403 without the explicit `users.list` permission of that company.
 */
export async function getCompanyUsers(companyId: string, query: CompanyDirectoryQuery, signal?: AbortSignal): Promise<CompanyDirectoryPage> {
  const params = new URLSearchParams();
  if (query.search?.trim()) params.set('search', query.search.trim());
  if (query.membershipActive !== undefined) params.set('membershipActive', String(query.membershipActive));
  params.set('page', String(query.page));
  params.set('pageSize', String(query.pageSize));
  return parseCompanyDirectory(await getJson(`/companies/${encodeURIComponent(companyId)}/users?${params.toString()}`, signal));
}

/** Changes the current account password; the server signs out all sessions after success. */
export async function changeMyPassword(request: ChangeMyPasswordRequest): Promise<void> {
  await sendJson('POST', '/me/password', request);
}

/** The active sessions of the signed-in user (`GET /me/sessions`, D-069): id, current flag and two UTC times only. */
export async function getMySessions(signal?: AbortSignal): Promise<MySession[]> {
  return parseMySessions(await getJson('/me/sessions', signal));
}

/**
 * Ends one of the user's own sessions (204; repeating it is a no-op). An unknown id and another user's id both
 * answer 404. Revoking the CURRENT session also signs this browser out on the server.
 */
export async function revokeMySession(sessionId: string): Promise<void> {
  await sendJson('POST', `/me/sessions/${encodeURIComponent(sessionId)}/revoke`);
}

/** Ends every other session of the user and returns how many were ended; the current session stays. */
export async function revokeMyOtherSessions(): Promise<number> {
  const response = await sendJson('POST', '/me/sessions/revoke-others');
  return parseRevokedSessionCount(await response.json());
}

export async function getSession(signal?: AbortSignal): Promise<Session> {
  return parseSession(await getJson('/session', signal));
}

export async function getCompanies(signal?: AbortSignal): Promise<CompanySummary[]> {
  return parseCompanies(await getJson('/companies', signal));
}

export async function getCompanyOverview(companyId: string, signal?: AbortSignal): Promise<CompanyOverview> {
  return parseCompanyOverview(await getJson(`/companies/${encodeURIComponent(companyId)}/overview`, signal));
}

/** The validated current choice, the choices and the entry mode, all decided by the server. */
export async function getWorkContext(signal?: AbortSignal): Promise<WorkContext> {
  return parseWorkContext(await getJson('/context', signal));
}

/** Chooses a company and one of its fiscal periods; the server validates the pair again. */
export async function selectWorkContext(companyId: string, fiscalPeriodId: string): Promise<SelectedContext> {
  const response = await sendJson('POST', '/context', { companyId, fiscalPeriodId });
  return parseSelectedContextResponse(await response.json());
}

/** Persists the preferred company for this user; it never selects the active work context. */
export async function setDefaultCompany(companyId: string | null): Promise<void> {
  await sendJson('POST', '/context/default-company', { companyId });
}

export async function logout(): Promise<void> {
  await post('/session/logout');
}

/**
 * The address of the existing Razor sign-in page, returning to the current client route.
 * Only a local path is passed; the server additionally accepts local URLs only.
 */
export function buildLoginUrl(location: Pick<Location, 'pathname' | 'search'>): string {
  const returnUrl = `${location.pathname}${location.search}`;
  const safeReturnUrl = returnUrl.startsWith('/') && !returnUrl.startsWith('//') ? returnUrl : '/app/';
  return `${loginPath}?ReturnUrl=${encodeURIComponent(safeReturnUrl)}`;
}

/** Company is resolved exclusively from the server's protected work context. */
export async function getCurrentPermissions(signal?: AbortSignal) {
  return parseCurrentPermissions(await getJson('/me/permissions', signal));
}

export async function getMembershipDestinations(search: string, page: number, signal?: AbortSignal) {
  const params = new URLSearchParams({ search, page: String(page), pageSize: '25' });
  return parseMembershipDestinations(await getJson(`/me/membership-requests/destinations?${params}`, signal));
}

export async function createMembershipRequest(destinationCompanyId: string): Promise<void> {
  await sendJson('POST', '/me/membership-requests', { destinationCompanyId });
}

export async function getAssignableRoles(companyId: string, page: number, signal?: AbortSignal) {
  return parseAssignableRoles(await getJson(`/admin/companies/${encodeURIComponent(companyId)}/membership-requests/assignable-roles?page=${page}&pageSize=100`, signal));
}

/**
 * The authentication events of the targeted user, for installation administrators only
 * (stage 3 profile). Same allow-listed shape as the self-service login history.
 */
export async function getAdminUserLoginHistory(userId: string, signal?: AbortSignal, take = 50): Promise<LoginHistoryItem[]> {
  return parseLoginHistory(
    await getJson(`/admin/users/${encodeURIComponent(userId)}/login-history?take=${Math.min(Math.max(take, 1), 100)}`, signal),
  );
}

/**
 * The active sessions of the targeted user (installation administrator). Revocation is a
 * separate step-up flow (D-073) and is intentionally not part of this read.
 */
export async function getAdminUserSessions(userId: string, signal?: AbortSignal): Promise<AdminUserSession[]> {
  return parseAdminUserSessions(await getJson(`/admin/users/${encodeURIComponent(userId)}/sessions`, signal));
}

/** The read-only D-075 access catalog subsystems (installation administrator). */
export async function getAccessCatalogSubsystems(signal?: AbortSignal): Promise<AccessSubsystem[]> {
  return parseAccessSubsystems(await getJson('/admin/access/catalog/subsystems', signal));
}
