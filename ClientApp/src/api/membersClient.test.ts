import { afterEach, describe, expect, it, vi } from 'vitest';
import { getCompanyRoleMembers, transferCompanyRoleMembers } from './client';
import { ContractError, parseCompanyRoleMembers, parseTransferredMemberCount } from './types';
import { memberItem, memberPage } from '../test/roleMembers';

const jsonResponse = (status: number, body: unknown) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
const token = () => jsonResponse(200, { headerName: 'X-XSRF-TOKEN', requestToken: 'tok' });

afterEach(() => vi.unstubAllGlobals());

describe('role members API client', () => {
  it('reads one page with encoded ids', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(200, memberPage([memberItem(1)], { page: 2, pageSize: 10, totalCount: 11 })));
    vi.stubGlobal('fetch', fetchMock);
    const page = await getCompanyRoleMembers('a/b', 'r 1', { page: 2, pageSize: 10 });
    expect(fetchMock.mock.calls[0]?.[0]).toBe('/api/v1/admin/companies/a%2Fb/roles/r%201/members?page=2&pageSize=10');
    expect(page).toMatchObject({ page: 2, pageSize: 10, totalCount: 11 });
    expect(page.items[0]).toMatchObject({ userId: 'user-1', username: 'member1', isUserActive: true });
  });


  it('sends the server-side search term only when it is non-empty', async () => {
    const fetchMock = vi.fn().mockImplementation(async () => jsonResponse(200, memberPage([memberItem(1)], { page: 1, pageSize: 20, totalCount: 1 })));
    vi.stubGlobal('fetch', fetchMock);
    await getCompanyRoleMembers('c1', 'r1', { page: 1, pageSize: 20, search: '\u0633\u0627\u0631\u0627' });
    expect(fetchMock.mock.calls[0]?.[0]).toBe('/api/v1/admin/companies/c1/roles/r1/members?page=1&pageSize=20&search=' + encodeURIComponent('\u0633\u0627\u0631\u0627'));
    await getCompanyRoleMembers('c1', 'r1', { page: 1, pageSize: 20, search: '   ' });
    const second = String(fetchMock.mock.calls[1]?.[0]);
    expect(new URL(second, 'http://localhost').searchParams.has('search')).toBe(false);
  });

  it('transfers with an anti-forgery token and exactly { targetRoleId }, returning the moved count', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(token()).mockResolvedValueOnce(jsonResponse(200, { movedCount: 3 }));
    vi.stubGlobal('fetch', fetchMock);
    expect(await transferCompanyRoleMembers('c1', 'r/1', 'r2')).toBe(3);
    const [url, init] = fetchMock.mock.calls[1] as [string, RequestInit];
    expect(url).toBe('/api/v1/admin/companies/c1/roles/r%2F1/members/transfer');
    expect(init.method).toBe('POST');
    expect((init.headers as Record<string, string>)['X-XSRF-TOKEN']).toBe('tok');
    expect(JSON.parse(String(init.body))).toEqual({ targetRoleId: 'r2' });
  });

  it('surfaces the Persian server message for a refused transfer', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValueOnce(token()).mockResolvedValueOnce(jsonResponse(409, { error: 'انتقال گروهی از یا به گروه سیستمی مجاز نیست.' })));
    await expect(transferCompanyRoleMembers('c1', 'r1', 'r2')).rejects.toMatchObject({ status: 409, message: 'انتقال گروهی از یا به گروه سیستمی مجاز نیست.' });
  });
});

describe('role members parsers', () => {
  it('rejects malformed pages, dates and counts', () => {
    expect(() => parseCompanyRoleMembers({ items: 'x', totalCount: 0, page: 1, pageSize: 1 })).toThrow(ContractError);
    expect(() => parseCompanyRoleMembers(memberPage([memberItem(1, { memberSinceUtc: 'soon' })]))).toThrow(ContractError);
    expect(() => parseCompanyRoleMembers(memberPage([memberItem(1, { isUserActive: 'yes' })]))).toThrow(ContractError);
    expect(() => parseCompanyRoleMembers(memberPage([], { page: 0 }))).toThrow(ContractError);
    expect(() => parseTransferredMemberCount({ movedCount: -1 })).toThrow(ContractError);
    expect(() => parseTransferredMemberCount(null)).toThrow(ContractError);
  });

  it('accepts null names and username', () => {
    const page = parseCompanyRoleMembers(memberPage([memberItem(1, { username: null, firstName: null, lastName: null })]));
    expect(page.items[0]).toMatchObject({ username: null, firstName: null, lastName: null });
  });
});
