import { useEffect, useState } from 'react';
import { ApiError } from '@/api/client';
import { Alert } from '@/components/ui/Alert';
import { usePageBreadcrumb } from '@/components/shell/PageBreadcrumb';
import { Button } from '@/components/ui/button';
import { FormField, Input } from '@/components/ui/FormField';
import { FormSection } from '@/components/ui/FormSection';
import { JalaliDateField } from '@/components/ui/JalaliDateField';
import { useNavigate } from '@/lib/router';
import { createHrEmployee } from '../api/hrClient';
import { buildCreateEmployeeBody, validateEmployeeNumber, validateRequiredName } from '../lib/hrValidation';
import { useFormGuard } from '@/lib/formGuard';

type Props = {
  onUnauthenticated: (error: unknown) => boolean;
  companyId: string;
};

type FormState = { employeeNumber: string; firstName: string; lastName: string; hiredOnIsoDay: string };

const initialForm: FormState = { employeeNumber: '', firstName: '', lastName: '', hiredOnIsoDay: '' };

/**
 * Creating an employee (`POST /org/employees`). All four fields of the DTO are here; the hire
 * date is optional. No user account is created and none is asked for — account association is a
 * separate, optional workflow on the detail page (D-072).
 */
export function HrEmployeeCreatePage({ onUnauthenticated, companyId }: Props) {
  const navigate = useNavigate();
  const [form, setForm] = useState<FormState>(initialForm);
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<string, string>>>({});
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [dirty, setDirty] = useState(false);
  // The workspace asks this registry before closing the tab, signing out or changing context;
  // a failed save keeps the `error` status until the user retries or cancels (D-076).
  const formGuard = useFormGuard({ dirty, saving: submitting, errorMessage: error, title: 'کارمند جدید' });

  const update = (patch: Partial<FormState>) => {
    setForm((current) => ({ ...current, ...patch }));
    setDirty(true);
  };

  useEffect(() => {
    if (!dirty) return undefined;
    const beforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
    };
    window.addEventListener('beforeunload', beforeUnload);
  return () => window.removeEventListener('beforeunload', beforeUnload);
  }, [dirty]);

  const submit = async () => {
    setError(null);
    const errors: Partial<Record<string, string>> = {};
    const numberError = validateEmployeeNumber(form.employeeNumber, true);
    if (numberError) errors.employeeNumber = numberError;
    const firstError = validateRequiredName(form.firstName, 'نام', 100);
    if (firstError) errors.firstName = firstError;
    const lastError = validateRequiredName(form.lastName, 'نام خانوادگی', 100);
    if (lastError) errors.lastName = lastError;
    setFieldErrors(errors);
    if (Object.keys(errors).length > 0) return;

    setSubmitting(true);
    try {
      const created = await createHrEmployee(companyId, buildCreateEmployeeBody(form));
      // Saved: closing this tab (or leaving the workspace) needs no warning anymore.
      formGuard.markClean();
      await navigate(`/hr/org/employees/${created.id}`);
    } catch (cause: unknown) {
      if (onUnauthenticated(cause)) return;
      setError(cause instanceof ApiError ? cause.message : 'ثبت کارمند انجام نشد. لطفاً دوباره تلاش کنید.');
      setSubmitting(false);
    }
  };

  usePageBreadcrumb([{ label: 'منابع انسانی' }, { label: 'کارکنان', to: '/hr/org/employees' }, { label: 'کارمند جدید' }]);

  return (
    <div className="space-y-5">
      <header className="space-y-2">
        <h1 className="text-2xl font-extrabold tracking-tight text-title">ثبت کارمند</h1>
        <p className="text-sm text-sub">
          شماره پرسنلی در این شرکت یکتاست و پس از ثبت تغییر نمی‌کند. ثبت کارمند نیازی به ساخت حساب کاربری ندارد.
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
        <FormSection title="اطلاعات پایه" description="نام و نام خانوادگی کارمند و شماره پرسنلی.">
          <FormField label="شماره پرسنلی" required error={fieldErrors.employeeNumber} hint="مثال: EMP-101 — حداکثر ۳۰ نویسه لاتین، یکتا در شرکت">
            <Input
              value={form.employeeNumber}
              maxLength={30}
              dir="ltr"
              autoComplete="off"
              onChange={(event) => update({ employeeNumber: event.target.value })}
            />
          </FormField>
          <FormField label="نام" required error={fieldErrors.firstName}>
            <Input value={form.firstName} maxLength={100} onChange={(event) => update({ firstName: event.target.value })} />
          </FormField>
          <FormField label="نام خانوادگی" required error={fieldErrors.lastName}>
            <Input value={form.lastName} maxLength={100} onChange={(event) => update({ lastName: event.target.value })} />
          </FormField>
          <FormField label="تاریخ شروع همکاری" hint="اختیاری؛ مبنای کنترل تاریخ انتساب‌هاست.">
            <JalaliDateField value={form.hiredOnIsoDay} onChange={(value) => update({ hiredOnIsoDay: value })} />
          </FormField>
        </FormSection>

        <FormSection title="ساختار سازمانی" description="پس از ثبت، از صفحهٔ کارمند انتساب به واحد و سمت را آغاز کنید؛ این فرم عمداً انتساب ندارد تا تراکنش انتساب سالم بماند.">
          <p className="sm:col-span-2 text-sm leading-6 text-sub">
            انتساب سازمانی (واحد، سمت و مدیر) در صفحهٔ خود کارمند و با قواعد سرور ثبت می‌شود.
          </p>
        </FormSection>

        <div className="flex flex-wrap gap-2">
          <Button type="submit" size="lg" disabled={submitting}>
            {submitting ? 'در حال ثبت…' : 'ثبت کارمند'}
          </Button>
          <Button type="button" variant="outline" size="lg" disabled={submitting} onClick={() => navigate('/hr/org/employees')}>
            انصراف
          </Button>
        </div>
      </form>
    </div>
  );
}
