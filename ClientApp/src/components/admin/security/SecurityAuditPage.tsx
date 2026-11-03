import { useEffect, useId, useMemo, useState, type FormEvent } from 'react';
import { ApiError, getSecurityAuditEvents, type SecurityAuditQuery } from '../../../api/client';
import type { SecurityAuditEvent, SecurityAuditPage as AuditPage } from '../../../api/types';
import { formatDateTime } from '../../../lib/format';
import { eventTypeLabel, knownEventTypes } from '../../../lib/securityEvents';
import { dayEndUtc, dayStartUtc } from '../../../lib/time';
import { Alert } from '../../ui/Alert';
import { Breadcrumb } from '../../ui/Breadcrumb';
import { Button } from '../../ui/button';
import { DataTable, type DataTableColumn } from '../../ui/DataTable';
import { FormField, Input, Select } from '../../ui/FormField';
import { JalaliDateField } from '../../ui/JalaliDateField';
import { useCompanyChoices } from '../roles/useCompanyChoices';
import { UserPicker, type PickedUser } from '../UserPicker';

type Props = { onUnauthenticated: (error: unknown) => boolean };

const pageSizes = [10, 25, 50, 100] as const;

/** Day limits of the date fields: keeps typed/picked years inside what the server and `Date` handle. */
const minDate = '2000-01-01';
const maxDate = '2100-12-31';

type Picked = { id: string; label: string };

type Draft = { eventType: string; from: string; to: string; user: PickedUser | null; companyId: string };

type Applied = {
  eventType: string;
  from: string;
  to: string;
  /** Already converted (and validated) UTC instants of `from`/`to`; the effect never converts. */
  fromUtc: string | null;
  toUtc: string | null;
  user: Picked | null;
  company: Picked | null;
};

const emptyDraft: Draft = { eventType: '', from: '', to: '', user: null, companyId: '' };
const empty: Applied = { eventType: '', from: '', to: '', fromUtc: null, toUtc: null, user: null, company: null };

function toQuery(applied: Applied, page: number, pageSize: number): SecurityAuditQuery {
  return {
    ...(applied.eventType ? { eventType: applied.eventType } : {}),
    ...(applied.user ? { userId: applied.user.id } : {}),
    ...(applied.company ? { companyId: applied.company.id } : {}),
    ...(applied.fromUtc ? { fromUtc: applied.fromUtc } : {}),
    ...(applied.toUtc ? { toUtc: applied.toUtc } : {}),
    page,
    pageSize,
  };
}

function actorLabel(event: SecurityAuditEvent): string {
  return event.actorDisplayName ?? event.actorUsername ?? 'کاربر ناشناس';
}

/**
 * The installation-wide security audit report (read-only). It shows who did what and when; the
 * server omits IP addresses and event payloads, and so does this page. An absent event proves
 * nothing about a session, so no duration or logout is ever inferred here.
 */
export function SecurityAuditPage({ onUnauthenticated }: Props) {
  const typeListId = useId();
  const [draft, setDraft] = useState<Draft>(emptyDraft);
  const [draftError, setDraftError] = useState<string | null>(null);
  const [applied, setApplied] = useState<Applied>(empty);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState<number>(25);
  const [data, setData] = useState<AuditPage | null>(null);
  const [loading, setLoading] = useState(true);
  const [failure, setFailure] = useState<{ message: string; status: number | null } | null>(null);
  const [attempt, setAttempt] = useState(0);
  const { state: companyChoices, retry: retryCompanies } = useCompanyChoices(onUnauthenticated);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setFailure(null);
    getSecurityAuditEvents(toQuery(applied, page, pageSize), controller.signal)
      .then((result) => {
        if (controller.signal.aborted) return;
        if (result.items.length === 0 && page > 1) {
          const lastPage = Math.max(1, Math.ceil(result.totalCount / pageSize));
          if (lastPage < page) {
            setPage(lastPage);
            return;
          }
        }
        setData(result);
        setLoading(false);
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || onUnauthenticated(cause)) return;
        const status = cause instanceof ApiError ? cause.status : null;
        setFailure({
          status,
          message:
            status === 403
              ? 'گزارش امنیتی فقط برای مدیر نصب در دسترس است.'
              : cause instanceof ApiError
                ? cause.message
                : 'دریافت گزارش امنیتی انجام نشد. لطفاً دوباره تلاش کنید.',
        });
        setLoading(false);
      });
    return () => controller.abort();
  }, [applied, page, pageSize, attempt, onUnauthenticated]);

  const companies = companyChoices.kind === 'ready' ? companyChoices.companies : [];

  const submit = (event: FormEvent) => {
    event.preventDefault();
    const fromUtc = draft.from ? dayStartUtc(draft.from) : null;
    const toUtc = draft.to ? dayEndUtc(draft.to) : null;
    if ((draft.from && !fromUtc) || (draft.to && !toUtc)) {
      setDraftError('تاریخ واردشده معتبر نیست.');
      return;
    }
    // Compare real instants, not strings (years of different length order wrongly as text).
    if (fromUtc && toUtc && Date.parse(fromUtc) > Date.parse(toUtc)) {
      setDraftError('تاریخ شروع نباید بعد از تاریخ پایان باشد.');
      return;
    }
    setDraftError(null);
    setPage(1);
    const company = draft.companyId ? companies.find((candidate) => candidate.id === draft.companyId) : undefined;
    setApplied((current) => ({
      eventType: draft.eventType.trim(),
      from: draft.from,
      to: draft.to,
      fromUtc,
      toUtc,
      user: draft.user,
      // A company chosen from a table cell (not in the user's own list) is kept unless the select changes it.
      company: draft.companyId ? (company ? { id: company.id, label: company.name } : current.company) : null,
    }));
  };

  const reset = () => {
    setDraft(emptyDraft);
    setDraftError(null);
    setPage(1);
    setApplied(empty);
  };

  const filterByUser = (user: Picked) => {
    setDraft((current) => ({ ...current, user }));
    setPage(1);
    setApplied((current) => ({ ...current, user }));
  };

  const filterByCompany = (company: Picked) => {
    setDraft((current) => ({ ...current, companyId: companies.some((candidate) => candidate.id === company.id) ? company.id : '' }));
    setPage(1);
    setApplied((current) => ({ ...current, company }));
  };

  const clearChip = (which: 'user' | 'company') => {
    setPage(1);
    if (which === 'user') setDraft((current) => ({ ...current, user: null }));
    else setDraft((current) => ({ ...current, companyId: '' }));
    setApplied((current) => ({ ...current, [which]: null }));
  };

  const columns = useMemo<readonly DataTableColumn<SecurityAuditEvent>[]>(
    () => [
      { key: 'time', header: 'زمان', cell: (event) => formatDateTime(event.occurredAtUtc) },
      {
        key: 'event',
        header: 'رویداد',
        cell: (event) => {
          const label = eventTypeLabel(event.eventType);
          return (
            <span className="flex flex-col">
              {label ? <span>{label}</span> : null}
              <code dir="ltr" className="text-xs text-sub">{event.eventType}</code>
            </span>
          );
        },
      },
      {
        key: 'actor',
        header: 'انجام‌دهنده',
        cell: (event) =>
          event.userId ? (
            <button
              type="button"
              className="min-h-8 text-start text-teal-deep underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-gold"
              onClick={() => filterByUser({ id: event.userId as string, label: actorLabel(event) })}
            >
              {actorLabel(event)}
              {event.actorUsername && event.actorDisplayName ? <code dir="ltr" className="ms-2 text-xs text-sub">{event.actorUsername}</code> : null}
              <span className="sr-only">، فقط رویدادهای این کاربر</span>
            </button>
          ) : (
            <span className="text-sub">سیستم / ناشناس</span>
          ),
      },
      {
        key: 'company',
        header: 'شرکت',
        cell: (event) =>
          event.companyId ? (
            <button
              type="button"
              className="min-h-8 text-start text-teal-deep underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-gold"
              onClick={() => filterByCompany({ id: event.companyId as string, label: event.companyName ?? 'شرکت ناشناخته' })}
            >
              {event.companyName ?? 'شرکت ناشناخته'}
              <span className="sr-only">، فقط رویدادهای این شرکت</span>
            </button>
          ) : (
            '—'
          ),
      },
      {
        key: 'entity',
        header: 'موجودیت',
        cell: (event) =>
          event.entityType || event.entityId ? (
            <span className="flex flex-col">
              {event.entityType ? <code dir="ltr" className="text-xs">{event.entityType}</code> : null}
              {event.entityId ? <code dir="ltr" className="break-all text-xs text-sub">{event.entityId}</code> : null}
            </span>
          ) : (
            '—'
          ),
      },
      {
        key: 'correlation',
        header: 'شناسهٔ همبستگی',
        cell: (event) => (event.correlationId ? <code dir="ltr" className="break-all text-xs text-sub">{event.correlationId}</code> : '—'),
      },
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps -- the cell handlers only call stable state setters
    [companies],
  );

  const hasFilters = applied.eventType !== '' || applied.fromUtc !== null || applied.toUtc !== null || applied.user !== null || applied.company !== null;
  const draftDirty = draft.eventType !== '' || draft.from !== '' || draft.to !== '' || draft.user !== null || draft.companyId !== '';

  return (
    <div className="space-y-5">
      <header className="space-y-2">
        <Breadcrumb items={[{ label: 'مدیریت سیستم' }, { label: 'گزارش امنیت و سوابق' }]} />
        <h1 className="text-2xl font-extrabold tracking-tight text-title">گزارش امنیت و سوابق</h1>
        <p className="text-sm text-sub">
          رویدادهای ثبت‌شدهٔ امنیتی و تغییر دسترسی در کل سامانه، از جدیدترین. نشانی IP و محتوای جزئی رویدادها عمداً نمایش داده نمی‌شود.
        </p>
      </header>

      <Alert tone="info">نبودِ یک رویداد به معنی نبودِ آن اتفاق یا پایان نشست نیست؛ این گزارش مدت نشست یا خروج ضمنی را نشان نمی‌دهد.</Alert>

      <form aria-label="فیلتر گزارش امنیتی" onSubmit={submit} noValidate className="grid gap-3 rounded-xl border border-line bg-white p-4 sm:grid-cols-2 lg:grid-cols-3">
        <FormField label="نوع رویداد" hint="کد دقیق رویداد؛ از پیشنهادها انتخاب کنید.">
          <Input
            list={typeListId}
            dir="ltr"
            value={draft.eventType}
            maxLength={120}
            autoComplete="off"
            onChange={(event) => setDraft((current) => ({ ...current, eventType: event.target.value }))}
          />
        </FormField>
        <datalist id={typeListId}>
          {knownEventTypes.map((code) => (
            <option key={code} value={code} label={eventTypeLabel(code) ?? code} />
          ))}
        </datalist>
        <FormField label="از تاریخ" hint="تاریخ شمسی؛ شروع روز به وقت محلی شما">
          <JalaliDateField
            value={draft.from}
            min={minDate}
            max={draft.to || maxDate}
            onChange={(value) => setDraft((current) => ({ ...current, from: value }))}
          />
        </FormField>
        <FormField label="تا تاریخ" hint="پایان روز به وقت محلی شما" error={draftError}>
          <JalaliDateField
            value={draft.to}
            min={draft.from || minDate}
            max={maxDate}
            onChange={(value) => setDraft((current) => ({ ...current, to: value }))}
          />
        </FormField>
        <FormField label="کاربر" hint="جستجو در فهرست کاربران نصب">
          <UserPicker value={draft.user} onChange={(user) => setDraft((current) => ({ ...current, user }))} onUnauthenticated={onUnauthenticated} />
        </FormField>
        <FormField
          label="شرکت"
          hint={
            companyChoices.kind === 'error'
              ? undefined
              : 'فقط شرکت‌هایی که خودتان عضو آن‌ها هستید؛ برای سایر شرکت‌ها روی نام شرکت در جدول بزنید.'
          }
          error={companyChoices.kind === 'error' ? companyChoices.message : undefined}
        >
          <Select
            value={draft.companyId}
            disabled={companyChoices.kind === 'loading'}
            onChange={(event) => setDraft((current) => ({ ...current, companyId: event.target.value }))}
          >
            <option value="">{companyChoices.kind === 'loading' ? 'در حال دریافت…' : 'همهٔ شرکت‌ها'}</option>
            {companies.map((company) => (
              <option key={company.id} value={company.id}>
                {company.name}
              </option>
            ))}
          </Select>
        </FormField>
        <div className="flex flex-wrap items-end gap-2 sm:col-span-2 lg:col-span-3">
          <Button type="submit" size="lg">اعمال فیلتر</Button>
          <Button type="button" size="lg" variant="outline" onClick={reset} disabled={!hasFilters && !draftDirty}>
            پاک‌کردن فیلترها
          </Button>
          {companyChoices.kind === 'error' ? (
            <Button type="button" size="lg" variant="outline" onClick={retryCompanies}>تلاش دوباره برای فهرست شرکت‌ها</Button>
          ) : null}
        </div>
      </form>

      {applied.user || applied.company ? (
        <ul aria-label="فیلترهای فعال" className="flex flex-wrap gap-2">
          {applied.user ? (
            <li>
              <Button type="button" size="sm" variant="outline" aria-label={`حذف فیلتر کاربر ${applied.user.label}`} onClick={() => clearChip('user')}>
                کاربر: {applied.user.label} ✕
              </Button>
            </li>
          ) : null}
          {applied.company ? (
            <li>
              <Button type="button" size="sm" variant="outline" aria-label={`حذف فیلتر شرکت ${applied.company.label}`} onClick={() => clearChip('company')}>
                شرکت: {applied.company.label} ✕
              </Button>
            </li>
          ) : null}
        </ul>
      ) : null}

      {failure?.status === 403 ? (
        <div role="alert" className="rounded-xl border border-line bg-white p-6 text-center">
          <p className="text-lg font-bold text-ink">دسترسی ندارید</p>
          <p className="mt-2 text-sm text-sub">{failure.message}</p>
        </div>
      ) : (
        <DataTable
          caption="فهرست رویدادهای امنیتی"
          columns={columns}
          rows={data?.items ?? []}
          getRowKey={(event) => event.id}
          loading={loading}
          error={failure?.message ?? null}
          onRetry={() => setAttempt((value) => value + 1)}
          emptyTitle="رویدادی پیدا نشد"
          emptyDescription={hasFilters ? 'با فیلترهای فعلی رویدادی ثبت نشده است.' : 'هنوز رویداد امنیتی‌ای ثبت نشده است.'}
          pagination={{
            page,
            pageSize,
            total: data?.totalCount ?? 0,
            pageSizeOptions: pageSizes,
            onPageChange: setPage,
            onPageSizeChange: (size) => {
              setPageSize(size);
              setPage(1);
            },
          }}
        />
      )}
    </div>
  );
}
