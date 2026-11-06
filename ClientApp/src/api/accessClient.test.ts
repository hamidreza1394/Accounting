import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  assignCompanyRoleMember,
  getMySessions,
  revokeMySession,
  revokeMyOtherSessions,
  UnauthenticatedError,
} from './client';
import { ContractError, parseMySessions, parseRevokedSessionCount } from './types';

const jsonResponse = (status: number, body: unknown) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
const token = () => jsonResponse(200, { headerName: 'X-XSRF-TOKEN', requestToken: 'tok' });

afterEach(() => vi.unstubAllGlobals());

describe('assign a member to a role (POST .../members/{userId})', () => {
  it('posts with an anti-forgery token and no body, encoding both ids', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(token()).mockResolvedValueOnce(new Response(null, { status: 204 }));
    vi.stubGlobal('fetch', fetchMock);
    await expect(assignCompanyRoleMember('c/1', 'r 1', 'u/1')).resolves.toBeUndefined();
    const [url, init] = fetchMock.mock.calls[1] as [string, RequestInit];
    expect(url).toBe('/api/v1/admin/companies/c%2F1/roles/r%201/members/u%2F1');
    expect(init.method).toBe('POST');
    expect((init.headers as Record<string, string>)['X-XSRF-TOKEN']).toBe('tok');
    expect(init.body).toBeUndefined();
  });

  it.each([
    [403, { error: 'شما مجاز به واگذاری این گروه یا مجوزها نیستید؛ سقف تفویض شما کمتر از این است.' }],
    [409, { error: 'تخصیص گروه سیستمی مجاز نیست.' }],
  ])('surfaces the Persian server message for %i', async (status, body) => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValueOnce(token()).mockResolvedValueOnce(jsonResponse(status, body)));
    await expect(assignCompanyRoleMember('c', 'r', 'u')).rejects.toMatchObject({ status, message: body.error });
  });

  it('maps 404 to the generic not-found message and 401 to session expiry', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValueOnce(token()).mockResolvedValueOnce(new Response(null, { status: 404 })));
    await expect(assignCompanyRoleMember('c', 'r', 'u')).rejects.toMatchObject({ status: 404 });
    vi.stubGlobal('fetch', vi.fn().mockResolvedValueOnce(token()).mockResolvedValueOnce(new Response(null, { status: 401 })));
    await expect(assignCompanyRoleMember('c', 'r', 'u')).rejects.toBeInstanceOf(UnauthenticatedError);
  });
});

const session = (n: number, overrides: Record<string, unknown> = {}) => ({
  sessionId: `00000000-0000-4000-8000-00000000000${n}`,
  isCurrent: false,
  createdAtUtc: '2026-11-05T08:00:00Z',
  lastSeenAtUtc: '2026-11-06T09:30:00Z',
  ...overrides,
});

describe('my sessions API (D-069)', () => {
  it('lists active sessions, never sending a body or a credential', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(200, [session(1, { isCurrent: true }), session(2)]));
    vi.stubGlobal('fetch', fetchMock);
    const sessions = await getMySessions();
    expect(fetchMock.mock.calls[0]?.[0]).toBe('/api/v1/me/sessions');
    expect(sessions).toHaveLength(2);
    expect(sessions[0]).toEqual({
      sessionId: '00000000-0000-4000-8000-000000000001',
      isCurrent: true,
      createdAtUtc: '2026-11-05T08:00:00Z',
      lastSeenAtUtc: '2026-11-06T09:30:00Z',
    });
  });

  it('revokes one session with an anti-forgery token and an encoded id', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(token()).mockResolvedValueOnce(new Response(null, { status: 204 }));
    vi.stubGlobal('fetch', fetchMock);
    await expect(revokeMySession('a/b')).resolves.toBeUndefined();
    const [url, init] = fetchMock.mock.calls[1] as [string, RequestInit];
    expect(url).toBe('/api/v1/me/sessions/a%2Fb/revoke');
    expect(init.method).toBe('POST');
    expect((init.headers as Record<string, string>)['X-XSRF-TOKEN']).toBe('tok');
  });

  it('reports 404 for an unknown or foreign session without telling them apart', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValueOnce(token()).mockResolvedValueOnce(new Response(null, { status: 404 })));
    await expect(revokeMySession('x')).rejects.toMatchObject({ status: 404 });
  });

  it('revokes the other sessions and returns the count', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(token()).mockResolvedValueOnce(jsonResponse(200, { revokedCount: 3 }));
    vi.stubGlobal('fetch', fetchMock);
    await expect(revokeMyOtherSessions()).resolves.toBe(3);
    const [url, init] = fetchMock.mock.calls[1] as [string, RequestInit];
    expect(url).toBe('/api/v1/me/sessions/revoke-others');
    expect(init.method).toBe('POST');
  });

  it('passes a 429 through with the Persian problem message', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(jsonResponse(429, { title: 'تعداد درخواست‌ها زیاد است. کمی بعد دوباره تلاش کنید.' })));
    await expect(getMySessions()).rejects.toMatchObject({ status: 429, message: 'تعداد درخواست‌ها زیاد است. کمی بعد دوباره تلاش کنید.' });
  });
});

describe('my sessions parsers', () => {
  it('rejects anything that is not the documented shape', () => {
    expect(() => parseMySessions({})).toThrow(ContractError);
    expect(() => parseMySessions([session(1, { sessionId: 5 })])).toThrow(ContractError);
    expect(() => parseMySessions([session(1, { isCurrent: 'yes' })])).toThrow(ContractError);
    expect(() => parseMySessions([session(1, { createdAtUtc: 'yesterday' })])).toThrow(ContractError);
    expect(() => parseMySessions([session(1, { lastSeenAtUtc: null })])).toThrow(ContractError);
    expect(() => parseMySessions([null])).toThrow(ContractError);
    expect(() => parseRevokedSessionCount({ revokedCount: -1 })).toThrow(ContractError);
    expect(() => parseRevokedSessionCount(null)).toThrow(ContractError);
  });

  it('keeps only the documented fields (no token, no extras)', () => {
    const [parsed] = parseMySessions([session(1, { token: 'secret', ipAddress: '1.2.3.4', userAgent: 'x' })]);
    expect(Object.keys(parsed ?? {}).sort()).toEqual(['createdAtUtc', 'isCurrent', 'lastSeenAtUtc', 'sessionId']);
  });

  it('accepts an empty list', () => {
    expect(parseMySessions([])).toEqual([]);
  });
});
