import { describe, expect, it } from 'vitest';
import { ApiError } from '@/api/client';
import { TradeContractError } from '../api/tradeTypes';
import { isModuleBlocked, mapProductFailure, mapTradeFailure, productMessages, tradeMessages } from './tradeErrors';

describe('mapTradeFailure', () => {
  it('maps a 400 ValidationProblem to field errors (camelCase keys) and never leaks unknown keys', () => {
    const failure = mapTradeFailure(new ApiError(400, 'x', { code: ['کد الزامی است.'], roles: ['نقش نامعتبر است.'], other: ['y'] }));
    expect(failure.kind).toBe('validation');
    expect(failure.fieldErrors).toEqual({ code: 'کد الزامی است.', roles: 'نقش نامعتبر است.' });
  });

  it('shows the server text of a 400 without a known field (bad request or anti-forgery)', () => {
    const failure = mapTradeFailure(new ApiError(400, 'درخواست نامعتبر است. صفحه را دوباره بارگذاری کنید.'));
    expect(failure.kind).toBe('generic');
    expect(failure.message).toContain('بارگذاری');
  });

  it('maps each 409 by its machine code to its own message', () => {
    const duplicate = mapTradeFailure(new ApiError(409, 'x', undefined, 'trade_party_code_duplicate'));
    expect(duplicate.kind).toBe('duplicate-code');
    expect(duplicate.fieldErrors.code).toBe(tradeMessages.duplicateCode);
    expect(mapTradeFailure(new ApiError(409, 'x', undefined, 'trade_concurrency_conflict')).kind).toBe('concurrency');
    expect(mapTradeFailure(new ApiError(409, 'x', undefined, 'trade_period_read_only')).message).toBe(tradeMessages.periodReadOnly);
    expect(mapTradeFailure(new ApiError(409, 'x', undefined, 'trade_context_required')).kind).toBe('context-required');
    expect(mapTradeFailure(new ApiError(409, 'raw server text')).message).toBe(tradeMessages.generic);
  });

  it('maps 403 by code: module unavailable vs forbidden; 404 to not-found', () => {
    expect(mapTradeFailure(new ApiError(403, 'x', undefined, 'trade_module_unavailable')).kind).toBe('module-unavailable');
    expect(mapTradeFailure(new ApiError(403, 'x', undefined, 'trade_forbidden')).kind).toBe('forbidden');
    expect(mapTradeFailure(new ApiError(403, 'x')).kind).toBe('forbidden');
    expect(mapTradeFailure(new ApiError(404, 'x')).kind).toBe('not-found');
  });

  it('treats contract and network failures as friendly generic/network errors', () => {
    expect(mapTradeFailure(new TradeContractError('party.id')).kind).toBe('generic');
    expect(mapTradeFailure(new TradeContractError('party.id')).message).not.toContain('party.id');
    expect(mapTradeFailure(new TypeError('Failed to fetch')).kind).toBe('network');
    expect(mapTradeFailure(new ApiError(500, 'خطای غیرمنتظره')).kind).toBe('generic');
  });

  it('maps the new server codes: 413 too large, 400 invalid body, 403 trade_forbidden on save', () => {
    const large = mapTradeFailure(new ApiError(413, 'x', undefined, 'trade_request_too_large'));
    expect(large.kind).toBe('too-large');
    expect(large.message).toBe(tradeMessages.tooLarge);
    expect(mapTradeFailure(new ApiError(413, 'x')).kind).toBe('too-large');
    const body = mapTradeFailure(new ApiError(400, 'raw server text', undefined, 'trade_invalid_request_body'));
    expect(body.kind).toBe('invalid-body');
    expect(body.message).toBe(tradeMessages.invalidBody);
    expect(mapTradeFailure(new ApiError(403, 'x', undefined, 'trade_forbidden')).message).toBe(tradeMessages.forbidden);
  });

  it('knows which failures block the whole module', () => {
    expect(isModuleBlocked(mapTradeFailure(new ApiError(403, 'x', undefined, 'trade_module_unavailable')))).toBe(true);
    expect(isModuleBlocked(mapTradeFailure(new ApiError(409, 'x', undefined, 'trade_context_required')))).toBe(true);
    expect(isModuleBlocked(mapTradeFailure(new ApiError(409, 'x', undefined, 'trade_concurrency_conflict')))).toBe(false);
    expect(isModuleBlocked(null)).toBe(false);
  });
});

describe('mapProductFailure', () => {
  it('maps a 400 to the product fields only (camelCase) and ignores party fields and unknown keys', () => {
    const failure = mapProductFailure(
      new ApiError(400, 'x', { hsCode: ['کد تعرفه نامعتبر.'], baseUnitCode: ['واحد پایه نامعتبر است.'], legalName: ['x'], other: ['y'] }),
    );
    expect(failure.kind).toBe('validation');
    expect(failure.fieldErrors).toEqual({ hsCode: 'کد تعرفه نامعتبر.', baseUnitCode: 'واحد پایه نامعتبر است.' });
  });

  it('maps the product duplicate code (and only it) to the code field with the product message', () => {
    const duplicate = mapProductFailure(new ApiError(409, 'x', undefined, 'trade_product_code_duplicate'));
    expect(duplicate.kind).toBe('duplicate-code');
    expect(duplicate.fieldErrors.code).toBe(productMessages.duplicateCode);
    expect(mapProductFailure(new ApiError(409, 'x', undefined, 'trade_party_code_duplicate')).kind).toBe('generic');
    expect(mapTradeFailure(new ApiError(409, 'x', undefined, 'trade_product_code_duplicate')).kind).toBe('generic');
  });

  it('uses product wording for concurrency and not-found, and the shared mapping for the rest', () => {
    expect(mapProductFailure(new ApiError(409, 'x', undefined, 'trade_concurrency_conflict')).message).toBe(productMessages.concurrency);
    expect(mapProductFailure(new ApiError(404, 'x')).message).toBe(productMessages.notFound);
    expect(mapProductFailure(new ApiError(413, 'x')).kind).toBe('too-large');
    expect(mapProductFailure(new ApiError(403, 'x', undefined, 'trade_forbidden')).kind).toBe('forbidden');
    expect(mapProductFailure(new ApiError(400, 'x', undefined, 'trade_invalid_request_body')).kind).toBe('invalid-body');
    expect(mapProductFailure(new TypeError('Failed to fetch')).kind).toBe('network');
    expect(mapProductFailure(new TradeContractError('product.id')).message).not.toContain('product.id');
  });
});
