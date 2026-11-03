import { useEffect, useId, useMemo, useRef, useState, type FormEvent } from 'react';
import { ApiError, createCompanyRole, getPermissionTemplates } from '../../../api/client';
import type { PermissionTemplate } from '../../../api/types';
import { Link, useNavigate, useSearchParams } from '../../../lib/router';
import { ErrorState, LoadingState } from '../../Feedback';
import { Alert } from '../../ui/Alert';
import { Breadcrumb } from '../../ui/Breadcrumb';
import { ConfirmDialog } from '../../ui/ConfirmDialog';
import { FormField, Input, Select } from '../../ui/FormField';
import { FormSection } from '../../ui/FormSection';
import { PermissionTree } from '../../ui/PermissionTree';
import { Button } from '../../ui/button';
import { normalizeText } from '../users/userFormValidation';
import { setRoleFlash } from './roleFlash';
import { delegationNote, normalizeRoleCode, roleNameMax, sameCodes, validateRoleFields, type RoleFieldErrors } from './roleValidation';
import { CompanySelect } from './CompanySelect';
import { newRolePath, resolveCompanyId, rolePath, rolesPath, useCompanyChoices } from './useCompanyChoices';
import { usePermissionCatalog } from './usePermissionCatalog';

type Props = { onUnauthenticated: (error: unknown) => boolean; defaultCompanyId: string };

const fallbackMessage = 'ذخیرهٔ گروه دسترسی انجام نشد. لطفاً دوباره تلاش کنید.';

export function RoleCreatePage({ onUnauthenticated, defaultCompanyId }: Props) {
  const [params] = useSearchParams();
  const companyId = resolveCompanyId(params, defaultCompanyId);
  const navigate = useNavigate();
  const { state: catalogState, retry } = usePermissionCatalog(onUnauthenticated);
  const { state: choices, retry: retryCompanies } = useCompanyChoices(onUnauthenticated);
  const unknownCompany = choices.kind === 'ready' && !choices.companies.some((company) => company.id === companyId);
  const companyName = choices.kind === 'ready' ? choices.companies.find((company) => company.id === companyId)?.name : undefined;
  const listPath = rolesPath(companyId);

  const formRef = useRef<HTMLFormElement>(null);
  const alertRef = useRef<HTMLDivElement>(null);
  const submittingRef = useRef(false);
  const doneRef = useRef(false);
  const templateId = useId();

  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [selection, setSelection] = useState<string[]>([]);
  const [templates, setTemplates] = useState<PermissionTemplate[] | 'failed' | null>(null);
  const [templateCode, setTemplateCode] = useState('');
  /** The selection the last applied template produced; a different selection counts as user edits. */
  const [baseline, setBaseline] = useState<string[]>([]);
  const [pendingTemplate, setPendingTemplate] = useState<PermissionTemplate | null>(null);
  const [touched, setTouched] = useState<{ code?: boolean; name?: boolean }>({});
  const [submitAttempted, setSubmitAttempted] = useState(false);
  const [serverErrors, setServerErrors] = useState<RoleFieldErrors>({});
  const [alertMessage, setAlertMessage] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [focusTick, setFocusTick] = useState(0);
  const [discardOpen, setDiscardOpen] = useState(false);

  useEffect(() => {
    const controller = new AbortController();
    getPermissionTemplates(controller.signal)
      .then((result) => {
        if (!controller.signal.aborted) setTemplates(result);
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || onUnauthenticated(cause)) return;
        setTemplates('failed');
      });
    return () => controller.abort();
  }, [onUnauthenticated]);

  // The select shows a template only while the selection still equals what it produced.
  const shownTemplate = templateCode !== '' && sameCodes(selection, baseline) ? templateCode : '';
  const catalog = catalogState.kind === 'ready' ? catalogState.catalog : null;
  const knownCodes = useMemo(() => new Set(catalog?.map((item) => item.code)), [catalog]);

  const clientErrors = validateRoleFields({ code, name }, 'create');
  const dirty = code.trim() !== '' || name.trim() !== '' || selection.length > 0;
  const errorFor = (field: keyof RoleFieldErrors): string | undefined =>
    serverErrors[field] ?? (touched[field] || submitAttempted ? clientErrors[field] : undefined);

  useEffect(() => {
    if (focusTick === 0) return;
    const firstInvalid = formRef.current?.querySelector<HTMLElement>('[aria-invalid="true"]');
    (firstInvalid ?? alertRef.current)?.focus();
  }, [focusTick]);

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

  const applyTemplate = (template: PermissionTemplate) => {
    const next = template.permissionCodes.filter((permission) => knownCodes.has(permission));
    setSelection(next);
    setBaseline(next);
    setTemplateCode(template.code);
  };

  const chooseTemplate = (value: string) => {
    if (value === '') {
      // "No template" only clears the label; the current selection is the user's and stays.
      setTemplateCode('');
      return;
    }
    const template = Array.isArray(templates) ? templates.find((item) => item.code === value) : undefined;
    if (!template) return;
    if (sameCodes(selection, baseline)) applyTemplate(template);
    else setPendingTemplate(template);
  };

  const showServerFailure = (cause: unknown) => {
    if (!(cause instanceof ApiError)) {
      setAlertMessage(fallbackMessage);
      return;
    }
    const mapped: RoleFieldErrors = {};
    const rest: string[] = [];
    for (const [key, messages] of Object.entries(cause.fieldErrors ?? {})) {
      if (key === 'code' || key === 'name') mapped[key] = messages.join(' ');
      else rest.push(...messages);
    }
    // A duplicate code answers 409 without naming the field.
    if (cause.status === 409 && Object.keys(mapped).length === 0) mapped.code = cause.message;
    setServerErrors(mapped);
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
      const created = await createCompanyRole(companyId, {
        code: normalizeRoleCode(code),
        name: normalizeText(name),
        permissionCodes: selection,
      });
      doneRef.current = true;
      setRoleFlash(created.id, 'created');
      navigate(rolePath(companyId, created.id));
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
    if (submittingRef.current) return;
    setSubmitAttempted(true);
    if (Object.keys(clientErrors).length > 0) {
      setFocusTick((tick) => tick + 1);
      return;
    }
    void send();
  };

  const requestCancel = () => {
    if (submittingRef.current) return;
    if (dirty) setDiscardOpen(true);
    else navigate(listPath);
  };

  let body;
  if (choices.kind === 'loading' || catalogState.kind === 'loading') {
    body = <LoadingState label="در حال دریافت کاتالوگ مجوزها…" />;
  } else if (unknownCompany) {
    body = (
      <div className="space-y-4">
        <Alert tone="error">شرکت انتخاب‌شده در فهرست شرکت‌های عضو شما نیست. شرکت دیگری را انتخاب کنید.</Alert>
        <CompanySelect choices={choices} value={companyId} onChange={(next) => navigate(newRolePath(next), { replace: true })} onRetry={retryCompanies} />
      </div>
    );
  } else if (catalogState.kind === 'error') {
    body = (
      <ErrorState
        message={catalogState.status === 403 ? 'شما مجوز ایجاد گروه دسترسی را ندارید.' : catalogState.message}
        {...(catalogState.status === 403 ? {} : { onRetry: retry })}
      />
    );
  } else {
    body = (
      <form ref={formRef} noValidate onSubmit={handleSubmit} aria-label="فرم ایجاد گروه دسترسی" className="space-y-5">
        {alertMessage ? (
          <Alert ref={alertRef} tone="error">
            {alertMessage}
          </Alert>
        ) : null}

        <Alert tone="info">{delegationNote}</Alert>

        <FormSection headingLevel={2} title="مشخصات گروه">
          <FormField label="کد گروه" required error={errorFor('code')} hint="حروف لاتین، ارقام و . _ - ؛ پس از ثبت قابل تغییر نیست.">
            <Input
              value={code}
              dir="ltr"
              autoComplete="off"
              autoCapitalize="characters"
              spellCheck={false}
              className="text-start"
              onChange={(event) => {
                setCode(event.target.value);
                setServerErrors(({ code: _removed, ...rest }) => rest);
              }}
              onBlur={() => {
                setCode((current) => normalizeRoleCode(current));
                setTouched((current) => ({ ...current, code: true }));
              }}
            />
          </FormField>
          <FormField label="عنوان گروه" required error={errorFor('name')} hint={`حداکثر ${new Intl.NumberFormat('fa-IR').format(roleNameMax)} نویسه.`}>
            <Input
              value={name}
              autoComplete="off"
              onChange={(event) => {
                setName(event.target.value);
                setServerErrors(({ name: _removed, ...rest }) => rest);
              }}
              onBlur={() => setTouched((current) => ({ ...current, name: true }))}
            />
          </FormField>
          <div className="sm:col-span-2">
            <FormField
              label="الگوی مجوز (اختیاری)"
              hint={
                templates === 'failed'
                  ? 'دریافت الگوها انجام نشد؛ می‌توانید مجوزها را دستی انتخاب کنید.'
                  : 'با انتخاب الگو، مجوزهای آن در فهرست پایین انتخاب می‌شوند و همچنان قابل تغییرند.'
              }
            >
              <Select
                id={templateId}
                className="h-11 sm:h-10"
                value={shownTemplate}
                disabled={!Array.isArray(templates) || submitting}
                onChange={(event) => chooseTemplate(event.target.value)}
              >
                <option value="">بدون الگو</option>
                {Array.isArray(templates)
                  ? templates.map((template) => (
                      <option key={template.code} value={template.code}>
                        {template.name}
                      </option>
                    ))
                  : null}
              </Select>
            </FormField>
          </div>
        </FormSection>

        <section aria-labelledby={`${templateId}-perm`} className="space-y-3 rounded-xl border border-line bg-white p-5">
          <h2 id={`${templateId}-perm`} className="text-base font-bold text-ink">
            مجوزها
          </h2>
          <PermissionTree permissions={catalog ?? []} value={selection} onChange={setSelection} disabled={submitting} label="مجوزهای گروه" />
        </section>

        <div className="flex flex-wrap items-center gap-2">
          <Button type="submit" size="lg" disabled={submitting} aria-busy={submitting || undefined}>
            {submitting ? 'در حال ذخیره…' : 'ایجاد گروه دسترسی'}
          </Button>
          <Button type="button" variant="outline" size="lg" disabled={submitting} onClick={requestCancel}>
            انصراف
          </Button>
        </div>

        <ConfirmDialog
          open={pendingTemplate !== null}
          title="جایگزینی مجوزهای انتخاب‌شده"
          description={`مجوزهایی که تاکنون انتخاب کرده‌اید با مجوزهای الگوی «${pendingTemplate?.name ?? ''}» جایگزین می‌شوند. ادامه می‌دهید؟`}
          confirmLabel="جایگزین کن"
          cancelLabel="نگه‌داشتن انتخاب فعلی"
          onConfirm={() => {
            if (pendingTemplate) applyTemplate(pendingTemplate);
            setPendingTemplate(null);
          }}
          onCancel={() => setPendingTemplate(null)}
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
            navigate(listPath);
          }}
          onCancel={() => setDiscardOpen(false)}
        />
      </form>
    );
  }

  return (
    <div className="space-y-5">
      <header className="space-y-2">
        <Breadcrumb items={[{ label: 'مدیریت سیستم' }, { label: 'گروه‌های دسترسی', to: listPath }, { label: 'ایجاد' }]} />
        <h1 className="text-2xl font-extrabold tracking-tight text-title">ایجاد گروه دسترسی</h1>
        <p className="text-sm text-sub">
          گروه جدید برای {companyName ? `شرکت «${companyName}»` : 'شرکت انتخاب‌شده'} ایجاد می‌شود.{' '}
          <Link to={listPath} className="rounded font-medium text-teal-deep underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-gold">
            بازگشت به فهرست
          </Link>
        </p>
      </header>
      {body}
    </div>
  );
}
