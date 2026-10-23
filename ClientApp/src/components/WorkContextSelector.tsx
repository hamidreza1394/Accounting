import { useEffect, useId, useMemo, useRef, useState } from 'react';
import { ApiError, selectWorkContext } from '../api/client';
import type { SelectedContext, Session, WorkContext } from '../api/types';
import { formatPersianDate } from '../lib/date';
import {
  companiesWithoutEnterablePeriod,
  formatCount,
  groupFiscalYears,
  initialSelection,
  selectedEntry,
  selectYear,
  type FiscalYear,
  type Selection,
  type YearEntry,
} from '../lib/fiscalYears';
import { matchesCompany } from '../lib/search';
import { SelectionLayout } from './SelectionLayout';
import { Alert } from './ui/Alert';
import { Badge } from './ui/Badge';
import { Button } from './ui/Button';
import { ChoiceCard } from './ui/ChoiceCard';
import { ArrowLeftIcon, BuildingIcon, CalendarIcon, CheckIcon, EyeIcon, InfoIcon, LockIcon, SearchIcon } from './ui/icons';

const statusLabels = { Open: 'باز', Closed: 'بسته', Archived: 'بایگانی‌شده' } as const;

/** The search appears only for long lists (D-031, item 7). */
const SEARCH_THRESHOLD = 6;

type SelectorProps = {
  session: Session;
  context: WorkContext;
  /** Shown when the user came here to change an existing choice. */
  current: SelectedContext | null;
  onSelected: (selected: SelectedContext) => void;
  /** Present only when there is something to go back to (the user came to change the choice). */
  onCancel?: (() => void) | undefined;
  onLogout: () => void;
  /** Reloads the choices from the server (after the server refused a stale choice). */
  onRefresh: () => void;
  /** Receives every error; returns true when it was a 401 and the app is leaving for the sign-in. */
  onUnauthenticated: (error: unknown) => boolean;
};

function PanelHeading({ id, step, children }: { id: string; step: string; children: string }) {
  return (
    <h2 id={id} className="flex items-center gap-3 text-lg font-extrabold text-navy">
      <span aria-hidden="true" className="inline-flex size-8 items-center justify-center rounded-full bg-navy text-sm text-white ring-4 ring-gold/25">
        {step}
      </span>
      {children}
    </h2>
  );
}

/** The raised surface of one panel (years, companies). */
const panelClass =
  'flex min-w-0 flex-col gap-3.5 rounded-2xl border border-line bg-white/80 p-4 shadow-[0_1px_2px_rgb(28_61_90/0.04),0_8px_24px_-12px_rgb(28_61_90/0.18)] sm:p-5';

function YearCard({
  year,
  checked,
  name,
  onChange,
}: {
  year: FiscalYear;
  checked: boolean;
  name: string;
  onChange: (key: string) => void;
}) {
  const hintId = `${name}-${year.key}-hint`;

  return (
    <ChoiceCard
      name={name}
      value={year.key}
      checked={checked}
      disabled={!year.isEnterable}
      describedBy={year.isEnterable ? undefined : hintId}
      onChange={onChange}
    >
      <span className="flex min-w-0 flex-col gap-1">
        <span className="text-base font-bold text-navy [overflow-wrap:anywhere]">{year.title}</span>
        <span className="flex flex-wrap items-center gap-x-1.5 text-[0.8125rem] text-sub">
          <span>از</span>
          <bdi dir="ltr" className="font-medium tabular-nums text-ink">
            {formatPersianDate(year.startDate)}
          </bdi>
          <span>تا</span>
          <bdi dir="ltr" className="font-medium tabular-nums text-ink">
            {formatPersianDate(year.endDate)}
          </bdi>
        </span>
        <span className="text-[0.8125rem] text-sub">
          {formatCount(year.companyCount)} شرکت
          {year.isEnterable && year.enterableCount < year.companyCount
            ? ` · ${formatCount(year.enterableCount)} قابل ورود`
            : ''}
        </span>
        {year.isEnterable ? null : (
          <span id={hintId} className="inline-flex items-center gap-1.5 text-[0.8125rem] font-medium text-sub">
            <LockIcon width={14} height={14} />
            ورود به این دوره امکان‌پذیر نیست.
          </span>
        )}
      </span>
    </ChoiceCard>
  );
}

function CompanyRow({
  entry,
  checked,
  name,
  onChange,
}: {
  entry: YearEntry;
  checked: boolean;
  name: string;
  onChange: (id: string) => void;
}) {
  const { company, period } = entry;
  const enterable = period.isSelectable;
  const hintId = `${name}-${company.id}-hint`;

  return (
    <ChoiceCard
      name={name}
      value={company.id}
      checked={checked}
      disabled={!enterable}
      describedBy={enterable ? undefined : hintId}
      onChange={onChange}
    >
      <span className="flex items-start gap-3">
        <span
          aria-hidden="true"
          className={`inline-flex size-10 shrink-0 items-center justify-center rounded-[10px] ${
            enterable ? 'bg-teal-soft text-teal-deep' : 'bg-slate-200 text-sub'
          }`}
        >
          <BuildingIcon width={22} height={22} />
        </span>
        <span className="flex min-w-0 flex-col gap-1">
          <span className="text-base font-bold leading-snug text-navy [overflow-wrap:anywhere]">{company.name}</span>
          <span className="flex flex-wrap items-center gap-x-4 gap-y-0.5 text-[0.8125rem] text-sub">
            {company.nationalId ? (
              <span>
                شناسه ملی:{' '}
                <bdi dir="ltr" className="font-medium tabular-nums text-ink">
                  {company.nationalId}
                </bdi>
              </span>
            ) : null}
            <span>
              نقش شما: <b className="font-semibold text-ink">{company.roleName}</b>
            </span>
          </span>
          <span className="flex flex-wrap items-center gap-2 pt-0.5">
            {!enterable ? (
              <Badge tone="neutral" icon={<LockIcon width={13} height={13} />}>
                {statusLabels[period.status]}
              </Badge>
            ) : period.accessMode === 'readOnly' ? (
              <Badge tone="info" icon={<EyeIcon width={13} height={13} />}>
                فقط مشاهده و گزارش‌گیری
              </Badge>
            ) : (
              <Badge tone="success" icon={<CheckIcon width={13} height={13} />}>
                {statusLabels[period.status]}
              </Badge>
            )}
            {enterable ? null : (
              <span id={hintId} className="text-[0.8125rem] text-sub">
                ورود به این دوره امکان‌پذیر نیست.
              </span>
            )}
          </span>
        </span>
      </span>
    </ChoiceCard>
  );
}

/**
 * Entering the working environment: the fiscal year first (right panel), then one of the
 * companies the user may enter in that year (left panel). The server decides which choices exist
 * and which can be entered; the screen only presents them, groups periods with the same dates
 * into years, and sends the (company, period) pair back, where the server validates it again.
 * Choosing a year is local: it asks the server for nothing.
 */
export function WorkContextSelector({
  session,
  context,
  current,
  onSelected,
  onCancel,
  onLogout,
  onRefresh,
  onUnauthenticated,
}: SelectorProps) {
  const { companies } = context;
  const groupId = useId();

  const years = useMemo(() => groupFiscalYears(companies), [companies]);
  const unavailableCompanies = useMemo(() => companiesWithoutEnterablePeriod(companies), [companies]);
  const hasEnterableYear = years.some((year) => year.isEnterable);

  const [selection, setSelection] = useState<Selection>(() => initialSelection(years, current));
  const [query, setQuery] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<{ message: string; stale: boolean } | null>(null);
  const submittingRef = useRef(false);
  const alertRef = useRef<HTMLDivElement>(null);
  const companiesPanelRef = useRef<HTMLElement>(null);
  // Arrow keys also change the radio; only a pointer choice scrolls the page (keyboard users keep their place).
  const keyboardRef = useRef(false);

  const year = years.find((item) => item.key === selection.yearKey);
  const entry = selectedEntry(years, selection);
  const searchable = (year?.entries.length ?? 0) > SEARCH_THRESHOLD;

  const visibleEntries = useMemo(
    () =>
      (year?.entries ?? []).filter(
        (item) => !searchable || matchesCompany(query, item.company.name, item.company.nationalId),
      ),
    [year, searchable, query],
  );

  // Move the focus to the message so a keyboard or screen-reader user notices a failure.
  useEffect(() => {
    if (error) {
      alertRef.current?.focus();
    }
  }, [error]);

  function chooseYear(key: string) {
    if (submittingRef.current) {
      return;
    }
    setSelection((previous) => selectYear(years, previous, key));
    setQuery('');
    setError(null);

    // One column on small screens: bring the companies into view without moving the focus.
    const panel = companiesPanelRef.current;
    if (!keyboardRef.current && panel && typeof panel.scrollIntoView === 'function' && !window.matchMedia?.('(min-width: 1024px)').matches) {
      const reduced = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false;
      panel.scrollIntoView({ behavior: reduced ? 'auto' : 'smooth', block: 'start' });
    }
  }

  function chooseCompany(id: string) {
    if (submittingRef.current) {
      return;
    }
    setSelection((previous) => ({ ...previous, companyId: id }));
    setError(null);
  }

  async function enter() {
    // A ref, not state: two quick activations must never send two requests.
    if (submittingRef.current || !entry) {
      return;
    }
    submittingRef.current = true;
    setSubmitting(true);
    setError(null);
    try {
      onSelected(await selectWorkContext(entry.company.id, entry.period.id));
      // Success: the button keeps showing "entering" until the screen is replaced.
    } catch (caught: unknown) {
      submittingRef.current = false;
      setSubmitting(false);
      if (onUnauthenticated(caught)) {
        return;
      }
      if (caught instanceof ApiError && (caught.status === 404 || caught.status === 409)) {
        setError({
          message:
            caught.status === 409
              ? caught.message
              : 'دسترسی شما به این شرکت یا دوره مالی تغییر کرده است. فهرست را به‌روز کنید و دوباره انتخاب کنید.',
          stale: true,
        });
      } else {
        setError({
          message: caught instanceof Error ? caught.message : 'ورود به محیط کاری با خطا مواجه شد. دوباره تلاش کنید.',
          stale: false,
        });
      }
    }
  }

  const yearsHeadingId = `${groupId}-years`;
  const companiesHeadingId = `${groupId}-companies`;

  const footer = (
    <footer className="sticky bottom-0 z-10 border-t border-line bg-white shadow-[0_-4px_14px_rgb(28_61_90/0.06)]">
      <div className="mx-auto flex max-w-[80rem] flex-wrap items-center gap-x-4 gap-y-3 px-4 py-3 sm:px-8">
        <p aria-live="polite" className="min-w-0 flex-1 basis-60 text-sm leading-snug">
          {entry && year ? (
            <>
              <span className="text-sub">انتخاب شما: </span>
              <strong className="font-bold text-navy">{year.title}</strong>
              <span className="text-sub"> · </span>
              <strong className="font-bold text-navy [overflow-wrap:anywhere]">{entry.company.name}</strong>
              {entry.company.nationalId ? (
                <bdi dir="ltr" className="text-sub">
                  {' '}
                  ({entry.company.nationalId})
                </bdi>
              ) : null}
              {entry.period.accessMode === 'readOnly' ? <span className="text-sub"> (فقط مشاهده)</span> : null}
            </>
          ) : (
            <span className="text-sub">
              {year ? 'یک شرکت را انتخاب کنید.' : 'دوره مالی و شرکت هنوز انتخاب نشده است.'}
            </span>
          )}
        </p>

        <div className="flex w-full flex-col-reverse gap-2.5 sm:w-auto sm:flex-row sm:items-center">
          {onCancel ? (
            <Button variant="secondary" onClick={onCancel} disabled={submitting}>
              انصراف
            </Button>
          ) : null}
          <Button disabled={!entry} loading={submitting} loadingLabel="در حال ورود…" onClick={() => void enter()}>
            ورود به محیط کاری
            <ArrowLeftIcon />
          </Button>
        </div>
      </div>
    </footer>
  );

  return (
    <SelectionLayout session={session} onLogout={onLogout} footer={footer}>
      <div className="flex flex-col gap-6">
        <div className="flex flex-col gap-1.5">
          <h1 className="text-[1.65rem] font-extrabold leading-snug text-navy sm:text-[2rem]">انتخاب شرکت و دوره مالی</h1>
          <p className="max-w-3xl text-sm leading-7 text-sub sm:text-[0.95rem]">
            برای ورود به محیط کاری، ابتدا دوره مالی و سپس شرکت موردنظر را انتخاب کنید. دوره باز برای کار عادی است و
            دوره بسته فقط برای مشاهده و گزارش‌گیری.
          </p>
          <p className="flex flex-wrap gap-2 pt-1.5 text-[0.8125rem] font-medium text-navy">
            <span className="rounded-full border border-line bg-white/80 px-3 py-1">
              {formatCount(companies.length)} شرکت قابل دسترسی
            </span>
            <span className="rounded-full border border-line bg-white/80 px-3 py-1">
              {formatCount(years.length)} دوره مالی
            </span>
          </p>
        </div>

        {error ? (
          <Alert
            ref={alertRef}
            tone="error"
            action={
              error.stale ? (
                <Button variant="secondary" onClick={onRefresh}>
                  به‌روزرسانی فهرست
                </Button>
              ) : undefined
            }
          >
            {error.message}
          </Alert>
        ) : null}

        <div className="grid gap-6 lg:grid-cols-[minmax(0,24rem)_minmax(0,1fr)] lg:items-start">
          <section aria-labelledby={yearsHeadingId} className={panelClass}>
            <PanelHeading id={yearsHeadingId} step="۱">
              دوره مالی
            </PanelHeading>
            <div
              role="radiogroup"
              aria-labelledby={yearsHeadingId}
              className="flex flex-col gap-2.5"
              onKeyDown={() => {
                keyboardRef.current = true;
              }}
              onPointerDown={() => {
                keyboardRef.current = false;
              }}
            >
              {years.map((item) => (
                <YearCard
                  key={item.key}
                  year={item}
                  name={`${groupId}-year`}
                  checked={item.key === selection.yearKey}
                  onChange={chooseYear}
                />
              ))}
            </div>
            {hasEnterableYear && unavailableCompanies.length > 0 ? (
              <p className="flex items-start gap-2 text-[0.8125rem] leading-6 text-sub">
                <InfoIcon width={16} height={16} className="mt-1 shrink-0" />
                <span>
                  دوره مالی قابل ورود برای این شرکت‌ها وجود ندارد:{' '}
                  {unavailableCompanies.map((item) => item.name).join('، ')}
                </span>
              </p>
            ) : null}
          </section>

          <section
            ref={companiesPanelRef}
            aria-labelledby={companiesHeadingId}
            className={`${panelClass} scroll-mt-4`}
          >
            <PanelHeading id={companiesHeadingId} step="۲">
              شرکت‌های مجاز
            </PanelHeading>

            {!year ? (
              <p className="rounded-xl border border-dashed border-slate-400 bg-white/70 px-4 py-8 text-center text-sm leading-7 text-sub">
                <CalendarIcon width={28} height={28} className="mx-auto mb-2 text-slate-500" />
                ابتدا یک دوره مالی انتخاب کنید.
              </p>
            ) : (
              <>
                <p className="text-[0.8125rem] text-sub">
                  شرکت‌هایی که در «{year.title}» دسترسی دارید ({formatCount(year.companyCount)} شرکت)
                </p>

                {searchable ? (
                  <div className="relative">
                    <label htmlFor={`${groupId}-search`} className="sr-only">
                      جست‌وجوی شرکت بر اساس نام یا شناسه ملی
                    </label>
                    <input
                      id={`${groupId}-search`}
                      type="search"
                      value={query}
                      onChange={(event) => setQuery(event.target.value)}
                      placeholder="جست‌وجوی شرکت بر اساس نام یا شناسه ملی"
                      autoComplete="off"
                      className="min-h-11 w-full rounded-[10px] border border-slate-400 bg-white pe-4 ps-11 text-[0.95rem] text-ink placeholder:text-sub"
                    />
                    <SearchIcon className="pointer-events-none absolute start-3.5 top-1/2 -translate-y-1/2 text-sub" />
                  </div>
                ) : null}

                {visibleEntries.length === 0 ? (
                  <p role="status" className="rounded-xl border border-dashed border-slate-400 bg-white/70 p-6 text-center text-sm text-sub">
                    شرکتی با این مشخصات پیدا نشد.
                  </p>
                ) : (
                  <div
                    role="radiogroup"
                    aria-labelledby={companiesHeadingId}
                    className="-m-1 flex flex-col gap-2.5 p-1 lg:max-h-[34rem] lg:overflow-y-auto"
                  >
                    {visibleEntries.map((item) => (
                      <CompanyRow
                        key={item.company.id}
                        entry={item}
                        name={`${groupId}-company`}
                        checked={item.company.id === selection.companyId}
                        onChange={chooseCompany}
                      />
                    ))}
                  </div>
                )}
              </>
            )}
          </section>
        </div>
      </div>
    </SelectionLayout>
  );
}
