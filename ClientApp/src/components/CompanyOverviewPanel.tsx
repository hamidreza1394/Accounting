import { useEffect, useState } from 'react';
import { getCompanyOverview } from '../api/client';
import type { CompanyOverview, FiscalPeriodStatus } from '../api/types';
import { formatPersianDate } from '../lib/date';
import { EmptyState, ErrorState, LoadingState } from './Feedback';

type OverviewState =
  | { kind: 'loading' }
  | { kind: 'error'; message: string }
  | { kind: 'ready'; overview: CompanyOverview };

const statusLabels: Record<FiscalPeriodStatus, string> = {
  Open: 'باز',
  Closed: 'بسته',
  Archived: 'بایگانی',
};

const statusStyles: Record<FiscalPeriodStatus, string> = {
  Open: 'bg-success-soft text-success',
  Closed: 'bg-warning-soft text-warning',
  Archived: 'bg-cool text-muted',
};

type CompanyOverviewPanelProps = {
  companyId: string;
  /** The fiscal period the user entered; it is marked in the list. */
  currentPeriodId: string;
  onUnauthenticated: (error: unknown) => boolean;
};

/**
 * The real data of one company, as returned by the server for the signed-in user. The
 * server decides whether the company may be accessed; this component never assumes it.
 */
export function CompanyOverviewPanel({ companyId, currentPeriodId, onUnauthenticated }: CompanyOverviewPanelProps) {
  const [state, setState] = useState<OverviewState>({ kind: 'loading' });
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    setState({ kind: 'loading' });

    getCompanyOverview(companyId, controller.signal)
      .then((overview) => setState({ kind: 'ready', overview }))
      .catch((error: unknown) => {
        if (controller.signal.aborted || onUnauthenticated(error)) {
          return;
        }
        setState({
          kind: 'error',
          message: error instanceof Error ? error.message : 'دریافت اطلاعات شرکت با خطا مواجه شد.',
        });
      });

    return () => controller.abort();
  }, [companyId, attempt, onUnauthenticated]);

  if (state.kind === 'loading') {
    return <LoadingState label="در حال دریافت اطلاعات شرکت…" />;
  }

  if (state.kind === 'error') {
    return <ErrorState message={state.message} onRetry={() => setAttempt((value) => value + 1)} />;
  }

  const { company, fiscalPeriods } = state.overview;

  return (
    <div className="flex flex-col gap-6">
      <section aria-labelledby="company-heading" className="rounded-2xl border border-line bg-white p-5 shadow-sm">
        <h2 id="company-heading" className="text-lg font-bold text-navy">
          {company.name}
        </h2>
        <dl className="mt-4 grid gap-4 text-sm sm:grid-cols-2">
          <div>
            <dt className="text-muted">نقش شما در این شرکت</dt>
            <dd className="mt-1 font-medium">{company.roleName}</dd>
          </div>
          <div>
            <dt className="text-muted">شناسه ملی</dt>
            <dd className="mt-1 font-medium">
              {company.nationalId ? <bdi dir="ltr">{company.nationalId}</bdi> : '—'}
            </dd>
          </div>
        </dl>
      </section>

      <section aria-labelledby="periods-heading" className="rounded-2xl border border-line bg-white p-5 shadow-sm">
        <h2 id="periods-heading" className="text-lg font-bold text-navy">
          دوره‌های مالی
        </h2>

        {fiscalPeriods.length === 0 ? (
          <div className="mt-4">
            <EmptyState title="هنوز دوره مالی برای این شرکت ثبت نشده است.">
              پس از تعریف دوره مالی، فهرست آن در این بخش نمایش داده می‌شود.
            </EmptyState>
          </div>
        ) : (
          <>
          {/* Small screens: one card per period instead of a horizontally scrolling table. */}
          <ul className="mt-4 flex flex-col gap-3 sm:hidden" aria-label="دوره‌های مالی">
            {fiscalPeriods.map((period) => (
              <li key={period.id} className="rounded-xl border border-line p-4 text-sm">
                <div className="flex items-center justify-between gap-2">
                  <span className="font-medium">
                    {period.title}
                    {period.id === currentPeriodId ? <span className="ms-2 text-xs font-semibold text-teal">(دوره جاری)</span> : null}
                  </span>
                  <span className={`rounded-full px-2.5 py-1 text-xs font-medium ${statusStyles[period.status]}`}>
                    {statusLabels[period.status]}
                  </span>
                </div>
                <p className="mt-2 text-muted tabular-nums">
                  {formatPersianDate(period.startDate)} تا {formatPersianDate(period.endDate)}
                </p>
              </li>
            ))}
          </ul>
          <div className="mt-4 hidden overflow-x-auto sm:block">
            <table className="w-full border-collapse text-sm">
              <caption className="sr-only">دوره‌های مالی {company.name}</caption>
              <thead>
                <tr className="border-b border-line text-start text-muted">
                  <th scope="col" className="py-2 pe-4 font-medium">عنوان</th>
                  <th scope="col" className="py-2 pe-4 font-medium">تاریخ شروع</th>
                  <th scope="col" className="py-2 pe-4 font-medium">تاریخ پایان</th>
                  <th scope="col" className="py-2 font-medium">وضعیت</th>
                </tr>
              </thead>
              <tbody>
                {fiscalPeriods.map((period) => (
                  <tr key={period.id} className="border-b border-line/70 last:border-0">
                    <td className="py-3 pe-4 font-medium">
                      {period.title}
                      {period.id === currentPeriodId ? <span className="ms-2 text-xs font-semibold text-teal">(دوره جاری)</span> : null}
                    </td>
                    <td className="py-3 pe-4 tabular-nums">{formatPersianDate(period.startDate)}</td>
                    <td className="py-3 pe-4 tabular-nums">{formatPersianDate(period.endDate)}</td>
                    <td className="py-3">
                      <span className={`rounded-full px-2.5 py-1 text-xs font-medium ${statusStyles[period.status]}`}>
                        {statusLabels[period.status]}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          </>
        )}
      </section>

      <section aria-labelledby="modules-heading" className="rounded-2xl border border-gold/40 bg-warning-soft p-5 text-sm">
        <h2 id="modules-heading" className="font-bold text-warning">
          وضعیت ماژول‌های حسابداری
        </h2>
        <p className="mt-2 text-ink">
          اسناد حسابداری، سرفصل حساب‌ها و گزارش‌های مالی هنوز پیاده‌سازی نشده‌اند؛ به همین دلیل هیچ آمار مالی در این
          داشبورد نمایش داده نمی‌شود.
        </p>
      </section>
    </div>
  );
}
