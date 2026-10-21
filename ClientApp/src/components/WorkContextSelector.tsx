import { useEffect, useId, useMemo, useRef, useState } from 'react';
import { ApiError, selectWorkContext } from '../api/client';
import type { CompanyChoice, FiscalPeriodChoice, SelectedContext, Session, WorkContext } from '../api/types';
import { formatPersianDate } from '../lib/date';
import { Alert } from './ui/Alert';
import { Badge } from './ui/Badge';
import { Button } from './ui/Button';
import { ChoiceCard } from './ui/ChoiceCard';
import { ArrowLeftIcon, BuildingIcon, CalendarIcon } from './ui/icons';

/** Companies are filtered only when the list is long enough for scanning to be tedious. */
export const searchThreshold = 6;

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
  const showSearch = companies.length > searchThreshold;

  const visibleCompanies = useMemo(() => {
    const needle = query.trim();
    return needle ? companies.filter((item) => item.name.includes(needle) || (item.nationalId ?? '').includes(needle)) : companies;
  }, [companies, query]);

  // Move the focus to the message so a keyboard or screen-reader user notices a failure.
  useEffect(() => {
    if (error) {
      alertRef.current?.focus();
    }
  }, [error]);

  function chooseCompany(id: string) {
    setCompanyId(id);
    setPeriodId(soleSelectablePeriod(companies.find((item) => item.id === id)));
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

  return (
    <div className="min-h-screen lg:grid lg:grid-cols-[minmax(0,24rem)_minmax(0,1fr)]">
      <aside className="bg-navy text-white lg:flex lg:flex-col lg:justify-between lg:p-10">
        <div className="mx-auto flex max-w-5xl items-center justify-between gap-3 px-4 py-4 sm:px-6 lg:mx-0 lg:block lg:p-0">
          <div className="flex items-center gap-3">
            <img src="/images/faranou-mark.svg" alt="" width={40} height={40} className="rounded-lg bg-white/10 p-1" />
            <span className="flex flex-col leading-tight">
              <strong className="text-lg">فرانو</strong>
              <span className="text-xs text-white/70">نرم‌افزار حسابداری هوشمند</span>
            </span>
          </div>
          <div className="flex items-center gap-3 text-sm lg:hidden">
            <span className="hidden text-white/80 sm:inline">{session.fullName}</span>
            <Button variant="secondary" onClick={onLogout}>
              خروج
            </Button>
          </div>
        </div>

        <div className="hidden lg:block">
          <h2 className="text-2xl font-bold leading-snug">به محیط کاری خود وارد شوید</h2>
          <p className="mt-3 text-sm leading-7 text-white/75">
            شرکت و دوره مالی‌ای را که می‌خواهید روی آن کار کنید انتخاب کنید. در هر ورود جدید این انتخاب دوباره پرسیده می‌شود.
          </p>
        </div>

        <div className="hidden flex-col gap-3 text-sm lg:flex">
          <p className="text-white/70">
            <span className="sr-only">کاربر وارد شده: </span>
            {session.fullName}
          </p>
          <div>
            <Button variant="secondary" onClick={onLogout}>
              خروج
            </Button>
          </div>
        </div>
      </aside>

      <main id="main" className="px-4 py-8 sm:px-8 lg:px-12 lg:py-12">
        <div className="mx-auto flex max-w-3xl flex-col gap-8">
          <header className="flex flex-col gap-2">
            <h1 className="text-2xl font-bold text-navy">انتخاب شرکت و دوره مالی</h1>
            <p className="text-sm leading-7 text-muted">
              ابتدا شرکت را انتخاب کنید، سپس یکی از دوره‌های مالی باز آن را. اطلاعات شرکت‌هایی که عضو آن‌ها نیستید نمایش داده نمی‌شود.
            </p>
          </header>

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

          <section aria-labelledby={`${groupId}-companies`} className="flex flex-col gap-4">
            <h2 id={`${groupId}-companies`} className="flex items-center gap-2 text-lg font-bold text-navy">
              <span aria-hidden="true" className="inline-flex size-7 items-center justify-center rounded-full bg-navy text-sm text-white">
                ۱
              </span>
              شرکت
            </h2>

            {showSearch ? (
              <div className="flex flex-col gap-1">
                <label htmlFor={`${groupId}-search`} className="text-sm font-medium">
                  جست‌وجوی شرکت
                </label>
                <input
                  id={`${groupId}-search`}
                  type="search"
                  value={query}
                  onChange={(event) => setQuery(event.target.value)}
                  placeholder="نام یا شناسه ملی"
                  className="min-h-11 rounded-lg border border-line bg-white px-3 py-2"
                />
              </div>
            ) : null}

            {visibleCompanies.length === 0 ? (
              <p role="status" className="rounded-xl border border-dashed border-line bg-white p-5 text-center text-sm text-muted">
                شرکتی با این مشخصات پیدا نشد.
              </p>
            ) : (
              <div role="radiogroup" aria-labelledby={`${groupId}-companies`} className="grid gap-3 sm:grid-cols-2">
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
                      <span className="flex items-start gap-3">
                        <span aria-hidden="true" className="mt-0.5 inline-flex size-10 shrink-0 items-center justify-center rounded-lg bg-cool text-navy">
                          <BuildingIcon />
                        </span>
                        <span className="flex min-w-0 flex-col gap-1 pe-7">
                          <span className="font-semibold text-ink [overflow-wrap:anywhere]">{item.name}</span>
                          <span className="text-xs text-muted">
                            شناسه ملی: {item.nationalId ? <bdi dir="ltr">{item.nationalId}</bdi> : '—'}
                          </span>
                          <span className="text-xs text-muted">نقش شما: {item.roleName}</span>
                          <span className="mt-1">
                            {unavailable ? (
                              <Badge tone="warning">بدون دوره مالی باز</Badge>
                            ) : (
                              <Badge tone="success">
                                {periodCount} دوره مالی باز
                              </Badge>
                            )}
                          </span>
                          {unavailable ? (
                            <span id={hintId} className="text-xs text-muted">
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

          <section aria-labelledby={`${groupId}-periods`} className="flex flex-col gap-4">
            <h2 id={`${groupId}-periods`} className="flex items-center gap-2 text-lg font-bold text-navy">
              <span aria-hidden="true" className="inline-flex size-7 items-center justify-center rounded-full bg-navy text-sm text-white">
                ۲
              </span>
              دوره مالی
            </h2>

            {!company ? (
              <p className="rounded-xl border border-dashed border-line bg-white p-5 text-center text-sm text-muted">
                پس از انتخاب شرکت، دوره‌های مالی آن در اینجا نمایش داده می‌شود.
              </p>
            ) : (
              <div role="radiogroup" aria-labelledby={`${groupId}-periods`} className="grid gap-3 sm:grid-cols-2">
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
                      <span className="flex items-start gap-3">
                        <span aria-hidden="true" className="mt-0.5 inline-flex size-10 shrink-0 items-center justify-center rounded-lg bg-cool text-navy">
                          <CalendarIcon />
                        </span>
                        <span className="flex min-w-0 flex-col gap-1 pe-7">
                          <span className="font-semibold text-ink [overflow-wrap:anywhere]">{period.title}</span>
                          <span className="text-xs tabular-nums text-muted">
                            {formatPersianDate(period.startDate)} تا {formatPersianDate(period.endDate)}
                          </span>
                          <span className="mt-1">
                            <Badge tone={period.isSelectable ? 'success' : 'neutral'}>{statusLabels[period.status]}</Badge>
                          </span>
                          {period.isSelectable ? null : (
                            <span id={hintId} className="text-xs text-muted">
                              ورود به این دوره امکان‌پذیر نیست.
                            </span>
                          )}
                        </span>
                      </span>
                    </ChoiceCard>
                  );
                })}
              </div>
            )}
          </section>

          <div className="flex flex-col-reverse gap-3 border-t border-line pt-6 sm:flex-row sm:items-center sm:justify-between">
            {onCancel ? (
              <Button variant="ghost" onClick={onCancel} disabled={submitting}>
                انصراف
              </Button>
            ) : (
              <span />
            )}
            <Button disabled={!canEnter} loading={submitting} loadingLabel="در حال ورود…" onClick={() => void enter()}>
              ورود به محیط کاری
              <ArrowLeftIcon />
            </Button>
          </div>
        </div>
      </main>
    </div>
  );
}
