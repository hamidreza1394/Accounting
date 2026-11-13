import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it } from 'vitest';
import { otherProductId, problem, productId, productItem, productList } from '../test/tradeFixtures';
import { onUnauthenticated, readyAccess, renderTradePage, stubTradeApi, type StubRequest } from '../test/tradeTestUtils';
import { ProductsPage } from './ProductsPage';

const listPath = 'GET /api/v1/trade/products';
const access = (permissions: ('view' | 'create' | 'update')[] = ['view', 'create', 'update'], mode: 'ReadWrite' | 'ReadOnly' = 'ReadWrite') =>
  readyAccess(permissions, mode, 'product');
const render = (value = access()) =>
  renderTradePage(<ProductsPage onUnauthenticated={onUnauthenticated} searchDelayMs={20} />, '/trade/products', '/trade/products', value);
const lastList = (calls: StubRequest[]) => calls.filter((call) => call.url.pathname === '/api/v1/trade/products').at(-1) as StubRequest;

describe('ProductsPage', () => {
  beforeEach(() => {
    onUnauthenticated.mockReset();
    onUnauthenticated.mockReturnValue(false);
  });

  it('shows a loading state, then the products with code, group, HS code, the unit name, a status badge and a link to the details', async () => {
    stubTradeApi({
      [listPath]: {
        status: 200,
        body: productList([productItem(), productItem({ id: otherProductId, code: 'PRD-002', name: 'جو', productGroupCode: null, hsCode: null, baseUnitCode: 'TNE', isActive: false })]),
      },
    });
    render();
    expect(screen.getByRole('table', { name: 'فهرست کالاها' })).toHaveAttribute('aria-busy', 'true');
    const table = await screen.findByRole('table', { name: 'فهرست کالاها' });
    await waitFor(() => expect(table).not.toHaveAttribute('aria-busy'));
    expect(table).toHaveTextContent('PRD-001');
    expect(table).toHaveTextContent('گندم دوروم');
    expect(table).toHaveTextContent('GRAIN');
    expect(table).toHaveTextContent('1001.19');
    await waitFor(() => expect(table).toHaveTextContent('کیلوگرم'));
    await waitFor(() => expect(table).toHaveTextContent('تن'));
    expect(table).toHaveTextContent('غیرفعال');
    expect(screen.getByRole('link', { name: /PRD-001/ })).toHaveAttribute('href', `/app/trade/products/${productId}`);
    // There is no destructive or state-changing action in the list.
    expect(screen.queryByRole('button', { name: /حذف|غیرفعال‌سازی|فعال‌سازی/ })).not.toBeInTheDocument();
  });

  it('keeps the code cell on one line (whitespace-nowrap) in left-to-right isolation', async () => {
    stubTradeApi({ [listPath]: { status: 200, body: productList([productItem()]) } });
    render();
    const cell = (await screen.findByText('PRD-001')).closest('bdi');
    expect(cell).toHaveClass('whitespace-nowrap');
    expect(cell).toHaveAttribute('dir', 'ltr');
  });

  it('shows an empty state with a create call-to-action only for a user who may create', async () => {
    stubTradeApi({ [listPath]: { status: 200, body: productList([]) } });
    const first = render();
    expect(await screen.findByText('کالایی پیدا نشد')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'نخستین کالا را ثبت کنید' })).toHaveAttribute('href', '/app/trade/products/new');
    first.unmount();
    render(access(['view']));
    expect(await screen.findByText('هنوز کالایی ثبت نشده است.')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'نخستین کالا را ثبت کنید' })).not.toBeInTheDocument();
  });

  it('shows a retryable error and recovers on retry', async () => {
    const user = userEvent.setup();
    let attempt = 0;
    stubTradeApi({ [listPath]: () => (++attempt === 1 ? { status: 500 } : { status: 200, body: productList([productItem()]) }) });
    render();
    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('خطای غیرمنتظره');
    await user.click(within(alert).getByRole('button', { name: 'تلاش دوباره' }));
    expect(await screen.findByText('گندم دوروم')).toBeInTheDocument();
  });

  it('sends the debounced search, resets the page, and shows a filter-specific empty message', async () => {
    const user = userEvent.setup();
    const { calls } = stubTradeApi({
      [listPath]: (request) => ({ status: 200, body: request.url.searchParams.get('search') ? productList([]) : productList([productItem()]) }),
    });
    render();
    await screen.findByText('گندم دوروم');
    await user.type(screen.getByLabelText('جست‌وجو'), 'PRD');
    await waitFor(() => expect(lastList(calls).url.searchParams.get('search')).toBe('PRD'));
    expect(calls.filter((call) => call.url.searchParams.has('search'))).toHaveLength(1);
    expect(lastList(calls).url.searchParams.get('page')).toBe('1');
    expect(await screen.findByText('با جست‌وجو یا فیلتر فعلی کالایی پیدا نشد.')).toBeInTheDocument();
  });

  it('does not start a search for whitespace only', async () => {
    const user = userEvent.setup();
    const { calls } = stubTradeApi({ [listPath]: { status: 200, body: productList([productItem()]) } });
    render();
    await screen.findByText('گندم دوروم');
    const before = calls.filter((call) => call.url.pathname === '/api/v1/trade/products').length;
    await user.type(screen.getByLabelText('جست‌وجو'), '   ');
    await new Promise((resolve) => setTimeout(resolve, 80));
    expect(calls.filter((call) => call.url.pathname === '/api/v1/trade/products')).toHaveLength(before);
  });

  it('filters by status through isActive, and has no role filter', async () => {
    const user = userEvent.setup();
    const { calls } = stubTradeApi({ [listPath]: { status: 200, body: productList([productItem()]) } });
    render();
    await screen.findByText('گندم دوروم');
    expect(screen.queryByLabelText('نقش')).not.toBeInTheDocument();
    await user.selectOptions(screen.getByLabelText('وضعیت'), 'inactive');
    await waitFor(() => expect(lastList(calls).url.searchParams.get('isActive')).toBe('false'));
    await user.selectOptions(screen.getByLabelText('وضعیت'), 'active');
    await waitFor(() => expect(lastList(calls).url.searchParams.get('isActive')).toBe('true'));
    await user.selectOptions(screen.getByLabelText('وضعیت'), 'all');
    await waitFor(() => expect(lastList(calls).url.searchParams.has('isActive')).toBe(false));
  });

  it('pages on the server: next page, and a new page size returns to page 1', async () => {
    const user = userEvent.setup();
    const { calls } = stubTradeApi({
      [listPath]: (request) => ({
        status: 200,
        body: productList([productItem()], { page: Number(request.url.searchParams.get('page')), pageSize: Number(request.url.searchParams.get('pageSize')), totalCount: 45 }),
      }),
    });
    render();
    await screen.findByText('گندم دوروم');
    expect(screen.getByText(/صفحه ۱ از ۳/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /بعدی/ }));
    await waitFor(() => expect(lastList(calls).url.searchParams.get('page')).toBe('2'));
    await user.selectOptions(screen.getByLabelText('تعداد در صفحه'), '50');
    await waitFor(() => {
      expect(lastList(calls).url.searchParams.get('pageSize')).toBe('50');
      expect(lastList(calls).url.searchParams.get('page')).toBe('1');
    });
  });

  it('shows the create button only with the create AND view hints (a closed period too)', async () => {
    stubTradeApi({ [listPath]: { status: 200, body: productList([productItem()]) } });
    const writer = render();
    expect(screen.getByRole('link', { name: 'کالای جدید' })).toHaveAttribute('href', '/app/trade/products/new');
    writer.unmount();
    const viewer = render(access(['view']));
    await screen.findByText('گندم دوروم');
    expect(screen.queryByRole('link', { name: 'کالای جدید' })).not.toBeInTheDocument();
    viewer.unmount();
    const createOnly = render(access(['create']));
    await screen.findByText('گندم دوروم');
    expect(screen.queryByRole('link', { name: 'کالای جدید' })).not.toBeInTheDocument();
    createOnly.unmount();
    render(access(['view', 'create'], 'ReadOnly'));
    expect(screen.getByRole('link', { name: 'کالای جدید' })).toBeInTheDocument();
    await screen.findByText('گندم دوروم');
    expect(screen.queryByText(/فقط‌خواندنی/)).not.toBeInTheDocument();
  });

  it('shows the module-unavailable state, the forbidden panel and the context message', async () => {
    stubTradeApi({ [listPath]: problem(403, 'trade_module_unavailable') });
    const unavailable = render();
    expect(await screen.findByRole('heading', { name: 'ماژول بازرگانی در دسترس نیست' })).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
    unavailable.unmount();
    stubTradeApi({ [listPath]: problem(403, 'trade_forbidden') });
    const forbidden = render();
    expect(await screen.findByRole('heading', { name: 'دسترسی ندارید' })).toBeInTheDocument();
    forbidden.unmount();
    stubTradeApi({ [listPath]: problem(409, 'trade_context_required') });
    render();
    expect(await screen.findByText(/شرکت یا دورهٔ مالی کاری معتبر نیست/)).toBeInTheDocument();
  });

  it('hands a 401 to the session handler instead of showing an error', async () => {
    stubTradeApi({ [listPath]: { status: 401 } });
    onUnauthenticated.mockReturnValue(true);
    render();
    await waitFor(() => expect(onUnauthenticated).toHaveBeenCalled());
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('still lists the products with the bare unit code when the unit reference fails', async () => {
    stubTradeApi({ [listPath]: { status: 200, body: productList([productItem()]) }, 'GET /api/v1/trade/reference/units': { status: 500 } });
    render();
    const table = await screen.findByRole('table', { name: 'فهرست کالاها' });
    await waitFor(() => expect(table).toHaveTextContent('KGM'));
    expect(table).not.toHaveTextContent('کیلوگرم');
  });
});
