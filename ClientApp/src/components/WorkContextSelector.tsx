import { useEffect, useId, useMemo, useRef, useState } from 'react';
import { ApiError, selectWorkContext } from '../api/client';
import type { CompanyChoice, FiscalPeriodChoice, SelectedContext, Session, WorkContext } from '../api/types';
import { formatPersianDate } from '../lib/date';
import { matchesCompany } from '../lib/search';
import { Alert } from './ui/Alert';
import { Badge } from './ui/Badge';
import { Button } from './ui/Button';
import { ChoiceCard } from './ui/ChoiceCard';
import { FaranouLogo } from './ui/FaranouLogo';
import { ArrowLeftIcon, BuildingIcon, CalendarIcon, CloseIcon, InfoIcon, LockIcon, SearchIcon, UserIcon } from './ui/icons';

const statusLabels = { Open: 'باز', Closed: 'بسته', Archived: 'بایگانی‌شده' } as const;

type SelectorProps = {
  session: Session;
  context: WorkContext;
  /** Shown when the user came here to change an existing choice. */
  current: SelectedContext | null;
  onSelected: (selected: SelectedContext) => void;
  onCancel?: (() => void) | undefined;
  onLogout: () => void;
  /** Reloads the choices from the server (after the server refused a stale choice). */
  onRefresh: () => void;
  /** Receives every error; returns true when it was a 401 and the app is leaving for the sign-in. */
  onUnauthenticated: (error: unknown) => boolean;
};

function selectablePeriods(company: CompanyChoice): FiscalPeriodChoice[] {
  return company.fiscalPeriods.filter((period) => period.isSelectable);
}

/** The period to pre-select: only when there is no ambiguity. */
function soleSelectablePeriod(company: CompanyChoice | undefined): string | null {
  const periods = company ? selectablePeriods(company) : [];
  return periods.length === 1 ? (periods[0]?.id ?? null) : null;
}

/**
 * Step 1 and 2 of entering the working environment: the company, then one of its fiscal
 * periods. The server decides which choices exist and which of them can be entered; the screen
 * only presents them and sends the pair back, where the server validates it once more.
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

  const [companyId, setCompanyId] = useState<string | null>(() => {
    if (current && companies.some((company) => company.id === current.company.id)) {
      return current.company.id;
    }
    return companies.length === 1 ? (companies[0]?.id ?? null) : null;
  });
  const [periodId, setPeriodId] = useState<string | null>(() => {
    const company = companies.find((item) => item.id === companyId);
    if (current && company?.id === current.company.id && company.fiscalPeriods.some((p) => p.id === current.fiscalPeriod.id && p.isSelectable)) {
      return current.fiscalPeriod.id;
    }
    return soleSelectablePeriod(company);
  });
  const [query, setQuery] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<{ message: string; stale: boolean } | null>(null);
  const submittingRef = useRef(false);
  const alertRef = useRef<HTMLDivElement>(null);

  const company = companies.find((item) => item.id === companyId);

  // The list is already limited to the user's own companies by the server (a handful per user),
  // so the search filters it in memory instead of asking the server again for every keystroke.
  const visibleCompanies = useMemo(
    () => companies.filter((item) => matchesCompany(query, item.name, item.nationalId)),
    [companies, query],
  );

  // Move the focus to the message so a keyboard or screen-reader user notices a failure.
  useEffect(() => {
    if (error) {
      alertRef.current?.focus();
    }
  }, [error]);

  function chooseCompany(id: string) {
    const next = companies.find((item) => item.id === id);
    setCompanyId(id);
    // The fiscal period belongs to one company: keep it only if it is still a valid choice of the
    // new company (it never is for a different company, because ids are per company), else clear it.
    setPeriodId((previous) =>
      previous && next?.fiscalPeriods.some((period) => period.id === previous && period.isSelectable)
        ? previous
        : soleSelectablePeriod(next),
    );
    setError(null);
  }

  async function enter() {
    // A ref, not state: two quick activations must never send two requests.
    if (submittingRef.current || !companyId || !periodId) {
      return;
    }
    submittingRef.current = true;
    setSubmitting(true);
    setError(null);
    try {
      onSelected(await selectWorkContext(companyId, periodId));
    } catch (caught: unknown) {
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
    } finally {
      submittingRef.current = false;
      setSubmitting(false);
    }
  }

  const canEnter = Boolean(companyId && periodId);
  const selectedPeriod = company?.fiscalPeriods.find((period) => period.id === periodId);

  return (
    <div className="flex min-h-screen flex-col bg-ws-bg text-ws-text">
      <header className="border-b-[3px] border-ws-teal bg-ws-navy text-white">
        <div className="mx-auto flex max-w-[88rem] items-center justify-between gap-3 px-4 py-3 sm:px-8">
          <FaranouLogo />
          <div className="flex items-center gap-3 sm:gap-4">
            <span aria-hidden="true" className="hidden size-10 items-center justify-center rounded-full border border-white/25 bg-white/10 sm:inline-flex">
              <UserIcon />
            </span>
            <span className="flex flex-col leading-snug">
              <span className="hidden text-xs text-slate-300 sm:block">خوش آمدید</span>
              <span className="text-sm font-semibold sm:text-base">
                <span className="sr-only">کاربر وارد شده: </span>
                {session.fullName}
              </span>
            </span>
            <button
              type="button"
              onClick={onLogout}
              className="min-h-11 cursor-pointer rounded-[10px] border border-white/30 px-4 text-sm font-medium hover:bg-white/10"
            >
              خروج
            </button>
          </div>
        </div>
      </header>

      <main id="main" className="mx-auto w-full max-w-[88rem] flex-1 px-4 py-6 sm:px-8 sm:py-8">
        <div className="flex flex-col gap-6">
          <div className="flex flex-col gap-1.5">
            <h1 className="text-2xl font-bold leading-snug text-ws-navy sm:text-[1.75rem]">انتخاب دوره مالی و شرکت</h1>
            <p className="max-w-3xl text-sm leading-8 text-ws-sub sm:text-[0.95rem]">
              ابتدا شرکت موردنظر خود را از میان شرکت‌های مجاز حساب کاربری‌تان انتخاب کنید، سپس یکی از دوره‌های مالی باز آن را انتخاب کرده و وارد محیط کاری شوید.
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

          <div className="grid gap-6 lg:grid-cols-[22rem_minmax(0,1fr)] lg:items-start">
            <section aria-labelledby={`${groupId}-companies`} className="flex min-w-0 flex-col gap-3.5 lg:col-start-2 lg:row-start-1">
              <h2 id={`${groupId}-companies`} className="flex items-center gap-3 text-lg font-bold text-ws-navy">
                <span aria-hidden="true" className="inline-flex size-[30px] items-center justify-center rounded-full bg-ws-navy text-sm text-white">
                  ۱
                </span>
                شرکت
              </h2>

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
                  className="min-h-12 w-full rounded-[10px] border border-slate-400 bg-white pe-4 ps-12 text-[0.95rem] text-ws-text placeholder:text-ws-sub"
                />
                <SearchIcon className="pointer-events-none absolute start-4 top-1/2 -translate-y-1/2 text-ws-sub" />
              </div>

              {visibleCompanies.length === 0 ? (
                <p role="status" className="rounded-xl border border-dashed border-slate-400 bg-white p-6 text-center text-sm text-ws-sub">
                  شرکتی با این مشخصات پیدا نشد.
                </p>
              ) : (
                <div role="radiogroup" aria-labelledby={`${groupId}-companies`} className="flex flex-col gap-2.5">
                  {visibleCompanies.map((item) => {
                    const periodCount = selectablePeriods(item).length;
                    const unavailable = periodCount === 0;
                    const hintId = `${groupId}-company-${item.id}-hint`;

                    return (
                      <ChoiceCard
                        key={item.id}
                        name={`${groupId}-company`}
                        value={item.id}
                        checked={item.id === companyId}
                        disabled={unavailable}
                        describedBy={unavailable ? hintId : undefined}
                        onChange={chooseCompany}
                      >
                        <span className="flex items-start gap-4">
                          <span
                            aria-hidden="true"
                            className={`inline-flex size-[52px] shrink-0 items-center justify-center rounded-xl ${
                              unavailable ? 'bg-slate-200 text-ws-off' : 'bg-ws-blue-soft text-ws-blue-deep'
                            }`}
                          >
                            <BuildingIcon width={26} height={26} />
                          </span>
                          <span className="flex min-w-0 flex-col gap-1.5">
                            <span className="flex flex-wrap items-center gap-x-3 gap-y-1">
                              <span className="text-lg font-bold text-ws-navy [overflow-wrap:anywhere]">{item.name}</span>
                              {unavailable ? (
                                <Badge tone="warning">بدون دوره مالی باز</Badge>
                              ) : (
                                <Badge tone="info" icon={<CalendarIcon width={14} height={14} />}>
                                  {periodCount} دوره مالی باز
                                </Badge>
                              )}
                            </span>
                            <span className="flex flex-wrap gap-x-5 gap-y-0.5 text-[0.8125rem] text-ws-sub">
                              <span>
                                شناسه ملی:{' '}
                                {item.nationalId ? (
                                  <bdi dir="ltr" className="font-medium tabular-nums text-ws-text">
                                    {item.nationalId}
                                  </bdi>
                                ) : (
                                  '—'
                                )}
                              </span>
                              <span>
                                نقش شما: <b className="font-semibold text-ws-text">{item.roleName}</b>
                              </span>
                            </span>
                            {unavailable ? (
                              <span id={hintId} className="inline-flex items-center gap-1.5 text-[0.8125rem] font-medium text-ws-sub">
                                <InfoIcon width={15} height={15} />
                                برای ورود، یک دوره مالی باز برای این شرکت لازم است.
                              </span>
                            ) : null}
                          </span>
                        </span>
                      </ChoiceCard>
                    );
                  })}
                </div>
              )}
            </section>

            <section
              aria-labelledby={`${groupId}-periods`}
              className="flex min-w-0 flex-col gap-3.5 rounded-xl border border-ws-line bg-white p-5 lg:col-start-1 lg:row-start-1"
            >
              <h2 id={`${groupId}-periods`} className="flex items-center gap-3 text-[1.0625rem] font-bold text-ws-navy">
                <span aria-hidden="true" className="inline-flex size-[30px] items-center justify-center rounded-full bg-ws-navy text-sm text-white">
                  ۲
                </span>
                دوره‌های مالی قابل انتخاب
              </h2>
              <p className="text-[0.8125rem] leading-8 text-ws-sub">
                {company ? `دوره‌های مالی متعلق به ${company.name}` : 'دوره‌های مالی متعلق به شرکت انتخاب‌شده'}
              </p>

              {!company ? (
                <p className="rounded-[10px] border border-dashed border-slate-400 px-4 py-6 text-center text-sm leading-8 text-ws-sub">
                  <CalendarIcon width={30} height={30} className="mx-auto mb-2 text-slate-500" />
                  ابتدا یک شرکت انتخاب کنید.
                </p>
              ) : (
                <div role="radiogroup" aria-labelledby={`${groupId}-periods`} className="flex flex-col gap-2.5">
                  {company.fiscalPeriods.map((period) => {
                    const hintId = `${groupId}-period-${period.id}-hint`;

                    return (
                      <ChoiceCard
                        key={period.id}
                        name={`${groupId}-period`}
                        value={period.id}
                        checked={period.id === periodId}
                        disabled={!period.isSelectable}
                        describedBy={period.isSelectable ? undefined : hintId}
                        onChange={setPeriodId}
                      >
                        <span className="flex min-w-0 flex-col gap-1">
                          <span className="flex flex-wrap items-center gap-x-2.5 gap-y-1">
                            <span className="text-base font-bold text-ws-navy [overflow-wrap:anywhere]">{period.title}</span>
                            <Badge
                              tone={period.isSelectable ? 'info' : 'neutral'}
                              icon={period.isSelectable ? null : <LockIcon width={13} height={13} />}
                            >
                              {statusLabels[period.status]}
                            </Badge>
                          </span>
                          <span className="text-[0.8125rem] tabular-nums text-ws-sub">
                            از {formatPersianDate(period.startDate)} تا {formatPersianDate(period.endDate)}
                          </span>
                          {period.isSelectable ? null : (
                            <span id={hintId} className="text-[0.8125rem] text-ws-sub">
                              ورود به این دوره امکان‌پذیر نیست.
                            </span>
                          )}
                        </span>
                      </ChoiceCard>
                    );
                  })}
                </div>
              )}

              <p className="flex items-start gap-2.5 border-t border-slate-200 pt-3.5 text-[0.8125rem] leading-8 text-ws-sub">
                <InfoIcon width={18} height={18} className="mt-2 shrink-0 text-ws-blue" />
                <span>فقط دوره‌های مالی مجاز و قابل دسترسی نمایش داده می‌شوند. برای دسترسی به دوره‌های دیگر، با مدیر سیستم تماس بگیرید.</span>
              </p>
            </section>
          </div>
        </div>
      </main>

      <footer className="sticky bottom-0 z-10 border-t border-ws-line bg-white shadow-[0_-6px_20px_rgba(11,37,69,0.06)]">
        <div className="mx-auto flex max-w-[88rem] flex-wrap items-center gap-3 px-4 py-3 sm:px-8">
          <div aria-live="polite" className="flex min-w-0 items-center gap-3 rounded-[10px] border border-ws-line bg-ws-bg px-3.5 py-2">
            <span aria-hidden="true" className="inline-flex size-11 shrink-0 items-center justify-center rounded-[10px] bg-ws-blue-soft text-ws-blue-deep">
              <BuildingIcon width={22} height={22} />
            </span>
            <span className="flex min-w-0 flex-col leading-snug">
              <span className="text-xs text-ws-sub">شرکت انتخاب‌شده</span>
              <strong className="text-base text-ws-navy [overflow-wrap:anywhere]">{company ? company.name : '—'}</strong>
              <span className="text-[0.8125rem] text-ws-sub">{selectedPeriod ? selectedPeriod.title : '—'}</span>
            </span>
          </div>

          <span className="hidden flex-1 sm:block" />

          <div className="flex w-full flex-col-reverse gap-2.5 sm:w-auto sm:flex-row sm:items-center">
            <Button variant="secondary" onClick={onCancel ?? onLogout} disabled={submitting}>
              <CloseIcon width={17} height={17} />
              انصراف
            </Button>
            <Button disabled={!canEnter} loading={submitting} loadingLabel="در حال ورود…" onClick={() => void enter()}>
              ورود به محیط کاری
              <ArrowLeftIcon />
            </Button>
          </div>
        </div>
      </footer>
    </div>
  );
}
