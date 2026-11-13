import { describe, expect, it, vi } from 'vitest';
import { ApiError, UnauthenticatedError, apiGetRaw, apiPostJson, buildLoginUrl, normalizeApiError, sendJson, getCompanies, getCompanyOverview, getWorkContext, logout, setDefaultCompany } from './client';
import { ContractError, parseAdminUserDetails } from './types';

function jsonResponse(status: number, body: unknown, contentType = 'application/json'): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': contentType } });
}

describe('api client', () => {
  it('sends same-origin credentials and parses the companies', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      jsonResponse(200, [{ id: 'c1', name: 'شرکت الف', nationalId: null, roleName: 'حسابدار' }]),
    );
    vi.stubGlobal('fetch', fetchMock);

    const companies = await getCompanies();

    expect(companies).toEqual([{ id: 'c1', name: 'شرکت الف', nationalId: null, roleName: 'حسابدار' }]);
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toBe('/api/v1/companies');
    expect(init.credentials).toBe('same-origin');
  });

  it('reports an expired session as UnauthenticatedError', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: 401 })));

    await expect(getCompanies()).rejects.toBeInstanceOf(UnauthenticatedError);
  });

  it('reports a company of another tenant as not found, without details', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: 404 })));

    const error = await getCompanyOverview('other').catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(404);
    expect((error as ApiError).message).toContain('دسترسی');
  });

  it('never shows the body of a server error to the user', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(jsonResponse(500, { title: 'SqlException: Login failed for user sa' }, 'application/problem+json')),
    );

    const error = await getCompanies().catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).message).not.toContain('Sql');
  });

  it('rejects a response that does not match the contract', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(jsonResponse(200, [{ id: 1 }])));

    await expect(getCompanies()).rejects.toBeInstanceOf(ContractError);
  });

  it('escapes the company id in the URL', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(200, {}));
    vi.stubGlobal('fetch', fetchMock);

    await getCompanyOverview('../session').catch(() => undefined);

    expect(fetchMock.mock.calls[0]?.[0]).toBe('/api/v1/companies/..%2Fsession/overview');
  });

  it('sends the anti-forgery request token in the header when signing out', async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(jsonResponse(200, { headerName: 'X-XSRF-TOKEN', requestToken: 'token-123' }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    vi.stubGlobal('fetch', fetchMock);

    await logout();

    const [url, init] = fetchMock.mock.calls[1] as [string, RequestInit];
    expect(url).toBe('/api/v1/session/logout');
    expect(init.method).toBe('POST');
    expect((init.headers as Record<string, string>)['X-XSRF-TOKEN']).toBe('token-123');
  });

  it('saves the default company with an anti-forgery token and JSON body', async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(jsonResponse(200, { headerName: 'X-XSRF-TOKEN', requestToken: 'token-123' }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    vi.stubGlobal('fetch', fetchMock);

    await setDefaultCompany('company-42');

    const [url, init] = fetchMock.mock.calls[1] as [string, RequestInit];
    expect(url).toBe('/api/v1/context/default-company');
    expect(init.method).toBe('POST');
    expect((init.headers as Record<string, string>)['X-XSRF-TOKEN']).toBe('token-123');
    expect(JSON.parse(String(init.body))).toEqual({ companyId: 'company-42' });
  });

  it('parses the saved default company and server-validated relationship labels', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(jsonResponse(200, {
      current: null,
      entry: 'selectionRequired',
      directSelection: null,
      defaultCompanyId: 'company-42',
      companies: [{
        id: 'company-42',
        name: 'شرکت مادر',
        nationalId: null,
        roleName: 'مدیر',
        relationshipTypes: ['parent'],
        fiscalPeriods: [{
          id: 'period-1', title: 'سال مالی', startDate: '2026-03-21', endDate: '2027-03-20',
          status: 'Open', isSelectable: true, accessMode: 'readWrite',
        }],
      }],
    })));

    const context = await getWorkContext();
    expect(context.defaultCompanyId).toBe('company-42');
    expect(context.companies[0]?.relationshipTypes).toEqual(['parent']);
  });

  it('rejects unknown company relationship type codes', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(jsonResponse(200, {
      current: null,
      entry: 'selectionRequired',
      directSelection: null,
      defaultCompanyId: null,
      companies: [{
        id: 'company-42', name: 'شرکت', nationalId: null, roleName: 'مدیر', relationshipTypes: ['not-a-real-type'],
        fiscalPeriods: [],
      }],
    })));

    await expect(getWorkContext()).rejects.toBeInstanceOf(ContractError);
  });

  it('builds a login URL that returns to a local path only', () => {
    expect(buildLoginUrl({ pathname: '/app/', search: '?company=c1' })).toBe(
      '/Account/Login?ReturnUrl=%2Fapp%2F%3Fcompany%3Dc1',
    );
    expect(buildLoginUrl({ pathname: '//evil.example', search: '' })).toBe('/Account/Login?ReturnUrl=%2Fapp%2F');
  });
});


describe('admin user response contracts', () => {
  const validDetails = {
    id: 'user-1',
    username: 'alice',
    firstName: 'آلیس',
    lastName: 'مثالی',
    emailAddress: 'alice@example.test',
    cellPhoneNumber: null,
    isActive: true,
    lastLoginDateTime: null,
    insertDateTime: '2026-11-02T08:00:00Z',
    isEmailAddressVerified: false,
    isCellPhoneNumberVerified: false,
    updateDateTime: '2026-11-02T08:00:00Z',
    legacyRoleName: null,
    memberships: [],
  };

  it('parses required detail fields into a fully typed user record', () => {
    expect(parseAdminUserDetails(validDetails)).toMatchObject({
      id: 'user-1',
      emailAddress: 'alice@example.test',
      activeCompanyCount: 0,
      memberships: [],
    });
  });

  it('rejects details when a required identity field is missing', () => {
    const { id: _ignored, ...withoutId } = validDetails;
    expect(() => parseAdminUserDetails(withoutId)).toThrow(ContractError);
  });
});

describe('error normalization', () => {
  it('reads RFC 7807 title and the errors dictionary as field errors', () => {
    const error = normalizeApiError(400, {
      title: 'ورودی نامعتبر است',
      errors: { Email: ['ایمیل معتبر نیست'], 'phone': ['کوتاه است', 'غیرمجاز'], Empty: [] },
    });
    expect(error.status).toBe(400);
    expect(error.message).toBe('ورودی نامعتبر است');
    expect(error.fieldErrors).toEqual({ email: ['ایمیل معتبر نیست'], phone: ['کوتاه است', 'غیرمجاز'] });
  });

  it('never shows detail; uses title, then the error string, then the status message', () => {
    expect(normalizeApiError(409, { detail: 'internal text' }).message).toBe('درخواست با خطا مواجه شد.');
    expect(normalizeApiError(409, { title: 'نام تکراری است', detail: 'internal text' }).message).toBe('نام تکراری است');
    expect(normalizeApiError(400, { error: 'بسته نامعتبر' }).message).toBe('بسته نامعتبر');
    expect(normalizeApiError(403, {}).message).toContain('مجوز');
    expect(normalizeApiError(400, 'text').message).toBe('درخواست با خطا مواجه شد.');
    expect(normalizeApiError(400, { title: 'x' }).fieldErrors).toBeUndefined();
  });

  it('exposes only a machine-code `detail` as problemCode, never free text, and never for 5xx', () => {
    expect(normalizeApiError(409, { title: 'x', detail: 'trade_concurrency_conflict' }).problemCode).toBe('trade_concurrency_conflict');
    expect(normalizeApiError(409, { detail: 'internal text' }).problemCode).toBeUndefined();
    expect(normalizeApiError(409, { detail: 'Some.Exception: boom' }).problemCode).toBeUndefined();
    expect(normalizeApiError(400, { title: 'x' }).problemCode).toBeUndefined();
    expect(normalizeApiError(500, { detail: 'trade_forbidden' }).problemCode).toBeUndefined();
  });

  it('replaces the English default validation title with the Persian status message', () => {
    const error = normalizeApiError(400, { title: 'One or more validation errors occurred.', errors: { Name: ['الزامی است'] } });
    expect(error.message).toBe('درخواست با خطا مواجه شد.');
    expect(error.fieldErrors).toEqual({ name: ['الزامی است'] });
    expect(normalizeApiError(400, { title: 'One or more validation errors occurred.' }).message).toBe(
      'One or more validation errors occurred.',
    );
  });

  it('never exposes the body of a server error', () => {
    const error = normalizeApiError(500, { title: 'SqlException', errors: { A: ['x'] } });
    expect(error.message).not.toContain('Sql');
    expect(error.fieldErrors).toBeUndefined();
  });

  it('carries field errors from a failed state change and still maps 401', async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(jsonResponse(200, { headerName: 'X-XSRF-TOKEN', requestToken: 't' }))
      .mockResolvedValueOnce(jsonResponse(400, { title: 'نامعتبر', errors: { Name: ['الزامی است'] } }, 'application/problem+json'));
    vi.stubGlobal('fetch', fetchMock);

    const error = await sendJson('POST', '/admin/users', { name: '' }).catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).fieldErrors).toEqual({ name: ['الزامی است'] });

    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: 401 })));
    await expect(sendJson('PUT', '/x', {})).rejects.toBeInstanceOf(UnauthenticatedError);
  });
});

describe('sendJson methods', () => {
  it.each(['PUT', 'PATCH'] as const)('sends %s with the anti-forgery header and a JSON body', async (method) => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(jsonResponse(200, { headerName: 'X-XSRF-TOKEN', requestToken: 'abc' }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    vi.stubGlobal('fetch', fetchMock);

    await sendJson(method, '/admin/users/7', { firstName: 'علی' });

    const [url, init] = fetchMock.mock.calls[1] as [string, RequestInit];
    expect(url).toBe('/api/v1/admin/users/7');
    expect(init.method).toBe(method);
    expect(init.headers).toMatchObject({ 'X-XSRF-TOKEN': 'abc', 'Content-Type': 'application/json' });
    expect(JSON.parse(String(init.body))).toEqual({ firstName: 'علی' });
  });
});

describe('provisioning helpers', () => {
  it('posts JSON with the given token, no-store, and returns status and body without throwing', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(410, { detail: 'منقضی' }));
    vi.stubGlobal('fetch', fetchMock);

    const result = await apiPostJson('/provisioning/apply', { a: 1 }, { headerName: 'X-T', requestToken: 'r' });

    expect(result).toEqual({ ok: false, status: 410, body: { detail: 'منقضی' } });
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toBe('/api/v1/provisioning/apply');
    expect(init).toMatchObject({ method: 'POST', credentials: 'same-origin', cache: 'no-store' });
    expect(init.headers).toEqual({ 'Content-Type': 'application/json', 'X-T': 'r' });
    expect(init.body).toBe('{"a":1}');
  });

  it('returns a null body for an empty or malformed answer, and GETs without caching', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 204 }));
    vi.stubGlobal('fetch', fetchMock);

    expect(await apiGetRaw('/antiforgery/token')).toEqual({ ok: true, status: 204, body: null });
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toBe('/api/v1/antiforgery/token');
    expect(init).toEqual({ credentials: 'same-origin', cache: 'no-store' });
  });
});
