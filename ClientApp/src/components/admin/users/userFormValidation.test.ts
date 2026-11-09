import { describe, expect, it } from 'vitest';
import { emptyUserForm, fieldTab, isUserFormDirty, tabsWithErrors, normalizeText, toCreateRequest, toUpdateRequest, validateUserForm, type UserFormValues } from './userFormValidation';

const valid: UserFormValues = {
  ...emptyUserForm,
  firstName: 'علی',
  lastName: 'رضایی',
  username: 'ali.r',
  emailAddress: 'ali@example.test',
  password: 'Str0ng-pass-phrase!',
  confirmPassword: 'Str0ng-pass-phrase!',
};

describe('validateUserForm', () => {
  it('accepts a minimal valid create form (only the mobile is optional)', () => {
    expect(validateUserForm(valid, 'create')).toEqual({});
  });

  it('requires username and, only when creating, the names and the password pair (the e-mail is optional)', () => {
    const errors = validateUserForm(emptyUserForm, 'create');
    expect(Object.keys(errors).sort()).toEqual(['confirmPassword', 'firstName', 'lastName', 'password', 'username']);
    expect(Object.keys(validateUserForm(emptyUserForm, 'edit')).sort()).toEqual(['username']);
  });

  it('enforces the username length 3..20 after trimming', () => {
    expect(validateUserForm({ ...valid, username: 'ab' }, 'create').username).toMatch(/دست‌کم ۳/);
    expect(validateUserForm({ ...valid, username: '  ab  ' }, 'create').username).toBeDefined();
    expect(validateUserForm({ ...valid, username: 'abc' }, 'create').username).toBeUndefined();
    expect(validateUserForm({ ...valid, username: 'a'.repeat(20) }, 'create').username).toBeUndefined();
    expect(validateUserForm({ ...valid, username: 'a'.repeat(21) }, 'create').username).toMatch(/بیش از ۲۰/);
  });

  it('enforces name length 50 and e-mail format and length 254', () => {
    expect(validateUserForm({ ...valid, firstName: 'x'.repeat(51) }, 'create').firstName).toBeDefined();
    expect(validateUserForm({ ...valid, lastName: 'x'.repeat(50) }, 'create').lastName).toBeUndefined();
    expect(validateUserForm({ ...valid, lastName: 'x'.repeat(51) }, 'create').lastName).toBeDefined();
    for (const bad of ['plain', '@x.com', 'a@', 'a b@x.com', 'a@@x.com']) {
      expect(validateUserForm({ ...valid, emailAddress: bad }, 'create').emailAddress, bad).toBe('قالب ایمیل معتبر نیست.');
    }
    expect(validateUserForm({ ...valid, emailAddress: `${'a'.repeat(250)}@x.co` }, 'create').emailAddress).toMatch(/بیش از ۲۵۴/);
  });

  it('accepts an empty mobile and only 11 Latin digits starting with 09', () => {
    expect(validateUserForm({ ...valid, cellPhoneNumber: '' }, 'create').cellPhoneNumber).toBeUndefined();
    expect(validateUserForm({ ...valid, cellPhoneNumber: '09123456789' }, 'create').cellPhoneNumber).toBeUndefined();
    for (const bad of ['0912345678', '091234567890', '08123456789', '۰۹۱۲۳۴۵۶۷۸۹', '0912345678a']) {
      expect(validateUserForm({ ...valid, cellPhoneNumber: bad }, 'create').cellPhoneNumber, bad).toBeDefined();
    }
  });

  it('enforces the password length boundary 8 (7 rejected, 8 accepted, empty rejected), the maximum and the confirmation match', () => {
    const rejected = 'رمز عبور باید حداقل ۸ نویسه داشته باشد.';
    expect(validateUserForm({ ...valid, password: 'abcdefg', confirmPassword: 'abcdefg' }, 'create').password).toBe(rejected);
    expect(validateUserForm({ ...valid, password: 'abcdefgh', confirmPassword: 'abcdefgh' }, 'create').password).toBeUndefined();
    expect(validateUserForm({ ...valid, password: '', confirmPassword: '' }, 'create').password).toBe('رمز عبور را وارد کنید.');
    expect(validateUserForm({ ...valid, password: 'short1', confirmPassword: 'short1' }, 'create').password).toBe(rejected);
    expect(validateUserForm({ ...valid, password: '        ', confirmPassword: '        ' }, 'create').password).toBe('رمز عبور را وارد کنید.');
    const long = 'p'.repeat(129);
    expect(validateUserForm({ ...valid, password: long, confirmPassword: long }, 'create').password).toMatch(/بیش از ۱۲۸/);
    expect(validateUserForm({ ...valid, confirmPassword: 'different-1' }, 'create').confirmPassword).toMatch(/یکسان نیست/);
  });
});

describe('optional e-mail', () => {
  it('accepts an empty or blank e-mail and still rejects a given one with a bad format', () => {
    for (const empty of ['', '   ']) {
      expect(validateUserForm({ ...valid, emailAddress: empty }, 'create').emailAddress, JSON.stringify(empty)).toBeUndefined();
      expect(validateUserForm({ ...valid, emailAddress: empty }, 'edit').emailAddress).toBeUndefined();
    }
    expect(validateUserForm({ ...valid, emailAddress: 'not-an-email' }, 'create').emailAddress).toBe('قالب ایمیل معتبر نیست.');
  });

  it('sends null (never an empty string) when the e-mail is empty', () => {
    expect(toCreateRequest({ ...valid, emailAddress: '  ' }).emailAddress).toBeNull();
    expect(toUpdateRequest({ ...valid, emailAddress: '' }).emailAddress).toBeNull();
    expect(toUpdateRequest({ ...valid, emailAddress: ' ali@example.test ' }).emailAddress).toBe('ali@example.test');
  });
});

describe('request mapping', () => {
  it('normalizes text like the server, sends null for empty optionals and never trims the password', () => {
    const values: UserFormValues = { ...valid, firstName: '  علی   رضا ', lastName: '   ', cellPhoneNumber: ' ', password: ' pass word ', confirmPassword: ' pass word ' };
    expect(toCreateRequest(values)).toEqual({
      username: 'ali.r',
      firstName: 'علی رضا',
      lastName: null,
      emailAddress: 'ali@example.test',
      cellPhoneNumber: null,
      password: ' pass word ',
      confirmPassword: ' pass word ',
      isActive: true,
    });
    expect(Object.keys(toUpdateRequest(values)).sort()).toEqual(['cellPhoneNumber', 'emailAddress', 'firstName', 'isActive', 'lastName', 'username']);
    expect(normalizeText('  a   b  ')).toBe('a b');
  });

  it('detects dirty state against the initial values', () => {
    expect(isUserFormDirty(emptyUserForm, emptyUserForm)).toBe(false);
    expect(isUserFormDirty({ ...emptyUserForm, isActive: false }, emptyUserForm)).toBe(true);
    expect(isUserFormDirty({ ...emptyUserForm, username: 'x' }, emptyUserForm)).toBe(true);
    // Text is compared as it would be sent; passwords are compared exactly.
    const base = { ...emptyUserForm, firstName: 'علی' };
    expect(isUserFormDirty({ ...base, firstName: ' علی  ' }, base)).toBe(false);
    expect(isUserFormDirty({ ...base, firstName: 'علی   رضا' }, { ...base, firstName: 'علی رضا' })).toBe(false);
    expect(isUserFormDirty({ ...emptyUserForm, password: ' ' }, emptyUserForm)).toBe(true);
  });
});

describe('personnel number and tabs', () => {
  it('accepts an empty personnel number and the server pattern, and rejects anything else', () => {
    for (const ok of ['', '1', 'EMP-101', '0042', 'a.b_c-d', 'x'.repeat(30)]) {
      expect(validateUserForm({ ...valid, employeeNumber: ok }, 'create').employeeNumber, ok).toBeUndefined();
    }
    for (const bad of ['-1', '.a', 'a b', 'ش۱', 'x'.repeat(31)]) {
      expect(validateUserForm({ ...valid, employeeNumber: bad }, 'create').employeeNumber, bad).toBeDefined();
    }
  });

  it('never validates or sends the personnel number when editing or in the user request', () => {
    expect(validateUserForm({ ...valid, employeeNumber: '-bad' }, 'edit').employeeNumber).toBeUndefined();
    expect(Object.keys(toCreateRequest({ ...valid, employeeNumber: '42' }))).not.toContain('employeeNumber');
  });

  it('maps every field to a tab and lists the tabs with errors in tab order', () => {
    expect(fieldTab.emailAddress).toBe('contact');
    expect(fieldTab.password).toBe('main');
    expect(tabsWithErrors({ emailAddress: 'x', username: 'y' })).toEqual(['main', 'contact']);
    expect(tabsWithErrors({})).toEqual([]);
  });

  it('counts a changed personnel number as a change', () => {
    expect(isUserFormDirty({ ...valid, employeeNumber: '7' }, valid)).toBe(true);
  });
});
