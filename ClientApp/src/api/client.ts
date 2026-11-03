import {
  parseAntiforgeryToken,
  parseMyProfile,
  parseLoginHistory,
  parseAdminUserList,
  parseAdminUserDetails,
  parseCreatedAdminUser,
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

/** Changes the current account password; the server signs out all sessions after success. */
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
