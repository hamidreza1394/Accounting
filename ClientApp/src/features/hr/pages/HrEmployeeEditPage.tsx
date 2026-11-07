import { useCallback, useEffect, useRef, useState } from 'react';
import { ApiError } from '@/api/client';
import { Alert } from '@/components/ui/Alert';
import { usePageBreadcrumb } from '@/components/shell/PageBreadcrumb';
import type { BreadcrumbItem } from '@/components/ui/Breadcrumb';
import { Button } from '@/components/ui/button';
import { FormField, Input, Select } from '@/components/ui/FormField';
import { FormSection } from '@/components/ui/FormSection';
import { JalaliDateField } from '@/components/ui/JalaliDateField';
import { useDocumentTitle, useNavigate, useParams } from '@/lib/router';
import { getHrEmployee, updateHrEmployee } from '../api/hrClient';
import type { HrEmployeeDetail } from '../api/hrTypes';
import { hrEmploymentStatusLabel } from '../lib/hrLabels';
import { buildUpdateEmployeeBody, validateEmployeeNumber, validateRequiredName } from '../lib/hrValidation';
import { useFormGuard } from '@/lib/formGuard';

type Props = {
  onUnauthenticated: (error: unknown) => boolean;
  companyId: string;
};

type FormState = { firstName: string; lastName: string; employmentStatus: 1 | 2; hiredOnIsoDay: string };

/**
 * Editing the employee's basic/employment data (`PUT /org/employees/{id}`). The server contract:
 * the employee number is immutable, `hiredOnUtc: null` keeps the stored value (clearing is not
 * possible), `employmentStatus: 3` is rejected (termination only via deactivate), and a
 * terminated employee cannot be edited at all — the page says so instead of sending a doomed
 * request. `rowVersion` is mandatory; a stale token answers 409 and the form re-reads.
 */
export function HrEmployeeEditPage({ onUnauthenticated, companyId }: Props) {
  const { employeeId } = useParams();
  const navigate = useNavigate();

  const [employee, setEmployee] = useState<HrEmployeeDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [failure, setFailure] = useState<{ message: string; status: number | null } | null>(null);
  const [attempt, setAttempt] = useState(0);

  const [form, setForm] = useState<FormState | null>(null);
  // The snapshot of the last server read: any edit creates a new object, so an identity
  // comparison is the whole dirty check for this small form (no framework needed).
  const savedFormRef = useRef<FormState | null>(null);
  const dirty = form !== null && savedFormRef.current !== null && form !== savedFormRef.current;
  // The workspace asks this registry before closing the tab, signing out or changing context.
  const formGuard = useFormGuard(dirty);
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<string, string>>>({});
  const [submitting, setSubmitting] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);

  const readEmployee = useCallback(
    (signal: AbortSignal) =>
      getHrEmployee(companyId, employeeId ?? '', signal).then((detail) => {
        setEmployee(detail);
        const loaded: FormState | null =
          detail.employmentStatus === 3
            ? null
            : {
                firstName: detail.firstName,
                lastName: detail.lastName,
                employmentStatus: detail.employmentStatus === 2 ? 2 : 1,
                hiredOnIsoDay: toIsoDay(detail.hiredOnUtc),
              };
        savedFormRef.current = loaded;
        setForm(loaded);
      }),
    [companyId, employeeId],
  );

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setFailure(null);
    readEmployee(controller.signal)
      .then(() => {
        if (!controller.signal.aborted) setLoading(false);
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || onUnauthenticated(cause)) return;
        const status = cause instanceof ApiError ? cause.status : null;
        setFailure({
          status,
          message:
            status === 403
              ? 'شما مجوز ویرایش کارکنان این شرکت (hr.employees.update) را ندارید.'
              : status === 404
                ? 'کارمند پیدا نشد یا به آن دسترسی ندارید.'
                : cause instanceof ApiError
                  ? cause.message
                  : 'دریافت اطلاعات کارمند انجام نشد. لطفاً دوباره تلاش کنید.',
        });
        setLoading(false);
      });
    return () => controller.abort();
  }, [companyId, employeeId, attempt, readEmployee, onUnauthenticated]);

  useDocumentTitle(employee ? `ویرایش ${employee.firstName} ${employee.lastName}` : null);

  const submit = async () => {
    if (employee === null || form === null) return;
    setSaveError(null);
    const errors: Partial<Record<string, string>> = {};
    const firstError = validateRequiredName(form.firstName, 'نام', 100);
    if (firstError) errors.firstName = firstError;
    const lastError = validateRequiredName(form.lastName, 'نام خانوادگی', 100);
    if (lastError) errors.lastName = lastError;
    // The number is immutable on this route; it is not editable but still echoed for context.
    const numberNote = validateEmployeeNumber(employee.employeeNumber, true);
    void numberNote;
    setFieldErrors(errors);
    if (Object.keys(errors).length > 0) return;

    setSubmitting(true);
    try {
      await updateHrEmployee(companyId, employee.id, buildUpdateEmployeeBody({ ...form, rowVersion: employee.rowVersion }));
      // Saved: closing this tab (or leaving the workspace) needs no warning anymore.
      formGuard.markClean();
      await navigate(`/hr/org/employees/${employee.id}`);
    } catch (cause: unknown) {
      if (onUnauthenticated(cause)) return;
      setSaveError(cause instanceof ApiError ? cause.message : 'ذخیرهٔ تغییرات انجام نشد. لطفاً دوباره تلاش کنید.');
      if (cause instanceof ApiError && cause.status === 409) setAttempt((value) => value + 1);
      setSubmitting(false);
    }
  };

  const crumbs: BreadcrumbItem[] = [
    { label: 'منابع انسانی' },
    { label: 'کارکنان', to: '/hr/org/employees' },
    employee ? { label: `${employee.firstName} ${employee.lastName}`, to: `/hr/org/employees/${employee.id}` } : { label: 'ویرایش' },
  ];
  usePageBreadcrumb(crumbs);
  if (loading) {
    return (
      <div className="space-y-4">
        <p role="status" className="rounded-xl border border-line bg-white p-5 text-sub">
          در حال بارگذاری…
        </p>
      </div>
    );
  }

  if (failure || employee === null) {
    return (
      <div className="space-y-4">
        <div role="alert" className="rounded-xl border border-line bg-white p-6 text-center">
          <p className="text-lg font-bold text-ink">یافت نشد یا دسترسی ندارید</p>
          <p className="mt-2 text-sm text-sub">{failure?.message}</p>
          <Button
            type="button"
            variant="outline"
            size="lg"
            className="mt-4"
            onClick={() => navigate(employee === null ? '/hr/org/employees' : `/hr/org/employees/${employee.id}`)}
          >
            بازگشت
          </Button>
        </div>
      </div>
    );
  }

  const terminated = employee.employmentStatus === 3;

  return (
    <div className="space-y-5">
      <header className="space-y-2">
        <h1 className="text-2xl font-extrabold tracking-tight text-title">ویرایش کارمند</h1>
        <p className="text-sm text-sub">
          شماره پرسنلی <code dir="ltr">{employee.employeeNumber}</code> پس از ثبت تغییرنکردنی است.
        </p>
      </header>

      {terminated ? (
        <Alert tone="warning" action={<Button type="button" variant="outline" size="lg" onClick={() => navigate(`/hr/org/employees/${employee.id}`)}>بازگشت به پرونده</Button>}>
          کارمند خاتمه‌همکاری از این مسیر قابل ویرایش یا فعال‌سازی مجدد نیست (رفتار سرور: 409/400). برای ثبت نیروی جدید، رکورد پرسنلی جداگانه بسازید.
        </Alert>
      ) : form !== null ? (
        <>
          {saveError ? <Alert tone="error">{saveError}</Alert> : null}
          <form
            onSubmit={(event) => {
              event.preventDefault();
              void submit();
            }}
            className="space-y-5"
          >
            <FormSection title="اطلاعات پایه" description="نام، نام خانوادگی و وضعیت اشتغال. خاتمه همکاری فقط از دکمهٔ مخصوص در پروندهٔ کارمند انجام می‌شود.">
              <FormField label="نام" required error={fieldErrors.firstName}>
                <Input value={form.firstName} maxLength={100} onChange={(event) => setForm({ ...form, firstName: event.target.value })} />
              </FormField>
              <FormField label="نام خانوادگی" required error={fieldErrors.lastName}>
                <Input value={form.lastName} maxLength={100} onChange={(event) => setForm({ ...form, lastName: event.target.value })} />
              </FormField>
              <FormField label="وضعیت اشتغال">
                <Select
                  value={String(form.employmentStatus)}
                  onChange={(event) => setForm({ ...form, employmentStatus: event.target.value === '2' ? 2 : 1 })}
                >
                  <option value="1">{hrEmploymentStatusLabel[1]}</option>
                  <option value="2">{hrEmploymentStatusLabel[2]}</option>
                </Select>
              </FormField>
              <FormField
                label="تاریخ شروع همکاری"
                hint={employee.hiredOnUtc === null ? 'هیچ تاریخی ثبت نشده؛ انتخاب تاریخ، آن را ثبت می‌کند.' : 'خالی‌کردن این فیلد، تاریخ فعلی را حفظ می‌کند (حذف تاریخ ممکن نیست).'}
              >
                <JalaliDateField value={form.hiredOnIsoDay} onChange={(value) => setForm({ ...form, hiredOnIsoDay: value })} />
              </FormField>
            </FormSection>

            <div className="flex flex-wrap gap-2">
              <Button type="submit" size="lg" disabled={submitting}>
                {submitting ? 'در حال ذخیره…' : 'ذخیرهٔ تغییرات'}
              </Button>
              <Button type="button" variant="outline" size="lg" disabled={submitting} onClick={() => navigate(`/hr/org/employees/${employee.id}`)}>
                انصراف
              </Button>
            </div>
          </form>
        </>
      ) : null}
    </div>
  );
}

/** The local ISO calendar day of a `*Utc` instant, for the Jalali edit field. */
function toIsoDay(instant: string | null): string {
  if (instant === null) return '';
  const date = new Date(instant);
  if (Number.isNaN(date.getTime())) return '';
  const pad = (value: number): string => String(value).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}
