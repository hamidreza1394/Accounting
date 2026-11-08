import { useState } from 'react';
import { ApiError } from '@/api/client';
import { Alert } from '@/components/ui/Alert';
import { usePageBreadcrumb } from '@/components/shell/PageBreadcrumb';
import { Button } from '@/components/ui/button';
import { FormField, Input } from '@/components/ui/FormField';
import { FormSection } from '@/components/ui/FormSection';
import { JalaliDateField } from '@/components/ui/JalaliDateField';
import { useDocumentTitle, useNavigate } from '@/lib/router';
import { useFormGuard } from '@/lib/formGuard';
import { ExternalLink } from 'lucide-react';
import { createHrEmployee } from '../api/hrClient';
type CreatedEmployee = { id: string; firstName: string; lastName: string };
import { buildCreateEmployeeBody, validateEmployeeNumber, validateRequiredName } from '../lib/hrValidation';
import { useFormExit } from '../components/HrFormExit';

type Props = {
  onUnauthenticated: (error: unknown) => boolean;
  companyId: string;
  /**
   * Local Form Flow (PR 5): the list page renders this form INSIDE its own workspace tab and
   * receives the created record instead of a navigation. Omitted only on the standalone route.
   */
  onSaved?: (employee: CreatedEmployee) => void;
  /** Where انصراف returns to; defaults to the list route of this family. */
  onExit?: () => void;
  /** Shows the optional «باز کردن در تب کاری جدید» action (only in the in-tab flow). */
  showOpenInNewTab?: boolean;
};

type FormState = { employeeNumber: string; firstName: string; lastName: string; hiredOnIsoDay: string };

const initialForm: FormState = { employeeNumber: '', firstName: '', lastName: '', hiredOnIsoDay: '' };

/**
 * Creating an employee (`POST /org/employees`). All four fields of the DTO are here; the hire
 * date is optional. No user account is created and none is asked for — account association is a
 * separate, optional workflow on the detail page (D-072).
 */
export function HrEmployeeCreatePage({ onUnauthenticated, companyId, onSaved, onExit, showOpenInNewTab = false }: Props) {
  const navigate = useNavigate();
  const [form, setForm] = useState<FormState>(initialForm);
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<string, string>>>({});
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [dirty, setDirty] = useState(false);
  // The workspace asks this registry before closing the tab, signing out or changing context;
  // a failed save keeps the `error` status until the user retries or cancels (D-076). The
  // registry also owns the `beforeunload` warning for every non-clean state.
  const formGuard = useFormGuard({ dirty, saving: submitting, errorMessage: error, title: 'کارمند جدید' });
  // The standalone route (deep link) names the browser tab. Inside the list's tab the parent
  // tab keeps its route title: the workspace tab must stay in step with the real route
  // (`UI_COMPONENTS.md`: route title, then the declared RECORD name — a create form has no
  // record to name yet), and the test `HrIntegration.test.tsx` pins exactly that contract.
  useDocumentTitle(onSaved ? null : 'ثبت کارمند');
  const { requestExit, exitBlocked, exitDialog } = useFormExit(formGuard, () => (onExit ? onExit() : navigate('/hr/org/employees')));

  const update = (patch: Partial<FormState>) => {
    setForm((current) => ({ ...current, ...patch }));
    setDirty(true);
  };

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
      if (onSaved) {
        // Local Form Flow: back to the same list tab, which refreshes itself.
        onSaved({ id: created.id, firstName: form.firstName, lastName: form.lastName });
        return;
      }
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

        <div className="flex flex-wrap items-center gap-2">
          <Button type="submit" size="lg" disabled={submitting}>
            {submitting ? 'در حال ثبت…' : 'ثبت کارمند'}
          </Button>
          <Button type="button" variant="outline" size="lg" disabled={exitBlocked} onClick={requestExit}>
            انصراف
          </Button>
          {showOpenInNewTab ? (
            <Button type="button" variant="ghost" size="lg" onClick={() => navigate('/hr/org/employees/new')}>
              <ExternalLink size={16} aria-hidden="true" />
              باز کردن در تب کاری جدید
            </Button>
          ) : null}
          {exitBlocked ? <span className="text-xs text-sub">ذخیره در حال انجام است؛ تا پایان عملیات امکان انصراف نیست.</span> : null}
        </div>
      </form>
      {exitDialog}
    </div>
  );
}
