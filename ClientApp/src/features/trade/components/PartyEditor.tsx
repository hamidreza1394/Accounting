import { useEffect, useRef, useState } from 'react';
import { Alert } from '@/components/ui/Alert';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/ui/ConfirmDialog';
import { FormField, Input } from '@/components/ui/FormField';
import { FormSection } from '@/components/ui/FormSection';
import { SearchableCombobox } from '@/components/ui/SearchableCombobox';
import { useFormGuard } from '@/lib/formGuard';
import { useFormExit } from '@/features/hr/components/HrFormExit';
import { partyRoles, type PartyDetail, type PartyRole, type PartyWriteBody } from '../api/tradeTypes';
import {
  buildPartyBody,
  formFromDetail,
  isPartyFormDirty,
  partyLimits,
  validatePartyForm,
  type PartyFieldErrors,
  type PartyFormState,
} from '../lib/partyValidation';
import { mapTradeFailure, type TradeFailure } from '../lib/tradeErrors';
import { partyRoleLabel } from '../lib/tradeLabels';
import { useCountries } from './useCountries';

type Props = {
  /** Title of the form for the workspace's unsaved-changes lists. */
  guardTitle: string;
  /** The last server state (empty for a new party); «dirty» means different from it. */
  baseline: PartyFormState;
  submitLabel: string;
  savingLabel: string;
  /** The request; it throws the shared `ApiError` on failure. */
  save: (body: PartyWriteBody) => Promise<PartyDetail>;
  onSaved: (party: PartyDetail) => void;
  /** Leaves the form (after the unsaved-changes confirmation when needed). */
  onExit: () => void;
  onUnauthenticated: (error: unknown) => boolean;
  /**
   * Edit only: re-reads the party after a concurrency conflict WITHOUT unmounting the form (the page just
   * replaces its stored row version) and returns the fresh detail. It throws the shared `ApiError` on failure.
   */
  onRefresh?: () => Promise<PartyDetail>;
};

const errorFocusSelector = '[aria-invalid="true"], [data-invalid="true"]';

/**
 * The shared create/edit form of a party: sections (identity, roles, country), required markers,
 * client-side validation that mirrors the server rules, server error mapping (400 fields, 409 by
 * machine code, 403), the workspace form lifecycle (dirty/saving/error) and a confirmed exit. All
 * entered data stays in the form after any failure.
 */
export function PartyEditor({
  guardTitle,
  baseline,
  submitLabel,
  savingLabel,
  save,
  onSaved,
  onExit,
  onUnauthenticated,
  onRefresh,
}: Props) {
  const countries = useCountries(onUnauthenticated);
  const [form, setForm] = useState<PartyFormState>(baseline);
  const [fieldErrors, setFieldErrors] = useState<PartyFieldErrors>({});
  const [failure, setFailure] = useState<TradeFailure | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [focusTick, setFocusTick] = useState(0);
  const [refreshing, setRefreshing] = useState(false);
  const [refreshError, setRefreshError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [discardOpen, setDiscardOpen] = useState(false);
  const formRef = useRef<HTMLFormElement>(null);
  const alertRef = useRef<HTMLDivElement>(null);

  const dirty = isPartyFormDirty(form, baseline);
  const guard = useFormGuard({ dirty, saving: submitting, errorMessage: failure?.message ?? null, title: guardTitle });
  const { requestExit, exitBlocked, exitDialog } = useFormExit(guard, onExit, 'اطلاعات طرف‌حساب');

  // After a failed validation or save, focus moves to the first invalid field, else to the message.
  useEffect(() => {
    if (focusTick === 0) return;
    const invalid = formRef.current?.querySelector<HTMLElement>(errorFocusSelector);
    if (invalid) invalid.focus();
    else alertRef.current?.focus();
  }, [focusTick]);

  const update = (patch: Partial<PartyFormState>) => {
    setForm((current) => ({ ...current, ...patch }));
    // The error of an edited field is stale as soon as the user changes it.
    setFieldErrors((current) => {
      const next = { ...current };
      for (const key of Object.keys(patch) as (keyof PartyFormState)[]) delete next[key];
      return next;
    });
  };

  const toggleRole = (role: PartyRole, checked: boolean) =>
    update({ roles: checked ? [...form.roles, role] : form.roles.filter((current) => current !== role) });

  /**
   * After a concurrency conflict: read the fresh record (silently, the form stays mounted and keeps every typed
   * value). `discard` additionally replaces the form values with the fresh ones (only after the user confirmed).
   */
  const refresh = async (discard: boolean) => {
    if (!onRefresh || refreshing) return;
    setRefreshing(true);
    setRefreshError(null);
    try {
      const fresh = await onRefresh();
      if (discard) {
        setForm(formFromDetail(fresh));
        setFieldErrors({});
        setNotice('مقادیر تازهٔ ثبت‌شده در سرور نمایش داده شد؛ ویرایش‌های قبلی شما کنار گذاشته شد.');
      } else {
        setNotice(
          'این طرف‌حساب در این فاصله توسط کاربر دیگری تغییر کرده بود. نسخهٔ تازه دریافت شد و مقادیر شما حفظ شده است؛ ذخیره‌کردن، تغییرات آن کاربر را بازنویسی می‌کند.',
        );
      }
      setFailure(null);
      setDiscardOpen(false);
    } catch (cause: unknown) {
      if (onUnauthenticated(cause)) return;
      // The form (and everything typed) stays; only an inline message is added.
      setRefreshError(mapTradeFailure(cause).message);
      setDiscardOpen(false);
    } finally {
      setRefreshing(false);
    }
  };

  const submit = async () => {
    if (submitting) return;
    setFailure(null);
    setNotice(null);
    setRefreshError(null);
    const errors = validatePartyForm(form, countries.codes);
    setFieldErrors(errors);
    if (Object.keys(errors).length > 0) {
      setFocusTick((tick) => tick + 1);
      return;
    }
    setSubmitting(true);
    try {
      const saved = await save(buildPartyBody(form));
      // Saved: leaving needs no warning anymore.
      guard.markClean();
      onSaved(saved);
    } catch (cause: unknown) {
      if (onUnauthenticated(cause)) return;
      const mapped = mapTradeFailure(cause);
      setFailure(mapped);
      setFieldErrors(mapped.fieldErrors);
      setSubmitting(false);
      setFocusTick((tick) => tick + 1);
    }
  };

  // The duplicate code is shown on its field; every other failure also gets the message box.
  const showAlert = failure !== null && failure.kind !== 'duplicate-code';
  const selectedCountry = form.countryCode ? { value: form.countryCode, code: form.countryCode, label: form.countryCode } : null;
  const rolesError = fieldErrors.roles;

  return (
    <>
      {showAlert ? (
        <Alert
          ref={alertRef}
          tone="error"
          action={
            failure.kind === 'concurrency' && onRefresh ? (
              <div className="flex flex-wrap gap-2">
                <Button type="button" variant="outline" size="lg" disabled={submitting || refreshing} onClick={() => void refresh(false)}>
                  {refreshing ? 'در حال دریافت…' : 'دریافت نسخهٔ تازه و حفظ ویرایش‌های من'}
                </Button>
                <Button type="button" variant="outline" size="lg" disabled={submitting || refreshing} onClick={() => setDiscardOpen(true)}>
                  نمایش مقادیر تازه
                </Button>
              </div>
            ) : undefined
          }
        >
          {failure.message}
        </Alert>
      ) : null}
      {refreshError ? <Alert tone="error">{`دریافت نسخهٔ تازه انجام نشد؛ فرم و مقادیر شما حفظ شد. ${refreshError}`}</Alert> : null}
      {notice ? <Alert tone="info">{notice}</Alert> : null}

      <form
        ref={formRef}
        noValidate
        aria-busy={submitting || undefined}
        onSubmit={(event) => {
          event.preventDefault();
          void submit();
        }}
        className="space-y-5"
      >
        <FormSection title="هویت" description="کد یکتا و نام طرف‌حساب در این شرکت.">
          <FormField label="کد" required error={fieldErrors.code} hint="حداکثر ۶۴ نویسه؛ در این شرکت یکتاست.">
            <Input
              value={form.code}
              maxLength={partyLimits.code}
              dir="ltr"
              autoComplete="off"
              disabled={submitting}
              onChange={(event) => update({ code: event.target.value })}
            />
          </FormField>
          <FormField label="نام حقوقی" required error={fieldErrors.legalName}>
            <Input
              value={form.legalName}
              maxLength={partyLimits.legalName}
              autoComplete="off"
              disabled={submitting}
              onChange={(event) => update({ legalName: event.target.value })}
            />
          </FormField>
          <FormField label="نام نمایشی" error={fieldErrors.displayName} hint="اختیاری؛ اگر خالی باشد نام حقوقی نمایش داده می‌شود.">
            <Input
              value={form.displayName}
              maxLength={partyLimits.displayName}
              autoComplete="off"
              disabled={submitting}
              onChange={(event) => update({ displayName: event.target.value })}
            />
          </FormField>
          <FormField label="شناسهٔ حقوقی" error={fieldErrors.legalIdentifier} hint="اختیاری؛ شناسهٔ ثبتی یا مالیاتی طرف‌حساب. فقط در صفحهٔ جزئیات و ویرایش نمایش داده می‌شود.">
            <Input
              value={form.legalIdentifier}
              maxLength={partyLimits.legalIdentifier}
              dir="ltr"
              autoComplete="off"
              disabled={submitting}
              onChange={(event) => update({ legalIdentifier: event.target.value })}
            />
          </FormField>
        </FormSection>

        <FormSection title="نقش‌ها" description="دست‌کم یک نقش انتخاب کنید؛ یک طرف‌حساب می‌تواند چند نقش داشته باشد.">
          <fieldset
            className="sm:col-span-2"
            aria-describedby={rolesError ? 'party-roles-error' : undefined}
            data-invalid={rolesError ? 'true' : undefined}
            tabIndex={rolesError ? -1 : undefined}
          >
            <legend className="mb-2 text-sm font-semibold text-ink">
              نقش‌های طرف‌حساب
              <span aria-hidden="true" className="ms-1 text-danger">
                *
              </span>
            </legend>
            <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
              {partyRoles.map((role) => (
                <label key={role} className="flex min-h-10 items-center gap-2 rounded-lg border border-line px-3 py-2 text-sm text-ink has-[:checked]:border-teal-deep has-[:checked]:bg-teal-soft">
                  <input
                    type="checkbox"
                    checked={form.roles.includes(role)}
                    disabled={submitting}
                    onChange={(event) => toggleRole(role, event.target.checked)}
                    className="size-5 shrink-0 accent-teal-deep outline-none focus-visible:ring-2 focus-visible:ring-gold disabled:cursor-not-allowed"
                  />
                  {partyRoleLabel[role]}
                </label>
              ))}
            </div>
            {rolesError ? (
              <p id="party-roles-error" aria-live="polite" className="mt-2 text-xs font-medium leading-5 text-danger">
                {rolesError}
              </p>
            ) : null}
          </fieldset>
        </FormSection>

        <FormSection title="کشور" description="اختیاری؛ کشور محل ثبت یا فعالیت طرف‌حساب.">
          <FormField
            label="کشور"
            error={fieldErrors.countryCode}
            hint={
              countries.status === 'error' ? (
                <span>
                  دریافت فهرست کشورها انجام نشد؛ مقدار فعلی حفظ می‌شود.{' '}
                  <button
                    type="button"
                    onClick={countries.retry}
                    className="rounded font-medium text-teal-deep underline-offset-4 outline-none hover:underline focus-visible:ring-2 focus-visible:ring-gold"
                  >
                    تلاش دوباره
                  </button>
                </span>
              ) : (
                'با نام فارسی یا انگلیسی یا کد دوحرفی جست‌وجو کنید.'
              )
            }
          >
            <SearchableCombobox
              value={form.countryCode}
              options={countries.options}
              selectedOption={selectedCountry}
              disabled={submitting || (countries.status === 'loading' && form.countryCode === null)}
              placeholder={countries.status === 'loading' ? 'در حال دریافت فهرست کشورها…' : 'جست‌وجو و انتخاب کشور…'}
              listLabel="فهرست کشورها"
              clearLabel="پاک‌کردن کشور"
              onChange={(code) => update({ countryCode: code })}
            />
          </FormField>
        </FormSection>

        <div className="flex flex-wrap items-center gap-2">
          <Button type="submit" size="lg" disabled={submitting}>
            {submitting ? savingLabel : submitLabel}
          </Button>
          <Button type="button" variant="outline" size="lg" disabled={exitBlocked} onClick={requestExit}>
            انصراف
          </Button>
          {exitBlocked ? <span className="text-xs text-sub">ذخیره در حال انجام است؛ تا پایان عملیات امکان انصراف نیست.</span> : null}
        </div>
      </form>
      {exitDialog}
      <ConfirmDialog
        open={discardOpen}
        title="نمایش مقادیر تازه"
        description="با ادامه، مقادیر واردشدهٔ شما کنار گذاشته می‌شود و فرم با اطلاعات تازهٔ ثبت‌شده در سرور پر می‌شود."
        confirmLabel="کنار گذاشتن ویرایش‌های من"
        cancelLabel="ادامهٔ ویرایش"
        tone="danger"
        busy={refreshing}
        onConfirm={() => refresh(true)}
        onCancel={() => setDiscardOpen(false)}
      />
    </>
  );
}
