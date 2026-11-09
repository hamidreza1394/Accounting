import { useEffect, useMemo, useRef, useState, type FormEvent, type ReactNode } from 'react';
import { ArrowRight, History, KeyRound, Phone, User as UserIcon, Briefcase } from 'lucide-react';
import { ApiError } from '../../../api/client';
import type { AdminUserDetails } from '../../../api/types';
import { EmptyState } from '../../Feedback';
import { Alert } from '../../ui/Alert';
import { ConfirmDialog } from '../../ui/ConfirmDialog';
import { Tabs } from '../../ui/Tabs';
import { Button } from '../../ui/button';
import {
  fieldNames,
  isUserFormDirty,
  tabsWithErrors,
  userFormTabOrder,
  validateUserForm,
  type FieldErrors,
  type FieldName,
  type UserFormTabId,
  type UserFormValues,
} from './userFormValidation';
import { useFormGuard } from '@/lib/formGuard';
import { ContactPanel, MainInfoPanel } from './form/UserFormMainPanels';
import { EmploymentPanel } from './form/UserFormHrPanel';
import { RolesPanel } from './form/UserFormRolesPanel';
import { HistoryPanel } from './form/UserFormHistoryPanel';
import { noUserFormContext, type FormTabCache, type UserFormContext } from './form/userFormShared';
import { useSuggestedEmployeeNumber } from './form/useSuggestedEmployeeNumber';
import { CreatePhotoField, EditPhotoField, type PhotoDraft } from './form/UserPhotoField';
import { Info } from './profile/profileShared';

type Mode = 'create' | 'edit';

/** What the form tells the page besides the values: the pre-filled personnel number, if any. */
export type UserFormSubmitMeta = {
  suggestedEmployeeNumber: string | null;
  /** Create mode: the photo chosen for the personnel record (uploaded after the record exists). */
  photo: File | null;
};

type UserFormProps = {
  mode: Mode;
  initialValues: UserFormValues;
  /** Sends the values; it throws (an `ApiError` or any error) when the save failed. */
  onSubmit: (values: UserFormValues, meta: UserFormSubmitMeta) => Promise<void>;
  /** Leaves the form without saving (only called after the discard confirmation when dirty). */
  onCancel: () => void;
  onUnauthenticated: (error: unknown) => boolean;
  /** Work-context company and permission hints; the default grants nothing. */
  workContext?: UserFormContext;
  /** Edit mode: the loaded user (HR, roles and history tabs read from it). */
  user?: AdminUserDetails;
  /** Edit mode: called after a role assignment so the page reloads the user (memberships) from the server. */
  onMembershipsChanged?: () => void;
};

const tabMeta: Record<UserFormTabId, { label: string; Icon: typeof UserIcon }> = {
  main: { label: 'اطلاعات اصلی', Icon: UserIcon },
  contact: { label: 'اطلاعات تماس', Icon: Phone },
  employment: { label: 'اطلاعات استخدامی', Icon: Briefcase },
  roles: { label: 'سمت‌ها و نقش‌ها', Icon: KeyRound },
  history: { label: 'سوابق', Icon: History },
};

function TabLabel({ id, hasError }: { id: UserFormTabId; hasError: boolean }) {
  const { label, Icon } = tabMeta[id];
  return (
    <span className="inline-flex items-center gap-1.5 max-sm:flex-col max-sm:gap-1">
      <Icon size={16} aria-hidden="true" />
      <span>{label}</span>
      {hasError ? (
        <>
          <span aria-hidden="true" className="size-2 shrink-0 rounded-full bg-danger" />
          <span className="sr-only">(دارای خطا)</span>
        </>
      ) : null}
    </span>
  );
}

const fallbackMessage = 'ذخیرهٔ اطلاعات انجام نشد. لطفاً دوباره تلاش کنید.';

/**
 * The create/edit form of a user, shared by both pages, with five inner tabs. It owns the typed
 * values (above the tab panels, so switching tabs never loses input), validation (on blur and on
 * submit), the server error mapping, the double-submit guard and the unsaved changes guard. The
 * password exists only in this component's state. Tabs 3-5 read real data lazily (the active panel
 * mounts alone) into a per-form cache, so a revisit never re-requests.
 */
export function UserForm({
  mode,
  initialValues,
  onSubmit,
  onCancel,
  onUnauthenticated,
  workContext = noUserFormContext,
  user,
  onMembershipsChanged,
}: UserFormProps) {
  const formRef = useRef<HTMLFormElement>(null);
  const alertRef = useRef<HTMLDivElement>(null);
  const submittingRef = useRef(false);
  const doneRef = useRef(false);
  // Lazy tab payloads of this form instance: in memory only, gone with the page.
  const cache = useRef<FormTabCache>(new Map()).current;

  const [values, setValues] = useState<UserFormValues>(initialValues);
  const [touched, setTouched] = useState<Partial<Record<FieldName, boolean>>>({});
  const [submitAttempted, setSubmitAttempted] = useState(false);
  const [serverErrors, setServerErrors] = useState<FieldErrors>({});
  const [alertMessage, setAlertMessage] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [focusTick, setFocusTick] = useState(0);
  const [deactivateOpen, setDeactivateOpen] = useState(false);
  const [discardOpen, setDiscardOpen] = useState(false);
  const [tab, setTab] = useState<UserFormTabId>('main');
  // The photo picked in create mode lives here (above the tab panels), so switching tabs keeps it.
  const [photoDraft, setPhotoDraft] = useState<PhotoDraft | null>(null);
  // The suggestion that was pre-filled into an empty personnel number field; it becomes part of
  // the baseline, so an untouched suggestion never makes the form "dirty".
  const [applied, setApplied] = useState<{ companyId: string | null; value: string } | null>(null);
  const appliedSuggestion = applied?.value ?? null;

  const canSuggest =
    mode === 'create' && workContext.activeCompanyId !== null && workContext.can('hr.employees.create', workContext.activeCompanyId);
  const suggestion = useSuggestedEmployeeNumber(workContext.activeCompanyId, canSuggest, onUnauthenticated);
  useEffect(() => {
    if (suggestion.status !== 'ready') return;
    const previous = applied;
    // A suggestion belongs to the company it was requested for: when the work company changes, an
    // untouched number from the previous company is replaced; a number the user typed is kept.
    if (previous !== null && previous.companyId === workContext.activeCompanyId && previous.value === suggestion.value) return;
    setApplied({ companyId: workContext.activeCompanyId, value: suggestion.value });
    setValues((current) =>
      current.employeeNumber === '' || (previous !== null && current.employeeNumber === previous.value)
        ? { ...current, employeeNumber: suggestion.value }
        : current,
    );
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [suggestion, workContext.activeCompanyId]);

  const baseline = useMemo<UserFormValues>(
    () => (appliedSuggestion === null ? initialValues : { ...initialValues, employeeNumber: appliedSuggestion }),
    [initialValues, appliedSuggestion],
  );

  const clientErrors = validateUserForm(values, mode);
  const dirty = isUserFormDirty(values, baseline) || (mode === 'create' && photoDraft !== null);
  // The workspace asks this registry before closing the tab, signing out or changing context;
  // a failed save keeps the `error` status until the user retries or cancels (D-076).
  const formGuard = useFormGuard({
    dirty,
    saving: submitting,
    errorMessage: alertMessage,
    title: mode === 'create' ? 'ایجاد کاربر' : 'ویرایش کاربر',
  });
  const activeCompanyId = workContext.activeCompanyId;
  const canCreateEmployee = activeCompanyId !== null && workContext.can('hr.employees.create', activeCompanyId) &&
    workContext.can('hr.employees.update', activeCompanyId);
  let photoUnavailable: string | null = null;
  if (activeCompanyId === null) photoUnavailable = 'شرکت کاری فعالی انتخاب نشده است؛ عکس همراه پرونده پرسنلی ذخیره می‌شود و بدون شرکت فعال ذخیره نمی‌شود.';
  else if (!canCreateEmployee) photoUnavailable = 'برای ذخیرهٔ عکس پرسنلی، مجوزهای «ایجاد کارمند» و «ویرایش کارمند» در شرکت فعال لازم است.';
  // A chosen photo needs the personnel record this save creates; without a personnel number it
  // would be silently dropped, so the save is blocked until the user fixes one side.
  const photoConflict =
    mode === 'create' && photoDraft !== null && values.employeeNumber.trim() === ''
      ? 'عکس بدون شماره پرسنلی ذخیره نمی‌شود؛ شماره پرسنلی را وارد کنید یا عکس را حذف کنید.'
      : null;
  const visibleFields = fieldNames.filter((name) => mode === 'create' || (name !== 'password' && name !== 'confirmPassword' && name !== 'employeeNumber'));

  const errorFor = (name: FieldName): string | undefined =>
    serverErrors[name] ?? (touched[name] || submitAttempted ? clientErrors[name] : undefined);

  const shownErrors: FieldErrors = {};
  for (const name of visibleFields) {
    const message = errorFor(name);
    if (message) shownErrors[name] = message;
  }
  const errorTabs = tabsWithErrors(shownErrors);

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

  /** Opens the first tab (in tab order) that holds one of the errors, then focuses its field. */
  const revealErrors = (errors: FieldErrors) => {
    const first = tabsWithErrors(errors)[0];
    if (first) setTab(first);
    setFocusTick((tick) => tick + 1);
  };

  const showServerFailure = (cause: unknown): FieldErrors => {
    if (!(cause instanceof ApiError)) {
      setAlertMessage(fallbackMessage);
      return {};
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
    return mapped;
  };

  const send = async () => {
    if (submittingRef.current) return;
    submittingRef.current = true;
    setSubmitting(true);
    setAlertMessage(null);
    setServerErrors({});
    try {
      await onSubmit(values, { suggestedEmployeeNumber: appliedSuggestion, photo: photoDraft?.file ?? null });
      doneRef.current = true;
      // Saved: closing this tab (or leaving the workspace) needs no warning anymore.
      formGuard.markClean();
    } catch (cause) {
      if (onUnauthenticated(cause)) return;
      revealErrors(showServerFailure(cause));
    } finally {
      submittingRef.current = false;
      setSubmitting(false);
    }
  };

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    // A submit from a nested form (the employee search of the link dialog bubbles through its
    // portal) is not this form's submit.
    if (event.target !== event.currentTarget) return;
    if (submittingRef.current || (mode === 'edit' && !dirty)) return;
    setSubmitAttempted(true);
    if (photoConflict !== null) {
      setTab('main');
      return;
    }
    if (Object.keys(clientErrors).length > 0) {
      revealErrors(clientErrors);
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

  const editPhotoVisible =
    mode === 'edit' && user !== undefined && activeCompanyId !== null && workContext.can('hr.employees.view', activeCompanyId) &&
    user.memberships.some((m) => m.isActive && m.companyId.toLowerCase() === activeCompanyId.toLowerCase());
  let photoField: ReactNode;
  if (mode === 'create') {
    photoField = (
      <CreatePhotoField
        draft={photoDraft}
        onDraftChange={setPhotoDraft}
        firstName={values.firstName}
        lastName={values.lastName}
        unavailableReason={photoUnavailable}
        conflictMessage={photoConflict}
      />
    );
  } else if (editPhotoVisible && user && activeCompanyId) {
    photoField = (
      <EditPhotoField
        userId={user.id}
        companyId={activeCompanyId}
        firstName={values.firstName}
        lastName={values.lastName}
        context={workContext}
        cache={cache}
        onUnauthenticated={onUnauthenticated}
      />
    );
  } else {
    photoField = (
      <Info label="عکس پرسنلی">
        <span className="text-sub">عکس همراه پرونده پرسنلی شرکت ذخیره می‌شود؛ برای تغییر آن، عضویت فعال در شرکت و مجوز مشاهدهٔ کارکنان لازم است.</span>
      </Info>
    );
  }
  const createNotice = (title: string, text: string) => <EmptyState title={title}>{text}</EmptyState>;
  const editReady = mode === 'edit' && user !== undefined;
  const panels: Record<UserFormTabId, ReactNode> = {
    main: (
      <MainInfoPanel
        mode={mode}
        field={field}
        errorFor={errorFor}
        isActive={values.isActive}
        onActiveChange={(value) => change('isActive', value)}
        context={workContext}
        suggestion={suggestion}
        {...(user ? { user } : {})}
        cache={cache}
        onUnauthenticated={onUnauthenticated}
        photo={photoField}
      />
    ),
    contact: <ContactPanel field={field} errorFor={errorFor} />,
    employment: editReady
      ? <EmploymentPanel user={user} cache={cache} context={workContext} onUnauthenticated={onUnauthenticated} />
      : createNotice('اطلاعات استخدامی پس از ایجاد حساب و عضویت در شرکت قابل تنظیم است.', 'واحد، سمت و مدیر مستقیم از ماژول منابع انسانی خوانده می‌شود؛ پس از ایجاد حساب و عضویت فعال در شرکت، این تب را در حالت ویرایش ببینید.'),
    roles: editReady
      ? (
        <RolesPanel
          user={user}
          memberships={user.memberships}
          cache={cache}
          context={workContext}
          onUnauthenticated={onUnauthenticated}
          onMembershipsChanged={onMembershipsChanged ?? (() => undefined)}
        />
      )
      : createNotice('سمت‌ها و نقش‌ها پس از ایجاد حساب و عضویت در شرکت قابل تنظیم است.', 'نقش نرم‌افزاری فقط به عضو فعال یک شرکت داده می‌شود؛ سمت سازمانی به‌تنهایی مجوز ایجاد نمی‌کند.'),
    history: editReady
      ? <HistoryPanel user={user} cache={cache} context={workContext} onUnauthenticated={onUnauthenticated} />
      : createNotice('سوابق پس از ایجاد حساب نمایش داده می‌شود.', 'تاریخ ایجاد، آخرین تغییر و سوابق ورود برای حساب ذخیره‌شده در دسترس است.'),
  };

  return (
    <form ref={formRef} noValidate onSubmit={handleSubmit} aria-label={mode === 'create' ? 'فرم ایجاد کاربر' : 'فرم ویرایش کاربر'} className="space-y-4">
      {mode === 'create' ? (
        <div>
          {/* Same exit as «انصراف»: it asks before discarding typed values, then returns to the plain list. */}
          <Button type="button" variant="ghost" size="sm" disabled={submitting} onClick={requestCancel}>
            <ArrowRight size={16} aria-hidden="true" />
            بازگشت به فهرست کاربران
          </Button>
        </div>
      ) : null}
      {alertMessage ? (
        <Alert ref={alertRef} tone="error">
          {alertMessage}
        </Alert>
      ) : null}

      <Tabs
        label="بخش‌های فرم کاربر"
        value={tab}
        onValueChange={(id) => setTab(id as UserFormTabId)}
        items={userFormTabOrder.map((id) => ({
          id,
          label: <TabLabel id={id} hasError={errorTabs.includes(id)} />,
          content: panels[id],
        }))}
      />

      <div className="sticky bottom-0 z-10 -mx-1 flex flex-wrap items-center gap-2 border-t border-line bg-white/95 px-1 py-3 backdrop-blur">
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
