import { describe, expect, it } from 'vitest';
import { ApiError, UnauthenticatedError } from '@/api/client';
import { TradeContractError, parsePartyDetail, parsePartyListResponse, parseTradeCapabilities } from './tradeTypes';
import { createParty, getParty, getTradeCapabilities, listCountries, listParties, updateParty } from './tradeClient';
import { countriesBody, freshRowVersion, partyDetail, partyId, partyItem, partyList, problem, rowVersion } from '../test/tradeFixtures';
import { stubTradeApi } from '../test/tradeTestUtils';

const body = { code: 'P-1', legalName: 'الف', displayName: null, legalIdentifier: null, countryCode: null, roles: ['Seller'] as const };

describe('tradeClient reads', () => {
  it('requests only same-origin, no-store JSON and parses the capabilities', async () => {
    const { fetchMock } = stubTradeApi({
      'GET /api/v1/trade/capabilities': { status: 200, body: { available: true, accessMode: 'ReadOnly', permissions: ['trade.form.party.view'] } },
    });
    const capabilities = await getTradeCapabilities();
    expect(capabilities).toEqual({ available: true, accessMode: 'ReadOnly', permissions: ['trade.form.party.view'] });
    const [url, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
    expect(url).toBe('/api/v1/trade/capabilities');
    expect(init.credentials).toBe('same-origin');
    expect(init.cache).toBe('no-store');
  });

  it('sends exactly the documented list query and nothing else (no tenant, no legal identifier)', async () => {
    const { calls } = stubTradeApi({ 'GET /api/v1/trade/parties': { status: 200, body: partyList([partyItem()]) } });
    await listParties({ search: 'نمونه', role: 'Seller', isActive: false, page: 2, pageSize: 50 });
    await listParties({ page: 1, pageSize: 20 });
    expect(calls[0]?.url.search).toBe(`?search=${encodeURIComponent('نمونه')}&role=Seller&isActive=false&page=2&pageSize=50`);
    expect(calls[1]?.url.search).toBe('?page=1&pageSize=20');
    expect(calls.map((call) => call.url.search).join('')).not.toMatch(/tenant|company|legalIdentifier/i);
  });

  it('parses the list and rejects an unknown role or a malformed row as a contract error', async () => {
    stubTradeApi({ 'GET /api/v1/trade/parties': { status: 200, body: partyList([partyItem({ roles: ['Unknown' as never] })]) } });
    await expect(listParties({ page: 1, pageSize: 20 })).rejects.toBeInstanceOf(TradeContractError);
    expect(() => parsePartyListResponse({ items: [{}], page: 1, pageSize: 1, totalCount: 1 })).toThrow(TradeContractError);
    expect(() => parsePartyListResponse(null)).toThrow(TradeContractError);
  });

  it('reads one party by an encoded id and parses the detail with the identifier and the audit times', async () => {
    const { calls } = stubTradeApi({ [`GET /api/v1/trade/parties/${partyId}`]: { status: 200, body: partyDetail() } });
    const detail = await getParty(partyId);
    expect(detail.legalIdentifier).toBe('14001234567');
    expect(detail.updatedAtUtc).toBeNull();
    expect(calls[0]?.url.pathname).toBe(`/api/v1/trade/parties/${partyId}`);
    expect(() => parsePartyDetail({ ...partyDetail(), createdAtUtc: 'not a date' })).toThrow(TradeContractError);
    expect(() => parsePartyDetail({ ...partyDetail(), id: 'x' })).toThrow(TradeContractError);
  });

  it('reads the countries', async () => {
    stubTradeApi({});
    expect(await listCountries()).toEqual(countriesBody);
  });

  it('rejects a capabilities body with an unknown access mode', () => {
    expect(() => parseTradeCapabilities({ available: true, accessMode: 'Maybe', permissions: [] })).toThrow(TradeContractError);
    expect(() => parseTradeCapabilities({ available: true, accessMode: 'ReadWrite', permissions: [1] })).toThrow(TradeContractError);
  });

  it('maps 401 to UnauthenticatedError and 404 to a not-found ApiError without details', async () => {
    stubTradeApi({ [`GET /api/v1/trade/parties/${partyId}`]: { status: 404 } });
    const notFound = await getParty(partyId).catch((cause: unknown) => cause);
    expect(notFound).toBeInstanceOf(ApiError);
    expect((notFound as ApiError).status).toBe(404);
    stubTradeApi({ 'GET /api/v1/trade/parties': { status: 401 } });
    await expect(listParties({ page: 1, pageSize: 20 })).rejects.toBeInstanceOf(UnauthenticatedError);
  });

  it('exposes the machine code of a 409 as problemCode and never a server 5xx text', async () => {
    stubTradeApi({ 'GET /api/v1/trade/parties': problem(403, 'trade_module_unavailable') });
    const forbidden = (await listParties({ page: 1, pageSize: 20 }).catch((cause: unknown) => cause)) as ApiError;
    expect(forbidden.status).toBe(403);
    expect(forbidden.problemCode).toBe('trade_module_unavailable');
    stubTradeApi({ 'GET /api/v1/trade/parties': { status: 500, body: { title: 'SqlException secret', detail: 'trade_forbidden' } } });
    const failure = (await listParties({ page: 1, pageSize: 20 }).catch((cause: unknown) => cause)) as ApiError;
    expect(failure.message).not.toContain('Sql');
    expect(failure.problemCode).toBeUndefined();
  });
});

describe('tradeClient writes', () => {
  it('POSTs the body field by field with the anti-forgery header and parses the 201 detail', async () => {
    const { calls } = stubTradeApi({ 'POST /api/v1/trade/parties': { status: 201, body: partyDetail({ code: 'P-1' }) } });
    const created = await createParty({ ...body, tenantId: 'x' } as never);
    expect(created.code).toBe('P-1');
    const post = calls.find((call) => call.method === 'POST');
    expect(post?.headers['X-XSRF-TOKEN']).toBe('token');
    expect(post?.body).toEqual(body);
    expect(post?.body).not.toHaveProperty('tenantId');
    expect(calls[0]?.url.pathname).toBe('/api/v1/antiforgery/token');
  });

  it('PUTs the full replacement with the rowVersion and parses the 200 detail', async () => {
    const { calls } = stubTradeApi({ [`PUT /api/v1/trade/parties/${partyId}`]: { status: 200, body: partyDetail({ rowVersion: freshRowVersion }) } });
    const updated = await updateParty(partyId, body, rowVersion);
    expect(updated.rowVersion).toBe(freshRowVersion);
    const put = calls.find((call) => call.method === 'PUT');
    expect(put?.body).toEqual({ ...body, rowVersion });
    expect(put?.headers['X-XSRF-TOKEN']).toBe('token');
  });

  it('surfaces a 409 conflict as an ApiError with its machine code', async () => {
    stubTradeApi({ [`PUT /api/v1/trade/parties/${partyId}`]: problem(409, 'trade_concurrency_conflict') });
    const error = (await updateParty(partyId, body, rowVersion).catch((cause: unknown) => cause)) as ApiError;
    expect(error.status).toBe(409);
    expect(error.problemCode).toBe('trade_concurrency_conflict');
  });
});
