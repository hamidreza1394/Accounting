import { partyRoles, type PartyDetail, type PartyRole, type PartyWriteBody } from '../api/tradeTypes';

// Client-side mirror of the server rules of `PartyService` (code <= 64, legal name <= 200, display
// name <= 200, legal identifier <= 64, 1..9 distinct roles, an optional country of the list). The
// server stays authoritative; this only saves a round trip. Messages never echo the value.

export const partyLimits = { code: 64, legalName: 200, displayName: 200, legalIdentifier: 64, maxRoles: partyRoles.length } as const;

export type PartyFormState = {
  code: string;
  legalName: string;
  displayName: string;
  legalIdentifier: string;
  countryCode: string | null;
  roles: readonly PartyRole[];
};

export const emptyPartyForm: PartyFormState = { code: '', legalName: '', displayName: '', legalIdentifier: '', countryCode: null, roles: [] };

export type PartyFieldKey = 'code' | 'legalName' | 'displayName' | 'legalIdentifier' | 'countryCode' | 'roles';
export type PartyFieldErrors = Partial<Record<PartyFieldKey, string>>;

export const forbiddenCharactersMessage = 'مقدار شامل نویسه‌های کنترلی یا جهت‌دهی متن غیرمجاز است.';

/** Control characters and bidi overrides/isolates/marks are refused (ZWNJ stays allowed), like the server. */
export function hasForbiddenCharacters(value: string): boolean {
  for (const character of value) {
    const code = character.codePointAt(0) ?? 0;
    if (code <= 0x1f || (code >= 0x7f && code <= 0x9f)) return true;
    // U+061C ALM, U+2028/U+2029 line/paragraph separators, U+FEFF BOM (JS trim() strips a leading/trailing U+FEFF, .NET does not).
    if (code === 0x061c || code === 0x2028 || code === 0x2029 || code === 0xfeff) return true;
    if (code === 0x200e || code === 0x200f || (code >= 0x202a && code <= 0x202e) || (code >= 0x2066 && code <= 0x2069)) return true;
  }
  return false;
}

export function validateText(value: string, label: string, max: number, required: boolean): string | undefined {
  const trimmed = value.trim();
  if (trimmed === '') return required ? `${label} الزامی است.` : undefined;
  if (hasForbiddenCharacters(trimmed)) return forbiddenCharactersMessage;
  if (trimmed.length > max) return `${label} نباید بیش از ${max.toLocaleString('fa-IR')} نویسه باشد.`;
  return undefined;
}

/** `knownCountryCodes` is the loaded reference list; when it is not loaded yet the server decides. */
export function validatePartyForm(form: PartyFormState, knownCountryCodes: ReadonlySet<string> | null): PartyFieldErrors {
  const errors: PartyFieldErrors = {};
  const code = validateText(form.code, 'کد', partyLimits.code, true);
  if (code) errors.code = code;
  const legalName = validateText(form.legalName, 'نام حقوقی', partyLimits.legalName, true);
  if (legalName) errors.legalName = legalName;
  const displayName = validateText(form.displayName, 'نام نمایشی', partyLimits.displayName, false);
  if (displayName) errors.displayName = displayName;
  const identifier = validateText(form.legalIdentifier, 'شناسهٔ حقوقی', partyLimits.legalIdentifier, false);
  if (identifier) errors.legalIdentifier = identifier;
  if (form.countryCode !== null && knownCountryCodes !== null && !knownCountryCodes.has(form.countryCode)) {
    errors.countryCode = 'کد کشور نامعتبر است.';
  }
  if (form.roles.length < 1) errors.roles = 'دست‌کم یک نقش انتخاب کنید.';
  else if (form.roles.length > partyLimits.maxRoles) errors.roles = `تعداد نقش‌ها باید بین ۱ تا ${partyLimits.maxRoles.toLocaleString('fa-IR')} باشد.`;
  else if (new Set(form.roles).size !== form.roles.length) errors.roles = 'نقش‌ها نباید تکراری باشند.';
  return errors;
}

const optional = (value: string): string | null => {
  const trimmed = value.trim();
  return trimmed === '' ? null : trimmed;
};

/** The request body, built field by field and trimmed like the server does. Roles keep the enum order. */
export function buildPartyBody(form: PartyFormState): PartyWriteBody {
  return {
    code: form.code.trim(),
    legalName: form.legalName.trim(),
    displayName: optional(form.displayName),
    legalIdentifier: optional(form.legalIdentifier),
    countryCode: form.countryCode,
    roles: partyRoles.filter((role) => form.roles.includes(role)),
  };
}

export function formFromDetail(party: PartyDetail): PartyFormState {
  return {
    code: party.code,
    legalName: party.legalName,
    displayName: party.displayName ?? '',
    legalIdentifier: party.legalIdentifier ?? '',
    countryCode: party.countryCode,
    roles: party.roles,
  };
}

/** True when the form differs from the baseline after normalization (trim, optional text, role set). */
export function isPartyFormDirty(current: PartyFormState, baseline: PartyFormState): boolean {
  const left = buildPartyBody(current);
  const right = buildPartyBody(baseline);
  return JSON.stringify(left) !== JSON.stringify(right);
}
