import { useEffect, useState } from 'react';
import { ApiError } from '@/api/client';
import { Alert } from '@/components/ui/Alert';
import { usePageBreadcrumb } from '@/components/shell/PageBreadcrumb';
import { Button } from '@/components/ui/button';
import { FormField, Input, Textarea } from '@/components/ui/FormField';
import { FormSection } from '@/components/ui/FormSection';
import { JalaliDateField } from '@/components/ui/JalaliDateField';
import { useNavigate } from '@/lib/router';
import { createHrUnit } from '../api/hrClient';
import { validateCode, validateDescription, validateRequiredName, validateValidityWindow, buildCreateUnitBody } from '../lib/hrValidation';
import { useFormGuard } from '@/lib/formGuard';
import { HrUnitParentPicker } from '../components/HrUnitParentPicker';

type Props = {
  onUnauthenticated: (error: unknown) => boolean;
  companyId: string;
};

type FormState = {
  code: string;
  name: string;
  description: string;
  parentUnitId: string | null;
  validFromIsoDay: string;
  validToIsoDay: string;
};

const initialForm: FormState = {
  code: '',
  name: '',
  description: '',
  parentUnitId: null,
  validFromIsoDay: '',
  validToIsoDay: '',
};

/**
 * Creating an organizational unit (`POST /org/units`). The code is unique per company/tenant and
 * follows the server pattern; the parent must be an active unit of the same company; the validity
 * window is optional. Server answers stay authoritative and are shown as-is.
 */
export function HrUnitCreatePage({ onUnauthenticated, companyId }: Props) {
  const navigate = useNavigate();
  const [form, setForm] = useState<FormState>(initialForm);
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<string, string>>>({});
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [dirty, setDirty] = useState(false);
  // The workspace asks this registry before closing the tab, signing out or changing context;
  // a failed save keeps the `error` status until the user retries or cancels (D-076).
  const formGuard = useFormGuard({ dirty, saving: submitting, errorMessage: error, title: 'واحد سازمانی جدید' });

  const update = (patch: Partial<FormState>) => {
    setForm((current) => ({ ...current, ...patch }));
    setDirty(true);
  };

  // The company switch or sign-out must not silently lose the draft.
  useEffect(() => {
    if (!dirty) return undefined;
    const beforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
    };
    window.addEventListener('beforeunload', beforeUnload);
  return () => window.removeEventListener('beforeunload', beforeUnload);
  }, [dirty]);

  const validate = (): boolean => {
    const errors: Partial<Record<string, string>> = {};
    const codeError = validateCode(form.code, 'کد واحد');
    if (codeError) errors.code = codeError;
    const nameError = validateRequiredName(form.name, 'عنوان واحد', 200);
    if (nameError) errors.name = nameError;
    const descriptionError = validateDescription(form.description);
    if (descriptionError) errors.description = descriptionError;
    const windowError = validateValidityWindow(form.validFromIsoDay, form.validToIsoDay);
    if (windowError) errors.validToIsoDay = windowError;
    setFieldErrors(errors);
    return Object.keys(errors).length === 0;
  };

  const submit = async () => {
    setError(null);
    if (!validate()) return;
    setSubmitting(true);
    try {
      const created = await createHrUnit(companyId, buildCreateUnitBody(form));
      // Saved: closing this tab (or leaving the workspace) needs no warning anymore.
      formGuard.markClean();
      await navigate(`/hr/org/units/${created.id}`);
    } catch (cause: unknown) {
      if (onUnauthenticated(cause)) return;
      setError(cause instanceof ApiError ? cause.message : 'ثبت واحد انجام نشد. لطفاً دوباره تلاش کنید.');
      setSubmitting(false);
    }
  };

  usePageBreadcrumb([{ label: 'منابع انسانی' }, { label: 'واحدهای سازمانی', to: '/hr/org/units' }, { label: 'واحد جدید' }]);

  return (
    <div className="space-y-5">
      <header className="space-y-2">
        <h1 className="text-2xl font-extrabold tracking-tight text-title">ایجاد واحد سازمانی</h1>
        <p className="text-sm text-sub">
          کد واحد در این شرکت یکتاست و پس از ثبت قابل تغییر نیست. واحد والد باید واحدی فعال از همین شرکت باشد.
        </p>
      </header>

      {error ? <Alert tone="error">{error}</Alert> : null}

      <form
        onSubmit={(event) => {
          event.preventDefault();
          void submit();
        }}
        className="space-y-5"
      >
        <FormSection title="اطلاعات واحد" description="کد، عنوان و توضیح واحد سازمانی.">
          <FormField label="کد واحد" required error={fieldErrors.code} hint="مثال: HQ یا SALES.01 — حداکثر ۵۰ نویسه لاتین">
            <Input
              value={form.code}
              maxLength={50}
              dir="ltr"
              autoComplete="off"
              onChange={(event) => update({ code: event.target.value })}
            />
          </FormField>
          <FormField label="عنوان واحد" required error={fieldErrors.name}>
            <Input value={form.name} maxLength={200} onChange={(event) => update({ name: event.target.value })} />
          </FormField>
          <FormField label="توضیحات" error={fieldErrors.description} className="sm:col-span-2">
            <Textarea value={form.description} maxLength={1000} onChange={(event) => update({ description: event.target.value })} />
          </FormField>
        </FormSection>

        <FormSection title="جایگاه در ساختار" description="واحد والد اختیاری است؛ بدون والد، واحد در سطح ریشه قرار می‌گیرد.">
          <FormField label="واحد والد" className="sm:col-span-2">
            <HrUnitParentPicker companyId={companyId} value={form.parentUnitId} onValueChange={(key) => update({ parentUnitId: key })} />
          </FormField>
        </FormSection>

        <FormSection title="بازه اعتبار" description="اختیاری. برای تعیین دورهٔ اعتبار ساختاری واحد (مثلاً یک شعبهٔ موقت) استفاده می‌شود.">
          <FormField label="شروع اعتبار">
            <JalaliDateField value={form.validFromIsoDay} onChange={(value) => update({ validFromIsoDay: value })} />
          </FormField>
          <FormField label="پایان اعتبار" error={fieldErrors.validToIsoDay}>
            <JalaliDateField value={form.validToIsoDay} onChange={(value) => update({ validToIsoDay: value })} />
          </FormField>
        </FormSection>

        <div className="flex flex-wrap gap-2">
          <Button type="submit" size="lg" disabled={submitting}>
            {submitting ? 'در حال ثبت…' : 'ثبت واحد'}
          </Button>
          <Button
            type="button"
            variant="outline"
            size="lg"
            disabled={submitting}
            onClick={() => navigate('/hr/org/units')}
          >
            انصراف
          </Button>
        </div>
      </form>
    </div>
  );
}
