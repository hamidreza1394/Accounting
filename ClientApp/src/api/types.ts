// Contracts of the Faranou JSON API v1 (Server/Api/ApiEndpoints.cs).
// Every response is checked at runtime before it is used, so a contract change on the
// server surfaces as a clear error instead of `undefined` deep inside a component.

export type Session = {
  username: string;
  fullName: string;
  accountKind: string;
  isAdministrator: boolean;
};

export type CompanySummary = {
  id: string;
  name: string;
  nationalId: string | null;
  roleName: string;
};

export type FiscalPeriodStatus = 'Open' | 'Closed' | 'Archived';

export type FiscalPeriod = {
  id: string;
  title: string;
  /** Calendar date as stored by the server: ISO `yyyy-MM-dd` (Gregorian). */
  startDate: string;
  /** Calendar date as stored by the server: ISO `yyyy-MM-dd` (Gregorian). */
  endDate: string;
  status: FiscalPeriodStatus;
};

export type CompanyOverview = {
  company: CompanySummary;
  fiscalPeriods: FiscalPeriod[];
};

export type AntiforgeryToken = {
  headerName: string;
  requestToken: string;
};

export class ContractError extends Error {
  constructor(what: string) {
    super(`پاسخ سرور با قرارداد API هم‌خوان نیست (${what}).`);
    this.name = 'ContractError';
  }
}

type JsonObject = Record<string, unknown>;

function isObject(value: unknown): value is JsonObject {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function readString(source: JsonObject, key: string, what: string): string {
  const value = source[key];
  if (typeof value !== 'string') {
    throw new ContractError(`${what}.${key}`);
  }
  return value;
}

function readNullableString(source: JsonObject, key: string, what: string): string | null {
  const value = source[key];
  if (value === null || value === undefined) {
    return null;
  }
  if (typeof value !== 'string') {
    throw new ContractError(`${what}.${key}`);
  }
  return value;
}

const isoDate = /^\d{4}-\d{2}-\d{2}$/;

function readIsoDate(source: JsonObject, key: string, what: string): string {
  const value = readString(source, key, what);
  if (!isoDate.test(value)) {
    throw new ContractError(`${what}.${key}`);
  }
  return value;
}

const statuses: readonly FiscalPeriodStatus[] = ['Open', 'Closed', 'Archived'];

function isFiscalPeriodStatus(value: string): value is FiscalPeriodStatus {
  return (statuses as readonly string[]).includes(value);
}

export function parseSession(value: unknown): Session {
  if (!isObject(value) || typeof value['isAdministrator'] !== 'boolean') {
    throw new ContractError('session');
  }
  return {
    username: readString(value, 'username', 'session'),
    fullName: readString(value, 'fullName', 'session'),
    accountKind: readString(value, 'accountKind', 'session'),
    isAdministrator: value['isAdministrator'],
  };
}

export function parseCompanySummary(value: unknown): CompanySummary {
  if (!isObject(value)) {
    throw new ContractError('company');
  }
  return {
    id: readString(value, 'id', 'company'),
    name: readString(value, 'name', 'company'),
    nationalId: readNullableString(value, 'nationalId', 'company'),
    roleName: readString(value, 'roleName', 'company'),
  };
}

export function parseCompanies(value: unknown): CompanySummary[] {
  if (!Array.isArray(value)) {
    throw new ContractError('companies');
  }
  return value.map(parseCompanySummary);
}

export function parseFiscalPeriod(value: unknown): FiscalPeriod {
  if (!isObject(value)) {
    throw new ContractError('fiscalPeriod');
  }
  const status = readString(value, 'status', 'fiscalPeriod');
  if (!isFiscalPeriodStatus(status)) {
    throw new ContractError('fiscalPeriod.status');
  }
  return {
    id: readString(value, 'id', 'fiscalPeriod'),
    title: readString(value, 'title', 'fiscalPeriod'),
    startDate: readIsoDate(value, 'startDate', 'fiscalPeriod'),
    endDate: readIsoDate(value, 'endDate', 'fiscalPeriod'),
    status,
  };
}

export function parseCompanyOverview(value: unknown): CompanyOverview {
  if (!isObject(value) || !Array.isArray(value['fiscalPeriods'])) {
    throw new ContractError('overview');
  }
  return {
    company: parseCompanySummary(value['company']),
    fiscalPeriods: value['fiscalPeriods'].map(parseFiscalPeriod),
  };
}

export function parseAntiforgeryToken(value: unknown): AntiforgeryToken {
  if (!isObject(value)) {
    throw new ContractError('antiforgery');
  }
  return {
    headerName: readString(value, 'headerName', 'antiforgery'),
    requestToken: readString(value, 'requestToken', 'antiforgery'),
  };
}
