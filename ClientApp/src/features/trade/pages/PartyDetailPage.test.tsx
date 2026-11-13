import { screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it } from 'vitest';
import { clearPartyFlash, setPartyFlash } from '../lib/partyFlash';
import { partyDetail, partyId, problem } from '../test/tradeFixtures';
import { onUnauthenticated, readyAccess, renderTradePage, stubTradeApi } from '../test/tradeTestUtils';
import { PartyDetailPage } from './PartyDetailPage';

const detailPath = `GET /api/v1/trade/parties/${partyId}`;
const render = (access = readyAccess()) =>
  renderTradePage(<PartyDetailPage onUnauthenticated={onUnauthenticated} />, `/trade/parties/${partyId}`, '/trade/parties/:id', access);

beforeEach(() => {
  onUnauthenticated.mockReset();
  onUnauthenticated.mockReturnValue(false);
  clearPartyFlash();
});

describe('PartyDetailPage', () => {
  it('shows the identity (with the legal identifier), the Persian roles, the country name and the audit times', async () => {
    stubTradeApi({ [detailPath]: { status: 200, body: partyDetail({ updatedAtUtc: '2026-11-05T10:00:00' }) } });
    render();
    expect(await screen.findByRole('heading', { level: 1, name: 'نمونه' })).toBeInTheDocument();
    expect(screen.getByText('شرکت بازرگانی نمونه')).toBeInTheDocument();
    expect(screen.getByText('14001234567')).toBeInTheDocument();
    expect(screen.getByRole('list', { name: 'نقش‌ها' })).toHaveTextContent('واردکننده');
    expect(screen.getByRole('list', { name: 'نقش‌ها' })).toHaveTextContent('فروشنده');
    await waitFor(() => expect(screen.getByText('ایران')).toBeInTheDocument());
    expect(screen.getByText('تاریخ ثبت')).toBeInTheDocument();
    expect(screen.getByText('آخرین ویرایش').nextElementSibling?.textContent).not.toBe('ثبت نشده');
    // isActive is a read-only badge; there is no delete/activate/deactivate action.
    expect(screen.getAllByText('فعال').length).toBeGreaterThan(0);
    expect(screen.queryByRole('button', { name: /حذف|غیرفعال‌سازی|فعال‌سازی/ })).not.toBeInTheDocument();
  });

  it('says so when optional values are absent', async () => {
    stubTradeApi({ [detailPath]: { status: 200, body: partyDetail({ displayName: null, legalIdentifier: null, countryCode: null }) } });
    render();
    expect(await screen.findByRole('heading', { level: 1, name: 'شرکت بازرگانی نمونه' })).toBeInTheDocument();
    expect(screen.getAllByText('ثبت نشده').length).toBeGreaterThanOrEqual(4);
  });

  it('offers the edit link only with the update AND view hints, in a closed period too', async () => {
    stubTradeApi({ [detailPath]: { status: 200, body: partyDetail() } });
    const writer = render();
    expect(await screen.findByRole('link', { name: 'ویرایش' })).toHaveAttribute('href', `/app/trade/parties/${partyId}/edit`);
    writer.unmount();
    const viewer = render(readyAccess(['view']));
    await screen.findByRole('heading', { level: 1 });
    expect(screen.queryByRole('link', { name: 'ویرایش' })).not.toBeInTheDocument();
    viewer.unmount();
    const updateOnly = render(readyAccess(['update']));
    await screen.findByRole('heading', { level: 1 });
    expect(screen.queryByRole('link', { name: 'ویرایش' })).not.toBeInTheDocument();
    updateOnly.unmount();
    render(readyAccess(['view', 'update'], 'ReadOnly'));
    // A closed period does not hide the edit link: Party is tenant-level master data.
    expect(await screen.findByRole('link', { name: 'ویرایش' })).toBeInTheDocument();
    expect(screen.queryByText(/فقط‌خواندنی/)).not.toBeInTheDocument();
  });

  it('shows the one-shot success notice handed over by the form, in memory only', async () => {
    setPartyFlash(partyId, 'created');
    stubTradeApi({ [detailPath]: { status: 200, body: partyDetail() } });
    render();
    expect(await screen.findByText('طرف‌حساب با موفقیت ثبت شد.')).toBeInTheDocument();
    expect(window.location.search).toBe('');
  });

  it('shows a loading state, then not-found (404) with a way back', async () => {
    stubTradeApi({ [detailPath]: { status: 404 } });
    render();
    expect(screen.getByRole('status')).toHaveTextContent('در حال بارگذاری');
    expect(await screen.findByRole('heading', { name: 'طرف‌حساب پیدا نشد' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'بازگشت به فهرست' })).toHaveAttribute('href', '/app/trade/parties');
  });

  it('shows a retryable error for a server failure and the friendly module state for 403', async () => {
    stubTradeApi({ [detailPath]: { status: 500 } });
    const failed = render();
    expect(await screen.findByRole('alert')).toHaveTextContent('خطای غیرمنتظره');
    expect(screen.getByRole('button', { name: 'تلاش دوباره' })).toBeInTheDocument();
    failed.unmount();
    stubTradeApi({ [detailPath]: problem(403, 'trade_module_unavailable') });
    render();
    expect(await screen.findByRole('heading', { name: 'ماژول بازرگانی در دسترس نیست' })).toBeInTheDocument();
  });
});
