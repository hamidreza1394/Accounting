import { useState } from 'react';
import { ApiError } from '@/api/client';
import { Alert } from '@/components/ui/Alert';
import { usePageBreadcrumb } from '@/components/shell/PageBreadcrumb';
import { Button } from '@/components/ui/button';
import { FormField, Input, Textarea } from '@/components/ui/FormField';
import { FormSection } from '@/components/ui/FormSection';
import { useDocumentTitle, useNavigate } from '@/lib/router';
import { createHrPosition } from '../api/hrClient';
import { validateCode, validateDescription, validateRank, validateRequiredName, buildCreatePositionBody } from '../lib/hrValidation';
import { useFormGuard } from '@/lib/formGuard';
import { useFormExit } from '../components/HrFormExit';
import { ExternalLink } from 'lucide-react';

type Props = {
  onUnauthenticated: (error: unknown) => boolean;
  companyId: string;
  /** Local Form Flow (PR 5): rendered inside the list's tab; receives the created position. */
  onSaved?: (position: { id: string; name: string }) => void;
  /** Where انصراف returns to; defaults to the list route of this family. */
  onExit?: () => void;
  /** Shows the optional «باز کردن در تب کاری جدید» action (only in the in-tab flow). */
  showOpenInNewTab?: boolean;
};

type FormState = { code: string; name: string; description: string; rank: string };

const initialForm: FormState = { code: '', name: '', description: '', rank: '' };

/**
 * Creating a position (`POST /org/positions`). The code is unique per company; the rank is an
 * optional ordering number. A position never carries permissions — it is not a role (D-072).
 */
export function HrPositionCreatePage({ onUnauthenticated, companyId, onSaved, onExit, showOpenInNewTab = false }: Props) {
  const navigate = useNavigate();
  const [form, setForm] = useState<FormState>(initialForm);
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<string, string>>>({});
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [dirty, setDirty] = useState(false);
  // The workspace asks this registry before closing the tab, signing out or changing context;
  // a failed save keeps the `error` status until the user retries or cancels (D-076). The
  // registry also owns the `beforeunload` warning for every non-clean state.
  const formGuard = useFormGuard({ dirty, saving: submitting, errorMessage: error, title: 'سمت سازمانی جدید' });
  useDocumentTitle('ایجاد سمت سازمانی');
  const { requestExit, exitBlocked, exitDialog } = useFormExit(formGuard, () => (onExit ? onExit() : navigate('/hr/org/positions')));

  const update = (patch: Partial<FormState>) => {
    setForm((current) => ({ ...current, ...patch }));
    setDirty(true);
  };

  const submit = async () => {
    setError(null);
    const errors: Partial<Record<string, string>> = {};
    const codeError = validateCode(form.code, 'کد سمت');
    if (codeError) errors.code = codeError;
    const nameError = validateRequiredName(form.name, 'عنوان سمت', 200);
    if (nameError) errors.name = nameError;
    const descriptionError = validateDescription(form.description);
    if (descriptionError) errors.description = descriptionError;
    const rankError = validateRank(form.rank);
    if (rankError) errors.rank = rankError;
    setFieldErrors(errors);
    if (Object.keys(errors).length > 0) return;

    setSubmitting(true);
    try {
      const created = await createHrPosition(companyId, buildCreatePositionBody(form));
      // Saved: closing this tab (or leaving the workspace) needs no warning anymore.
      formGuard.markClean();
      if (onSaved) {
        // Local Form Flow: back to the same list tab, which refreshes itself.
        onSaved({ id: created.id, name: form.name });
        return;
      }
      await navigate(`/hr/org/positions/${created.id}`);
    } catch (cause: unknown) {
      if (onUnauthenticated(cause)) return;
      setError(cause instanceof ApiError ? cause.message : 'ثبت سمت انجام نشد. لطفاً دوباره تلاش کنید.');
      setSubmitting(false);
    }
  };

  usePageBreadcrumb([{ label: 'منابع انسانی' }, { label: 'سمت‌های سازمانی', to: '/hr/org/positions' }, { label: 'سمت جدید' }]);
  return (
    <div className="space-y-5">
      <header className="space-y-2">
        <h1 className="text-2xl font-extrabold tracking-tight text-title">ایجاد سمت سازمانی</h1>
        <p className="text-sm text-sub">کد سمت در این شرکت یکتاست. یک سمت را می‌توان در چند واحد سازمانی به کار برد.</p>
      </header>

      {error ? <Alert tone="error">{error}</Alert> : null}

      <form
        onSubmit={(event) => {
          event.preventDefault();
          void submit();
        }}
        className="space-y-5"
      >
        <FormSection title="اطلاعات سمت" description="کد، عنوان، توضیح و رتبهٔ اختیاری سمت.">
          <FormField label="کد سمت" required error={fieldErrors.code} hint="مثال: ACC یا HR.MGR — حداکثر ۵۰ نویسه لاتین">
            <Input value={form.code} maxLength={50} dir="ltr" autoComplete="off" onChange={(event) => update({ code: event.target.value })} />
          </FormField>
          <FormField label="عنوان سمت" required error={fieldErrors.name}>
            <Input value={form.name} maxLength={200} onChange={(event) => update({ name: event.target.value })} />
          </FormField>
          <FormField label="رتبه (اختیاری)" error={fieldErrors.rank} hint="عدد صحیح برای مرتب‌سازی نمایشی؛ خالی = بدون رتبه.">
            <Input value={form.rank} dir="ltr" inputMode="numeric" autoComplete="off" onChange={(event) => update({ rank: event.target.value })} />
          </FormField>
          <FormField label="توضیحات" error={fieldErrors.description} className="sm:col-span-2">
            <Textarea value={form.description} maxLength={1000} onChange={(event) => update({ description: event.target.value })} />
          </FormField>
        </FormSection>

        <div className="flex flex-wrap items-center gap-2">
          <Button type="submit" size="lg" disabled={submitting}>
            {submitting ? 'در حال ثبت…' : 'ثبت سمت'}
          </Button>
          <Button type="button" variant="outline" size="lg" disabled={exitBlocked} onClick={requestExit}>
            انصراف
          </Button>
          {showOpenInNewTab ? (
            <Button type="button" variant="ghost" size="lg" onClick={() => navigate('/hr/org/positions/new')}>
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
