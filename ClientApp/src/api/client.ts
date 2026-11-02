import {
  parseAntiforgeryToken,
  parseMyProfile,
  parseLoginHistory,
  parseAdminUserList,
  parseAdminUserDetails,
  parseCompanyRoles,
  parsePermissionCatalog,
  parseCompanyRoleTemplates,
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
  type CompanyRoleSummary,
  type PermissionCatalogItem,
  type CompanyRoleTemplate,
  type CreateCompanyRoleRequest,
  type UpdateCompanyRoleRequest,
  type ChangeMyPasswordRequest,
  type CompanyOverview,
  type CompanySummary,
  type SelectedContext,
  type Session,
  type WorkContext,
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

  constructor(status: number, message: string) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
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

async function readProblemTitle(response: Response): Promise<string | null> {
  const contentType = response.headers.get('Content-Type') ?? '';
  if (!contentType.includes('json')) {
    return null;
  }
  try {
    const body: unknown = await response.json();
    if (typeof body === 'object' && body !== null && 'title' in body && typeof body.title === 'string') {
      return body.title;
    }
  } catch {
    // A malformed error body still produces the status-based message below.
  }
  return null;
}

async function ensureSuccess(response: Response): Promise<void> {
  if (response.status === 401) {
    throw new UnauthenticatedError();
  }
  if (!response.ok) {
    // The server only puts user-facing text into the problem title; details stay in its log.
    const title = response.status < 500 ? await readProblemTitle(response) : null;
    throw new ApiError(response.status, title ?? messageForStatus(response.status));
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
async function sendJson(method: 'POST' | 'PUT' | 'DELETE', path: string, body?: unknown): Promise<Response> {
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


/** Retrieves only the authenticated user's safe profile fields. */
export async function getMyProfile(signal?: AbortSignal): Promise<MyProfile> {
  return parseMyProfile(await getJson('/me/profile', signal));
}

/** Retrieves authentication events belonging only to the current user. */
export async function getMyLoginHistory(signal?: AbortSignal, take = 50): Promise<LoginHistoryItem[]> {
  const pageSize = Math.max(1, Math.min(100, Math.trunc(take)));
  return parseLoginHistory(await getJson(`/me/login-history?take=${pageSize}`, signal));
}

/** Changes the current account password; the server signs out all sessions after success. */
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
 
export async function createAdminUser(request: CreateAdminUserRequest): Promise<string> {
  const response = await sendJson('POST', '/admin/users', request);
  const value: unknown = await response.json();
  if (typeof value !== 'object' || value === null || !('id' in value) || typeof value.id !== 'string') {
    throw new ApiError(500, 'پاسخ ایجاد کاربر معتبر نیست.');
  }
  return value.id;
}

export async function updateAdminUser(userId: string, request: UpdateAdminUserRequest): Promise<void> {
  await sendJson('PUT', `/admin/users/${encodeURIComponent(userId)}`, request);
}
 
export async function getCompanyRoles(companyId: string, signal?: AbortSignal): Promise<CompanyRoleSummary[]> {
  return parseCompanyRoles(await getJson(`/admin/companies/${encodeURIComponent(companyId)}/roles`, signal));
}

export async function getPermissionCatalog(signal?: AbortSignal): Promise<PermissionCatalogItem[]> {
  return parsePermissionCatalog(await getJson('/admin/permission-catalog', signal));
}

export async function getCompanyRoleTemplates(signal?: AbortSignal): Promise<CompanyRoleTemplate[]> {
  return parseCompanyRoleTemplates(await getJson('/admin/permission-templates', signal));
}

export async function createCompanyRole(companyId: string, request: CreateCompanyRoleRequest): Promise<string> {
  const response = await sendJson('POST', `/admin/companies/${encodeURIComponent(companyId)}/roles`, request);
  const value: unknown = await response.json();
  if (typeof value !== 'object' || value === null || !('id' in value) || typeof value.id !== 'string') {
    throw new ApiError(500, 'پاسخ ایجاد گروه دسترسی معتبر نیست.');
  }
  return value.id;
}

export async function updateCompanyRole(companyId: string, roleId: string, request: UpdateCompanyRoleRequest): Promise<void> {
  await sendJson('PUT', `/admin/companies/${encodeURIComponent(companyId)}/roles/${encodeURIComponent(roleId)}`, request);
}
 
export async function assignUserCompanyRole(companyId: string, roleId: string, userId: string): Promise<void> {
  await sendJson(
    'POST',
    `/admin/companies/${encodeURIComponent(companyId)}/roles/${encodeURIComponent(roleId)}/members/${encodeURIComponent(userId)}`,
  );
}




export async function changeMyPassword(request: ChangeMyPasswordRequest): Promise<void> {
  await sendJson('POST', '/me/password', request);
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
