import { useEffect, useRef, useState } from 'react';
import { Alert } from '@/components/ui/Alert';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/ui/ConfirmDialog';
import { FormField, Input } from '@/components/ui/FormField';
import { FormSection } from '@/components/ui/FormSection';
import { SearchableCombobox } from '@/components/ui/SearchableCombobox';
import { useFormGuard } from '@/lib/formGuard';
import { useFormExit } from '@/features/hr/components/HrFormExit';
import type { ProductDetail, ProductWriteBody } from '../api/tradeTypes';
import {
  buildProductBody,
  isProductFormDirty,
  productFormFromDetail,
  productLimits,
  validateProductForm,
  type ProductFieldErrors,
  type ProductFieldKey,
  type ProductFormState,
} from '../lib/productValidation';
import { mapProductFailure, type TradeFailure } from '../lib/tradeErrors';
import { useUnits } from './useUnits';

type Props = {
  /** Title of the form for the workspace's unsaved-changes lists. */
  guardTitle: string;
  /** The last server state (empty for a new product); «dirty» means different from it. */
  baseline: ProductFormState;
  submitLabel: string;
  savingLabel: string;
  /** The request; it throws the shared `ApiError` on failure. */
  save: (body: ProductWriteBody) => Promise<ProductDetail>;
  onSaved: (product: ProductDetail) => void;
  /** Leaves the form (after the unsaved-changes confirmation when needed). */
  onExit: () => void;
  onUnauthenticated: (error: unknown) => boolean;
  /**
   * Edit only: re-reads the product after a concurrency conflict WITHOUT unmounting the form (the page just
   * replaces its stored row version) and returns the fresh detail. It throws the shared `ApiError` on failure.
   */
  onRefresh?: () => Promise<ProductDetail>;
};

const errorFocusSelector = '[aria-invalid="true"], [data-invalid="true"]';

/**
 * The shared create/edit form of a product: sections (identity, classification, unit), required markers,
 * client-side validation that mirrors the server rules, server error mapping (400 fields, 409 by
 * machine code, 403), the workspace form lifecycle (dirty/saving/error) and a confirmed exit. All
 * entered data stays in the form after any failure.
 */
export function ProductEditor({
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
  const units = useUnits(onUnauthenticated);
  const [form, setForm] = useState<ProductFormState>(baseline);
  const [fieldErrors, setFieldErrors] = useState<ProductFieldErrors>({});
  const [failure, setFailure] = useState<TradeFailure<ProductFieldKey> | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [focusTick, setFocusTick] = useState(0);
  const [refreshing, setRefreshing] = useState(false);
  const [refreshError, setRefreshError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [discardOpen, setDiscardOpen] = useState(false);
  const formRef = useRef<HTMLFormElement>(null);
  const alertRef = useRef<HTMLDivElement>(null);

  const dirty = isProductFormDirty(form, baseline);
  const guard = useFormGuard({ dirty, saving: submitting, errorMessage: failure?.message ?? null, title: guardTitle });
  const { requestExit, exitBlocked, exitDialog } = useFormExit(guard, onExit, 'اطلاعات کالا');

  // After a failed validation or save, focus moves to the first invalid field, else to the message.
  useEffect(() => {
    if (focusTick === 0) return;
    const invalid = formRef.current?.querySelector<HTMLElement>(errorFocusSelector);
    if (invalid) invalid.focus();
    else alertRef.current?.focus();
  }, [focusTick]);

  const update = (patch: Partial<ProductFormState>) => {
    setForm((current) => ({ ...current, ...patch }));
    // The error of an edited field is stale as soon as the user changes it.
    setFieldErrors((current) => {
      const next = { ...current };
      for (const key of Object.keys(patch) as (keyof ProductFormState)[]) delete next[key];
      return next;
    });
  };

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
        setForm(productFormFromDetail(fresh));
        setFieldErrors({});
        setNotice('مقادیر تازهٔ ثبت‌شده در سرور نمایش داده شد؛ ویرایش‌های قبلی شما کنار گذاشته شد.');
      } else {
        setNotice(
          'این کالا در این فاصله توسط کاربر دیگری تغییر کرده بود. نسخهٔ تازه دریافت شد و مقادیر شما حفظ شده است؛ ذخیره‌کردن، تغییرات آن کاربر را بازنویسی می‌کند.',
        );
      }
      setFailure(null);
      setDiscardOpen(false);
    } catch (cause: unknown) {
      if (onUnauthenticated(cause)) return;
      // The form (and everything typed) stays; only an inline message is added.
      setRefreshError(mapProductFailure(cause).message);
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
    const errors = validateProductForm(form, units.codes);
    setFieldErrors(errors);
    if (Object.keys(errors).length > 0) {
      setFocusTick((tick) => tick + 1);
      return;
    }
    setSubmitting(true);
    try {
      const saved = await save(buildProductBody(form));
      // Saved: leaving needs no warning anymore.
      guard.markClean();
      onSaved(saved);
    } catch (cause: unknown) {
      if (onUnauthenticated(cause)) return;
      const mapped = mapProductFailure(cause);
      setFailure(mapped);
      setFieldErrors(mapped.fieldErrors);
      setSubmitting(false);
      setFocusTick((tick) => tick + 1);
    }
  };

  // The duplicate code is shown on its field; every other failure also gets the message box.
  const showAlert = failure !== null && failure.kind !== 'duplicate-code';
  const selectedUnit = form.baseUnitCode ? { value: form.baseUnitCode, code: form.baseUnitCode, label: form.baseUnitCode } : null;

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
        <FormSection title="مشخصات" description="کد یکتا و نام کالا در این شرکت.">
          <FormField label="کد" required error={fieldErrors.code} hint="حداکثر ۶۴ نویسه؛ در این شرکت یکتاست.">
            <Input
              value={form.code}
              maxLength={productLimits.code}
              dir="ltr"
              autoComplete="off"
              disabled={submitting}
              onChange={(event) => update({ code: event.target.value })}
            />
          </FormField>
          <FormField label="نام" required error={fieldErrors.name}>
            <Input
              value={form.name}
              maxLength={productLimits.name}
              autoComplete="off"
              disabled={submitting}
              onChange={(event) => update({ name: event.target.value })}
            />
          </FormField>
        </FormSection>

        <FormSection title="طبقه‌بندی" description="اختیاری؛ گروه کالا و کد تعرفهٔ گمرکی.">
          <FormField label="کد گروه کالا" error={fieldErrors.productGroupCode} hint="اختیاری؛ حداکثر ۶۴ نویسه.">
            <Input
              value={form.productGroupCode}
              maxLength={productLimits.productGroupCode}
              dir="ltr"
              autoComplete="off"
              disabled={submitting}
              onChange={(event) => update({ productGroupCode: event.target.value })}
            />
          </FormField>
          <FormField label="کد تعرفه (HS)" error={fieldErrors.hsCode} hint="اختیاری؛ متن لاتین (مثلاً 0101.21)، حداکثر ۱۶ نویسه.">
            <Input
              value={form.hsCode}
              dir="ltr"
              inputMode="text"
              autoComplete="off"
              disabled={submitting}
              onChange={(event) => update({ hsCode: event.target.value })}
            />
          </FormField>
        </FormSection>

        <FormSection title="واحد" description="واحد پایهٔ اندازه‌گیری کالا از فهرست مرجع.">
          <FormField
            label="واحد پایه"
            required
            error={fieldErrors.baseUnitCode}
            hint={
              units.status === 'error' ? (
                <span>
                  دریافت فهرست واحدها انجام نشد؛ مقدار فعلی حفظ می‌شود.{' '}
                  <button
                    type="button"
                    onClick={units.retry}
                    className="rounded font-medium text-teal-deep underline-offset-4 outline-none hover:underline focus-visible:ring-2 focus-visible:ring-gold"
                  >
                    تلاش دوباره
                  </button>
                </span>
              ) : (
                'با نام فارسی یا انگلیسی یا کد واحد جست‌وجو کنید.'
              )
            }
          >
            <SearchableCombobox
              value={form.baseUnitCode}
              options={units.options}
              selectedOption={selectedUnit}
              disabled={submitting || (units.status === 'loading' && form.baseUnitCode === null)}
              placeholder={units.status === 'loading' ? 'در حال دریافت فهرست واحدها…' : 'جست‌وجو و انتخاب واحد…'}
              listLabel="فهرست واحدها"
              clearLabel="پاک‌کردن واحد"
              onChange={(code) => update({ baseUnitCode: code })}
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
