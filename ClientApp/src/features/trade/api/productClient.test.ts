import { describe, expect, it } from 'vitest';
import { ApiError } from '@/api/client';
import { TradeContractError, parseProductDetail, parseProductListResponse, parseUnits } from './tradeTypes';
import { createProduct, getProduct, listProducts, listUnits, updateProduct } from './tradeClient';
import { freshRowVersion, problem, productDetail, productId, productItem, productList, rowVersion, unitsBody } from '../test/tradeFixtures';
import { stubTradeApi } from '../test/tradeTestUtils';

const body = { code: 'PRD-1', name: 'گندم', productGroupCode: null, hsCode: '1001', baseUnitCode: 'KGM' };

describe('tradeClient product reads', () => {
  it('sends exactly the documented list query and nothing else (no tenant, no group filter)', async () => {
    const { calls } = stubTradeApi({ 'GET /api/v1/trade/products': { status: 200, body: productList([productItem()]) } });
    await listProducts({ search: 'گندم', isActive: false, page: 2, pageSize: 50 });
    await listProducts({ page: 1, pageSize: 20 });
    expect(calls[0]?.url.search).toBe(`?search=${encodeURIComponent('گندم')}&isActive=false&page=2&pageSize=50`);
    expect(calls[1]?.url.search).toBe('?page=1&pageSize=20');
    expect(calls.map((call) => call.url.search).join('')).not.toMatch(/tenant|company/i);
  });

  it('parses the list and rejects a malformed row or a missing unit as a contract error', async () => {
    stubTradeApi({ 'GET /api/v1/trade/products': { status: 200, body: productList([productItem({ baseUnitCode: '' })]) } });
    await expect(listProducts({ page: 1, pageSize: 20 })).rejects.toBeInstanceOf(TradeContractError);
    expect(() => parseProductListResponse({ items: [{}], page: 1, pageSize: 1, totalCount: 1 })).toThrow(TradeContractError);
    expect(() => parseProductListResponse(null)).toThrow(TradeContractError);
    const parsed = parseProductListResponse(productList([productItem({ productGroupCode: null, hsCode: null })]));
    expect(parsed.items[0]).toMatchObject({ productGroupCode: null, hsCode: null });
  });

  it('keeps the HS code as text (leading zeroes) and reads one product by its id', async () => {
    const { calls } = stubTradeApi({ [`GET /api/v1/trade/products/${productId}`]: { status: 200, body: productDetail({ hsCode: '0101.21' }) } });
    const detail = await getProduct(productId);
    expect(detail.hsCode).toBe('0101.21');
    expect(detail.rowVersion).toBe(rowVersion);
    expect(detail.updatedAtUtc).toBeNull();
    expect(calls[0]?.url.pathname).toBe(`/api/v1/trade/products/${productId}`);
    expect(() => parseProductDetail({ ...productDetail(), createdAtUtc: 'not a date' })).toThrow(TradeContractError);
    expect(() => parseProductDetail({ ...productDetail(), rowVersion: undefined })).toThrow(TradeContractError);
  });

  it('reads the units and rejects a malformed list', async () => {
    stubTradeApi({});
    expect(await listUnits()).toEqual(unitsBody);
    expect(() => parseUnits({})).toThrow(TradeContractError);
    expect(() => parseUnits([{ code: 'KGM' }])).toThrow(TradeContractError);
  });

  it('maps a 404 to an ApiError without details', async () => {
    stubTradeApi({ [`GET /api/v1/trade/products/${productId}`]: { status: 404 } });
    await expect(getProduct(productId)).rejects.toMatchObject({ status: 404 });
  });
});

describe('tradeClient product writes', () => {
  it('POSTs exactly the documented fields with the anti-forgery header and parses the 201 detail', async () => {
    const { writes } = stubTradeApi({ 'POST /api/v1/trade/products': { status: 201, body: productDetail() } });
    const created = await createProduct({ ...body, isActive: false, tenantId: 'x' } as typeof body);
    expect(created.id).toBe(productId);
    expect(writes()[0]?.body).toEqual(body);
    expect(writes()[0]?.headers['X-XSRF-TOKEN']).toBe('token');
  });

  it('PUTs the full replacement with the rowVersion, to the encoded id, and parses the detail', async () => {
    const { writes } = stubTradeApi({ [`PUT /api/v1/trade/products/${productId}`]: { status: 200, body: productDetail({ rowVersion: freshRowVersion }) } });
    const updated = await updateProduct(productId, body, rowVersion);
    expect(updated.rowVersion).toBe(freshRowVersion);
    expect(writes()[0]?.body).toEqual({ ...body, rowVersion });
  });

  it('exposes the machine code of a 409', async () => {
    stubTradeApi({ 'POST /api/v1/trade/products': problem(409, 'trade_product_code_duplicate') });
    const failure = await createProduct(body).catch((cause: unknown) => cause);
    expect(failure).toBeInstanceOf(ApiError);
    expect((failure as ApiError).problemCode).toBe('trade_product_code_duplicate');
  });
});
