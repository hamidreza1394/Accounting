import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  approveMembershipRequest,
  getMyMembershipRequests,
  getPendingMembershipRequests,
  rejectMembershipRequest,
} from './client';
import { ContractError, parseMyMembershipRequests, parsePendingMembershipRequests } from './types';
import { myRequest, pageBody, pendingItem } from '../test/membership';

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}
const token = () => jsonResponse(200, { headerName: 'X-XSRF-TOKEN', requestToken: 'tok' });

afterEach(() => vi.unstubAllGlobals());

describe('membership request API client', () => {
  it('reads the own requests with the page parameters', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(200, pageBody([myRequest()], { page: 2, pageSize: 10, totalCount: 11 })));
    vi.stubGlobal('fetch', fetchMock);
    const page = await getMyMembershipRequests(2, 10);
    expect(fetchMock.mock.calls[0]?.[0]).toBe('/api/v1/me/membership-requests?page=2&pageSize=10');
    expect(page).toMatchObject({ page: 2, pageSize: 10, totalCount: 11 });
    expect(page.items[0]).toMatchObject({ status: 'Pending', decidedAtUtc: null, approvedRoleName: null });
  });

  it('reads the pending queue of one company with an encoded id', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(200, pageBody([pendingItem(1)])));
    vi.stubGlobal('fetch', fetchMock);
    const page = await getPendingMembershipRequests('a/../b', 1, 20);
    expect(fetchMock.mock.calls[0]?.[0]).toBe('/api/v1/admin/companies/a%2F..%2Fb/membership-requests/pending?page=1&pageSize=20');
    expect(page.items[0]).toMatchObject({ requesterDisplayName: 'کاربر 1', isRequesterActive: true });
  });

  it('approves with the anti-forgery token and exactly { roleId }', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(token()).mockResolvedValueOnce(new Response(null, { status: 204 }));
    vi.stubGlobal('fetch', fetchMock);
    await approveMembershipRequest('c 1', 'r/1', 'role-9');
    const [url, init] = fetchMock.mock.calls[1] as [string, RequestInit];
    expect(url).toBe('/api/v1/admin/companies/c%201/membership-requests/r%2F1/approve');
    expect(init.method).toBe('POST');
    expect((init.headers as Record<string, string>)['X-XSRF-TOKEN']).toBe('tok');
    expect(JSON.parse(String(init.body))).toEqual({ roleId: 'role-9' });
  });

  it('rejects with a reason, or with null when there is none', async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(token())
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockResolvedValueOnce(token())
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    vi.stubGlobal('fetch', fetchMock);
    await rejectMembershipRequest('c1', 'r1', 'سمت نامرتبط');
    await rejectMembershipRequest('c1', 'r2', null);
    expect(JSON.parse(String((fetchMock.mock.calls[1] as [string, RequestInit])[1].body))).toEqual({ reason: 'سمت نامرتبط' });
    expect(JSON.parse(String((fetchMock.mock.calls[3] as [string, RequestInit])[1].body))).toEqual({ reason: null });
  });

  it('surfaces the Persian server message for self-approval (403) and a decided request (409)', async () => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn()
        .mockResolvedValueOnce(token())
        .mockResolvedValueOnce(jsonResponse(403, { error: 'درخواست‌کننده نمی‌تواند درخواست عضویت خودش را تأیید کند.' }))
        .mockResolvedValueOnce(token())
        .mockResolvedValueOnce(jsonResponse(409, { error: 'درخواست قبلاً توسط کاربر دیگری تعیین تکلیف شده است.' })),
    );
    await expect(approveMembershipRequest('c1', 'r1', 'x')).rejects.toMatchObject({ status: 403 });
    await expect(rejectMembershipRequest('c1', 'r1', null)).rejects.toMatchObject({
      status: 409,
      message: 'درخواست قبلاً توسط کاربر دیگری تعیین تکلیف شده است.',
    });
  });

  it('answers 404 for a company the user may not approve for', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(jsonResponse(404, {})));
    await expect(getPendingMembershipRequests('c1', 1, 20)).rejects.toMatchObject({ status: 404 });
  });
});

describe('membership request parsers', () => {
  it('rejects an unknown status, a bad date and a malformed page', () => {
    expect(() => parseMyMembershipRequests(pageBody([myRequest({ status: 'Maybe' })]))).toThrow(ContractError);
    expect(() => parseMyMembershipRequests(pageBody([myRequest({ requestedAtUtc: 'yesterday' })]))).toThrow(ContractError);
    expect(() => parseMyMembershipRequests(pageBody([myRequest({ requestedAtUtc: null })]))).toThrow(ContractError);
    expect(() => parseMyMembershipRequests({ items: 'x', page: 1, pageSize: 1, totalCount: 0 })).toThrow(ContractError);
    expect(() => parseMyMembershipRequests(pageBody([], { page: 0 }))).toThrow(ContractError);
    expect(() => parsePendingMembershipRequests(pageBody([pendingItem(1, { isRequesterActive: 'yes' })]))).toThrow(ContractError);
    expect(() => parsePendingMembershipRequests(pageBody([pendingItem(1, { requesterDisplayName: 5 })]))).toThrow(ContractError);
  });

  it('keeps a decided request with its role and reason', () => {
    const page = parseMyMembershipRequests(
      pageBody([
        myRequest({
          status: 'Approved',
          decidedAtUtc: '2026-11-02T09:00:00Z',
          decisionReason: null,
          approvedRoleCode: 'ACCOUNTANT',
          approvedRoleName: 'حسابدار',
        }),
      ]),
    );
    expect(page.items[0]).toMatchObject({ status: 'Approved', approvedRoleName: 'حسابدار', decidedAtUtc: '2026-11-02T09:00:00Z' });
  });

  it('reads offset-less server timestamps as UTC', () => {
    const mine = parseMyMembershipRequests(pageBody([myRequest({ requestedAtUtc: '2026-11-01T08:00:00', decidedAtUtc: '2026-11-02T09:00:00.5' })]));
    expect(mine.items[0]).toMatchObject({ requestedAtUtc: '2026-11-01T08:00:00Z', decidedAtUtc: '2026-11-02T09:00:00.5Z' });
    const pending = parsePendingMembershipRequests(pageBody([pendingItem(1, { requestedAtUtc: '2026-11-01T08:00:00' })]));
    expect(pending.items[0]?.requestedAtUtc).toBe('2026-11-01T08:00:00Z');
  });
});
