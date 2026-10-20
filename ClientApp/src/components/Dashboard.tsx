import { useState } from 'react';
import type { CompanySummary, Session } from '../api/types';
import { CompanyOverviewPanel } from './CompanyOverviewPanel';
import { EmptyState } from './Feedback';

const companyParameter = 'company';

function readRequestedCompany(): string | null {
  return new URLSearchParams(window.location.search).get(companyParameter);
}

function rememberCompany(companyId: string): void {
  const url = new URL(window.location.href);
  url.searchParams.set(companyParameter, companyId);
  window.history.replaceState(null, '', url);
}

type DashboardProps = {
  session: Session;
  companies: CompanySummary[];
  onUnauthenticated: (error: unknown) => boolean;
};

export function Dashboard({ session, companies, onUnauthenticated }: DashboardProps) {
  // The company in the address bar is only a preference: it is used when it is one of the
  // companies the server returned for this user, and the server checks it again.
  const [selectedId, setSelectedId] = useState<string | null>(() => {
    const requested = readRequestedCompany();
    return companies.find((company) => company.id === requested)?.id ?? companies[0]?.id ?? null;
  });

  function selectCompany(companyId: string) {
    setSelectedId(companyId);
    rememberCompany(companyId);
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-bold text-navy">داشبورد</h1>
        <p className="text-muted">خوش آمدید، {session.fullName}.</p>
      </div>

      {companies.length === 0 ? (
        <EmptyState title="در حال حاضر عضویت فعالی در شرکتی ندارید.">
          {session.accountKind === 'Infrastructure'
            ? 'حساب زیرساختی به شرکتی تعلق ندارد. برای کار با اطلاعات یک شرکت، با حساب کاربری عضو آن شرکت وارد شوید.'
            : 'دسترسی به اطلاعات هر شرکت تنها با عضویت معتبر و فعال در آن شرکت امکان‌پذیر است. برای دریافت دسترسی با مدیر سیستم تماس بگیرید.'}
        </EmptyState>
      ) : (
        <>
          {companies.length > 1 ? (
            <div className="flex flex-col gap-2 sm:flex-row sm:items-center">
              <label htmlFor="company-select" className="font-medium">
                شرکت
              </label>
              <select
                id="company-select"
                value={selectedId ?? ''}
                onChange={(event) => selectCompany(event.target.value)}
                className="rounded-lg border border-line bg-white px-3 py-2 sm:min-w-72"
              >
                {companies.map((company) => (
                  <option key={company.id} value={company.id}>
                    {company.name}
                  </option>
                ))}
              </select>
            </div>
          ) : null}

          {selectedId ? (
            <CompanyOverviewPanel companyId={selectedId} onUnauthenticated={onUnauthenticated} />
          ) : null}
        </>
      )}
    </div>
  );
}
