import { describe, expect, it } from 'vitest';
import { partyDetail } from '../test/tradeFixtures';
import {
  buildPartyBody,
  emptyPartyForm,
  formFromDetail,
  hasForbiddenCharacters,
  isPartyFormDirty,
  validatePartyForm,
  type PartyFormState,
} from './partyValidation';

const valid: PartyFormState = { ...emptyPartyForm, code: 'P-1', legalName: 'شرکت', roles: ['Seller'] };
const countries = new Set(['IR', 'DE']);

describe('validatePartyForm (mirror of the server rules)', () => {
  it('accepts a minimal valid form and a valid country', () => {
    expect(validatePartyForm(valid, countries)).toEqual({});
    expect(validatePartyForm({ ...valid, countryCode: 'IR' }, countries)).toEqual({});
  });

  it('requires the code and the legal name', () => {
    const errors = validatePartyForm({ ...valid, code: '  ', legalName: '' }, countries);
    expect(errors.code).toBe('کد الزامی است.');
    expect(errors.legalName).toBe('نام حقوقی الزامی است.');
  });

  it('enforces the length limits on the trimmed value (64 / 200 / 200 / 64)', () => {
    expect(validatePartyForm({ ...valid, code: 'a'.repeat(64) }, countries).code).toBeUndefined();
    expect(validatePartyForm({ ...valid, code: 'a'.repeat(65) }, countries).code).toMatch(/بیش از/);
    expect(validatePartyForm({ ...valid, legalName: 'a'.repeat(201) }, countries).legalName).toMatch(/بیش از/);
    expect(validatePartyForm({ ...valid, displayName: 'a'.repeat(201) }, countries).displayName).toMatch(/بیش از/);
    expect(validatePartyForm({ ...valid, legalIdentifier: 'a'.repeat(65) }, countries).legalIdentifier).toMatch(/بیش از/);
    expect(validatePartyForm({ ...valid, code: ` ${'a'.repeat(64)} ` }, countries).code).toBeUndefined();
  });

  it('never echoes the submitted value in a message', () => {
    const errors = validatePartyForm({ ...valid, legalIdentifier: `SECRET${'x'.repeat(80)}` }, countries);
    expect(errors.legalIdentifier).not.toContain('SECRET');
  });

  it('refuses control and bidi characters but allows the ZWNJ', () => {
    expect(hasForbiddenCharacters('a\u0000b')).toBe(true);
    expect(hasForbiddenCharacters('a‮b')).toBe(true);
    expect(hasForbiddenCharacters('a⁦b')).toBe(true);
    expect(hasForbiddenCharacters('می‌خواهم')).toBe(false);
    for (const forbidden of ['\u061c', '\u2028', '\u2029', '\ufeff']) {
      expect(hasForbiddenCharacters(`a${forbidden}b`)).toBe(true);
      expect(validatePartyForm({ ...valid, legalName: `ab${forbidden}cd` }, countries).legalName).toMatch(/غیرمجاز/);
    }
    expect(validatePartyForm({ ...valid, legalName: 'ab‮cd' }, countries).legalName).toMatch(/غیرمجاز/);
  });

  it('requires 1 to 9 distinct roles', () => {
    expect(validatePartyForm({ ...valid, roles: [] }, countries).roles).toBeDefined();
    expect(validatePartyForm({ ...valid, roles: ['Seller', 'Seller'] }, countries).roles).toMatch(/تکراری/);
    const all = ['Importer', 'Seller', 'FxBroker', 'Carrier', 'Bank', 'Beneficiary', 'Inspector', 'VesselAgent', 'LivestockSupport'] as const;
    expect(validatePartyForm({ ...valid, roles: all }, countries).roles).toBeUndefined();
  });

  it('checks the country against the loaded list only when it is loaded', () => {
    expect(validatePartyForm({ ...valid, countryCode: 'ZZ' }, countries).countryCode).toBe('کد کشور نامعتبر است.');
    expect(validatePartyForm({ ...valid, countryCode: 'ZZ' }, null).countryCode).toBeUndefined();
  });
});

describe('party body and dirty tracking', () => {
  it('builds a trimmed body: empty optional text becomes null and roles keep the enum order', () => {
    expect(buildPartyBody({ ...valid, code: ' P-1 ', displayName: '  ', legalIdentifier: ' 123 ', roles: ['Seller', 'Importer'] })).toEqual({
      code: 'P-1',
      legalName: 'شرکت',
      displayName: null,
      legalIdentifier: '123',
      countryCode: null,
      roles: ['Importer', 'Seller'],
    });
  });

  it('is dirty only after a real change and clean again when the values return (whitespace and role order do not count)', () => {
    const baseline = formFromDetail(partyDetail());
    expect(isPartyFormDirty(baseline, baseline)).toBe(false);
    expect(isPartyFormDirty({ ...baseline, legalName: ` ${baseline.legalName} `, roles: [...baseline.roles].reverse() }, baseline)).toBe(false);
    expect(isPartyFormDirty({ ...baseline, legalName: 'تغییر' }, baseline)).toBe(true);
    expect(isPartyFormDirty({ ...baseline, countryCode: null }, baseline)).toBe(true);
  });
});
