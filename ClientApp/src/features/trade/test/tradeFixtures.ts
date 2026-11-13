// Trade test fixtures: builders for the real backend DTO shapes (see tradeTypes.ts and
// `Modules/Trade/Trade.Application/Parties/PartyDtos.cs`). A failure means the client or the
// documented contract drifted — never a made-up shape.

import type { PartyDetail, PartyListItem, PartyListResponse, ProductDetail, ProductListItem, ProductListResponse } from '../api/tradeTypes';

export const partyId = '3b1f5c70-1111-4000-8000-00000000a001';
export const otherPartyId = '3b1f5c70-2222-4000-8000-00000000a002';
export const rowVersion = 'AAAAAAAAB9I=';
export const freshRowVersion = 'AAAAAAAACdE=';

export function partyItem(overrides: Partial<PartyListItem> = {}): PartyListItem {
  return {
    id: partyId,
    code: 'P-001',
    legalName: 'شرکت بازرگانی نمونه',
    displayName: 'نمونه',
    countryCode: 'IR',
    isActive: true,
    roles: ['Importer', 'Seller'],
    rowVersion,
    ...overrides,
  };
}

export function partyList(items: readonly PartyListItem[], overrides: Partial<PartyListResponse> = {}): PartyListResponse {
  return { items, page: 1, pageSize: 20, totalCount: items.length, ...overrides };
}

export function partyDetail(overrides: Partial<PartyDetail> = {}): PartyDetail {
  return {
    ...partyItem(),
    legalIdentifier: '14001234567',
    createdAtUtc: '2026-11-01T08:30:00',
    updatedAtUtc: null,
    ...overrides,
  };
}

export const countriesBody = [
  { code: 'AE', nameFa: 'امارات متحدهٔ عربی', nameEn: 'United Arab Emirates' },
  { code: 'CN', nameFa: 'چین', nameEn: 'China' },
  { code: 'DE', nameFa: 'آلمان', nameEn: 'Germany' },
  { code: 'IR', nameFa: 'ایران', nameEn: 'Iran' },
];

/** The ProblemDetails of a 409/403 as the server sends it: Persian title and a machine code in `detail`. */
export const problem = (status: number, detail: string, title = 'عنوان فارسی') => ({ status, body: { title, status, detail } });

export const readWriteCapabilities = {
  available: true,
  accessMode: 'ReadWrite',
  permissions: ['trade.form.party.view', 'trade.form.party.create', 'trade.form.party.update'],
};

export const productId = '3b1f5c70-3333-4000-8000-00000000b001';
export const otherProductId = '3b1f5c70-4444-4000-8000-00000000b002';

export function productItem(overrides: Partial<ProductListItem> = {}): ProductListItem {
  return {
    id: productId,
    code: 'PRD-001',
    name: 'گندم دوروم',
    productGroupCode: 'GRAIN',
    hsCode: '1001.19',
    baseUnitCode: 'KGM',
    isActive: true,
    ...overrides,
  };
}

export function productList(items: readonly ProductListItem[], overrides: Partial<ProductListResponse> = {}): ProductListResponse {
  return { items, page: 1, pageSize: 20, totalCount: items.length, ...overrides };
}

export function productDetail(overrides: Partial<ProductDetail> = {}): ProductDetail {
  return { ...productItem(), rowVersion, createdAtUtc: '2026-11-01T08:30:00', updatedAtUtc: null, ...overrides };
}

export const unitsBody = [
  { code: 'KGM', nameFa: 'کیلوگرم', nameEn: 'Kilogram' },
  { code: 'MTR', nameFa: 'متر', nameEn: 'Metre' },
  { code: 'TNE', nameFa: 'تن', nameEn: 'Tonne' },
];

export const productCapabilities = {
  available: true,
  accessMode: 'ReadWrite',
  permissions: ['trade.form.product.view', 'trade.form.product.create', 'trade.form.product.update'],
};
