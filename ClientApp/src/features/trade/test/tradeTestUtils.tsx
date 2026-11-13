import { render } from '@testing-library/react';
import { vi } from 'vitest';
import type { ReactElement, ReactNode } from 'react';
import { AppRouteDataContext, type AppRouteData } from '@/components/AppRoutes';
import { RouteView, RouterProvider, type Route } from '@/lib/router';
import { TradeAccessContext, TradeAccessProvider, createTradeAccess, type TradeAccess } from '../access/TradeAccess';
import { countriesBody, readWriteCapabilities } from './tradeFixtures';

export type StubRequest = { method: string; url: URL; body: unknown; headers: Record<string, string> };
export type StubResponse = { status: number; body?: unknown };
export type StubHandler = StubResponse | ((request: StubRequest) => StubResponse);

/**
 * Stubs `fetch` with handlers keyed `"METHOD /path"` (the query is NOT part of the key, so a test can inspect
 * it in the handler). An unknown route answers 404 with no body; the anti-forgery token route and the
 * country list have defaults. Returns the mock and the list of calls for assertions.
 */
export function stubTradeApi(handlers: Record<string, StubHandler>) {
  const calls: StubRequest[] = [];
  const all: Record<string, StubHandler> = {
    'GET /api/v1/antiforgery/token': { status: 200, body: { headerName: 'X-XSRF-TOKEN', requestToken: 'token' } },
    'GET /api/v1/trade/reference/countries': { status: 200, body: countriesBody },
    ...handlers,
  };
  const fetchMock = vi.fn((input: string, init?: RequestInit) => {
    const url = new URL(String(input), 'http://localhost');
    const method = init?.method ?? 'GET';
    const request: StubRequest = {
      method,
      url,
      body: typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : undefined,
      headers: (init?.headers ?? {}) as Record<string, string>,
    };
    calls.push(request);
    const handler = all[`${method} ${url.pathname}`];
    if (!handler) return Promise.resolve(new Response(null, { status: 404 }));
    const route = typeof handler === 'function' ? handler(request) : handler;
    return Promise.resolve(
      new Response(route.body === undefined ? null : JSON.stringify(route.body), {
        status: route.status,
        headers: route.body === undefined ? {} : { 'Content-Type': 'application/json' },
      }),
    );
  });
  vi.stubGlobal('fetch', fetchMock);
  return { fetchMock, calls, writes: () => calls.filter((call) => call.method !== 'GET') };
}

/** A ready access value with the given permission hints (short names are mapped to the server codes). */
export function readyAccess(
  permissions: readonly ('view' | 'create' | 'update')[] = ['view', 'create', 'update'],
  accessMode: 'ReadWrite' | 'ReadOnly' = 'ReadWrite',
): TradeAccess {
  return createTradeAccess({ status: 'ready', accessMode, permissions: permissions.map((action) => `trade.form.party.${action}`) });
}

export const onUnauthenticated = vi.fn((): boolean => false);

/** Renders one page inside the real in-house router with injected Trade access. */
export function renderTradePage(element: ReactElement, path: string, routePath: string, access: TradeAccess = readyAccess()) {
  const routes: Route[] = [{ path: routePath, title: 'صفحه', element: () => element }];
  window.history.pushState(null, '', `/app${path}`);
  return render(
    <TradeAccessContext.Provider value={access}>
      <RouterProvider routes={routes}>
        <RouteView />
      </RouterProvider>
    </TradeAccessContext.Provider>,
  );
}

const routeData = {
  session: { username: 'alice', fullName: 'آلیس', accountKind: 'User', isAdministrator: false },
  context: {
    company: { id: 'c0000000-0000-4000-8000-000000000001', name: 'شرکت نمونه', nationalId: null, roleName: 'حسابدار' },
    fiscalPeriod: {
      id: 'f0000000-0000-4000-8000-000000000001',
      title: '1405',
      startDate: '2026-03-21',
      endDate: '2027-03-20',
      status: 'Open',
      isSelectable: true,
      accessMode: 'readWrite',
    },
  },
  access: { status: 'ready', isInstallationAdministrator: false, companyId: 'c0000000-0000-4000-8000-000000000001', companyCodes: [] },
  refreshAccess: () => undefined,
  onUnauthenticated: (): boolean => false,
} as unknown as AppRouteData;

/**
 * Renders the REAL Trade routes (`tradeRoutes`) with the real capabilities provider fed by the stubbed
 * `/trade/capabilities`, exactly as the shell mounts them: a direct load of a URL.
 */
export function renderTradeApp(path: string, routes: readonly Route[], extra?: ReactNode) {
  window.history.pushState(null, '', `/app${path}`);
  return render(
    <AppRouteDataContext.Provider value={routeData}>
      <TradeAccessProvider scope="c1:p1">
        <RouterProvider routes={routes}>
          <RouteView />
          {extra}
        </RouterProvider>
      </TradeAccessProvider>
    </AppRouteDataContext.Provider>,
  );
}

export const capabilitiesRoute = (body: unknown = readWriteCapabilities, status = 200): Record<string, StubHandler> => ({
  'GET /api/v1/trade/capabilities': { status, body },
});
