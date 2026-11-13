// Real DTOs of the Trade API, copied field by field from
// `Modules/Trade/Trade.Application/Parties/PartyDtos.cs`, `Server/Api/Trade/TradeEndpoints.cs` and
// `TradeReferenceEndpoints.cs`. Nothing is invented: when the backend grows, this file grows with it.
// Roles arrive as the NAMES of the `PartyRole` enum; the Persian labels live in `lib/tradeLabels.ts`.

/** Thrown when a response does not match the documented contract; the page shows an error state. */
export class TradeContractError extends Error {
  constructor(part: string) {
    super(`Trade API contract mismatch: ${part}`);
    this.name = 'TradeContractError';
  }
}

/** The members of `Faranou.Modules.Trade.Domain.Parties.PartyRole`, in enum order (the exact names the API speaks). */
export const partyRoles = [
  'Importer',
  'Seller',
  'FxBroker',
  'Carrier',
  'Bank',
  'Beneficiary',
  'Inspector',
  'VesselAgent',
  'LivestockSupport',
] as const;
export type PartyRole = (typeof partyRoles)[number];

export type TradeAccessMode = 'ReadWrite' | 'ReadOnly';

/** `GET /trade/capabilities`: the Trade permission codes of the ACTIVE company (empty unless `available`). */
export type TradeCapabilities = { available: boolean; accessMode: TradeAccessMode; permissions: readonly string[] };

export type PartyListItem = {
  id: string;
  code: string;
  legalName: string;
  displayName: string | null;
  countryCode: string | null;
  isActive: boolean;
  roles: readonly PartyRole[];
  rowVersion: string;
};

export type PartyListResponse = { items: readonly PartyListItem[]; page: number; pageSize: number; totalCount: number };

export type PartyDetail = PartyListItem & {
  /** Only the detail DTO carries it; it must never reach a URL, a log or an error text. */
  legalIdentifier: string | null;
  createdAtUtc: string;
  updatedAtUtc: string | null;
};

export type CountryRef = { code: string; nameFa: string; nameEn: string };

/** The create/update body. `rowVersion` (base64) is mandatory on update only. */
export type PartyWriteBody = {
  code: string;
  legalName: string;
  displayName: string | null;
  legalIdentifier: string | null;
  countryCode: string | null;
  roles: readonly PartyRole[];
};

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function asObject(value: unknown, what: string): Record<string, unknown> {
  if (!isObject(value)) throw new TradeContractError(what);
  return value;
}

function readString(value: Record<string, unknown>, key: string, what: string): string {
  const field = value[key];
  if (typeof field !== 'string' || field === '') throw new TradeContractError(`${what}.${key}`);
  return field;
}

function readOptionalString(value: Record<string, unknown>, key: string, what: string): string | null {
  const field = value[key];
  if (field === null || field === undefined) return null;
  if (typeof field !== 'string') throw new TradeContractError(`${what}.${key}`);
  return field;
}

function readBoolean(value: Record<string, unknown>, key: string, what: string): boolean {
  const field = value[key];
  if (typeof field !== 'boolean') throw new TradeContractError(`${what}.${key}`);
  return field;
}

function readInt(value: Record<string, unknown>, key: string, what: string): number {
  const field = value[key];
  if (typeof field !== 'number' || !Number.isInteger(field)) throw new TradeContractError(`${what}.${key}`);
  return field;
}

const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function readGuid(value: Record<string, unknown>, key: string, what: string): string {
  const field = readString(value, key, what);
  if (!guidPattern.test(field)) throw new TradeContractError(`${what}.${key}`);
  return field;
}

function readInstant(value: Record<string, unknown>, key: string, what: string): string {
  const field = readString(value, key, what);
  if (Number.isNaN(new Date(field).getTime())) throw new TradeContractError(`${what}.${key}`);
  return field;
}

function readOptionalInstant(value: Record<string, unknown>, key: string, what: string): string | null {
  const field = value[key];
  if (field === null || field === undefined) return null;
  if (typeof field !== 'string' || Number.isNaN(new Date(field).getTime())) throw new TradeContractError(`${what}.${key}`);
  return field;
}

function readRoles(value: Record<string, unknown>, what: string): readonly PartyRole[] {
  const field = value['roles'];
  if (!Array.isArray(field)) throw new TradeContractError(`${what}.roles`);
  return field.map((role): PartyRole => {
    if (typeof role !== 'string' || !(partyRoles as readonly string[]).includes(role)) throw new TradeContractError(`${what}.roles`);
    return role as PartyRole;
  });
}

export function parseTradeCapabilities(body: unknown): TradeCapabilities {
  const what = 'capabilities';
  const value = asObject(body, what);
  const mode = readString(value, 'accessMode', what);
  if (mode !== 'ReadWrite' && mode !== 'ReadOnly') throw new TradeContractError(`${what}.accessMode`);
  const permissions = value['permissions'];
  if (!Array.isArray(permissions) || permissions.some((code) => typeof code !== 'string')) {
    throw new TradeContractError(`${what}.permissions`);
  }
  return { available: readBoolean(value, 'available', what), accessMode: mode, permissions: permissions as string[] };
}

function parsePartyListItem(body: unknown, what: string): PartyListItem {
  const value = asObject(body, what);
  return {
    id: readGuid(value, 'id', what),
    code: readString(value, 'code', what),
    legalName: readString(value, 'legalName', what),
    displayName: readOptionalString(value, 'displayName', what),
    countryCode: readOptionalString(value, 'countryCode', what),
    isActive: readBoolean(value, 'isActive', what),
    roles: readRoles(value, what),
    rowVersion: readString(value, 'rowVersion', what),
  };
}

export function parsePartyListResponse(body: unknown): PartyListResponse {
  const what = 'partyList';
  const value = asObject(body, what);
  const items = value['items'];
  if (!Array.isArray(items)) throw new TradeContractError(`${what}.items`);
  return {
    items: items.map((item, index) => parsePartyListItem(item, `${what}.items[${index}]`)),
    page: readInt(value, 'page', what),
    pageSize: readInt(value, 'pageSize', what),
    totalCount: readInt(value, 'totalCount', what),
  };
}

export function parsePartyDetail(body: unknown): PartyDetail {
  const what = 'party';
  const value = asObject(body, what);
  return {
    ...parsePartyListItem(body, what),
    legalIdentifier: readOptionalString(value, 'legalIdentifier', what),
    createdAtUtc: readInstant(value, 'createdAtUtc', what),
    updatedAtUtc: readOptionalInstant(value, 'updatedAtUtc', what),
  };
}

export function parseCountries(body: unknown): readonly CountryRef[] {
  if (!Array.isArray(body)) throw new TradeContractError('countries');
  return body.map((item, index) => {
    const what = `countries[${index}]`;
    const value = asObject(item, what);
    return {
      code: readString(value, 'code', what),
      nameFa: readString(value, 'nameFa', what),
      nameEn: readString(value, 'nameEn', what),
    };
  });
}
