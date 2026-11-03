import { useEffect, useId, useRef, useState, type ComponentProps, type FormEvent } from 'react';
import { Eye, EyeOff } from 'lucide-react';
import { ApiError } from '../../../api/client';
import { Alert } from '../../ui/Alert';
import { ConfirmDialog } from '../../ui/ConfirmDialog';
import { FormField, Input } from '../../ui/FormField';
import { FormSection } from '../../ui/FormSection';
import { Button } from '../../ui/button';
import {
  faDigits,
  fieldNames,
  isUserFormDirty,
  limits,
  validateUserForm,
  type FieldErrors,
  type FieldName,
  type UserFormValues,
} from './userFormValidation';

type Mode = 'create' | 'edit';

type UserFormProps = {
  mode: Mode;
  initialValues: UserFormValues;
  /** Sends the values; it throws (an `ApiError` or any error) when the save failed. */
  onSubmit: (values: UserFormValues) => Promise<void>;
  /** Leaves the form without saving (only called after the discard confirmation when dirty). */
  onCancel: () => void;
  onUnauthenticated: (error: unknown) => boolean;
};

const fallbackMessage = 'ذخیرهٔ اطلاعات انجام نشد. لطفاً دوباره تلاش کنید.';

/** A password box with a show/hide toggle; the value is only kept in component state. */
function PasswordInput({ className, ...props }: ComponentProps<'input'>) {
  const [visible, setVisible] = useState(false);
  const label = props['aria-label'] ?? 'رمز عبور';
  return (
    // The wrapper is ltr so the toggle (`end-0`), the padding (`pe-11`) and the text (`text-start`)
    // all refer to the same side, whatever the page direction is.
    <div className="relative" dir="ltr">
      <Input
        {...props}
        type={visible ? 'text' : 'password'}
        dir="ltr"
        spellCheck={false}
        autoCapitalize="none"
        autoCorrect="off"
        className={`pe-11 text-start ${className ?? ''}`}
      />
      <button
        type="button"
        aria-pressed={visible}
        aria-label={visible ? `پنهان‌کردن ${label}` : `نمایش ${label}`}
        onClick={() => setVisible((value) => !value)}
        className="absolute inset-y-0 end-0 flex w-10 items-center justify-center rounded-e-lg text-sub outline-none hover:text-ink focus-visible:ring-2 focus-visible:ring-gold"
      >
        {visible ? <EyeOff aria-hidden="true" size={16} /> : <Eye aria-hidden="true" size={16} />}
      </button>
    </div>
  );
}

/**
 * The create/edit form of a user, shared by both pages. It owns the typed values, validation
 * (on blur and on submit), the server error mapping, the double-submit guard and the unsaved
 * changes guard. The password exists only in this component's state.
 */
export function UserForm({ mode, initialValues, onSubmit, onCancel, onUnauthenticated }: UserFormProps) {
  const formRef = useRef<HTMLFormElement>(null);
  const alertRef = useRef<HTMLDivElement>(null);
  const submittingRef = useRef(false);
  const doneRef = useRef(false);
  const statusHintId = useId();

  const [values, setValues] = useState<UserFormValues>(initialValues);
  const [touched, setTouched] = useState<Partial<Record<FieldName, boolean>>>({});
  const [submitAttempted, setSubmitAttempted] = useState(false);
  const [serverErrors, setServerErrors] = useState<FieldErrors>({});
  const [alertMessage, setAlertMessage] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [focusTick, setFocusTick] = useState(0);
  const [deactivateOpen, setDeactivateOpen] = useState(false);
  const [discardOpen, setDiscardOpen] = useState(false);

  const clientErrors = validateUserForm(values, mode);
  const dirty = isUserFormDirty(values, initialValues);
  const visibleFields = fieldNames.filter((name) => mode === 'create' || (name !== 'password' && name !== 'confirmPassword'));

  const errorFor = (name: FieldName): string | undefined =>
    serverErrors[name] ?? (touched[name] || submitAttempted ? clientErrors[name] : undefined);

  // Moves focus to the first invalid field, or to the message box when no field is at fault.
  useEffect(() => {
    if (focusTick === 0) return;
    const firstInvalid = formRef.current?.querySelector<HTMLElement>('[aria-invalid="true"]');
    (firstInvalid ?? alertRef.current)?.focus();
  }, [focusTick]);

  // A browser warning while unsaved changes exist; removed as soon as the form is saved or left.
  useEffect(() => {
    if (!dirty) return undefined;
    const warn = (event: BeforeUnloadEvent) => {
      if (doneRef.current) return;
      event.preventDefault();
      event.returnValue = '';
    };
    window.addEventListener('beforeunload', warn);
    return () => window.removeEventListener('beforeunload', warn);
  }, [dirty]);

  const change = <K extends keyof UserFormValues>(key: K, value: UserFormValues[K]) => {
    setValues((current) => ({ ...current, [key]: value }));
    const name = fieldNames.find((candidate) => candidate === key);
    if (name) {
      setServerErrors((current) => {
        if (!current[name]) return current;
        const { [name]: _removed, ...rest } = current;
        return rest;
      });
    }
  };

  const blur = (name: FieldName) => setTouched((current) => (current[name] ? current : { ...current, [name]: true }));

  const showServerFailure = (cause: unknown) => {
    if (!(cause instanceof ApiError)) {
      setAlertMessage(fallbackMessage);
      return;
    }
    const mapped: FieldErrors = {};
    const rest: string[] = [];
    for (const [key, messages] of Object.entries(cause.fieldErrors ?? {})) {
      const field = visibleFields.find((name) => name === key);
      if (field) mapped[field] = messages.join(' ');
      else rest.push(...messages);
    }
    setServerErrors(mapped);
    // Field errors are shown at their fields; the Alert carries what has no field, or the status
    // message when the server named no field at all.
    if (rest.length > 0) setAlertMessage(rest.join(' '));
    else setAlertMessage(Object.keys(mapped).length > 0 ? null : cause.message);
  };

  const send = async () => {
    if (submittingRef.current) return;
    submittingRef.current = true;
    setSubmitting(true);
    setAlertMessage(null);
    setServerErrors({});
    try {
      await onSubmit(values);
      doneRef.current = true;
    } catch (cause) {
      if (onUnauthenticated(cause)) return;
      showServerFailure(cause);
      setFocusTick((tick) => tick + 1);
    } finally {
      submittingRef.current = false;
      setSubmitting(false);
    }
  };

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (submittingRef.current || (mode === 'edit' && !dirty)) return;
    setSubmitAttempted(true);
    if (Object.keys(clientErrors).length > 0) {
      setFocusTick((tick) => tick + 1);
      return;
    }
    if (mode === 'edit' && initialValues.isActive && !values.isActive) {
      setDeactivateOpen(true);
      return;
    }
    void send();
  };

  const requestCancel = () => {
    if (submittingRef.current) return;
    if (dirty) setDiscardOpen(true);
    else onCancel();
  };

  const field = (name: FieldName) => ({
    value: values[name],
    onChange: (event: { target: { value: string } }) => change(name, event.target.value),
    onBlur: () => blur(name),
  });

  return (
    <form ref={formRef} noValidate onSubmit={handleSubmit} aria-label={mode === 'create' ? 'فرم ایجاد کاربر' : 'فرم ویرایش کاربر'} className="space-y-5">
      {alertMessage ? (
        <Alert ref={alertRef} tone="error">
          {alertMessage}
        </Alert>
      ) : null}

      <FormSection title="اطلاعات هویتی">
        <FormField label="نام" error={errorFor('firstName')}>
          <Input {...field('firstName')} autoComplete="off" />
        </FormField>
        <FormField label="نام خانوادگی" error={errorFor('lastName')}>
          <Input {...field('lastName')} autoComplete="off" />
        </FormField>
      </FormSection>

      <FormSection title="اطلاعات ورود">
        <FormField label="نام کاربری" required error={errorFor('username')} hint={`${faDigits(limits.usernameMin)} تا ${faDigits(limits.usernameMax)} نویسه؛ برای ورود به سامانه استفاده می‌شود.`}>
          <Input {...field('username')} dir="ltr" autoComplete="off" autoCapitalize="none" spellCheck={false} className="text-start" />
        </FormField>
        {mode === 'create' ? (
          <>
            <FormField label="رمز عبور" required error={errorFor('password')} hint={`دست‌کم ${faDigits(limits.passwordMin)} نویسه؛ رمزهای رایج و قابل‌حدس پذیرفته نمی‌شوند.`}>
              <PasswordInput {...field('password')} autoComplete="new-password" />
            </FormField>
            <FormField label="تکرار رمز عبور" required error={errorFor('confirmPassword')}>
              <PasswordInput {...field('confirmPassword')} aria-label="تکرار رمز عبور" autoComplete="new-password" />
            </FormField>
          </>
        ) : null}
      </FormSection>

      <FormSection title="اطلاعات تماس">
        <FormField label="ایمیل" required error={errorFor('emailAddress')}>
          <Input {...field('emailAddress')} type="email" dir="ltr" autoComplete="off" spellCheck={false} className="text-start" />
        </FormField>
        <FormField label="شماره موبایل" error={errorFor('cellPhoneNumber')} hint={`اختیاری؛ ${faDigits(limits.mobileLength)} رقم لاتین، مانند 09123456789.`}>
          <Input {...field('cellPhoneNumber')} type="tel" inputMode="tel" dir="ltr" autoComplete="off" className="text-start" />
        </FormField>
      </FormSection>

      <FormSection title="وضعیت">
        <div className="sm:col-span-2">
          <label className="flex cursor-pointer items-start gap-3">
            <input
              type="checkbox"
              checked={values.isActive}
              onChange={(event) => change('isActive', event.target.checked)}
              aria-describedby={statusHintId}
              className="mt-0.5 size-5 shrink-0 cursor-pointer accent-teal-deep outline-none focus-visible:ring-2 focus-visible:ring-gold"
            />
            <span className="text-sm font-semibold text-ink">حساب فعال باشد</span>
          </label>
          <p id={statusHintId} className="mt-1.5 ps-8 text-xs leading-5 text-sub">
            {values.isActive ? 'کاربر می‌تواند وارد سامانه شود.' : 'حساب غیرفعال است و کاربر نمی‌تواند وارد سامانه شود.'}
          </p>
        </div>
      </FormSection>

      <div className="flex flex-wrap items-center gap-2">
        <Button type="submit" size="lg" disabled={submitting || (mode === 'edit' && !dirty)} aria-busy={submitting || undefined}>
          {submitting ? 'در حال ذخیره…' : mode === 'create' ? 'ایجاد کاربر' : 'ذخیرهٔ تغییرات'}
        </Button>
        <Button type="button" variant="outline" size="lg" disabled={submitting} onClick={requestCancel}>
          انصراف
        </Button>
        {mode === 'edit' && !dirty ? <span className="text-xs text-sub">تغییری برای ذخیره وجود ندارد.</span> : null}
      </div>

      <ConfirmDialog
        open={deactivateOpen}
        tone="danger"
        title="غیرفعال‌کردن حساب کاربر"
        description="با غیرفعال‌کردن این حساب، کاربر دیگر نمی‌تواند وارد سامانه شود. اطلاعات و عضویت‌های او حذف نمی‌شود و می‌توانید بعداً حساب را دوباره فعال کنید."
        confirmLabel="غیرفعال‌کردن و ذخیره"
        cancelLabel="انصراف"
        busy={submitting}
        onConfirm={async () => {
          // Close first: the dialog gives focus back to its opener when it closes, and that must
          // happen before a failure moves focus to the message box.
          setDeactivateOpen(false);
          await send();
        }}
        onCancel={() => setDeactivateOpen(false)}
      />
      <ConfirmDialog
        open={discardOpen}
        tone="danger"
        title="تغییرات ذخیره‌نشده"
        description="تغییرات واردشده ذخیره نشده‌اند و با خروج از دست می‌روند. خارج می‌شوید؟"
        confirmLabel="خروج بدون ذخیره"
        cancelLabel="ادامه ویرایش"
        onConfirm={() => {
          doneRef.current = true;
          setDiscardOpen(false);
          onCancel();
        }}
        onCancel={() => setDiscardOpen(false)}
      />
    </form>
  );
}
