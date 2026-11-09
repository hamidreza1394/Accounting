import type { CreateAdminUserRequest, UpdateAdminUserRequest } from '../../../api/types';
import { hrEmployeeNumberPattern } from '@/features/hr/lib/hrLabels';
import { passwordMaxLength, passwordMinLength, passwordMinMessage } from '@/lib/passwordPolicy';

// The limits mirror Server/Api/UserAdministrationEndpoints.cs and Constants/*. The server stays
// authoritative and validates again; this only gives the user fast, field-level feedback.
export const limits = {
  usernameMin: 3,
  usernameMax: 20,
  nameMax: 50,
  emailMax: 254,
  mobileLength: 11,
  passwordMin: passwordMinLength,
  passwordMax: passwordMaxLength,
} as const;

const fa = new Intl.NumberFormat('fa-IR', { useGrouping: false });

/** A number in Persian digits, for messages and hints derived from {@link limits}. */
export const faDigits = (value: number): string => fa.format(value);

export type UserFormValues = {
  firstName: string;
  lastName: string;
  username: string;
  password: string;
  confirmPassword: string;
  emailAddress: string;
  cellPhoneNumber: string;
  isActive: boolean;
  /**
   * The personnel number of the HR record created together with the account (create mode only,
   * optional). It belongs to the company's HR record, not to the user (D-072), so it is never
   * part of the user request.
   */
  employeeNumber: string;
};

export type FieldName = Exclude<keyof UserFormValues, 'isActive'>;
export const fieldNames: readonly FieldName[] = [
  'firstName',
  'lastName',
  'username',
  'password',
  'confirmPassword',
  'employeeNumber',
  'emailAddress',
  'cellPhoneNumber',
];

/** The inner tabs of the user form, in display order. */
export type UserFormTabId = 'main' | 'contact' | 'employment' | 'roles' | 'history';
export const userFormTabOrder: readonly UserFormTabId[] = ['main', 'contact', 'employment', 'roles', 'history'];

/** The tab that holds each field, so an error can be flagged on its tab. */
export const fieldTab: Record<FieldName, UserFormTabId> = {
  firstName: 'main',
  lastName: 'main',
  username: 'main',
  password: 'main',
  confirmPassword: 'main',
  employeeNumber: 'main',
  emailAddress: 'contact',
  cellPhoneNumber: 'contact',
};

/** The tabs (in tab order) that hold at least one of the given field errors. */
export function tabsWithErrors(errors: Partial<Record<FieldName, string>>): UserFormTabId[] {
  const tabs = new Set<UserFormTabId>();
  for (const name of Object.keys(errors) as FieldName[]) {
    if (errors[name]) tabs.add(fieldTab[name]);
  }
  return userFormTabOrder.filter((tab) => tabs.has(tab));
}

export type FieldErrors = Partial<Record<FieldName, string>>;

export const emptyUserForm: UserFormValues = {
  firstName: '',
  lastName: '',
  username: '',
  password: '',
  confirmPassword: '',
  emailAddress: '',
  cellPhoneNumber: '',
  isActive: true,
  employeeNumber: '',
};

/** Same normalization as the server's text fixer: trim and collapse repeated spaces. */
export function normalizeText(value: string): string {
  return value.trim().replace(/ {2,}/g, ' ');
}

const mobilePattern = /^09\d{9}$/;
// The server uses the .NET e-mail check: one `@` with text before and after it.
const emailPattern = /^[^\s@]+@[^\s@]+$/;

export function validateUserForm(values: UserFormValues, mode: 'create' | 'edit'): FieldErrors {
  const errors: FieldErrors = {};
  const username = normalizeText(values.username);
  if (username === '') errors.username = 'نام کاربری را وارد کنید.';
  else if (username.length < limits.usernameMin) errors.username = `نام کاربری باید دست‌کم ${fa.format(limits.usernameMin)} نویسه باشد.`;
  else if (username.length > limits.usernameMax) errors.username = `نام کاربری نباید بیش از ${fa.format(limits.usernameMax)} نویسه باشد.`;

  if (mode === 'create' && normalizeText(values.firstName) === '') errors.firstName = 'نام را وارد کنید.';
  else if (normalizeText(values.firstName).length > limits.nameMax) errors.firstName = `نام نباید بیش از ${fa.format(limits.nameMax)} نویسه باشد.`;
  if (mode === 'create' && normalizeText(values.lastName) === '') errors.lastName = 'نام خانوادگی را وارد کنید.';
  else if (normalizeText(values.lastName).length > limits.nameMax) errors.lastName = `نام خانوادگی نباید بیش از ${fa.format(limits.nameMax)} نویسه باشد.`;

  const email = normalizeText(values.emailAddress);
  // The e-mail is optional: only a given value is checked (the server stores NULL when empty).
  if (email === '') {
    // nothing to check
  } else if (email.length > limits.emailMax) errors.emailAddress = `ایمیل نباید بیش از ${fa.format(limits.emailMax)} نویسه باشد.`;
  else if (!emailPattern.test(email)) errors.emailAddress = 'قالب ایمیل معتبر نیست.';

  const mobile = normalizeText(values.cellPhoneNumber);
  if (mobile !== '' && !mobilePattern.test(mobile)) {
    errors.cellPhoneNumber = 'شماره موبایل باید ۱۱ رقم لاتین باشد و با 09 شروع شود.';
  }

  if (mode === 'create') {
    if (values.password.trim() === '') errors.password = 'رمز عبور را وارد کنید.';
    else if (values.password.length < limits.passwordMin) errors.password = passwordMinMessage;
    else if (values.password.length > limits.passwordMax) errors.password = `رمز عبور نباید بیش از ${fa.format(limits.passwordMax)} نویسه باشد.`;

    if (values.confirmPassword === '') errors.confirmPassword = 'تکرار رمز عبور را وارد کنید.';
    else if (values.confirmPassword !== values.password) errors.confirmPassword = 'تکرار رمز عبور با رمز عبور یکسان نیست.';

    // The same rule as the server's personnel number (letters, digits, `.`, `_`, `-`; at most 30).
    const employeeNumber = values.employeeNumber.trim();
    if (employeeNumber !== '' && !hrEmployeeNumberPattern.test(employeeNumber)) {
      errors.employeeNumber = 'شماره پرسنلی فقط شامل حروف لاتین، رقم و نویسه‌های . _ - است و حداکثر ۳۰ نویسه می‌شود.';
    }
  }
  return errors;
}

const orNull = (value: string): string | null => normalizeText(value) || null;

export function toUpdateRequest(values: UserFormValues): UpdateAdminUserRequest {
  return {
    username: normalizeText(values.username),
    firstName: orNull(values.firstName),
    lastName: orNull(values.lastName),
    emailAddress: orNull(values.emailAddress),
    cellPhoneNumber: orNull(values.cellPhoneNumber),
    isActive: values.isActive,
  };
}

export function toCreateRequest(values: UserFormValues): CreateAdminUserRequest {
  return { ...toUpdateRequest(values), password: values.password, confirmPassword: values.confirmPassword };
}

/**
 * Compares what would be sent: text is normalized like the server does (so a trailing space is not
 * a change); passwords are compared exactly because they are never trimmed.
 */
export function isUserFormDirty(values: UserFormValues, initial: UserFormValues): boolean {
  return (Object.keys(initial) as (keyof UserFormValues)[]).some((key) => {
    const current = values[key];
    const original = initial[key];
    if (typeof current === 'string' && typeof original === 'string' && key !== 'password' && key !== 'confirmPassword' && key !== 'employeeNumber') {
      return normalizeText(current) !== normalizeText(original);
    }
    return current !== original;
  });
}
