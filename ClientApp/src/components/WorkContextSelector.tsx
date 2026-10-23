import { useEffect, useId, useMemo, useRef, useState, type ReactNode } from 'react';
import { ApiError, selectWorkContext, setDefaultCompany as saveDefaultCompany } from '../api/client';
import type { FiscalPeriodStatus, SelectedContext, Session, WorkContext } from '../api/types';
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
import { matchesCompany, matchesYear } from '../lib/search';
import { SelectionFooter } from './SelectionFooter';
import { SelectionLayout } from './SelectionLayout';
import { Alert } from './ui/Alert';
import { Badge } from './ui/Badge';
import { Button } from './ui/Button';
import { ChoiceCard } from './ui/ChoiceCard';
import { BuildingIcon, CalendarIcon, EyeIcon, InfoIcon, LockIcon, SearchIcon, UsersIcon } from './ui/icons';

const statusLabels = { Open: 'باز', Closed: 'بسته', Archived: 'بایگانی‌شده' } as const;

/** The badge of a fiscal year in the period list. */
const yearBadges: Record<FiscalPeriodStatus, { label: string; className: string }> = {
  Open: { label: 'فعال', className: 'bg-[#dcf4e8] text-success' },
  Closed: { label: 'بسته شده', className: 'bg-[#e1e7f0] text-sub' },
  Archived: { label: 'بایگانی', className: 'bg-[#e1e7f0] text-sub' },
};

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

function IconTile({ children, raised = false }: { children: ReactNode; raised?: boolean }) {
  return (
    <span
      aria-hidden="true"
      className={`inline-flex size-[2.875rem] shrink-0 items-center justify-center rounded-[0.875rem] text-[#1d4f86] ${
        raised ? 'bg-white shadow-[0_2px_8px_rgb(60_110_180/0.15)]' : 'bg-[#e9f0f9]'
      }`}
    >
      {children}
    </span>
  );
}

function Chip({ children, tone }: { children: ReactNode; tone: 'plain' | 'period' }) {
  return (
    <span
      className={`inline-flex h-7 shrink-0 items-center whitespace-nowrap rounded-full px-3.5 text-[0.8125rem] ${
        tone === 'period' ? 'border border-[#cfe0f3] bg-white text-[#3f567a]' : 'bg-[#e9eef6] text-[#475a70]'
      }`}
    >
      {children}
    </span>
  );
}

function SearchBox({
  id,
  label,
  placeholder,
  value,
  onChange,
  disabled = false,
  tone,
}: {
  id: string;
  label: string;
  placeholder: string;
  value: string;
  onChange: (value: string) => void;
  disabled?: boolean;
  tone: 'plain' | 'period';
}) {
  return (
    <div className="relative min-w-0">
      <label htmlFor={id} className="sr-only">
        {label}
      </label>
      <input
        id={id}
        type="search"
        value={value}
        disabled={disabled}
        onChange={(event) => onChange(event.target.value)}
        placeholder={placeholder}
        autoComplete="off"
        className={`h-11 w-full rounded-[0.875rem] border-[1.5px] bg-white pe-4 ps-10 text-[0.84375rem] text-ink placeholder:text-muted disabled:cursor-not-allowed disabled:bg-white/60 ${
          tone === 'period' ? 'border-[#c6d9ef]' : 'border-[#dde6f1]'
        }`}
      />
      <SearchIcon width={18} height={18} strokeWidth={2} className="pointer-events-none absolute start-3.5 top-1/2 -translate-y-1/2 text-[#7b8ba0]" />
    </div>
  );
}

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
  const badge = yearBadges[year.status];

  return (
    <ChoiceCard
      variant="period"
      name={name}
      value={year.key}
      checked={checked}
      disabled={!year.isEnterable}
      describedBy={year.isEnterable ? undefined : hintId}
      onChange={onChange}
    >
      <span className="flex min-w-0 flex-col gap-1.5">
        <span className="flex flex-wrap items-center gap-x-2.5 gap-y-1">
          <span className="text-lg font-extrabold leading-snug text-title [overflow-wrap:anywhere]">{year.title}</span>
          <span className={`inline-flex h-[1.375rem] items-center rounded-full px-2.5 text-xs font-semibold ${badge.className}`}>{badge.label}</span>
        </span>
        <span className="flex flex-wrap items-center gap-x-2 gap-y-0.5 text-[0.78125rem] text-sub">
          <CalendarIcon width={14} height={14} strokeWidth={2} className="shrink-0" />
          <span className="sr-only">از</span>
          <bdi dir="ltr" className="font-medium tabular-nums">
            {formatPersianDate(year.startDate)}
          </bdi>
          <span aria-hidden="true">-</span>
          <span className="sr-only">تا</span>
          <bdi dir="ltr" className="font-medium tabular-nums">
            {formatPersianDate(year.endDate)}
          </bdi>
          <span aria-hidden="true">·</span>
          <span>
            {formatCount(year.companyCount)} شرکت
            {year.isEnterable && year.enterableCount < year.companyCount
              ? ` · ${formatCount(year.enterableCount)} قابل ورود`
              : ''}
          </span>
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
  isDefault,
}: {
  entry: YearEntry;
  checked: boolean;
  name: string;
  onChange: (id: string) => void;
  isDefault: boolean;
}) {
  const { company, period } = entry;
  const enterable = period.isSelectable;
  const hintId = `${name}-${company.id}-hint`;
  const readOnly = enterable && period.accessMode === 'readOnly';

  return (
    <ChoiceCard
      variant="company"
      name={name}
      value={company.id}
      checked={checked}
      disabled={!enterable}
      describedBy={enterable ? undefined : hintId}
      onChange={onChange}
    >
      {/* The row lays itself out by its own width: one line (name, code, role) when it is wide,
          stacked when it is narrow. */}
      <span className="@container flex items-center gap-4">
        <span
          aria-hidden="true"
          className={`inline-flex size-[3.125rem] shrink-0 items-center justify-center rounded-full border border-[#e1e9f3] shadow-[0_1px_4px_rgb(30_70_120/0.12)] ${
            enterable ? 'bg-white text-[#1d4f86]' : 'bg-slate-100 text-sub'
          }`}
        >
          <BuildingIcon width={24} height={24} />
        </span>
        <span className="grid min-w-0 flex-1 items-center gap-x-4 gap-y-1 @2xl:grid-cols-[minmax(0,1fr)_auto_auto] @4xl:grid-cols-[20.5rem_15rem_auto]">
          <span className="flex min-w-0 flex-col gap-1">
            <span title={company.name} className="line-clamp-2 text-lg font-bold leading-snug text-title [overflow-wrap:anywhere]">
              {company.name}
              {isDefault ? <Badge tone="info">پیش‌فرض شما</Badge> : null}
            </span>
            {!enterable || readOnly ? (
              <span className="flex flex-wrap items-center gap-2">
                {!enterable ? (
                  <Badge tone="neutral" icon={<LockIcon width={13} height={13} />}>
                    {statusLabels[period.status]}
                  </Badge>
                ) : (
                  <Badge tone="info" icon={<EyeIcon width={13} height={13} />}>
                    فقط مشاهده و گزارش‌گیری
                  </Badge>
                )}
                {enterable ? null : (
                  <span id={hintId} className="text-[0.8125rem] text-sub">
                    ورود به این دوره امکان‌پذیر نیست.
                  </span>
                )}
              </span>
            ) : null}
          </span>
          <span className="flex flex-wrap items-center gap-x-4 gap-y-1 @2xl:contents">
            {company.nationalId ? (
              <span className="whitespace-nowrap text-[0.90625rem] text-sub">
                کد ملی:{' '}
                <bdi dir="ltr" className="font-medium tabular-nums text-ink">
                  {company.nationalId}
                </bdi>
              </span>
            ) : (
              <span aria-hidden="true" className="@max-2xl:hidden" />
            )}
            {company.roleName ? (
              <span className="inline-flex h-[1.875rem] max-w-full items-center justify-self-start truncate rounded-full bg-[#dff3fe] px-4 text-[0.8125rem] font-semibold text-[#1f6ea6]">
                <span className="sr-only">نقش: </span>
                {company.roleName}
              </span>
            ) : null}
          </span>
        </span>
      </span>
    </ChoiceCard>
  );
}

/**
 * Entering the working environment: the fiscal year first (right panel), then one of the
 * companies the user may enter in that year (left panel, one company per row). The server decides
 * which choices exist and which can be entered; the screen only presents them, groups periods
 * with the same dates into years, and sends the (company, period) pair back, where the server
 * validates it again. Choosing a year is local: it asks the server for nothing.
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

  const [selection, setSelection] = useState<Selection>(() => initialSelection(years, current, context.defaultCompanyId));
  const [defaultCompanyId, setDefaultCompanyId] = useState<string | null>(() => context.defaultCompanyId);
  const [savingDefault, setSavingDefault] = useState(false);
  const [query, setQuery] = useState('');
  const [yearQuery, setYearQuery] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<{ message: string; stale: boolean } | null>(null);
  const submittingRef = useRef(false);
  const alertRef = useRef<HTMLDivElement>(null);
  const companiesPanelRef = useRef<HTMLElement>(null);
  // Arrow keys also change the radio; only a pointer choice scrolls the page (keyboard users keep their place).
  const keyboardRef = useRef(false);

  const year = years.find((item) => item.key === selection.yearKey);
  const entry = selectedEntry(years, selection);

  const visibleYears = useMemo(
    () => years.filter((item) => matchesYear(yearQuery, item.title, formatPersianDate(item.startDate), formatPersianDate(item.endDate))),
    [years, yearQuery],
  );
  const visibleEntries = useMemo(
    () => (year?.entries ?? []).filter((item) => matchesCompany(query, item.company.name, item.company.nationalId)),
    [year, query],
  );

  // Move the focus to the message so a keyboard or screen-reader user notices a failure.
  useEffect(() => {
    if (error) {
      alertRef.current?.focus();
    }
  }, [error]);

  useEffect(() => {
    setDefaultCompanyId(context.defaultCompanyId);
  }, [context.defaultCompanyId]);

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

  async function saveSelectedCompanyAsDefault() {
    if (!entry || savingDefault) {
      return;
    }
    setSavingDefault(true);
    setError(null);
    try {
      await saveDefaultCompany(entry.company.id);
      setDefaultCompanyId(entry.company.id);
    } catch (caught: unknown) {
      if (onUnauthenticated(caught)) {
        return;
      }
      setError({
        message: caught instanceof Error ? caught.message : 'ذخیره شرکت پیش‌فرض انجام نشد. دوباره تلاش کنید.',
        stale: false,
      });
    } finally {
      setSavingDefault(false);
    }
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

  return (
    <SelectionLayout
      session={session}
      onLogout={onLogout}
      footer={<SelectionFooter year={year} entry={entry} submitting={submitting} onEnter={() => void enter()} onCancel={onCancel} />}
    >
      <div className="flex min-h-0 flex-1 flex-col gap-5">
        <div className="flex flex-wrap items-start justify-between gap-x-6 gap-y-3">
          <div>
            <div className="flex items-center gap-3">
              <span aria-hidden="true" className="inline-flex size-12 shrink-0 items-center justify-center rounded-2xl bg-[#e6eef9] text-[#1d4f86]">
                <CalendarIcon width={26} height={26} strokeWidth={1.7} />
              </span>
              <h1 className="text-[1.625rem] font-extrabold leading-snug text-title sm:text-[2rem]">فرانو - دوره مالی</h1>
            </div>
            <p className="mt-2 max-w-3xl text-sm leading-7 text-sub">
              برای ورود به محیط کاری، ابتدا دوره مالی و سپس شرکت موردنظر را انتخاب کنید.
            </p>
          </div>
          <p className="inline-flex items-center gap-3 rounded-2xl border border-white bg-white/90 px-5 py-3 text-[0.8125rem] font-medium text-title shadow-[0_4px_18px_rgb(31_70_120/0.1)]">
            <UsersIcon width={24} height={24} className="text-[#2f7bd0]" />
            <span>شما به {formatCount(companies.length)} شرکت دسترسی دارید</span>
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

        <div className="grid gap-5 lg:min-h-0 lg:flex-1 lg:grid-cols-[26.25rem_minmax(0,1fr)] lg:grid-rows-[minmax(0,1fr)]">
          <section
            aria-labelledby={yearsHeadingId}
            className="relative flex min-h-0 min-w-0 flex-col rounded-[1.25rem] border-[1.5px] border-period-line bg-gradient-to-b from-period to-[#eef4fc] p-4 shadow-[0_10px_30px_rgb(45_100_170/0.14)] sm:p-[1.125rem]"
          >
            <span aria-hidden="true" className="absolute inset-x-6 -top-[1.5px] h-1 rounded-b-md bg-gradient-to-r from-[#8fc3ee] via-pick to-[#8fc3ee]" />
            <div className="flex min-h-12 items-center gap-3">
              <IconTile raised>
                <CalendarIcon width={26} height={26} strokeWidth={1.7} />
              </IconTile>
              <h2 id={yearsHeadingId} className="text-[1.4375rem] font-extrabold text-title">
                دوره‌های مالی
              </h2>
              <span className="ms-auto">
                <Chip tone="period">{formatCount(years.length)} دوره مالی</Chip>
              </span>
            </div>
            <div className="mt-3">
              <SearchBox
                id={`${groupId}-year-search`}
                label="جست‌وجوی دوره مالی بر اساس عنوان یا تاریخ"
                placeholder="جستجو در دوره‌های مالی..."
                value={yearQuery}
                onChange={setYearQuery}
                tone="period"
              />
            </div>

            {visibleYears.length === 0 ? (
              <p role="status" className="mt-3 rounded-xl border border-dashed border-period-line bg-white/70 p-6 text-center text-sm text-sub">
                دوره مالی با این مشخصات پیدا نشد.
              </p>
            ) : (
              <div
                role="radiogroup"
                aria-labelledby={yearsHeadingId}
                className="-mx-1 mt-3 flex min-h-0 flex-1 flex-col gap-2.5 px-1 py-1 lg:overflow-y-auto"
                onKeyDown={() => {
                  keyboardRef.current = true;
                }}
                onPointerDown={() => {
                  keyboardRef.current = false;
                }}
              >
                {visibleYears.map((item) => (
                  <YearCard
                    key={item.key}
                    year={item}
                    name={`${groupId}-year`}
                    checked={item.key === selection.yearKey}
                    onChange={chooseYear}
                  />
                ))}
              </div>
            )}

            <p className="mt-3 flex items-start gap-2 rounded-xl bg-white/70 px-3 py-1.5 text-xs leading-6 text-sub">
              <InfoIcon width={16} height={16} className="mt-1 shrink-0 text-[#1d6aa5]" />
              <span>دوره بسته فقط برای مشاهده و گزارش‌گیری است.</span>
            </p>
          </section>

          <section
            ref={companiesPanelRef}
            aria-labelledby={companiesHeadingId}
            className="flex min-h-0 min-w-0 scroll-mt-4 flex-col rounded-[1.25rem] border border-[#dbe6f3] bg-white/90 p-4 shadow-[0_8px_28px_rgb(31_70_120/0.08)] sm:p-[1.125rem]"
          >
            <div className="flex min-h-12 flex-wrap items-center gap-x-3 gap-y-3">
              <IconTile>
                <BuildingIcon width={26} height={26} strokeWidth={1.7} />
              </IconTile>
              <h2 id={companiesHeadingId} className="text-[1.4375rem] font-extrabold text-title">
                شرکت‌ها
              </h2>
              <div className="order-last basis-full sm:order-none sm:mx-2 sm:basis-0 sm:flex-1">
                <SearchBox
                  id={`${groupId}-search`}
                  label="جست‌وجوی شرکت بر اساس نام یا کد ملی"
                  placeholder="جستجو در شرکت‌ها بر اساس نام یا کد ملی..."
                  value={query}
                  onChange={setQuery}
                  disabled={!year}
                  tone="plain"
                />
              </div>
              {year ? (
                <span className="ms-auto sm:ms-0">
                  <Chip tone="plain">{formatCount(year.companyCount)} شرکت</Chip>
                </span>
              ) : null}
            </div>

            {!year ? (
              <p className="mt-4 rounded-xl border border-dashed border-slate-400 bg-white/70 px-4 py-8 text-center text-sm leading-7 text-sub">
                <CalendarIcon width={28} height={28} className="mx-auto mb-2 text-slate-500" />
                ابتدا یک دوره مالی انتخاب کنید.
              </p>
            ) : visibleEntries.length === 0 ? (
              <p role="status" className="mt-4 rounded-xl border border-dashed border-slate-400 bg-white/70 p-6 text-center text-sm text-sub">
                شرکتی با این مشخصات پیدا نشد.
              </p>
            ) : (
              <div
                role="radiogroup"
                aria-labelledby={companiesHeadingId}
                className="-mx-2.5 mt-1.5 flex min-h-0 flex-1 flex-col gap-2.5 px-2.5 pb-1 pt-3.5 lg:overflow-y-auto"
              >
                {visibleEntries.map((item) => (
                  <CompanyRow
                    key={item.company.id}
                    entry={item}
                    name={`${groupId}-company`}
                    checked={item.company.id === selection.companyId}
                    onChange={chooseCompany}
                    isDefault={item.company.id === defaultCompanyId}
                  />
                ))}
              </div>
            )}

            {entry ? (
              <div className="mt-3 flex flex-wrap items-center gap-3 border-t border-[#e5edf6] pt-3">
                {defaultCompanyId === entry.company.id ? (
                  <Badge tone="info">این شرکت، پیش‌فرض حساب شماست</Badge>
                ) : (
                  <Button variant="secondary" disabled={savingDefault} onClick={() => void saveSelectedCompanyAsDefault()}>
                    {savingDefault ? 'در حال ذخیره...' : 'ذخیره شرکت انتخاب‌شده به‌عنوان پیش‌فرض'}
                  </Button>
                )}
                <span className="text-xs leading-5 text-sub">این ترجیح فقط برای حساب کاربری شما ذخیره می‌شود و ورود بعدی همچنان صفحه انتخاب را نمایش می‌دهد.</span>
              </div>
            ) : null}

            {hasEnterableYear && unavailableCompanies.length > 0 ? (
              <p className="mt-3 flex items-start gap-2 text-[0.8125rem] leading-6 text-sub">
                <InfoIcon width={16} height={16} className="mt-1 shrink-0" />
                <span>
                  دوره مالی قابل ورود برای این شرکت‌ها وجود ندارد:{' '}
                  {unavailableCompanies.map((item) => item.name).join('، ')}
                </span>
              </p>
            ) : null}
          </section>
        </div>
      </div>
    </SelectionLayout>
  );
}
