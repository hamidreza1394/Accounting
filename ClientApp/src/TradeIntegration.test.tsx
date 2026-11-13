import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { App } from './App';
import './features/trade/pages/PartiesPage';
import './features/trade/pages/PartyCreatePage';
import './features/trade/pages/ProductsPage';
import './features/trade/pages/ProductCreatePage';
import { json } from './test/adminUsers';
import { company, contextBody, period, selected, session } from './test/api';
import { countriesBody, partyItem, partyList, productItem, productList, unitsBody } from './features/trade/test/tradeFixtures';

vi.setConfig({ testTimeout: 30_000 });

// Integration of the Trade party slice into the shared shell: routes, workspace tab, sidebar leaf and the
// capabilities provider mounted by `App` (the real wiring, not an injected context).
const p1 = period('p1', 'سال مالی ۱۴۰۵');
const companyA = company('a', 'شرکت الف', [p1], '10101010101');

type Capabilities = { status: number; body?: unknown };
const view = { available: true, accessMode: 'ReadWrite', permissions: ['trade.form.party.view', 'trade.form.party.create'] };
const productView = { available: true, accessMode: 'ReadWrite', permissions: ['trade.form.product.view', 'trade.form.product.create'] };

function stubAll(capabilities: Capabilities) {
  const fetchMock = vi.fn((input: string, init?: RequestInit) => {
    const method = init?.method ?? 'GET';
    const url = new URL(input, 'http://localhost');
    if (url.pathname === '/api/v1/me/permissions') {
      return Promise.resolve(json({ isInstallationAdministrator: false, company: { id: 'a', permissionCodes: [] } }));
    }
    const routes: Record<string, () => Response> = {
      'GET /api/v1/antiforgery/token': () => json({ headerName: 'X-XSRF-TOKEN', requestToken: 't' }),
      'GET /api/v1/session': () => json(session),
      'GET /api/v1/context': () => json(contextBody({ entry: 'directEntry', directSelection: { companyId: 'a', fiscalPeriodId: 'p1' }, companies: [companyA] })),
      'POST /api/v1/context': () => json(selected(companyA, p1)),
      'GET /api/v1/trade/capabilities': () =>
        new Response(capabilities.body === undefined ? null : JSON.stringify(capabilities.body), {
          status: capabilities.status,
          headers: { 'Content-Type': 'application/json' },
        }),
      'GET /api/v1/trade/parties': () => json(partyList([partyItem()])),
      'GET /api/v1/trade/reference/countries': () => json(countriesBody),
      'GET /api/v1/trade/products': () => json(productList([productItem()])),
      'GET /api/v1/trade/reference/units': () => json(unitsBody),
    };
    return Promise.resolve(routes[`${method} ${url.pathname}`]?.() ?? new Response(null, { status: 404 }));
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

async function openShell(path: string) {
  window.history.replaceState(null, '', path);
  render(<App navigate={vi.fn()} />);
  return screen.findByRole('navigation', { name: 'منوی اصلی' });
}

describe('Trade party slice inside the app shell', () => {
  beforeEach(() => window.history.replaceState(null, '', '/app/'));

  it('direct load of /app/trade/parties shows the list; the sidebar leaf appears and is active', async () => {
    stubAll({ status: 200, body: view });
    const nav = await openShell('/app/trade/parties');
    expect(await screen.findByText('شرکت بازرگانی نمونه')).toBeInTheDocument();
    const user = userEvent.setup();
    await user.click(within(nav).getByRole('button', { name: 'بازرگانی خارجی' }));
    expect(await within(nav).findByRole('link', { name: 'طرف‌حساب‌ها' })).toHaveAttribute('aria-current', 'page');
  });

  it('opens the create form from the list button inside the workspace', async () => {
    stubAll({ status: 200, body: view });
    await openShell('/app/trade/parties');
    const user = userEvent.setup();
    await user.click(await screen.findByRole('link', { name: 'طرف‌حساب جدید' }));
    expect(await screen.findByRole('heading', { level: 1, name: 'طرف‌حساب جدید' })).toBeInTheDocument();
    expect(window.location.pathname).toBe('/app/trade/parties/new');
  });

  it('shows the friendly state and no sidebar leaf when the module is unavailable (403 trade_module_unavailable)', async () => {
    stubAll({ status: 403, body: { title: 'دسترسی به ماژول بازرگانی ممکن نیست.', status: 403, detail: 'trade_module_unavailable' } });
    const nav = await openShell('/app/trade/parties');
    expect(await screen.findByRole('heading', { name: 'ماژول بازرگانی در دسترس نیست' })).toBeInTheDocument();
    const user = userEvent.setup();
    await user.click(within(nav).getByRole('button', { name: 'بازرگانی خارجی' }));
    expect(within(nav).queryByRole('link', { name: 'طرف‌حساب‌ها' })).toBeNull();
  });

  it('is fail-closed when /trade/capabilities fails: no page, no leaf', async () => {
    stubAll({ status: 500 });
    const nav = await openShell('/app/trade/parties');
    expect(await screen.findByText(/دریافت دسترسی‌های ماژول بازرگانی انجام نشد/)).toBeInTheDocument();
    expect(screen.queryByText('شرکت بازرگانی نمونه')).toBeNull();
    const user = userEvent.setup();
    await user.click(within(nav).getByRole('button', { name: 'بازرگانی خارجی' }));
    expect(within(nav).queryByRole('link', { name: 'طرف‌حساب‌ها' })).toBeNull();
  });
});

describe('Trade product slice inside the app shell', () => {
  beforeEach(() => window.history.replaceState(null, '', '/app/'));

  it('direct load of /app/trade/products shows the list; the «کالاها» leaf appears and is active', async () => {
    stubAll({ status: 200, body: productView });
    const nav = await openShell('/app/trade/products');
    expect(await screen.findByText('گندم دوروم')).toBeInTheDocument();
    const user = userEvent.setup();
    await user.click(within(nav).getByRole('button', { name: 'بازرگانی خارجی' }));
    expect(await within(nav).findByRole('link', { name: 'کالاها' })).toHaveAttribute('aria-current', 'page');
    expect(within(nav).queryByRole('link', { name: 'طرف‌حساب‌ها' })).toBeNull();
  });

  it('opens the create form from the list button inside the workspace', async () => {
    stubAll({ status: 200, body: productView });
    await openShell('/app/trade/products');
    const user = userEvent.setup();
    await user.click(await screen.findByRole('link', { name: 'کالای جدید' }));
    expect(await screen.findByRole('heading', { level: 1, name: 'کالای جدید' })).toBeInTheDocument();
    expect(window.location.pathname).toBe('/app/trade/products/new');
  });

  it('shows the forbidden panel and no leaf for a user with only Party codes', async () => {
    stubAll({ status: 200, body: view });
    const nav = await openShell('/app/trade/products');
    expect(await screen.findByRole('heading', { name: 'دسترسی ندارید' })).toBeInTheDocument();
    expect(screen.queryByText('گندم دوروم')).toBeNull();
    const user = userEvent.setup();
    await user.click(within(nav).getByRole('button', { name: 'بازرگانی خارجی' }));
    expect(within(nav).queryByRole('link', { name: 'کالاها' })).toBeNull();
    expect(within(nav).getByRole('link', { name: 'طرف‌حساب‌ها' })).toBeInTheDocument();
  });

  it('is fail-closed when /trade/capabilities fails: no page, no leaf', async () => {
    stubAll({ status: 500 });
    const nav = await openShell('/app/trade/products');
    expect(await screen.findByText(/دریافت دسترسی‌های ماژول بازرگانی انجام نشد/)).toBeInTheDocument();
    expect(screen.queryByText('گندم دوروم')).toBeNull();
    const user = userEvent.setup();
    await user.click(within(nav).getByRole('button', { name: 'بازرگانی خارجی' }));
    expect(within(nav).queryByRole('link', { name: 'کالاها' })).toBeNull();
  });
});
