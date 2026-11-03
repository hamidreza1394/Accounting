import { afterEach, describe, expect, it, vi } from 'vitest';
import { getSecurityAuditEvents } from './client';
import { ContractError, parseSecurityAuditEvents } from './types';
import { auditItem, auditPage } from '../test/securityAudit';

const jsonResponse = (status: number, body: unknown) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });

afterEach(() => vi.unstubAllGlobals());

describe('security audit API client', () => {
  it('sends only the filters that are set, plus the page', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(200, auditPage([auditItem(1)])));
    vi.stubGlobal('fetch', fetchMock);
    await getSecurityAuditEvents({ page: 1, pageSize: 25 });
    expect(fetchMock.mock.calls[0]?.[0]).toBe('/api/v1/admin/security/audit-events?page=1&pageSize=25');
  });

  it('encodes every filter exactly', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(200, auditPage([])));
    vi.stubGlobal('fetch', fetchMock);
    await getSecurityAuditEvents({
      eventType: 'Company Role&x',
      userId: 'u1',
      companyId: 'c1',
      fromUtc: '2026-11-01T00:00:00.000Z',
      toUtc: '2026-11-03T23:59:59.999Z',
      page: 3,
      pageSize: 50,
    });
    const url = new URL(String(fetchMock.mock.calls[0]?.[0]), 'http://localhost');
    expect(Object.fromEntries(url.searchParams)).toEqual({
      eventType: 'Company Role&x',
      userId: 'u1',
      companyId: 'c1',
      fromUtc: '2026-11-01T00:00:00.000Z',
      toUtc: '2026-11-03T23:59:59.999Z',
      page: '3',
      pageSize: '50',
    });
  });

  it('surfaces 403 for a non-administrator and the Persian 400 message for bad filters', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValueOnce(jsonResponse(403, {})).mockResolvedValueOnce(jsonResponse(400, { error: 'فیلترهای گزارش امنیتی معتبر نیستند.' })));
    await expect(getSecurityAuditEvents({ page: 1, pageSize: 25 })).rejects.toMatchObject({ status: 403 });
    await expect(getSecurityAuditEvents({ page: 1, pageSize: 25 })).rejects.toMatchObject({ status: 400, message: 'فیلترهای گزارش امنیتی معتبر نیستند.' });
  });
});

describe('security audit parser', () => {
  it('keeps nullable actor, company and entity fields', () => {
    const page = parseSecurityAuditEvents(
      auditPage([auditItem(1, { userId: null, actorUsername: null, actorDisplayName: null, companyId: null, companyName: null, entityType: null, entityId: null, correlationId: null })]),
    );
    expect(page.items[0]).toMatchObject({ userId: null, companyId: null, entityType: null, correlationId: null });
  });

  it('rejects malformed pages, dates and fields', () => {
    expect(() => parseSecurityAuditEvents(null)).toThrow(ContractError);
    expect(() => parseSecurityAuditEvents({ items: 'x', totalCount: 0, page: 1, pageSize: 1 })).toThrow(ContractError);
    expect(() => parseSecurityAuditEvents(auditPage([auditItem(1, { occurredAtUtc: 'yesterday' })]))).toThrow(ContractError);
    expect(() => parseSecurityAuditEvents(auditPage([auditItem(1, { occurredAtUtc: null })]))).toThrow(ContractError);
    expect(() => parseSecurityAuditEvents(auditPage([auditItem(1, { eventType: 7 })]))).toThrow(ContractError);
    expect(() => parseSecurityAuditEvents(auditPage([], { page: 0 }))).toThrow(ContractError);
    expect(() => parseSecurityAuditEvents(auditPage([], { totalCount: -1 }))).toThrow(ContractError);
  });

  it('does not carry an IP address or payload even if a server ever sent one', () => {
    const page = parseSecurityAuditEvents(auditPage([auditItem(1, { ipAddress: '10.0.0.1', detailsJson: '{"secret":1}' })]));
    expect(Object.keys(page.items[0] as object)).not.toContain('ipAddress');
    expect(Object.keys(page.items[0] as object)).not.toContain('detailsJson');
  });
});
