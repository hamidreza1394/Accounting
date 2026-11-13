// Trade API client (Party slice). Every function maps 1:1 onto a real endpoint under
// `/api/v1/trade` (`Server/Api/Trade/*`). Reads use the shared same-origin GET helper (`same-origin`,
// `no-store`, 401 -> UnauthenticatedError, errors
// through the shared `getJson`, so a 409 exposes its machine code as `ApiError.problemCode`); writes
// reuse the shared `sendJson`, which fetches the anti-forgery token and sends it in the header.
// The company and the fiscal period are never sent: the server takes both from the work context
// (D-013); nothing tenant-related travels in a path, query or body.

import { getJson, sendJson } from '@/api/client';
import {
  parseCountries,
  parsePartyDetail,
  parsePartyListResponse,
  parseTradeCapabilities,
  type CountryRef,
  type PartyDetail,
  type PartyListResponse,
  type PartyRole,
  type PartyWriteBody,
  type TradeCapabilities,
} from './tradeTypes';

const tradeGet = (path: string, signal?: AbortSignal): Promise<unknown> => getJson(`/trade${path}`, signal);

function query(params: Record<string, string | number | boolean | undefined>): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== '') search.set(key, String(value));
  }
  const text = search.toString();
  return text ? `?${text}` : '';
}

/** `GET /trade/capabilities`: the only source of Trade permission hints; the server stays authoritative. */
export async function getTradeCapabilities(signal?: AbortSignal): Promise<TradeCapabilities> {
  return parseTradeCapabilities(await tradeGet('/capabilities', signal));
}

/** Query of `GET /trade/parties`; the server clamps `page`/`pageSize` and validates `role`. */
export type PartyQuery = {
  search?: string;
  role?: PartyRole;
  isActive?: boolean;
  page: number;
  pageSize: number;
};

export async function listParties(queryValues: PartyQuery, signal?: AbortSignal): Promise<PartyListResponse> {
  const path = `/parties${query({
    search: queryValues.search,
    role: queryValues.role,
    isActive: queryValues.isActive,
    page: queryValues.page,
    pageSize: queryValues.pageSize,
  })}`;
  return parsePartyListResponse(await tradeGet(path, signal));
}

/** 404 with an empty body for a missing, other-tenant or deleted party (the same answer for all three). */
export async function getParty(partyId: string, signal?: AbortSignal): Promise<PartyDetail> {
  return parsePartyDetail(await tradeGet(`/parties/${encodeURIComponent(partyId)}`, signal));
}

function writeBody(body: PartyWriteBody): Record<string, unknown> {
  // Field by field (never a spread), so no stray field of a form state can reach the server.
  return {
    code: body.code,
    legalName: body.legalName,
    displayName: body.displayName,
    legalIdentifier: body.legalIdentifier,
    countryCode: body.countryCode,
    roles: [...body.roles],
  };
}

/** `POST /trade/parties` -> 201 with the created detail. */
export async function createParty(body: PartyWriteBody): Promise<PartyDetail> {
  const response = await sendJson('POST', '/trade/parties', writeBody(body));
  return parsePartyDetail(await response.json());
}

/** `PUT /trade/parties/{id}` -> 200 with the updated detail. `rowVersion` is the base64 token of the last read. */
export async function updateParty(partyId: string, body: PartyWriteBody, rowVersion: string): Promise<PartyDetail> {
  const response = await sendJson('PUT', `/trade/parties/${encodeURIComponent(partyId)}`, { ...writeBody(body), rowVersion });
  return parsePartyDetail(await response.json());
}

/** `GET /trade/reference/countries` -> every country with its ISO code and Persian/English names. */
export async function listCountries(signal?: AbortSignal): Promise<readonly CountryRef[]> {
  return parseCountries(await tradeGet('/reference/countries', signal));
}
