import { useEffect, useId, useMemo, useState, type FormEvent } from 'react';
import { ApiError, getSecurityAuditEvents, type SecurityAuditQuery } from '../../../api/client';
import type { SecurityAuditEvent, SecurityAuditPage as AuditPage } from '../../../api/types';
import { formatDateTime } from '../../../lib/format';
import { eventTypeLabel, knownEventTypes } from '../../../lib/securityEvents';
import { Alert } from '../../ui/Alert';
import { Breadcrumb } from '../../ui/Breadcrumb';
import { Button } from '../../ui/button';
import { DataTable, type DataTableColumn } from '../../ui/DataTable';
import { FormField, Input } from '../../ui/FormField';

type Props = { onUnauthenticated: (error: unknown) => boolean };

const pageSizes = [10, 25, 50, 100] as const;

type Applied = {
  eventType: string;
  from: string;
  to: string;
  user: { id: string; label: string } | null;
  company: { id: string; label: string } | null;
};

const empty: Applied = { eventType: '', from: '', to: '', user: null, company: null };

/** Start of the chosen local calendar day, as a UTC instant. */
export function dayStartUtc(date: string): string {
  return new Date(`${date}T00:00:00`).toISOString();
}

/** End of the chosen local calendar day, as a UTC instant. */
export function dayEndUtc(date: string): string {
  return new Date(`${date}T23:59:59.999`).toISOString();
}

function toQuery(applied: Applied, page: number, pageSize: number): SecurityAuditQuery {
  return {
    ...(applied.eventType ? { eventType: applied.eventType } : {}),
    ...(applied.user ? { userId: applied.user.id } : {}),
    ...(applied.company ? { companyId: applied.company.id } : {}),
    ...(applied.from ? { fromUtc: dayStartUtc(applied.from) } : {}),
    ...(applied.to ? { toUtc: dayEndUtc(applied.to) } : {}),
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
  const [draft, setDraft] = useState({ eventType: '', from: '', to: '' });
  const [draftError, setDraftError] = useState<string | null>(null);
  const [applied, setApplied] = useState<Applied>(empty);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState<number>(25);
  const [data, setData] = useState<AuditPage | null>(null);
  const [loading, setLoading] = useState(true);
  const [failure, setFailure] = useState<{ message: string; status: number | null } | null>(null);
  const [attempt, setAttempt] = useState(0);

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

  const submit = (event: FormEvent) => {
    event.preventDefault();
    if (draft.from && draft.to && draft.from > draft.to) {
      setDraftError('تاریخ شروع نباید بعد از تاریخ پایان باشد.');
      return;
    }
    setDraftError(null);
    setPage(1);
    setApplied((current) => ({ ...current, eventType: draft.eventType.trim(), from: draft.from, to: draft.to }));
  };

  const reset = () => {
    setDraft({ eventType: '', from: '', to: '' });
    setDraftError(null);
    setPage(1);
    setApplied(empty);
  };

  const filterBy = (patch: Partial<Applied>) => {
    setPage(1);
    setApplied((current) => ({ ...current, ...patch }));
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
              className="text-start text-teal-deep underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-gold"
              aria-label={`فقط رویدادهای ${actorLabel(event)}`}
              onClick={() => filterBy({ user: { id: event.userId as string, label: actorLabel(event) } })}
            >
              {actorLabel(event)}
              {event.actorUsername && event.actorDisplayName ? <code dir="ltr" className="ms-2 text-xs text-sub">{event.actorUsername}</code> : null}
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
              className="text-start text-teal-deep underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-gold"
              aria-label={`فقط رویدادهای شرکت ${event.companyName ?? 'ناشناخته'}`}
              onClick={() => filterBy({ company: { id: event.companyId as string, label: event.companyName ?? 'شرکت ناشناخته' } })}
            >
              {event.companyName ?? 'شرکت ناشناخته'}
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
    [],
  );

  const hasFilters = applied.eventType !== '' || applied.from !== '' || applied.to !== '' || applied.user !== null || applied.company !== null;

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

      <form aria-label="فیلتر گزارش امنیتی" onSubmit={submit} noValidate className="grid gap-3 rounded-xl border border-line bg-white p-4 sm:grid-cols-2 lg:grid-cols-4 lg:items-end">
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
        <FormField label="از تاریخ" hint="به وقت محلی مرورگر">
          <Input type="date" value={draft.from} onChange={(event) => setDraft((current) => ({ ...current, from: event.target.value }))} />
        </FormField>
        <FormField label="تا تاریخ" error={draftError}>
          <Input type="date" value={draft.to} onChange={(event) => setDraft((current) => ({ ...current, to: event.target.value }))} />
        </FormField>
        <div className="flex flex-wrap gap-2">
          <Button type="submit" size="lg">اعمال فیلتر</Button>
          <Button type="button" size="lg" variant="outline" onClick={reset} disabled={!hasFilters && draft.eventType === '' && draft.from === '' && draft.to === ''}>
            پاک‌کردن فیلترها
          </Button>
        </div>
      </form>

      {applied.user || applied.company ? (
        <ul aria-label="فیلترهای فعال" className="flex flex-wrap gap-2">
          {applied.user ? (
            <li>
              <Button type="button" size="sm" variant="outline" aria-label={`حذف فیلتر کاربر ${applied.user.label}`} onClick={() => filterBy({ user: null })}>
                کاربر: {applied.user.label} ✕
              </Button>
            </li>
          ) : null}
          {applied.company ? (
            <li>
              <Button type="button" size="sm" variant="outline" aria-label={`حذف فیلتر شرکت ${applied.company.label}`} onClick={() => filterBy({ company: null })}>
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
