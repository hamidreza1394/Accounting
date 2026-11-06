import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  exportSecurityAuditEventsCsv,
  fileNameFromDisposition,
  getAdminCompanies,
  getSecurityAuditEventTypes,
  getSecurityAuditEvents,
} from './client';
import { asUtcInstant } from '../lib/time';
import { exportRangeProblem } from '../lib/securityEvents';
import { ContractError, parseAdminCompanies, parseSecurityAuditEventTypes, parseSecurityAuditEvents } from './types';
import { adminCompanyItem, adminCompanyPage, auditEventTypesBody, auditItem, auditPage, csvBytes } from '../test/securityAudit';

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

  it('sends the allowlisted sort key and direction so the server orders the rows, not the page', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(200, auditPage([auditItem(1)])));
    vi.stubGlobal('fetch', fetchMock);
    await getSecurityAuditEvents({ sort: 'eventType', descending: false, page: 2, pageSize: 25 });
    expect(fetchMock.mock.calls[0]?.[0]).toBe('/api/v1/admin/security/audit-events?sort=eventType&descending=false&page=2&pageSize=25');
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
      sort: 'occurredAt',
      descending: true,
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
      sort: 'occurredAt',
      descending: 'true',
      page: '3',
      pageSize: '50',
    });
  });

  it('surfaces 403 for a non-administrator and the Persian 400 message for bad filters', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValueOnce(jsonResponse(403, {})).mockResolvedValueOnce(jsonResponse(400, { error: 'فیلترهای گزارش امنیتی معتبر نیستند.' })));
    await expect(getSecurityAuditEvents({ page: 1, pageSize: 25 })).rejects.toMatchObject({ status: 403 });
    await expect(getSecurityAuditEvents({ page: 1, pageSize: 25 })).rejects.toMatchObject({ status: 400, message: 'فیلترهای گزارش امنیتی معتبر نیستند.' });
  });

  it('fetches the event-type registry of the server, unknown codes included', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(200, auditEventTypesBody));
    vi.stubGlobal('fetch', fetchMock);
    const types = await getSecurityAuditEventTypes();
    expect(fetchMock.mock.calls[0]?.[0]).toBe('/api/v1/admin/security/audit-event-types');
    expect(types).toEqual(auditEventTypesBody);
  });

  it('searches the installation-wide company list on the server', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(200, adminCompanyPage([adminCompanyItem('c1', 'شرکت نمونه', false)])));
    vi.stubGlobal('fetch', fetchMock);
    const page = await getAdminCompanies({ search: ' نمونه ', page: 2, pageSize: 20 });
    expect(fetchMock.mock.calls[0]?.[0]).toBe('/api/v1/admin/companies?page=2&pageSize=20&search=%D9%86%D9%85%D9%88%D9%86%D9%87');
    expect(page.items[0]).toEqual({ id: 'c1', name: 'شرکت نمونه', isActive: false });
  });

  it('exports the CSV with a POST body and saves it under the server file name', async () => {
    const tokenResponse = jsonResponse(200, { headerName: 'X-XSRF-TOKEN', requestToken: 'tok' });
    const fetchMock = vi.fn().mockResolvedValueOnce(tokenResponse).mockResolvedValueOnce(csvBytes('security-audit-events-20261103-010203.csv'));
    vi.stubGlobal('fetch', fetchMock);
    const createObjectURL = vi.fn(() => 'blob:download-1');
    const revokeObjectURL = vi.fn();
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});
    const originalCreate = URL.createObjectURL;
    const originalRevoke = URL.revokeObjectURL;
    (URL as unknown as Record<string, unknown>).createObjectURL = createObjectURL;
    (URL as unknown as Record<string, unknown>).revokeObjectURL = revokeObjectURL;
    let downloadedName: string | null = null;
    try {
      await exportSecurityAuditEventsCsv({ eventType: 'LoginFailed', fromUtc: '2026-11-01T00:00:00Z', toUtc: '2026-11-05T23:59:59Z' });
      const anchor = click.mock.instances[0] as HTMLAnchorElement | undefined;
      downloadedName = anchor?.download ?? null;
    } finally {
      click.mockRestore();
      (URL as unknown as Record<string, unknown>).createObjectURL = originalCreate;
      (URL as unknown as Record<string, unknown>).revokeObjectURL = originalRevoke;
    }
    expect(fetchMock).toHaveBeenCalledTimes(2);
    const [url, init] = fetchMock.mock.calls[1] ?? [];
    expect(url).toBe('/api/v1/admin/security/audit-events/export');
    expect(init?.method).toBe('POST');
    expect(init?.headers).toMatchObject({ 'X-XSRF-TOKEN': 'tok', 'Content-Type': 'application/json' });
    expect(init?.body).toBe(JSON.stringify({ eventType: 'LoginFailed', fromUtc: '2026-11-01T00:00:00Z', toUtc: '2026-11-05T23:59:59Z' }));
    expect(createObjectURL).toHaveBeenCalledTimes(1);
    expect(downloadedName).toBe('security-audit-events-20261103-010203.csv');
    expect(revokeObjectURL).toHaveBeenCalledWith('blob:download-1');
  });

  it('maps export failures to their Persian messages without losing the filters', async () => {
    const token = () => jsonResponse(200, { headerName: 'X-XSRF-TOKEN', requestToken: 'tok' });
    vi.stubGlobal(
      'fetch',
      vi.fn()
        .mockResolvedValueOnce(token()).mockResolvedValueOnce(jsonResponse(400, { error: 'برای خروجی، بازه زمانی معتبر حداکثر ۳۱ روز و فیلترهای معتبر الزامی است.' }))
        .mockResolvedValueOnce(token()).mockResolvedValueOnce(jsonResponse(403, {}))
        .mockResolvedValueOnce(token()).mockResolvedValueOnce(jsonResponse(413, { error: 'تعداد رکوردهای خروجی بیش از حد مجاز است؛ بازه یا فیلترها را محدود کنید.' }))
        .mockResolvedValueOnce(token()).mockResolvedValueOnce(new Response(null, { status: 500 })),
    );
    const request = { fromUtc: '2026-11-01T00:00:00Z', toUtc: '2026-11-05T23:59:59Z' };
    await expect(exportSecurityAuditEventsCsv(request)).rejects.toMatchObject({ status: 400, message: 'برای خروجی، بازه زمانی معتبر حداکثر ۳۱ روز و فیلترهای معتبر الزامی است.' });
    await expect(exportSecurityAuditEventsCsv(request)).rejects.toMatchObject({ status: 403 });
    await expect(exportSecurityAuditEventsCsv(request)).rejects.toMatchObject({ status: 413, message: 'تعداد رکوردهای خروجی بیش از حد مجاز است؛ بازه یا فیلترها را محدود کنید.' });
    await expect(exportSecurityAuditEventsCsv(request)).rejects.toMatchObject({ status: 500 });
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

  it('reads an offset-less server timestamp as UTC, never as browser-local time', () => {
    // EF returns SQL Server datetime2 as Kind=Unspecified, which System.Text.Json writes without Z.
    const page = parseSecurityAuditEvents(auditPage([auditItem(1, { occurredAtUtc: '2026-11-03T10:00:00' }), auditItem(2, { occurredAtUtc: '2026-11-03T10:00:00.1234567' })]));
    expect(page.items[0]?.occurredAtUtc).toBe('2026-11-03T10:00:00Z');
    expect(Date.parse(page.items[0]?.occurredAtUtc ?? '')).toBe(Date.UTC(2026, 10, 3, 10, 0, 0));
    expect(page.items[1]?.occurredAtUtc).toBe('2026-11-03T10:00:00.1234567Z');
  });

  it('leaves values that already carry Z or an offset untouched', () => {
    expect(asUtcInstant('2026-11-03T10:00:00Z')).toBe('2026-11-03T10:00:00Z');
    expect(asUtcInstant('2026-11-03T13:30:00+03:30')).toBe('2026-11-03T13:30:00+03:30');
  });
});

describe('audit event-type parser', () => {
  it('keeps every entry, including a code unknown to any registry', () => {
    const types = parseSecurityAuditEventTypes([
      { code: 'LoginSucceeded', label: 'ورود موفق' },
      { code: 'BrandNewEvent', label: 'BrandNewEvent' },
    ]);
    expect(types).toHaveLength(2);
    expect(types[1]).toEqual({ code: 'BrandNewEvent', label: 'BrandNewEvent' });
  });

  it('accepts an empty registry', () => {
    expect(parseSecurityAuditEventTypes([])).toEqual([]);
  });

  it('rejects a non-array, a non-object entry and missing or blank fields', () => {
    expect(() => parseSecurityAuditEventTypes({ code: 'X', label: 'x' })).toThrow(ContractError);
    expect(() => parseSecurityAuditEventTypes([{ code: 'X' }])).toThrow(ContractError);
    expect(() => parseSecurityAuditEventTypes([{ code: 'X', label: 5 }])).toThrow(ContractError);
    expect(() => parseSecurityAuditEventTypes([{ code: '', label: 'x' }])).toThrow(ContractError);
    expect(() => parseSecurityAuditEventTypes([{ code: 'X', label: ' ' }])).toThrow(ContractError);
    expect(() => parseSecurityAuditEventTypes([null])).toThrow(ContractError);
  });

  it('asks for the range before sending anything and rejects a range beyond 31 days', () => {
    expect(exportRangeProblem(null, null)).toBe('برای خروجی CSV ابتدا بازهٔ زمانی (از تاریخ و تا تاریخ) را اعمال کنید.');
    expect(exportRangeProblem('2026-11-01T00:00:00Z', null)).toBe('برای خروجی CSV ابتدا بازهٔ زمانی (از تاریخ و تا تاریخ) را اعمال کنید.');
    expect(exportRangeProblem('2026-11-01T00:00:00Z', '2026-11-06T20:29:59.999Z')).toBeNull();
    // Exactly 31 days is the server limit and stays allowed.
    expect(exportRangeProblem('2026-11-01T00:00:00Z', '2026-12-02T00:00:00Z')).toBeNull();
    expect(exportRangeProblem('2026-11-01T00:00:00Z', '2026-12-02T00:00:00.001Z')).toBe('بازهٔ زمانی خروجی حداکثر ۳۱ روز است؛ بازه را کوچک‌تر کنید.');
  });
});

describe('admin company list parser', () => {
  it('reads the paged list and keeps inactive companies filterable', () => {
    const page = parseAdminCompanies(adminCompanyPage([adminCompanyItem('c1', 'شرکت نمونه', true), adminCompanyItem('c2', 'شرکت قدیمی', false)], { totalCount: 2 }));
    expect(page.items).toEqual([
      { id: 'c1', name: 'شرکت نمونه', isActive: true },
      { id: 'c2', name: 'شرکت قدیمی', isActive: false },
    ]);
    expect(page.totalCount).toBe(2);
  });

  it('rejects malformed pages and items', () => {
    expect(() => parseAdminCompanies(null)).toThrow(ContractError);
    expect(() => parseAdminCompanies({ items: 3, totalCount: 0, page: 1, pageSize: 1 })).toThrow(ContractError);
    expect(() => parseAdminCompanies(adminCompanyPage([{ id: 'c1' }]))).toThrow(ContractError);
    expect(() => parseAdminCompanies(adminCompanyPage([adminCompanyItem('c1', 'شرکت', 'yes' as unknown as boolean)]))).toThrow(ContractError);
    expect(() => parseAdminCompanies(adminCompanyPage([], { page: 0 }))).toThrow(ContractError);
  });
});

describe('Content-Disposition file name', () => {
  it('reads plain and UTF-8 names and refuses unsafe or missing ones', () => {
    expect(fileNameFromDisposition('attachment; filename="security-audit-events-20261103-010203.csv"')).toBe('security-audit-events-20261103-010203.csv');
    expect(fileNameFromDisposition("attachment; filename*=UTF-8''%DA%AF%D8%B2%D8%A7%D8%B1%D8%B4.csv")).toBe('گزارش.csv');
    expect(fileNameFromDisposition('attachment')).toBeNull();
    expect(fileNameFromDisposition(null)).toBeNull();
    expect(fileNameFromDisposition('attachment; filename=".."')).toBeNull();
    expect(fileNameFromDisposition("attachment; filename*=UTF-8''a%2Fb.csv")).toBeNull();
  });
});
