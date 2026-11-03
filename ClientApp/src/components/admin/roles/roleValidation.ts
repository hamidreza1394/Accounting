import { normalizeText } from '../users/userFormValidation';

/** Mirrors the server: `^[A-Z0-9][A-Z0-9._-]{1,79}$` after trimming and upper-casing. */
export const roleCodePattern = /^[A-Z0-9][A-Z0-9._-]{1,79}$/;
export const roleNameMax = 120;

export const roleCodeMessage = 'کد باید ۲ تا ۸۰ نویسه از حروف لاتین، ارقام و . _ - باشد و با حرف یا رقم شروع شود.';

/** The code as the server will read it. */
export function normalizeRoleCode(value: string): string {
  return value.trim().toUpperCase();
}

export type RoleFieldErrors = { code?: string; name?: string };

export function validateRoleFields(values: { code?: string; name: string }, mode: 'create' | 'edit'): RoleFieldErrors {
  const errors: RoleFieldErrors = {};
  if (mode === 'create') {
    const code = normalizeRoleCode(values.code ?? '');
    if (code === '') errors.code = 'کد گروه الزامی است.';
    else if (!roleCodePattern.test(code)) errors.code = roleCodeMessage;
  }
  const name = normalizeText(values.name);
  if (name === '') errors.name = 'عنوان گروه الزامی است.';
  else if (name.length > roleNameMax) errors.name = 'عنوان گروه نباید بیش از ۱۲۰ نویسه باشد.';
  return errors;
}

export function sameCodes(a: readonly string[], b: readonly string[]): boolean {
  if (a.length !== b.length) return false;
  const set = new Set(a);
  return b.every((code) => set.has(code));
}

export const delegationNote = 'سقف تفویض هنوز در سمت سرور پیاده نشده است؛ هر مدیر نصب می‌تواند هر مجوز ثبت‌شده را به نقش‌ها اختصاص دهد.';
