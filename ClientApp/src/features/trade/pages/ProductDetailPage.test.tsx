import { screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it } from 'vitest';
import { clearProductFlash, setProductFlash } from '../lib/productFlash';
import { problem, productDetail, productId } from '../test/tradeFixtures';
import { onUnauthenticated, readyAccess, renderTradePage, stubTradeApi } from '../test/tradeTestUtils';
import { ProductDetailPage } from './ProductDetailPage';

const detailPath = `GET /api/v1/trade/products/${productId}`;
const access = (permissions: ('view' | 'create' | 'update')[] = ['view', 'create', 'update'], mode: 'ReadWrite' | 'ReadOnly' = 'ReadWrite') =>
  readyAccess(permissions, mode, 'product');
const render = (value = access()) =>
  renderTradePage(<ProductDetailPage onUnauthenticated={onUnauthenticated} />, `/trade/products/${productId}`, '/trade/products/:id', value);

beforeEach(() => {
  onUnauthenticated.mockReset();
  onUnauthenticated.mockReturnValue(false);
  clearProductFlash();
});

describe('ProductDetailPage', () => {
  it('shows the identity, the classification, the base unit name and the audit times', async () => {
    stubTradeApi({ [detailPath]: { status: 200, body: productDetail({ updatedAtUtc: '2026-11-05T10:00:00' }) } });
    render();
    expect(await screen.findByRole('heading', { level: 1, name: 'گندم دوروم' })).toBeInTheDocument();
    expect(screen.getAllByText('PRD-001').length).toBeGreaterThan(0);
    expect(screen.getByText('GRAIN')).toBeInTheDocument();
    expect(screen.getByText('1001.19')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByText('کیلوگرم')).toBeInTheDocument());
    expect(screen.getByText('تاریخ ثبت')).toBeInTheDocument();
    expect(screen.getByText('آخرین ویرایش').nextElementSibling?.textContent).not.toBe('ثبت نشده');
    expect(screen.getAllByText('فعال').length).toBeGreaterThan(0);
    expect(screen.queryByRole('button', { name: /حذف|غیرفعال‌سازی|فعال‌سازی/ })).not.toBeInTheDocument();
  });

  it('says so when optional values are absent', async () => {
    stubTradeApi({ [detailPath]: { status: 200, body: productDetail({ productGroupCode: null, hsCode: null }) } });
    render();
    await screen.findByRole('heading', { level: 1, name: 'گندم دوروم' });
    expect(screen.getAllByText('ثبت نشده').length).toBeGreaterThanOrEqual(3);
  });

  it('offers the edit link only with the update AND view hints, in a closed period too', async () => {
    stubTradeApi({ [detailPath]: { status: 200, body: productDetail() } });
    const writer = render();
    expect(await screen.findByRole('link', { name: 'ویرایش' })).toHaveAttribute('href', `/app/trade/products/${productId}/edit`);
    writer.unmount();
    const viewer = render(access(['view']));
    await screen.findByRole('heading', { level: 1 });
    expect(screen.queryByRole('link', { name: 'ویرایش' })).not.toBeInTheDocument();
    viewer.unmount();
    const updateOnly = render(access(['update']));
    await screen.findByRole('heading', { level: 1 });
    expect(screen.queryByRole('link', { name: 'ویرایش' })).not.toBeInTheDocument();
    updateOnly.unmount();
    render(access(['view', 'update'], 'ReadOnly'));
    expect(await screen.findByRole('link', { name: 'ویرایش' })).toBeInTheDocument();
  });

  it('shows the one-shot success notice handed over by the form, in memory only', async () => {
    setProductFlash(productId, 'created');
    stubTradeApi({ [detailPath]: { status: 200, body: productDetail() } });
    render();
    expect(await screen.findByText('کالا با موفقیت ثبت شد.')).toBeInTheDocument();
    expect(window.location.search).toBe('');
  });

  it('shows a loading state, then not-found (404) with a way back', async () => {
    stubTradeApi({ [detailPath]: { status: 404 } });
    render();
    expect(screen.getByRole('status')).toHaveTextContent('در حال بارگذاری');
    expect(await screen.findByRole('heading', { name: 'کالا پیدا نشد' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'بازگشت به فهرست' })).toHaveAttribute('href', '/app/trade/products');
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
