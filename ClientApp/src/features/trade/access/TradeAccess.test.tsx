import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { RouterProvider } from '@/lib/router';
import { TradeRouteGuard } from '../components/TradeStates';
import { problem, readWriteCapabilities } from '../test/tradeFixtures';
import { capabilitiesRoute, stubTradeApi } from '../test/tradeTestUtils';
import { TradeAccessProvider, createTradeAccess, deniedTradeAccess, useTradeAccess } from './TradeAccess';

function Probe() {
  const access = useTradeAccess();
  return (
    <div>
      <span data-testid="status">{access.status}</span>
      <span data-testid="reason">{access.unavailableReason ?? '-'}</span>
      <span data-testid="view">{String(access.can('party.view'))}</span>
      <span data-testid="create">{String(access.can('party.create'))}</span>
      <span data-testid="update">{String(access.can('party.update'))}</span>
      <button type="button" onClick={access.retry}>retry</button>
    </div>
  );
}

const text = (id: string) => screen.getByTestId(id).textContent;

describe('createTradeAccess (pure hint logic)', () => {
  it('grants nothing by default, while loading and on error', () => {
    for (const access of [deniedTradeAccess, createTradeAccess({ status: 'loading' }), createTradeAccess({ status: 'error' })]) {
      expect(access.can('party.view')).toBe(false);
    }
  });

  it('a Party write hint needs its write code AND party.view (the server answers 403 otherwise)', () => {
    const both = createTradeAccess({ status: 'ready', accessMode: 'ReadWrite', permissions: ['trade.form.party.view', 'trade.form.party.update', 'trade.form.party.create'] });
    expect(both.can('party.update')).toBe(true);
    expect(both.can('party.create')).toBe(true);
    const updateOnly = createTradeAccess({ status: 'ready', accessMode: 'ReadWrite', permissions: ['trade.form.party.update'] });
    expect(updateOnly.can('party.update')).toBe(false);
    expect(updateOnly.can('party.view')).toBe(false);
    const createOnly = createTradeAccess({ status: 'ready', accessMode: 'ReadWrite', permissions: ['trade.form.party.create'] });
    expect(createOnly.can('party.create')).toBe(false);
  });

  it('never looks at the fiscal period mode for Party: a closed (ReadOnly) period still allows create and update', () => {
    const closed = createTradeAccess({
      status: 'ready',
      accessMode: 'ReadOnly',
      permissions: ['trade.form.party.view', 'trade.form.party.update', 'trade.form.party.create'],
    });
    expect(closed.can('party.view')).toBe(true);
    expect(closed.can('party.create')).toBe(true);
    expect(closed.can('party.update')).toBe(true);
  });

  it('grants a hint only with its exact code', () => {
    const rw = createTradeAccess({ status: 'ready', accessMode: 'ReadWrite', permissions: ['trade.form.party.view'] });
    expect(rw.can('party.create')).toBe(false);
    const lookalike = createTradeAccess({ status: 'ready', accessMode: 'ReadWrite', permissions: ['trade.form.partyx.view'] });
    expect(lookalike.can('party.view')).toBe(false);
  });
});

describe('TradeAccessProvider', () => {
  it('is fail-closed while loading, then grants what /trade/capabilities says', async () => {
    stubTradeApi(capabilitiesRoute({ available: true, accessMode: 'ReadWrite', permissions: ['trade.form.party.view'] }));
    render(<TradeAccessProvider scope="a"><Probe /></TradeAccessProvider>);
    expect(text('status')).toBe('loading');
    expect(text('view')).toBe('false');
    await waitFor(() => expect(text('status')).toBe('ready'));
    expect(text('view')).toBe('true');
    expect(text('create')).toBe('false');
  });

  it('treats available:false as an unavailable module and grants nothing even if codes are present', async () => {
    stubTradeApi(capabilitiesRoute({ available: false, accessMode: 'ReadOnly', permissions: ['trade.form.party.view'] }));
    render(<TradeAccessProvider scope="a"><Probe /></TradeAccessProvider>);
    await waitFor(() => expect(text('status')).toBe('unavailable'));
    expect(text('reason')).toBe('module');
    expect(text('view')).toBe('false');
  });

  it('maps 403 trade_module_unavailable and 409 trade_context_required to unavailable, with a reason', async () => {
    stubTradeApi({ 'GET /api/v1/trade/capabilities': problem(403, 'trade_module_unavailable') });
    const first = render(<TradeAccessProvider scope="a"><Probe /></TradeAccessProvider>);
    await waitFor(() => expect(text('status')).toBe('unavailable'));
    expect(text('reason')).toBe('module');
    first.unmount();
    stubTradeApi({ 'GET /api/v1/trade/capabilities': problem(409, 'trade_context_required') });
    render(<TradeAccessProvider scope="a"><Probe /></TradeAccessProvider>);
    await waitFor(() => expect(text('status')).toBe('unavailable'));
    expect(text('reason')).toBe('context');
  });

  it('fails closed on trade_forbidden, a server error or a malformed body, and a retry can recover', async () => {
    const user = userEvent.setup();
    let attempt = 0;
    stubTradeApi({
      'GET /api/v1/trade/capabilities': () => {
        attempt += 1;
        return attempt === 1 ? { status: 500 } : { status: 200, body: readWriteCapabilities };
      },
    });
    render(<TradeAccessProvider scope="a"><Probe /></TradeAccessProvider>);
    await waitFor(() => expect(text('status')).toBe('error'));
    expect(text('view')).toBe('false');
    await user.click(screen.getByRole('button', { name: 'retry' }));
    await waitFor(() => expect(text('status')).toBe('ready'));
    expect(text('update')).toBe('true');

  });

  it.each([
    ['403 trade_forbidden', problem(403, 'trade_forbidden')],
    ['a malformed body', { status: 200, body: { available: true } }],
  ])('fails closed on %s', async (_name, route) => {
    stubTradeApi({ 'GET /api/v1/trade/capabilities': route });
    render(<TradeAccessProvider scope="x"><Probe /></TradeAccessProvider>);
    await waitFor(() => expect(text('status')).toBe('error'));
    expect(text('view')).toBe('false');
  });

  it('hands a 401 to the session handler and grants nothing', async () => {
    stubTradeApi({ 'GET /api/v1/trade/capabilities': { status: 401 } });
    const handler = vi.fn(() => true);
    render(<TradeAccessProvider scope="a" onUnauthenticated={handler}><Probe /></TradeAccessProvider>);
    await waitFor(() => expect(handler).toHaveBeenCalledTimes(1));
    expect(text('view')).toBe('false');
  });

  it('drops the answer when the work context (scope) changes and reloads for the new one', async () => {
    let call = 0;
    stubTradeApi({
      'GET /api/v1/trade/capabilities': () => {
        call += 1;
        return { status: 200, body: call === 1 ? readWriteCapabilities : { available: true, accessMode: 'ReadOnly', permissions: ['trade.form.party.view'] } };
      },
    });
    function Host() {
      const [scope, setScope] = useState('company-1');
      return (
        <TradeAccessProvider scope={scope}>
          <Probe />
          <button type="button" onClick={() => setScope('company-2')}>switch</button>
        </TradeAccessProvider>
      );
    }
    const user = userEvent.setup();
    render(<Host />);
    await waitFor(() => expect(text('update')).toBe('true'));
    await user.click(screen.getByRole('button', { name: 'switch' }));
    // Never the old company's permission under the new scope, not even for one render.
    expect(text('update')).toBe('false');
    await waitFor(() => expect(text('status')).toBe('ready'));
    expect(text('update')).toBe('false');
    expect(text('view')).toBe('true');
  });
});

describe('TradeRouteGuard', () => {
  const renderGuard = (permission: 'party.view' | 'party.create' | 'party.update') =>
    render(
      <RouterProvider routes={[]}>
        <TradeRouteGuard permission={permission}>
          <p>محتوای محافظت‌شده</p>
        </TradeRouteGuard>
      </RouterProvider>,
    );

  it('shows a loading state and never the page while the hints load (default context)', () => {
    renderGuard('party.view');
    expect(screen.getByRole('status')).toHaveTextContent('در حال بررسی دسترسی');
    expect(screen.queryByText('محتوای محافظت‌شده')).not.toBeInTheDocument();
  });

  it('shows the friendly unavailable state, the forbidden panel, the read-only panel and the page, by hint', async () => {
    stubTradeApi({ 'GET /api/v1/trade/capabilities': problem(403, 'trade_module_unavailable') });
    const unavailable = render(
      <RouterProvider routes={[]}>
        <TradeAccessProvider scope="a">
          <TradeRouteGuard permission="party.view"><p>محتوای محافظت‌شده</p></TradeRouteGuard>
        </TradeAccessProvider>
      </RouterProvider>,
    );
    expect(await screen.findByRole('heading', { name: 'ماژول بازرگانی در دسترس نیست' })).toBeInTheDocument();
    expect(screen.queryByText('محتوای محافظت‌شده')).not.toBeInTheDocument();
    unavailable.unmount();

    stubTradeApi(capabilitiesRoute({ available: true, accessMode: 'ReadOnly', permissions: ['trade.form.party.view', 'trade.form.party.update'] }));
    // A closed period (ReadOnly) does not block Party writes; the missing create code does.
    const guarded = (permission: 'party.view' | 'party.create' | 'party.update') =>
      render(
        <RouterProvider routes={[]}>
          <TradeAccessProvider scope="b">
            <TradeRouteGuard permission={permission}><p>محتوای محافظت‌شده</p></TradeRouteGuard>
          </TradeAccessProvider>
        </RouterProvider>,
      );
    const create = guarded('party.create');
    expect(await screen.findByRole('heading', { name: 'دسترسی ندارید' })).toBeInTheDocument();
    create.unmount();
    const update = guarded('party.update');
    expect(await screen.findByText('محتوای محافظت‌شده')).toBeInTheDocument();
    update.unmount();
    guarded('party.view');
    expect(await screen.findByText('محتوای محافظت‌شده')).toBeInTheDocument();
  });

  it('shows a retryable error when the capabilities cannot be read', async () => {
    stubTradeApi({ 'GET /api/v1/trade/capabilities': { status: 500 } });
    render(
      <RouterProvider routes={[]}>
        <TradeAccessProvider scope="a">
          <TradeRouteGuard permission="party.view"><p>محتوای محافظت‌شده</p></TradeRouteGuard>
        </TradeAccessProvider>
      </RouterProvider>,
    );
    expect(await screen.findByRole('alert')).toHaveTextContent('دریافت دسترسی‌های ماژول بازرگانی انجام نشد');
    expect(screen.getByRole('button', { name: 'تلاش دوباره' })).toBeInTheDocument();
    expect(screen.queryByText('محتوای محافظت‌شده')).not.toBeInTheDocument();
  });
});
