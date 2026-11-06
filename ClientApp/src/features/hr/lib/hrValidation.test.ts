import { describe, expect, it } from 'vitest';
import {
  buildCreateEmployeeBody,
  buildCreatePositionBody,
  buildCreateUnitBody,
  buildUpdateEmployeeBody,
  buildUpdatePositionBody,
  buildUpdateUnitBody,
  calendarDayToUtcInstant,
  validateAssignmentEnd,
  validateAssignmentStart,
  validateCode,
  validateDescription,
  validateEmployeeNumber,
  validateRank,
  validateRequiredName,
  validateValidityWindow,
} from './hrValidation';

describe('hrValidation', () => {
  it('accepts employee numbers that satisfy the server pattern and rejects the rest', () => {
    expect(validateEmployeeNumber('EMP-101', true)).toBeNull();
    expect(validateEmployeeNumber('a.b_c-9', true)).toBeNull();
    expect(validateEmployeeNumber('', true)).not.toBeNull();
    expect(validateEmployeeNumber('', false)).toBeNull();
    expect(validateEmployeeNumber('۱۰۱', true)).not.toBeNull(); // Persian digits are not allowed
    expect(validateEmployeeNumber('-leading', true)).not.toBeNull(); // must start alphanumerically
    expect(validateEmployeeNumber('a'.repeat(31), true)).not.toBeNull();
    expect(validateEmployeeNumber('a'.repeat(30), true)).toBeNull();
  });

  it('enforces the name, code and description limits exactly like the server', () => {
    expect(validateRequiredName('علی', 'نام', 100)).toBeNull();
    expect(validateRequiredName(' ', 'نام', 100)).not.toBeNull();
    expect(validateRequiredName('ا'.repeat(101), 'نام', 100)).not.toBeNull();
    expect(validateCode('HQ-1', 'کد واحد')).toBeNull();
    expect(validateCode('ا ب', 'کد واحد')).not.toBeNull();
    expect(validateDescription('ا'.repeat(1000))).toBeNull();
    expect(validateDescription('ا'.repeat(1001))).not.toBeNull();
  });

  it('validates the validity window on calendar days', () => {
    expect(validateValidityWindow('2026-01-01', '2026-02-01')).toBeNull();
    expect(validateValidityWindow('2026-02-01', '2026-01-01')).not.toBeNull();
    expect(validateValidityWindow('', '2026-01-01')).toBeNull();
  });

  it('checks the rank shape', () => {
    expect(validateRank('')).toBeNull();
    expect(validateRank('3')).toBeNull();
    expect(validateRank('-2')).toBeNull();
    expect(validateRank('3.5')).not.toBeNull();
    expect(validateRank('abc')).not.toBeNull();
  });

  it('turns calendar days into UTC instants and rejects invalid ones', () => {
    const instant = calendarDayToUtcInstant('2026-01-10');
    expect(instant).not.toBeNull();
    expect(new Date(instant as string).toISOString()).toMatch(/^2026-01-09T2\d:|2026-01-10T/);
    expect(calendarDayToUtcInstant('')).toBeNull();
    expect(calendarDayToUtcInstant('1405/01/01')).toBeNull();
  });

  it('maps the create-employee body field by field', () => {
    expect(
      buildCreateEmployeeBody({ employeeNumber: ' EMP-101 ', firstName: ' علی ', lastName: 'محمدی', hiredOnIsoDay: '2026-01-10' }),
    ).toEqual({
      employeeNumber: 'EMP-101',
      firstName: 'علی',
      lastName: 'محمدی',
      hiredOnUtc: calendarDayToUtcInstant('2026-01-10'),
    });
    expect(buildCreateEmployeeBody({ employeeNumber: 'E', firstName: 'ع', lastName: 'م', hiredOnIsoDay: '' }).hiredOnUtc).toBeNull();
  });

  it('maps the update-employee body with rowVersion and keep-semantics', () => {
    const body = buildUpdateEmployeeBody({ firstName: 'علی', lastName: 'محمدی', employmentStatus: 2, hiredOnIsoDay: '2026-01-10', rowVersion: 'AAAAAAAAB9I=' });
    expect(body.employmentStatus).toBe(2);
    expect(body.rowVersion).toBe('AAAAAAAAB9I=');
    expect(body.hiredOnUtc).not.toBeNull();
  });

  it('maps the unit bodies with their documented null semantics', () => {
    const created = buildCreateUnitBody({
      code: 'HQ',
      name: 'دفتر مرکزی',
      description: ' ',
      parentUnitId: null,
      validFromIsoDay: '',
      validToIsoDay: '',
    });
    expect(created.description).toBeNull();

    const updated = buildUpdateUnitBody({
      name: 'دفتر مرکزی',
      description: '',
      parentUnitId: null,
      isActive: true,
      validFromIsoDay: '2026-01-01',
      validFromTouched: false,
      validToIsoDay: '',
      rowVersion: 'AAAAAAAAB9I=',
    });
    // validFrom untouched → null means "keep" on the server; validTo empty → null means "clear".
    expect(updated.validFromUtc).toBeNull();
    expect(updated.validToUtc).toBeNull();

    const changedFrom = buildUpdateUnitBody({
      name: 'دفتر مرکزی',
      description: '',
      parentUnitId: null,
      isActive: true,
      validFromIsoDay: '2026-01-01',
      validFromTouched: true,
      validToIsoDay: '',
      rowVersion: 'AAAAAAAAB9I=',
    });
    expect(changedFrom.validFromUtc).not.toBeNull();
  });

  it('maps the position bodies, clearing an empty rank on update', () => {
    expect(buildCreatePositionBody({ code: 'ACC', name: 'حسابدار', description: '', rank: '' })).toEqual({
      code: 'ACC',
      name: 'حسابدار',
      description: null,
      rank: null,
    });
    const updated = buildUpdatePositionBody({ name: 'حسابدار', description: 'توضیح', rank: '4', isActive: false, rowVersion: 'AAAAAAAAB9I=' });
    expect(updated.rank).toBe(4);
    expect(updated.isActive).toBe(false);
  });

  it('rejects future assignment starts with the server wording', () => {
    const future = new Date(Date.now() + 24 * 60 * 60 * 1000);
    const futureIso = `${future.getFullYear()}-${String(future.getMonth() + 1).padStart(2, '0')}-${String(future.getDate()).padStart(2, '0')}`;
    expect(validateAssignmentStart(futureIso, null)).toContain('آینده‌نگر');

    const hired = '2026-06-01T00:00:00Z';
    expect(validateAssignmentStart('2026-05-01', hired)).toContain('قبل از تاریخ شروع همکاری');
    expect(validateAssignmentStart('', null)).not.toBeNull();
  });

  it('rejects future assignment ends and accepts an empty (end-now) value', () => {
    const future = new Date(Date.now() + 24 * 60 * 60 * 1000);
    const futureIso = `${future.getFullYear()}-${String(future.getMonth() + 1).padStart(2, '0')}-${String(future.getDate()).padStart(2, '0')}`;
    expect(validateAssignmentEnd(futureIso)).not.toBeNull();
    expect(validateAssignmentEnd('')).toBeNull();
  });
});
