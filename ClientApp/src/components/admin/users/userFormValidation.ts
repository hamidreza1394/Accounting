import type { CreateAdminUserRequest, UpdateAdminUserRequest } from '../../../api/types';

// The limits mirror Server/Api/UserAdministrationEndpoints.cs and Constants/*. The server stays
// authoritative and validates again; this only gives the user fast, field-level feedback.
export const limits = {
  usernameMin: 3,
  usernameMax: 20,
  nameMax: 50,
  emailMax: 254,
  mobileLength: 11,
  passwordMin: 15,
  passwordMax: 128,
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
};

export type FieldName = Exclude<keyof UserFormValues, 'isActive'>;
export const fieldNames: readonly FieldName[] = ['firstName', 'lastName', 'username', 'password', 'confirmPassword', 'emailAddress', 'cellPhoneNumber'];

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

  if (normalizeText(values.firstName).length > limits.nameMax) errors.firstName = `نام نباید بیش از ${fa.format(limits.nameMax)} نویسه باشد.`;
  if (normalizeText(values.lastName).length > limits.nameMax) errors.lastName = `نام خانوادگی نباید بیش از ${fa.format(limits.nameMax)} نویسه باشد.`;

  const email = normalizeText(values.emailAddress);
  if (email === '') errors.emailAddress = 'ایمیل را وارد کنید.';
  else if (email.length > limits.emailMax) errors.emailAddress = `ایمیل نباید بیش از ${fa.format(limits.emailMax)} نویسه باشد.`;
  else if (!emailPattern.test(email)) errors.emailAddress = 'قالب ایمیل معتبر نیست.';

  const mobile = normalizeText(values.cellPhoneNumber);
  if (mobile !== '' && !mobilePattern.test(mobile)) {
    errors.cellPhoneNumber = 'شماره موبایل باید ۱۱ رقم لاتین باشد و با 09 شروع شود.';
  }

  if (mode === 'create') {
    if (values.password.trim() === '') errors.password = 'رمز عبور را وارد کنید.';
    else if (values.password.length < limits.passwordMin) errors.password = `رمز عبور باید دست‌کم ${fa.format(limits.passwordMin)} نویسه باشد.`;
    else if (values.password.length > limits.passwordMax) errors.password = `رمز عبور نباید بیش از ${fa.format(limits.passwordMax)} نویسه باشد.`;

    if (values.confirmPassword === '') errors.confirmPassword = 'تکرار رمز عبور را وارد کنید.';
    else if (values.confirmPassword !== values.password) errors.confirmPassword = 'تکرار رمز عبور با رمز عبور یکسان نیست.';
  }
  return errors;
}

const orNull = (value: string): string | null => normalizeText(value) || null;

export function toUpdateRequest(values: UserFormValues): UpdateAdminUserRequest {
  return {
    username: normalizeText(values.username),
    firstName: orNull(values.firstName),
    lastName: orNull(values.lastName),
    emailAddress: normalizeText(values.emailAddress),
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
    if (typeof current === 'string' && typeof original === 'string' && key !== 'password' && key !== 'confirmPassword') {
      return normalizeText(current) !== normalizeText(original);
    }
    return current !== original;
  });
}
