import { describe, expect, it, vi } from 'vitest';
import { ApiError, UnauthenticatedError, buildLoginUrl, getCompanies, getCompanyOverview, logout } from './client';
import { ContractError } from './types';

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

  it('builds a login URL that returns to a local path only', () => {
    expect(buildLoginUrl({ pathname: '/app/', search: '?company=c1' })).toBe(
      '/Account/Login?ReturnUrl=%2Fapp%2F%3Fcompany%3Dc1',
    );
    expect(buildLoginUrl({ pathname: '//evil.example', search: '' })).toBe('/Account/Login?ReturnUrl=%2Fapp%2F');
  });
});
