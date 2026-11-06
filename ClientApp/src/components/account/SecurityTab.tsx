import { useEffect, useRef, useState, type FormEvent } from 'react';
import { KeyRound } from 'lucide-react';
import { ApiError, changeMyPassword, UnauthenticatedError } from '../../api/client';
import { Alert } from '../ui/Alert';
import { FormField } from '../ui/FormField';
import { PasswordInput } from '../ui/PasswordInput';

export type PasswordField = 'currentPassword' | 'newPassword' | 'confirmNewPassword';
export type PasswordFieldErrors = Partial<Record<PasswordField, string>>;

const fieldOrder: readonly PasswordField[] = ['currentPassword', 'newPassword', 'confirmNewPassword'];
// The same bounds the server enforces (D-073 §5: minimum 15 for new or changed passwords);
// no further policy is invented here.
const minLength = 15;
const maxLength = 128;
const genericError = 'تغییر رمز با خطا مواجه شد.';

/** Client-side checks that run before any request is sent. The server stays the authority. */
export function validatePasswordChange(values: Record<PasswordField, string>): PasswordFieldErrors {
  const errors: PasswordFieldErrors = {};
  if (!values.currentPassword) errors.currentPassword = 'رمز فعلی را وارد کنید.';
  if (!values.newPassword) errors.newPassword = 'رمز جدید را وارد کنید.';
  else if (values.newPassword.length < minLength) errors.newPassword = `رمز جدید باید دست‌کم ${minLength.toLocaleString('fa-IR')} نویسه باشد.`;
  else if (values.newPassword.length > maxLength) errors.newPassword = `رمز جدید نباید بیش از ${maxLength.toLocaleString('fa-IR')} نویسه باشد.`;
  else if (values.newPassword === values.currentPassword) errors.newPassword = 'رمز جدید نباید با رمز فعلی یکسان باشد.';
  if (!values.confirmNewPassword) errors.confirmNewPassword = 'تکرار رمز جدید را وارد کنید.';
  else if (values.confirmNewPassword !== values.newPassword) errors.confirmNewPassword = 'تکرار رمز جدید با رمز جدید یکسان نیست.';
  return errors;
}

/** Maps the server's field errors to the form fields; everything else becomes one alert message. */
function mapServerError(error: unknown): { fields: PasswordFieldErrors; general: string | null } {
  const fields: PasswordFieldErrors = {};
  const general: string[] = [];
  if (error instanceof ApiError && error.fieldErrors) {
    for (const [key, messages] of Object.entries(error.fieldErrors)) {
      // Keys may carry a path prefix such as `$.newPassword` or `request.newPassword`.
      const bareKey = key.split('.').pop() ?? key;
      const field = fieldOrder.find((candidate) => candidate.toLowerCase() === bareKey.toLowerCase());
      const text = messages.join(' ');
      if (!text) continue;
      if (field) fields[field] = fields[field] ? `${fields[field]} ${text}` : text;
      else general.push(text);
    }
    if (Object.keys(fields).length === 0 && general.length === 0) general.push(error.message);
  } else if (error instanceof ApiError || error instanceof UnauthenticatedError) {
    general.push(error.message);
  } else {
    general.push(genericError);
  }
  return { fields, general: general.length > 0 ? general.join(' ') : null };
}

type SecurityTabProps = {
  /** Owned by the drawer so an in-flight request survives leaving the tab and blocks a second submit. */
  submitting: boolean;
  onSubmittingChange: (submitting: boolean) => void;
  onPasswordChanged: () => void;
  onUnauthenticated: (error: unknown) => boolean;
};

export function SecurityTab({ submitting, onSubmittingChange, onPasswordChanged, onUnauthenticated }: SecurityTabProps) {
  const [values, setValues] = useState<Record<PasswordField, string>>({ currentPassword: '', newPassword: '', confirmNewPassword: '' });
  const [fieldErrors, setFieldErrors] = useState<PasswordFieldErrors>({});
  const [alertMessage, setAlertMessage] = useState<string | null>(null);
  const [focusToken, setFocusToken] = useState(0);
  const submittingRef = useRef(false);
  const inputRefs = useRef<Record<PasswordField, HTMLInputElement | null>>({ currentPassword: null, newPassword: null, confirmNewPassword: null });
  const alertRef = useRef<HTMLDivElement>(null);

  // After a failed attempt, move focus to the first invalid field, or to the alert when no field is invalid.
  useEffect(() => {
    if (focusToken === 0) return;
    const firstInvalid = fieldOrder.find((field) => fieldErrors[field]);
    if (firstInvalid) inputRefs.current[firstInvalid]?.focus();
    else alertRef.current?.focus();
    // Only a new failed attempt moves focus, not every later edit of the errors.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [focusToken]);

  function update(field: PasswordField, value: string) {
    setValues((previous) => ({ ...previous, [field]: value }));
    setFieldErrors((previous) => (previous[field] ? { ...previous, [field]: undefined } : previous));
  }

  function clearAll() {
    setValues({ currentPassword: '', newPassword: '', confirmNewPassword: '' });
    setFieldErrors({});
    setAlertMessage(null);
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (submittingRef.current || submitting) return;
    setAlertMessage(null);
    const errors = validatePasswordChange(values);
    if (Object.keys(errors).length > 0) {
      setFieldErrors(errors);
      setFocusToken((token) => token + 1);
      return;
    }
    setFieldErrors({});
    submittingRef.current = true;
    onSubmittingChange(true);
    try {
      await changeMyPassword(values);
      // The server revoked every session; the form stays locked while the caller redirects to sign-in.
      setValues({ currentPassword: '', newPassword: '', confirmNewPassword: '' });
      onPasswordChanged();
    } catch (error: unknown) {
      if (error instanceof UnauthenticatedError && onUnauthenticated(error)) return;
      const { fields, general } = mapServerError(error);
      setFieldErrors(fields);
      setAlertMessage(general);
      // A rejected current password is never kept on screen; a generic 4xx may be that rejection too.
      if (fields.currentPassword || error instanceof ApiError) setValues((previous) => ({ ...previous, currentPassword: '' }));
      setFocusToken((token) => token + 1);
      submittingRef.current = false;
      onSubmittingChange(false);
    }
  }

  return (
    <div className="space-y-4">
      <div>
        <h3 className="font-extrabold text-ink">تغییر رمز عبور</h3>
        <p className="mt-1 text-xs leading-6 text-sub">
          برای تغییر رمز، رمز فعلی را وارد کنید. پس از موفقیت از همه نشست‌های قبلی خارج می‌شوید و باید دوباره وارد شوید.
        </p>
      </div>
      <form
        noValidate
        onSubmit={(event) => void handleSubmit(event)}
        className="space-y-4 rounded-xl border border-line bg-white p-4 sm:p-5"
      >
        <FormField label="رمز فعلی" required error={fieldErrors.currentPassword}>
          <PasswordInput
            fieldName="رمز فعلی"
            ref={(node) => { inputRefs.current.currentPassword = node; }}
            value={values.currentPassword}
            onChange={(event) => update('currentPassword', event.target.value)}
            autoComplete="current-password"
            maxLength={maxLength}
          />
        </FormField>
        <FormField
          label="رمز جدید"
          required
          hint={`رمز جدید باید حداقل ${minLength.toLocaleString('fa-IR')} و حداکثر ${maxLength.toLocaleString('fa-IR')} نویسه داشته باشد و در فهرست رمزهای رایج نباشد.`}
          error={fieldErrors.newPassword}
        >
          <PasswordInput
            fieldName="رمز جدید"
            ref={(node) => { inputRefs.current.newPassword = node; }}
            value={values.newPassword}
            onChange={(event) => update('newPassword', event.target.value)}
            autoComplete="new-password"
            maxLength={maxLength}
          />
        </FormField>
        <FormField label="تکرار رمز جدید" required error={fieldErrors.confirmNewPassword}>
          <PasswordInput
            fieldName="تکرار رمز جدید"
            ref={(node) => { inputRefs.current.confirmNewPassword = node; }}
            value={values.confirmNewPassword}
            onChange={(event) => update('confirmNewPassword', event.target.value)}
            autoComplete="new-password"
            maxLength={maxLength}
          />
        </FormField>
        {alertMessage ? <Alert ref={alertRef} tone="error">{alertMessage}</Alert> : null}
        <div className="flex flex-wrap items-center justify-end gap-2 border-t border-line pt-4">
          <button
            type="button"
            onClick={clearAll}
            disabled={submitting}
            className="min-h-11 rounded-lg border border-line px-4 py-2.5 text-xs font-bold text-sub outline-none hover:bg-cool focus-visible:ring-2 focus-visible:ring-gold disabled:opacity-60"
          >
            پاک‌کردن
          </button>
          <button
            type="submit"
            disabled={submitting}
            className="inline-flex min-h-11 items-center gap-2 rounded-lg bg-teal-deep px-5 py-2.5 text-xs font-bold text-white outline-none transition hover:bg-navy focus-visible:ring-2 focus-visible:ring-gold disabled:cursor-wait disabled:opacity-60"
          >
            <KeyRound size={15} aria-hidden="true" />
            {submitting ? 'در حال ثبت…' : 'تغییر رمز و خروج'}
          </button>
        </div>
      </form>
    </div>
  );
}
