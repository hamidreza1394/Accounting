import { afterEach, describe, expect, it, vi } from 'vitest';
import { createCompanyRole, getCompanyRoles, getPermissionCatalog, getPermissionTemplates, updateCompanyRole } from './client';
import { ContractError, parseCompanyRoles, parsePermissionCatalog, parsePermissionTemplates } from './types';
import { roleItem } from '../test/roles';

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}
const token = () => jsonResponse(200, { headerName: 'X-XSRF-TOKEN', requestToken: 'tok' });

afterEach(() => vi.unstubAllGlobals());

describe('role and permission catalog API client', () => {
  it('reads roles with an encoded company id', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(200, [roleItem()]));
    vi.stubGlobal('fetch', fetchMock);
    const roles = await getCompanyRoles('a/../b');
    expect(fetchMock.mock.calls[0]?.[0]).toBe('/api/v1/admin/companies/a%2F..%2Fb/roles');
    expect(roles[0]).toMatchObject({ code: 'SALES', isSystemRole: false, permissionCodes: ['trade.orders.view', 'trade.orders.create'] });
  });

  it('creates a role with an anti-forgery token and the exact body', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(token()).mockResolvedValueOnce(jsonResponse(201, { id: 'new-id' }));
    vi.stubGlobal('fetch', fetchMock);
    const created = await createCompanyRole('c 1', { code: 'SALES', name: 'فروش', permissionCodes: ['a.b.view'] });
    expect(created).toEqual({ id: 'new-id' });
    const [url, init] = fetchMock.mock.calls[1] as [string, RequestInit];
    expect(url).toBe('/api/v1/admin/companies/c%201/roles');
    expect(init.method).toBe('POST');
    expect((init.headers as Record<string, string>)['X-XSRF-TOKEN']).toBe('tok');
    expect(JSON.parse(String(init.body))).toEqual({ code: 'SALES', name: 'فروش', permissionCodes: ['a.b.view'] });
  });

  it('updates a role with PUT and encodes both ids', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(token()).mockResolvedValueOnce(new Response(null, { status: 204 }));
    vi.stubGlobal('fetch', fetchMock);
    await updateCompanyRole('c1', 'r/1', { name: 'x', isActive: false, permissionCodes: [] });
    const [url, init] = fetchMock.mock.calls[1] as [string, RequestInit];
    expect(url).toBe('/api/v1/admin/companies/c1/roles/r%2F1');
    expect(init.method).toBe('PUT');
    expect(JSON.parse(String(init.body))).toEqual({ name: 'x', isActive: false, permissionCodes: [] });
  });

  it('surfaces a Persian 409 message from the server', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValueOnce(token()).mockResolvedValueOnce(jsonResponse(409, { error: 'گروه سیستمی قابل ویرایش نیست.' })));
    await expect(updateCompanyRole('c1', 'r1', { name: 'x', isActive: true, permissionCodes: [] })).rejects.toMatchObject({
      status: 409,
      message: 'گروه سیستمی قابل ویرایش نیست.',
    });
  });

  it('reads the catalog and the templates', async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(jsonResponse(200, [{ id: 'i', code: 'trade.orders.view', name: 'n', description: null, subsystemCode: 'trade' }]))
      .mockResolvedValueOnce(jsonResponse(200, [{ code: 't', name: 'n', permissionCodes: ['trade.orders.view'] }]));
    vi.stubGlobal('fetch', fetchMock);
    expect((await getPermissionCatalog())[0]).toMatchObject({ code: 'trade.orders.view', description: null });
    expect((await getPermissionTemplates())[0]).toMatchObject({ code: 't' });
    expect(fetchMock.mock.calls.map((call) => call[0])).toEqual(['/api/v1/admin/permission-catalog', '/api/v1/admin/permission-templates']);
  });

  it('rejects responses that break the contract', () => {
    expect(() => parseCompanyRoles({})).toThrow(ContractError);
    expect(() => parseCompanyRoles([{ ...roleItem(), permissionCount: -1 }])).toThrow(ContractError);
    expect(() => parseCompanyRoles([{ ...roleItem(), permissionCodes: [1] }])).toThrow(ContractError);
    expect(() => parseCompanyRoles([{ ...roleItem(), isActive: 'yes' }])).toThrow(ContractError);
    expect(() => parsePermissionCatalog([{ id: 'a', code: 'x', name: 'n', description: 5, subsystemCode: 'x' }])).toThrow(ContractError);
    expect(() => parsePermissionTemplates([{ code: 'a', name: 'b' }])).toThrow(ContractError);
  });
});
