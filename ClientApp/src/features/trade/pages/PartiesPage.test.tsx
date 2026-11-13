import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it } from 'vitest';
import { otherPartyId, partyId, partyItem, partyList, problem } from '../test/tradeFixtures';
import { onUnauthenticated, readyAccess, renderTradePage, stubTradeApi, type StubRequest } from '../test/tradeTestUtils';
import { PartiesPage } from './PartiesPage';

const listPath = 'GET /api/v1/trade/parties';
const render = (access = readyAccess()) => renderTradePage(<PartiesPage onUnauthenticated={onUnauthenticated} searchDelayMs={20} />, '/trade/parties', '/trade/parties', access);
const lastList = (calls: StubRequest[]) => calls.filter((call) => call.url.pathname === '/api/v1/trade/parties').at(-1) as StubRequest;

describe('PartiesPage', () => {
  beforeEach(() => {
    onUnauthenticated.mockReset();
    onUnauthenticated.mockReturnValue(false);
  });

  it('shows a loading state, then the parties with Persian roles, a status badge, the country name and a link to the details', async () => {
    stubTradeApi({
      [listPath]: { status: 200, body: partyList([partyItem(), partyItem({ id: otherPartyId, code: 'P-002', legalName: 'غیرفعال', displayName: null, countryCode: null, isActive: false, roles: ['Bank'] })]) },
    });
    render();
    expect(screen.getByRole('table', { name: 'فهرست طرف‌حساب‌ها' })).toHaveAttribute('aria-busy', 'true');
    const table = await screen.findByRole('table', { name: 'فهرست طرف‌حساب‌ها' });
    await waitFor(() => expect(table).not.toHaveAttribute('aria-busy'));
    expect(table).toHaveTextContent('نمونه');
    expect(table).toHaveTextContent('شرکت بازرگانی نمونه');
    expect(table).toHaveTextContent('P-001');
    expect(table).toHaveTextContent('واردکننده');
    expect(table).toHaveTextContent('فروشنده');
    expect(table).toHaveTextContent('بانک');
    await waitFor(() => expect(table).toHaveTextContent('ایران'));
    expect(table).toHaveTextContent('فعال');
    expect(table).toHaveTextContent('غیرفعال');
    expect(screen.getByRole('link', { name: /نمونه/ })).toHaveAttribute('href', `/app/trade/parties/${partyId}`);
    // There is no destructive or state-changing action in the list.
    expect(screen.queryByRole('button', { name: /حذف|غیرفعال‌سازی|فعال‌سازی/ })).not.toBeInTheDocument();
  });

  it('never asks for or shows the legal identifier in the list', async () => {
    const { calls } = stubTradeApi({ [listPath]: { status: 200, body: partyList([partyItem()]) } });
    render();
    await screen.findByRole('table');
    expect(document.body.textContent).not.toContain('14001234567');
    expect(calls.map((call) => call.url.href).join(' ')).not.toMatch(/identifier/i);
  });

  it('shows an empty state with a create call-to-action only for a user who may create', async () => {
    stubTradeApi({ [listPath]: { status: 200, body: partyList([]) } });
    const first = render();
    expect(await screen.findByText('طرف‌حسابی پیدا نشد')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'نخستین طرف‌حساب را ثبت کنید' })).toHaveAttribute('href', '/app/trade/parties/new');
    first.unmount();
    render(readyAccess(['view']));
    expect(await screen.findByText('هنوز طرف‌حسابی ثبت نشده است.')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'نخستین طرف‌حساب را ثبت کنید' })).not.toBeInTheDocument();
  });

  it('shows a retryable error and recovers on retry', async () => {
    const user = userEvent.setup();
    let attempt = 0;
    stubTradeApi({ [listPath]: () => (++attempt === 1 ? { status: 500 } : { status: 200, body: partyList([partyItem()]) }) });
    render();
    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('خطای غیرمنتظره');
    await user.click(within(alert).getByRole('button', { name: 'تلاش دوباره' }));
    expect(await screen.findByText('شرکت بازرگانی نمونه')).toBeInTheDocument();
  });

  it('sends the debounced search, resets the page, and shows a filter-specific empty message', async () => {
    const user = userEvent.setup();
    const { calls } = stubTradeApi({
      [listPath]: (request) => ({ status: 200, body: request.url.searchParams.get('search') ? partyList([]) : partyList([partyItem()]) }),
    });
    render();
    await screen.findByText('شرکت بازرگانی نمونه');
    await user.type(screen.getByLabelText('جست‌وجو'), 'ABC');
    // Debounced: typing alone does not fire one request per key.
    await waitFor(() => expect(lastList(calls).url.searchParams.get('search')).toBe('ABC'));
    expect(calls.filter((call) => call.url.searchParams.has('search'))).toHaveLength(1);
    expect(lastList(calls).url.searchParams.get('page')).toBe('1');
    expect(await screen.findByText('با جست‌وجو یا فیلتر فعلی طرف‌حسابی پیدا نشد.')).toBeInTheDocument();
  });

  it('does not start a search (or reset the page) for whitespace only', async () => {
    const user = userEvent.setup();
    const { calls } = stubTradeApi({ [listPath]: { status: 200, body: partyList([partyItem()]) } });
    render();
    await screen.findByText('شرکت بازرگانی نمونه');
    const before = calls.filter((call) => call.url.pathname === '/api/v1/trade/parties').length;
    await user.type(screen.getByLabelText('جست‌وجو'), '   ');
    await new Promise((resolve) => setTimeout(resolve, 80));
    expect(calls.filter((call) => call.url.pathname === '/api/v1/trade/parties')).toHaveLength(before);
  });

  it('filters by role and by status through the documented query parameters', async () => {
    const user = userEvent.setup();
    const { calls } = stubTradeApi({ [listPath]: { status: 200, body: partyList([partyItem()]) } });
    render();
    await screen.findByText('شرکت بازرگانی نمونه');
    await user.selectOptions(screen.getByLabelText('نقش'), 'Carrier');
    await waitFor(() => expect(lastList(calls).url.searchParams.get('role')).toBe('Carrier'));
    await user.selectOptions(screen.getByLabelText('وضعیت'), 'inactive');
    await waitFor(() => expect(lastList(calls).url.searchParams.get('isActive')).toBe('false'));
    await user.selectOptions(screen.getByLabelText('وضعیت'), 'active');
    await waitFor(() => expect(lastList(calls).url.searchParams.get('isActive')).toBe('true'));
    await user.selectOptions(screen.getByLabelText('وضعیت'), 'all');
    await user.selectOptions(screen.getByLabelText('نقش'), 'all');
    await waitFor(() => {
      const last = lastList(calls).url.searchParams;
      expect(last.has('role')).toBe(false);
      expect(last.has('isActive')).toBe(false);
    });
  });

  it('pages on the server: next page, and a new page size returns to page 1', async () => {
    const user = userEvent.setup();
    const { calls } = stubTradeApi({
      [listPath]: (request) => ({
        status: 200,
        body: partyList([partyItem()], { page: Number(request.url.searchParams.get('page')), pageSize: Number(request.url.searchParams.get('pageSize')), totalCount: 45 }),
      }),
    });
    render();
    await screen.findByText('شرکت بازرگانی نمونه');
    expect(screen.getByText(/صفحه ۱ از ۳/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /بعدی/ }));
    await waitFor(() => expect(lastList(calls).url.searchParams.get('page')).toBe('2'));
    await user.selectOptions(screen.getByLabelText('تعداد در صفحه'), '50');
    await waitFor(() => {
      expect(lastList(calls).url.searchParams.get('pageSize')).toBe('50');
      expect(lastList(calls).url.searchParams.get('page')).toBe('1');
    });
  });

  it('shows the create button only with the create AND view hints; a closed period shows it enabled and no banner', async () => {
    stubTradeApi({ [listPath]: { status: 200, body: partyList([partyItem()]) } });
    const writer = render();
    expect(screen.getByRole('link', { name: 'طرف‌حساب جدید' })).toHaveAttribute('href', '/app/trade/parties/new');
    writer.unmount();
    const viewer = render(readyAccess(['view']));
    await screen.findByText('شرکت بازرگانی نمونه');
    expect(screen.queryByRole('link', { name: 'طرف‌حساب جدید' })).not.toBeInTheDocument();
    viewer.unmount();
    const createOnly = render(readyAccess(['create']));
    await screen.findByText('شرکت بازرگانی نمونه');
    expect(screen.queryByRole('link', { name: 'طرف‌حساب جدید' })).not.toBeInTheDocument();
    createOnly.unmount();
    render(readyAccess(['view', 'create'], 'ReadOnly'));
    expect(screen.getByRole('link', { name: 'طرف‌حساب جدید' })).toHaveAttribute('href', '/app/trade/parties/new');
    await screen.findByText('شرکت بازرگانی نمونه');
    expect(screen.queryByText(/فقط‌خواندنی/)).not.toBeInTheDocument();
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });

  it('shows the friendly module-unavailable state on 403 trade_module_unavailable, without a crash', async () => {
    stubTradeApi({ [listPath]: problem(403, 'trade_module_unavailable') });
    render();
    expect(await screen.findByRole('heading', { name: 'ماژول بازرگانی در دسترس نیست' })).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('shows the forbidden panel on 403 trade_forbidden and the context message on 409 trade_context_required', async () => {
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
});
