import { afterEach, describe, expect, it, vi } from 'vitest';
import { getCompanyUsers } from './client';
import { ContractError, parseCompanyDirectory } from './types';
import { directoryPage, directoryUser } from '../test/companyDirectory';

const jsonResponse = (status: number, body: unknown) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });

afterEach(() => vi.unstubAllGlobals());

describe('company user directory client', () => {
  it('sends only the set filters and encodes the company id', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(200, directoryPage([directoryUser(1)])));
    vi.stubGlobal('fetch', fetchMock);
    await getCompanyUsers('a/b', { page: 1, pageSize: 25 });
    expect(fetchMock.mock.calls[0]?.[0]).toBe('/api/v1/companies/a%2Fb/users?page=1&pageSize=25');
  });

  it('sends search and the membership filter (false is a real value)', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(200, directoryPage([])));
    vi.stubGlobal('fetch', fetchMock);
    await getCompanyUsers('c1', { search: '  علی ', membershipActive: false, page: 2, pageSize: 50 });
    const url = new URL(String(fetchMock.mock.calls[0]?.[0]), 'http://localhost');
    expect(Object.fromEntries(url.searchParams)).toEqual({ search: 'علی', membershipActive: 'false', page: '2', pageSize: '50' });
  });

  it('surfaces 403 (no users.list) and 404 (no access to the company)', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValueOnce(jsonResponse(403, {})).mockResolvedValueOnce(jsonResponse(404, {})));
    await expect(getCompanyUsers('c1', { page: 1, pageSize: 25 })).rejects.toMatchObject({ status: 403 });
    await expect(getCompanyUsers('c1', { page: 1, pageSize: 25 })).rejects.toMatchObject({ status: 404 });
  });
});

describe('company user directory parser', () => {
  it('reads the envelope and an offset-less membership date as UTC', () => {
    const page = parseCompanyDirectory(directoryPage([directoryUser(1, { membershipCreatedAtUtc: '2026-10-01T09:00:00' })]));
    expect(page).toMatchObject({ companyId: 'a', companyName: 'شرکت الف', totalCount: 1 });
    expect(page.items[0]?.membershipCreatedAtUtc).toBe('2026-10-01T09:00:00Z');
  });

  it('keeps null names and username, and rejects malformed input', () => {
    const page = parseCompanyDirectory(directoryPage([directoryUser(1, { username: null, firstName: null, lastName: null })]));
    expect(page.items[0]).toMatchObject({ username: null, firstName: null, lastName: null });
    expect(() => parseCompanyDirectory(null)).toThrow(ContractError);
    expect(() => parseCompanyDirectory({ ...directoryPage([]), companyName: 3 })).toThrow(ContractError);
    expect(() => parseCompanyDirectory(directoryPage([directoryUser(1, { roleIsActive: 'yes' })]))).toThrow(ContractError);
    expect(() => parseCompanyDirectory(directoryPage([directoryUser(1, { membershipCreatedAtUtc: 'soon' })]))).toThrow(ContractError);
    expect(() => parseCompanyDirectory(directoryPage([], { page: 0 }))).toThrow(ContractError);
  });

  it('does not carry contact or credential fields even if a server ever sent them', () => {
    const page = parseCompanyDirectory(directoryPage([directoryUser(1, { emailAddress: 'a@b.c', cellPhoneNumber: '0912', password: 'x' })]));
    const keys = Object.keys(page.items[0] as object);
    for (const forbidden of ['emailAddress', 'cellPhoneNumber', 'password']) expect(keys).not.toContain(forbidden);
  });
});
