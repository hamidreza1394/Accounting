import { vi } from 'vitest';

export type Route = { status: number; body?: unknown };
export type RouteHandler = Route | ((request: { method: string; url: string; body: unknown }) => Route);

/**
 * Stubs `fetch` with routes keyed `"METHOD /path"`. An unknown route answers 404, so a test
 * fails loudly when the client calls an endpoint it should not.
 */
export function stubApi(routes: Record<string, RouteHandler>) {
  const fetchMock = vi.fn((input: string, init?: RequestInit) => {
    const method = init?.method ?? 'GET';
    const handler = routes[`${method} ${input}`];
    if (!handler) {
      return Promise.resolve(new Response(null, { status: 404 }));
    }
    const body: unknown = typeof init?.body === 'string' ? JSON.parse(init.body) : undefined;
    const route = typeof handler === 'function' ? handler({ method, url: input, body }) : handler;
    return Promise.resolve(
      new Response(route.body === undefined ? null : JSON.stringify(route.body), {
        status: route.status,
        headers: { 'Content-Type': 'application/json' },
      }),
    );
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

export const antiforgeryRoute: Record<string, RouteHandler> = {
  'GET /api/v1/antiforgery/token': { status: 200, body: { headerName: 'X-XSRF-TOKEN', requestToken: 'token' } },
};

export const session = { username: 'alice', fullName: 'آلیس احمدی', accountKind: 'User', isAdministrator: false };

export function period(id: string, title: string, overrides: Record<string, unknown> = {}) {
  return {
    id,
    title,
    startDate: '2026-03-21',
    endDate: '2027-03-20',
    status: 'Open',
    isSelectable: true,
    accessMode: 'readWrite',
    ...overrides,
  };
}

export function company(id: string, name: string, fiscalPeriods: unknown[], nationalId: string | null = null) {
  return { id, name, nationalId, roleName: 'حسابدار', relationshipTypes: ['unknown'], fiscalPeriods };
}

export function contextBody(overrides: Record<string, unknown> = {}) {
  return { current: null, entry: 'selectionRequired', directSelection: null, defaultCompanyId: null, companies: [], ...overrides };
}

export function selected(c: { id: string; name: string; nationalId: string | null; roleName: string }, p: unknown) {
  return { company: { id: c.id, name: c.name, nationalId: c.nationalId, roleName: c.roleName }, fiscalPeriod: p };
}
