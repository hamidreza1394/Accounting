import { screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { appRoutes } from '@/components/AppRoutes';
import { countriesBody, partyDetail, partyId, partyItem, partyList, problem } from './test/tradeFixtures';
import { capabilitiesRoute, renderTradeApp, stubTradeApi } from './test/tradeTestUtils';
import { tradeRoutes } from './tradeRoutes';

const api = {
  'GET /api/v1/trade/parties': { status: 200, body: partyList([partyItem()]) },
  [`GET /api/v1/trade/parties/${partyId}`]: { status: 200, body: partyDetail() },
  'GET /api/v1/trade/reference/countries': { status: 200, body: countriesBody },
};
const caps = (permissions: string[], accessMode = 'ReadWrite') => capabilitiesRoute({ available: true, accessMode, permissions });
const all = ['trade.form.party.view', 'trade.form.party.create', 'trade.form.party.update'];

describe('Trade routes (direct load through the real guard and capabilities)', () => {
  it('registers the four routes in the shell route table, /new before /:id, and a Persian title each', () => {
    const paths = appRoutes.map((route) => route.path);
    for (const path of ['/trade/parties', '/trade/parties/new', '/trade/parties/:id', '/trade/parties/:id/edit']) expect(paths).toContain(path);
    expect(paths.indexOf('/trade/parties/new')).toBeLessThan(paths.indexOf('/trade/parties/:id'));
    for (const route of tradeRoutes) expect(/[؀-ۿ]/.test(route.title)).toBe(true);
  });

  it('direct load of /trade/parties shows the list for a viewer', async () => {
    stubTradeApi({ ...api, ...caps(['trade.form.party.view']) });
    renderTradeApp('/trade/parties', tradeRoutes);
    expect(await screen.findByRole('heading', { level: 1, name: 'طرف‌حساب‌ها' })).toBeInTheDocument();
    expect(await screen.findByText('شرکت بازرگانی نمونه')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'طرف‌حساب جدید' })).not.toBeInTheDocument();
  });

  it('direct load of /trade/parties/new is the create form (not read as an id) for a creator', async () => {
    stubTradeApi({ ...api, ...caps(all) });
    renderTradeApp('/trade/parties/new', tradeRoutes);
    expect(await screen.findByRole('heading', { level: 1, name: 'طرف‌حساب جدید' })).toBeInTheDocument();
  });

  it('direct load of /trade/parties/:id is the details for a viewer', async () => {
    stubTradeApi({ ...api, ...caps(['trade.form.party.view']) });
    renderTradeApp(`/trade/parties/${partyId}`, tradeRoutes);
    expect(await screen.findByRole('heading', { level: 1, name: 'نمونه' })).toBeInTheDocument();
  });

  it('direct load of /trade/parties/:id/edit is the edit form for an updater', async () => {
    stubTradeApi({ ...api, ...caps(all) });
    renderTradeApp(`/trade/parties/${partyId}/edit`, tradeRoutes);
    expect(await screen.findByRole('heading', { level: 1, name: 'ویرایش طرف‌حساب' })).toBeInTheDocument();
    await waitFor(() => expect(screen.getByDisplayValue('P-001')).toBeInTheDocument());
  });

  it('denies each route without its permission and never requests the page data', async () => {
    const { calls } = stubTradeApi({ ...api, ...caps(['trade.form.party.view']) });
    for (const path of ['/trade/parties/new', `/trade/parties/${partyId}/edit`]) {
      const view = renderTradeApp(path, tradeRoutes);
      expect(await screen.findByRole('heading', { name: 'دسترسی ندارید' })).toBeInTheDocument();
      view.unmount();
    }
    stubTradeApi({ ...api, ...caps(['trade.form.party.create']) });
    renderTradeApp('/trade/parties', tradeRoutes);
    expect(await screen.findByRole('heading', { name: 'دسترسی ندارید' })).toBeInTheDocument();
    expect(calls.filter((call) => call.url.pathname.startsWith('/api/v1/trade/parties'))).toHaveLength(0);
  });

  it('allows create and edit in a closed (ReadOnly) period: no banner, enabled submit', async () => {
    for (const [path, heading, submit] of [
      ['/trade/parties/new', 'طرف‌حساب جدید', 'ثبت طرف‌حساب'],
      [`/trade/parties/${partyId}/edit`, 'ویرایش طرف‌حساب', 'ذخیرهٔ تغییرات'],
    ] as const) {
      stubTradeApi({ ...api, ...caps(all, 'ReadOnly') });
      const view = renderTradeApp(path, tradeRoutes);
      expect(await screen.findByRole('heading', { level: 1, name: heading })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: submit })).toBeEnabled();
      expect(screen.queryByText(/فقط‌خواندنی/)).not.toBeInTheDocument();
      view.unmount();
    }
  });

  it('denies edit for update-without-view and create for create-without-view, and hides the buttons', async () => {
    const { calls } = stubTradeApi({ ...api, ...caps(['trade.form.party.update']) });
    const edit = renderTradeApp(`/trade/parties/${partyId}/edit`, tradeRoutes);
    expect(await screen.findByRole('heading', { name: 'دسترسی ندارید' })).toBeInTheDocument();
    edit.unmount();
    stubTradeApi({ ...api, ...caps(['trade.form.party.create']) });
    const create = renderTradeApp('/trade/parties/new', tradeRoutes);
    expect(await screen.findByRole('heading', { name: 'دسترسی ندارید' })).toBeInTheDocument();
    create.unmount();
    expect(calls.filter((call) => call.url.pathname.startsWith('/api/v1/trade/parties'))).toHaveLength(0);
  });

  it('shows the friendly unavailable state when the module is not enabled, on every route', async () => {
    for (const path of ['/trade/parties', '/trade/parties/new', `/trade/parties/${partyId}`, `/trade/parties/${partyId}/edit`]) {
      stubTradeApi({ ...api, 'GET /api/v1/trade/capabilities': problem(403, 'trade_module_unavailable') });
      const view = renderTradeApp(path, tradeRoutes);
      expect(await screen.findByRole('heading', { name: 'ماژول بازرگانی در دسترس نیست' })).toBeInTheDocument();
      view.unmount();
    }
  });

  it('never shows a page while the capabilities load or after they failed (fail-closed)', async () => {
    stubTradeApi({ ...api, 'GET /api/v1/trade/capabilities': { status: 500 } });
    renderTradeApp('/trade/parties', tradeRoutes);
    expect(screen.queryByRole('heading', { level: 1, name: 'طرف‌حساب‌ها' })).not.toBeInTheDocument();
    expect(await screen.findByText(/دریافت دسترسی‌های ماژول بازرگانی انجام نشد/)).toBeInTheDocument();
    expect(screen.queryByText('شرکت بازرگانی نمونه')).not.toBeInTheDocument();
  });
});
