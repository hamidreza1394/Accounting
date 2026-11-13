import { screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { appRoutes } from '@/components/AppRoutes';
import { partyId, problem, productCapabilities, productDetail, productId, productItem, productList, unitsBody } from './test/tradeFixtures';
import { capabilitiesRoute, renderTradeApp, stubTradeApi } from './test/tradeTestUtils';
import { tradeRoutes } from './tradeRoutes';

const api = {
  'GET /api/v1/trade/products': { status: 200, body: productList([productItem()]) },
  [`GET /api/v1/trade/products/${productId}`]: { status: 200, body: productDetail() },
  'GET /api/v1/trade/reference/units': { status: 200, body: unitsBody },
};
const caps = (permissions: string[], accessMode = 'ReadWrite') => capabilitiesRoute({ available: true, accessMode, permissions });
const all = productCapabilities.permissions;
/** Every request except the capabilities one: a denied route must make none (products, units, anything). */
const dataCalls = (calls: { url: URL }[]) => calls.filter((call) => call.url.pathname !== '/api/v1/trade/capabilities');
async function expectDenied(path: string, permissions: string[]) {
  const { calls } = stubTradeApi({ ...api, ...caps(permissions) });
  const view = renderTradeApp(path, tradeRoutes);
  expect(await screen.findByRole('heading', { name: 'دسترسی ندارید' })).toBeInTheDocument();
  expect(calls.some((call) => call.url.pathname === '/api/v1/trade/capabilities')).toBe(true);
  expect(dataCalls(calls).map((call) => call.url.pathname)).toEqual([]);
  view.unmount();
}

describe('Trade product routes (direct load through the real guard and capabilities)', () => {
  it('registers the four routes in the shell route table, /new before /:id, with a Persian title each', () => {
    const paths = appRoutes.map((route) => route.path);
    for (const path of ['/trade/products', '/trade/products/new', '/trade/products/:id', '/trade/products/:id/edit']) expect(paths).toContain(path);
    expect(paths.indexOf('/trade/products/new')).toBeLessThan(paths.indexOf('/trade/products/:id'));
    for (const route of tradeRoutes.filter((candidate) => candidate.path.startsWith('/trade/products'))) expect(/[؀-ۿ]/.test(route.title)).toBe(true);
  });

  it('direct load of /trade/products shows the list for a viewer, without the create button', async () => {
    stubTradeApi({ ...api, ...caps(['trade.form.product.view']) });
    renderTradeApp('/trade/products', tradeRoutes);
    expect(await screen.findByRole('heading', { level: 1, name: 'کالاها' })).toBeInTheDocument();
    expect(await screen.findByText('گندم دوروم')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'کالای جدید' })).not.toBeInTheDocument();
  });

  it('direct load of /trade/products/new is the create form (not read as an id) for a creator', async () => {
    stubTradeApi({ ...api, ...caps(all) });
    renderTradeApp('/trade/products/new', tradeRoutes);
    expect(await screen.findByRole('heading', { level: 1, name: 'کالای جدید' })).toBeInTheDocument();
  });

  it('direct load of /trade/products/:id is the details, and /:id/edit the edit form', async () => {
    stubTradeApi({ ...api, ...caps(all) });
    const details = renderTradeApp(`/trade/products/${productId}`, tradeRoutes);
    expect(await screen.findByRole('heading', { level: 1, name: 'گندم دوروم' })).toBeInTheDocument();
    details.unmount();
    renderTradeApp(`/trade/products/${productId}/edit`, tradeRoutes);
    expect(await screen.findByRole('heading', { level: 1, name: 'ویرایش کالا' })).toBeInTheDocument();
    await waitFor(() => expect(screen.getByDisplayValue('PRD-001')).toBeInTheDocument());
  });

  it('denies each route without its permission and never requests any data', async () => {
    await expectDenied('/trade/products/new', ['trade.form.product.view']);
    await expectDenied(`/trade/products/${productId}/edit`, ['trade.form.product.view']);
    await expectDenied('/trade/products', ['trade.form.product.create']);
    await expectDenied(`/trade/products/${productId}`, ['trade.form.product.create', 'trade.form.product.update']);
  });

  it('denies edit for update-without-view and create for create-without-view', async () => {
    await expectDenied(`/trade/products/${productId}/edit`, ['trade.form.product.update']);
    await expectDenied('/trade/products/new', ['trade.form.product.create']);
  });

  it('does not let Party permissions open Product pages (and vice versa)', async () => {
    for (const path of ['/trade/products', '/trade/products/new', `/trade/products/${productId}`, `/trade/products/${productId}/edit`]) {
      await expectDenied(path, ['trade.form.party.view', 'trade.form.party.create', 'trade.form.party.update']);
    }
    stubTradeApi({ ...api, ...caps(['trade.form.product.view', 'trade.form.product.create', 'trade.form.product.update']) });
    renderTradeApp(`/trade/parties/${partyId}`, tradeRoutes);
    expect(await screen.findByRole('heading', { name: 'دسترسی ندارید' })).toBeInTheDocument();
  });

  it('allows create and edit in a closed (ReadOnly) period: no banner, enabled submit', async () => {
    for (const [path, heading, submit] of [
      ['/trade/products/new', 'کالای جدید', 'ثبت کالا'],
      [`/trade/products/${productId}/edit`, 'ویرایش کالا', 'ذخیرهٔ تغییرات'],
    ] as const) {
      stubTradeApi({ ...api, ...caps(all, 'ReadOnly') });
      const view = renderTradeApp(path, tradeRoutes);
      expect(await screen.findByRole('heading', { level: 1, name: heading })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: submit })).toBeEnabled();
      expect(screen.queryByText(/فقط‌خواندنی/)).not.toBeInTheDocument();
      view.unmount();
    }
  });

  it('shows the friendly unavailable state when the module is not enabled, on every route', async () => {
    for (const path of ['/trade/products', '/trade/products/new', `/trade/products/${productId}`, `/trade/products/${productId}/edit`]) {
      stubTradeApi({ ...api, 'GET /api/v1/trade/capabilities': problem(403, 'trade_module_unavailable') });
      const view = renderTradeApp(path, tradeRoutes);
      expect(await screen.findByRole('heading', { name: 'ماژول بازرگانی در دسترس نیست' })).toBeInTheDocument();
      view.unmount();
    }
  });

  it('never shows a page while the capabilities fail (fail-closed)', async () => {
    stubTradeApi({ ...api, 'GET /api/v1/trade/capabilities': { status: 500 } });
    renderTradeApp('/trade/products', tradeRoutes);
    expect(screen.queryByRole('heading', { level: 1, name: 'کالاها' })).not.toBeInTheDocument();
    expect(await screen.findByText(/دریافت دسترسی‌های ماژول بازرگانی انجام نشد/)).toBeInTheDocument();
    expect(screen.queryByText('گندم دوروم')).not.toBeInTheDocument();
  });
});
