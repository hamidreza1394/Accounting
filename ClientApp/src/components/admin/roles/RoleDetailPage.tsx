import { useEffect, useId, useMemo, useRef, useState, type FormEvent, type ReactNode } from 'react';
import { ApiError, getCompanyRoles, previewCompanyRolePermissions, updateCompanyRole } from '../../../api/client';
import type { CompanyRole, PermissionCatalogItem, RolePermissionPreview } from '../../../api/types';
import { diffCodes } from '../../../lib/permissionTree';
import { Link, useDocumentTitle, useNavigate, useParams, useSearchParams } from '../../../lib/router';
import { ErrorState, LoadingState } from '../../Feedback';
import { Alert } from '../../ui/Alert';
import { usePageBreadcrumb } from '@/components/shell/PageBreadcrumb';
import { ConfirmDialog } from '../../ui/ConfirmDialog';
import { FormField, Input } from '../../ui/FormField';
import { FormSection } from '../../ui/FormSection';
import { PermissionTree } from '../../ui/PermissionTree';
import { StatusBadge } from '../../ui/StatusBadge';
import { Button } from '../../ui/button';
import type { CanHint } from '../../../lib/permissions';
import { normalizeText } from '../users/userFormValidation';
import { RoleMembersSection } from './RoleMembersSection';
import { clearRoleFlash, peekRoleFlash, roleFlashText, setRoleFlash } from './roleFlash';
import { delegationNote, roleNameMax, sameCodes, validateRoleFields } from './roleValidation';
import { resolveCompanyId, rolesPath, useCompanyChoices } from './useCompanyChoices';
import { usePermissionCatalog } from './usePermissionCatalog';

type Props = {
  onUnauthenticated: (error: unknown) => boolean;
  defaultCompanyId: string;
  /** UI hints only (the server decides) from the real `/me/permissions` of the active company. */
  can?: CanHint;
  /** Installation administrators are not bound by a delegation ceiling. */
  isInstallationAdministrator?: boolean;
  /** A 403 from the server: the caller re-reads the real permissions. */
  onForbidden?: (() => void) | undefined;
};

const denyAll: CanHint = () => false;

type RoleState =
  | { kind: 'loading' }
  | { kind: 'ready'; role: CompanyRole; key: string; version: number }
  | { kind: 'missing' }
  | { kind: 'error'; status: number | null; message: string };

const number = new Intl.NumberFormat('fa-IR');
const fallbackMessage = 'ذخیرهٔ گروه دسترسی انجام نشد. لطفاً دوباره تلاش کنید.';

function PermissionList({ title, codes, names, tone }: { title: string; codes: readonly string[]; names: ReadonlyMap<string, string>; tone: 'add' | 'remove' }) {
  if (codes.length === 0) return null;
  return (
    <section className="mt-3">
      <h3 className={tone === 'add' ? 'text-sm font-bold text-success' : 'text-sm font-bold text-danger'}>
        {title} ({number.format(codes.length)})
      </h3>
      <ul tabIndex={0} aria-label={title} className="mt-1 max-h-40 space-y-1 overflow-y-auto rounded-lg border border-line p-2 outline-none focus-visible:ring-2 focus-visible:ring-gold">
        {codes.map((code) => (
          <li key={code} className="text-xs leading-5 text-ink">
            <span aria-hidden="true">{tone === 'add' ? '+ ' : '− '}</span>
            {names.get(code) ?? 'مجوز ناشناخته'} <bdi dir="ltr" className="text-sub">({code})</bdi>
          </li>
        ))}
      </ul>
    </section>
  );
}

function RoleEditor({
  role,
  memberCount,
  catalog,
  companyId,
  canUpdate,
  isInstallationAdministrator,
  onUnauthenticated,
  onForbidden,
  onConflict,
  onSaved,
}: {
  role: CompanyRole;
  /** The live number of active members (kept current by the members section; the editor is not remounted). */
  memberCount: number;
  catalog: PermissionCatalogItem[];
  companyId: string;
  canUpdate: boolean;
  isInstallationAdministrator: boolean;
  onUnauthenticated: (error: unknown) => boolean;
  onForbidden?: (() => void) | undefined;
  /** 409: the role changed meanwhile; the caller reloads it so the form shows the real state. */
  onConflict: () => void;
  onSaved: (saved: { name: string; isActive: boolean; permissionCodes: string[] }) => void;
}) {
  const navigate = useNavigate();
  const listPath = rolesPath(companyId);
  const statusHintId = useId();
  const sectionId = useId();
  const formRef = useRef<HTMLFormElement>(null);
  const alertRef = useRef<HTMLDivElement>(null);
  const submittingRef = useRef(false);
  const doneRef = useRef(false);

  // A company administrator can only edit a role whose permissions are all within their own ceiling (D-071):
  // the catalog they receive holds exactly those codes, so any other code on the role is beyond reach.
  const catalogCodes = useMemo(() => new Set(catalog.map((item) => item.code)), [catalog]);
  const exceedsCeiling = !isInstallationAdministrator && role.permissionCodes.some((code) => !catalogCodes.has(code));
  const editable = canUpdate && !role.isSystemRole && !exceedsCeiling;
  const [conflict, setConflict] = useState(false);
  const [name, setName] = useState(role.name);
  const [isActive, setIsActive] = useState(role.isActive);
  const [selection, setSelection] = useState<string[]>(role.permissionCodes);
  const [nameTouched, setNameTouched] = useState(false);
  const [submitAttempted, setSubmitAttempted] = useState(false);
  const [alertMessage, setAlertMessage] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [focusTick, setFocusTick] = useState(0);
  const [previewOpen, setPreviewOpen] = useState(false);
  const [discardOpen, setDiscardOpen] = useState(false);
  const [serverPreview, setServerPreview] = useState<
    { kind: 'idle' } | { kind: 'loading' } | { kind: 'ready'; data: RolePermissionPreview } | { kind: 'error'; message: string }
  >({ kind: 'idle' });

  const names = useMemo(() => new Map(catalog.map((item) => [item.code, item.name])), [catalog]);
  const normalizedName = normalizeText(name);
  const clientErrors = validateRoleFields({ name }, 'edit');
  const nameError = nameTouched || submitAttempted ? clientErrors.name : undefined;
  const changes = useMemo(() => diffCodes(role.permissionCodes, selection), [role.permissionCodes, selection]);
  const nameChanged = normalizedName !== role.name;
  const statusChanged = isActive !== role.isActive;
  const dirty = editable && (nameChanged || statusChanged || !sameCodes(role.permissionCodes, selection));
  const deactivating = role.isActive && !isActive;

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

  const send = async () => {
    if (submittingRef.current) return;
    submittingRef.current = true;
    setSubmitting(true);
    setAlertMessage(null);
    try {
      await updateCompanyRole(companyId, role.id, { name: normalizedName, isActive, permissionCodes: selection });
      doneRef.current = true;
      setRoleFlash(role.id, 'updated');
      onSaved({ name: normalizedName, isActive, permissionCodes: selection });
    } catch (cause) {
      if (onUnauthenticated(cause)) return;
      if (cause instanceof ApiError && cause.status === 403) onForbidden?.();
      // 409 carries the Persian reason (system role, active members that must move first, or a concurrent change).
      setConflict(cause instanceof ApiError && cause.status === 409);
      setAlertMessage(cause instanceof ApiError ? cause.message : fallbackMessage);
      setFocusTick((tick) => tick + 1);
    } finally {
      submittingRef.current = false;
      setSubmitting(false);
    }
  };

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!editable || submittingRef.current || !dirty) return;
    setSubmitAttempted(true);
    if (Object.keys(clientErrors).length > 0) {
      setFocusTick((tick) => tick + 1);
      return;
    }
    // Every change is previewed before it is applied (D-064); a deactivation is called out.
    setPreviewOpen(true);
  };

  // The server-side impact preview (stage 2): member count, sensitive codes and ceiling
  // violations are computed by the domain, not guessed in the client. Purely advisory: the
  // save path re-runs every control, so a failed preview never blocks the honest save flow.
  useEffect(() => {
    if (!previewOpen) {
      setServerPreview({ kind: 'idle' });
      return undefined;
    }
    const controller = new AbortController();
    setServerPreview({ kind: 'loading' });
    previewCompanyRolePermissions(companyId, role.id, selection, controller.signal)
      .then((data) => {
        if (!controller.signal.aborted) setServerPreview({ kind: 'ready', data });
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || onUnauthenticated(cause)) return;
        if (cause instanceof ApiError && cause.status === 403) onForbidden?.();
        setServerPreview({
          kind: 'error',
          message: cause instanceof ApiError ? cause.message : 'محاسبهٔ اثر تغییرات انجام نشد؛ ادامه با بررسی محلی مجاز است.',
        });
      });
    return () => controller.abort();
    // The preview snapshot is bound to the moment the dialog opened: it describes exactly the
    // change the user was shown, while the save re-reads everything fresh on the server.
  }, [previewOpen]);

  const requestCancel = () => {
    if (submittingRef.current) return;
    if (dirty) setDiscardOpen(true);
    else navigate(listPath);
  };

  let preview: ReactNode = null;
  if (previewOpen) {
    const anyPermissionChange = changes.added.length > 0 || changes.removed.length > 0;
    preview = (
      <div>
        <p>تغییرات زیر برای گروه «{role.name}» اعمال می‌شود:</p>
        {nameChanged ? (
          <p className="mt-2">
            عنوان: «{role.name}» به «{normalizedName}»
          </p>
        ) : null}
        {statusChanged ? <p className="mt-2">وضعیت: {isActive ? 'فعال می‌شود.' : 'غیرفعال می‌شود.'}</p> : null}
        {deactivating ? (
          <p className="mt-2 font-semibold text-danger">
            گروه غیرفعال‌شده را نمی‌توان به عضو جدید داد. اگر عضو فعالی در این گروه باشد، سرور ذخیره را رد می‌کند.
          </p>
        ) : null}
        <PermissionList title="مجوزهای افزوده‌شده" codes={changes.added} names={names} tone="add" />
        <PermissionList title="مجوزهای حذف‌شده" codes={changes.removed} names={names} tone="remove" />
        {!anyPermissionChange ? <p className="mt-2">مجوزهای گروه تغییری نمی‌کند.</p> : null}
        <p className="mt-3">
          تعداد مجوزها: {number.format(role.permissionCodes.length)} ← {number.format(selection.length)}
        </p>
        {serverPreview.kind === 'loading' ? <p role="status" className="mt-3 text-sm text-sub">در حال محاسبهٔ اثر تغییرات روی اعضا…</p> : null}
        {serverPreview.kind === 'error' ? (
          <p role="alert" className="mt-3 text-sm text-danger">{serverPreview.message}</p>
        ) : null}
        {serverPreview.kind === 'ready' ? (
          <div className="mt-3 space-y-2">
            <p className="text-sm text-sub">
              اعضای فعال تحت تأثیر: <span className="font-semibold text-ink">{number.format(serverPreview.data.activeMemberCount)} نفر</span>
              {statusChanged && !isActive ? ' (پس از غیرفعال‌شدن گروه، این اعضا دیگر این مجوزها را ندارند)' : ''}
            </p>
            {serverPreview.data.sensitiveChangedPermissionCodes.length > 0 ? (
              <p className="text-sm font-semibold text-danger">
                مجوزهای حساس تغییرکرده: {serverPreview.data.sensitiveChangedPermissionCodes.map((code) => names.get(code) ?? code).join('، ')}
              </p>
            ) : null}
            {serverPreview.data.ceilingViolatedPermissionCodes.length > 0 ? (
              <p className="text-sm font-semibold text-danger">
                این کدها خارج از سقف تفویض شما است و ذخیره از سمت سرور رد می‌شود: {serverPreview.data.ceilingViolatedPermissionCodes.join('، ')}
              </p>
            ) : null}
          </div>
        ) : null}
      </div>
    );
  }

  return (
    <form ref={formRef} noValidate onSubmit={handleSubmit} aria-label="فرم ویرایش گروه دسترسی" className="space-y-5">
      {alertMessage ? (
        <Alert
          ref={alertRef}
          tone="error"
          action={
            conflict ? (
              <Button type="button" variant="outline" size="sm" onClick={onConflict}>
                دریافت آخرین اطلاعات
              </Button>
            ) : undefined
          }
        >
          {alertMessage}
        </Alert>
      ) : null}
      {role.isSystemRole ? (
        <Alert tone="info">این گروه سیستمی است و قابل ویرایش نیست؛ مشخصات و مجوزهای آن فقط نمایش داده می‌شوند.</Alert>
      ) : !canUpdate ? (
        <Alert tone="info">شما مجوز ویرایش گروه‌های دسترسی را ندارید؛ اطلاعات فقط نمایش داده می‌شود.</Alert>
      ) : exceedsCeiling ? (
        <Alert tone="info">این گروه مجوزهایی دارد که خارج از سقف مجوزهای شما است؛ برای جلوگیری از حذف ناخواستهٔ آن‌ها فقط نمایش داده می‌شود.</Alert>
      ) : null}
      <Alert tone="info">{delegationNote(isInstallationAdministrator)}</Alert>

      <FormSection headingLevel={2} title="مشخصات گروه">
        <div className="flex flex-col gap-1.5">
          <span className="text-sm font-semibold text-ink">کد گروه</span>
          <code dir="ltr" className="text-start text-sm text-ink">
            {role.code}
          </code>
        </div>
        <div className="flex flex-col gap-1.5">
          <span className="text-sm font-semibold text-ink">اعضای فعال</span>
          <span className="text-sm text-ink">{number.format(memberCount)} نفر</span>
        </div>
        <FormField label="عنوان گروه" required={editable} error={nameError} hint={editable ? `حداکثر ${number.format(roleNameMax)} نویسه.` : undefined}>
          <Input value={name} readOnly={!editable} autoComplete="off" onChange={(event) => setName(event.target.value)} onBlur={() => setNameTouched(true)} />
        </FormField>
        <div>
          <label className={editable ? 'flex cursor-pointer items-start gap-3' : 'flex items-start gap-3'}>
            <input
              type="checkbox"
              checked={isActive}
              disabled={!editable}
              onChange={(event) => setIsActive(event.target.checked)}
              aria-describedby={statusHintId}
              className="mt-0.5 size-5 shrink-0 accent-teal-deep outline-none focus-visible:ring-2 focus-visible:ring-gold"
            />
            <span className="text-sm font-semibold text-ink">گروه فعال باشد</span>
          </label>
          <p id={statusHintId} className="mt-1.5 ps-8 text-xs leading-5 text-sub">
            {isActive ? 'گروه برای تخصیص به اعضا در دسترس است.' : 'گروه غیرفعال است. برای غیرفعال‌کردن باید اعضای فعال آن به گروه دیگری منتقل شده باشند.'}
          </p>
        </div>
      </FormSection>

      <section aria-labelledby={sectionId} className="space-y-3 rounded-xl border border-line bg-white p-5">
        <h2 id={sectionId} className="text-base font-bold text-ink">
          مجوزها
        </h2>
        <PermissionTree permissions={catalog} value={selection} onChange={setSelection} readOnly={!editable} disabled={submitting} label="مجوزهای گروه" />
      </section>

      <div className="flex flex-wrap items-center gap-2">
        {editable ? (
          <>
            <Button type="submit" size="lg" disabled={submitting || !dirty} aria-busy={submitting || undefined}>
              {submitting ? 'در حال ذخیره…' : 'بررسی و ذخیرهٔ تغییرات'}
            </Button>
            <Button type="button" variant="outline" size="lg" disabled={submitting} onClick={requestCancel}>
              انصراف
            </Button>
            {!dirty ? <span className="text-xs text-sub">تغییری برای ذخیره وجود ندارد.</span> : null}
          </>
        ) : (
          <Link to={listPath} className="inline-flex min-h-11 items-center rounded font-medium text-teal-deep underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-gold">
            بازگشت به فهرست گروه‌ها
          </Link>
        )}
      </div>

      <ConfirmDialog
        open={previewOpen}
        tone={deactivating ? 'danger' : 'default'}
        title={deactivating ? 'غیرفعال‌کردن گروه و ذخیرهٔ تغییرات' : 'پیش‌نمایش تغییرات گروه'}
        description={preview}
        confirmLabel={deactivating ? 'غیرفعال‌کردن و ذخیره' : 'تأیید و ذخیره'}
        cancelLabel="بازگشت به ویرایش"
        busy={submitting}
        onConfirm={async () => {
          // Close first so a failure can move focus to the message box (focus returns on close).
          setPreviewOpen(false);
          await send();
        }}
        onCancel={() => setPreviewOpen(false)}
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

export function RoleDetailPage({ onUnauthenticated, defaultCompanyId, can = denyAll, isInstallationAdministrator = false, onForbidden }: Props) {
  const { roleId = '' } = useParams();
  const [params] = useSearchParams();
  const companyId = resolveCompanyId(params, defaultCompanyId);
  const listPath = rolesPath(companyId);
  const { state: catalogState, retry: retryCatalog } = usePermissionCatalog(onUnauthenticated);
  const { state: choices } = useCompanyChoices(onUnauthenticated);
  const companyName = choices.kind === 'ready' ? choices.companies.find((company) => company.id === companyId)?.name : undefined;
  const [state, setState] = useState<RoleState>({ kind: 'loading' });
  const [attempt, setAttempt] = useState(0);
  const [saveCount, setSaveCount] = useState(0);
  const [flash] = useState(() => peekRoleFlash(roleId));
  // Updated by the members section after a load/transfer, so the editor never contradicts the table below it.
  const [liveMemberCount, setLiveMemberCount] = useState<{ roleId: string; count: number } | null>(null);
  useEffect(() => clearRoleFlash, []);

  useEffect(() => {
    const controller = new AbortController();
    // A reload after saving keeps the form on screen instead of flashing the loading state.
    const loadKey = `${companyId}:${roleId}`;
    // A different company or role shows the loading state; only a reload of the same role (after a
    // save) keeps the form on screen.
    setState((current) => (current.kind === 'ready' && current.key === loadKey ? current : { kind: 'loading' }));
    getCompanyRoles(companyId, controller.signal)
      .then((roles) => {
        if (controller.signal.aborted) return;
        const role = roles.find((candidate) => candidate.id === roleId);
        // The editor remounts (new `version`) only when fresh data arrives, so it never shows stale values.
        setState((current) =>
          role ? { kind: 'ready', role, key: loadKey, version: (current.kind === 'ready' ? current.version : 0) + 1 } : { kind: 'missing' },
        );
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || onUnauthenticated(cause)) return;
        setState({
          kind: 'error',
          status: cause instanceof ApiError ? cause.status : null,
          message: cause instanceof ApiError ? cause.message : 'دریافت گروه دسترسی انجام نشد. لطفاً دوباره تلاش کنید.',
        });
      });
    return () => controller.abort();
  }, [companyId, roleId, attempt, onUnauthenticated]);

  const role = state.kind === 'ready' ? state.role : null;
  useDocumentTitle(role ? `گروه ${role.name}` : null);

  const backToList = (
    <Link to={listPath} className="mt-3 inline-block rounded font-medium text-teal-deep underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-gold">
      بازگشت به فهرست گروه‌ها
    </Link>
  );

  usePageBreadcrumb([{ label: 'مدیریت سیستم' }, { label: 'گروه‌های دسترسی', to: listPath }, { label: role?.name ?? 'جزئیات گروه' }]);
  let body: ReactNode;
  if (state.kind === 'loading' || catalogState.kind === 'loading') {
    body = <LoadingState label="در حال دریافت گروه دسترسی…" />;
  } else if (state.kind === 'error') {
    body = (
      <div className="space-y-4">
        {state.status === 403 ? (
          <ErrorState message="شما مجوز مشاهدهٔ گروه‌های دسترسی را ندارید." />
        ) : state.status === 404 ? (
          <div role="alert" className="rounded-xl border border-line bg-white p-6 text-center">
            <p className="text-lg font-bold text-ink">شرکت پیدا نشد</p>
            <p className="mt-2 text-sm text-sub">شرکتی با این شناسه وجود ندارد یا فعال نیست.</p>
          </div>
        ) : (
          <ErrorState message={state.message} onRetry={() => setAttempt((value) => value + 1)} />
        )}
        {backToList}
      </div>
    );
  } else if (state.kind === 'missing') {
    body = (
      <div className="space-y-4">
        <div role="alert" className="rounded-xl border border-line bg-white p-6 text-center">
          <p className="text-lg font-bold text-ink">گروه دسترسی پیدا نشد</p>
          <p className="mt-2 text-sm text-sub">گروهی با این شناسه در شرکت انتخاب‌شده وجود ندارد.</p>
        </div>
        {backToList}
      </div>
    );
  } else if (catalogState.kind === 'error') {
    body = <ErrorState message={catalogState.message} onRetry={retryCatalog} />;
  } else {
    body = (
      <>
        {flash && saveCount === 0 ? <p role="status" className="rounded-xl border border-line bg-cool p-4 text-sm font-medium text-navy">{roleFlashText[flash]}</p> : null}
        {saveCount > 0 ? <p role="status" className="rounded-xl border border-line bg-cool p-4 text-sm font-medium text-navy">{roleFlashText.updated}</p> : null}
        <RoleEditor
          key={`${state.role.id}:${state.version}`}
          role={state.role}
          memberCount={liveMemberCount?.roleId === state.role.id ? liveMemberCount.count : state.role.activeMemberCount}
          catalog={catalogState.catalog}
          companyId={companyId}
          canUpdate={can('admin.roles.update', companyId)}
          isInstallationAdministrator={isInstallationAdministrator}
          onUnauthenticated={onUnauthenticated}
          onForbidden={onForbidden}
          onConflict={() => setAttempt((value) => value + 1)}
          onSaved={(saved) => {
            // Show the saved values at once, then confirm them with a silent reload.
            setState((current) =>
              current.kind === 'ready'
                ? {
                    ...current,
                    version: current.version + 1,
                    role: { ...current.role, ...saved, permissionCount: saved.permissionCodes.length },
                  }
                : current,
            );
            setSaveCount((value) => value + 1);
            setAttempt((value) => value + 1);
          }}
        />
        <RoleMembersSection
          key={state.role.id}
          role={state.role}
          companyId={companyId}
          canAssign={can('admin.roles.assign', companyId)}
          onUnauthenticated={onUnauthenticated}
          onForbidden={onForbidden}
          onMemberCount={(count) => setLiveMemberCount({ roleId: state.role.id, count })}
        />
      </>
    );
  }

  return (
    <div className="space-y-5">
      <header className="space-y-2">
        <div className="flex flex-wrap items-center gap-3">
          <h1 className="text-2xl font-extrabold tracking-tight text-title">{role ? role.name : 'جزئیات گروه دسترسی'}</h1>
          {role ? (
            <>
              {role.isSystemRole ? <StatusBadge status="readonly">سیستمی</StatusBadge> : null}
              <StatusBadge status={role.isActive ? 'active' : 'inactive'} />
            </>
          ) : null}
        </div>
        {role ? <p className="text-sm text-sub">گروه دسترسی {companyName ? `شرکت «${companyName}»` : 'شرکت انتخاب‌شده'}.</p> : null}
      </header>
      {body}
    </div>
  );
}
