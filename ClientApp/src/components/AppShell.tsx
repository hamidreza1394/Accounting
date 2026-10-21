import { useState, type ReactNode } from 'react';
import type { SelectedContext, Session } from '../api/types';
import { formatPersianDate } from '../lib/date';
import { Button } from './ui/Button';
import { BuildingIcon, CalendarIcon, SwapIcon } from './ui/icons';

type AppShellProps = {
  session: Session;
  /** The company and fiscal period the user works in during this sign-in. */
  context: SelectedContext;
  onChangeContext: () => void;
  onLogout: () => Promise<void>;
  children: ReactNode;
};

/**
 * The right-to-left frame of the client: brand, the signed-in user, sign-out and the main
 * navigation. Only screens that exist are listed; future accounting modules are not shown
 * as if they were available.
 */
export function AppShell({ session, context, onChangeContext, onLogout, children }: AppShellProps) {
  const [loggingOut, setLoggingOut] = useState(false);
  const [logoutError, setLogoutError] = useState<string | null>(null);

  async function handleLogout() {
    setLoggingOut(true);
    setLogoutError(null);
    try {
      await onLogout();
    } catch (error: unknown) {
      setLogoutError(error instanceof Error ? error.message : 'خروج با خطا مواجه شد.');
      setLoggingOut(false);
    }
  }

  return (
    <div className="min-h-screen">
      <header className="bg-navy text-white shadow-md">
        <div className="mx-auto flex max-w-7xl flex-wrap items-center justify-between gap-3 px-4 py-3 sm:px-6">
          <a href="/app/" className="flex items-center gap-3" aria-label="فرانو — داشبورد">
            <img src="/images/faranou-mark.svg" alt="" width={36} height={36} className="rounded-lg bg-white/10 p-1" />
            <span className="flex flex-col leading-tight">
              <strong className="text-lg">فرانو</strong>
              <span className="text-xs text-white/70">نرم‌افزار حسابداری هوشمند</span>
            </span>
          </a>

          <div className="flex items-center gap-3 text-sm">
            <span className="hidden sm:inline text-white/80">
              <span className="sr-only">کاربر وارد شده: </span>
              {session.fullName}
            </span>
            <button
              type="button"
              onClick={() => void handleLogout()}
              disabled={loggingOut}
              className="rounded-lg border border-white/30 px-3 py-1.5 font-medium hover:bg-white/10 disabled:opacity-60"
            >
              {loggingOut ? 'در حال خروج…' : 'خروج'}
            </button>
          </div>
        </div>
      </header>

      <div className="border-b border-line bg-white">
        <div className="mx-auto flex max-w-7xl flex-col gap-3 px-4 py-3 text-sm sm:px-6 md:flex-row md:items-center md:justify-between">
          <dl aria-label="زمینه کاری جاری" className="flex flex-wrap items-center gap-x-6 gap-y-2">
            <div className="flex min-w-0 items-center gap-2">
              <BuildingIcon className="shrink-0 text-navy" />
              <dt className="text-muted">شرکت</dt>
              <dd className="min-w-0 font-semibold text-ink [overflow-wrap:anywhere]">{context.company.name}</dd>
            </div>
            <div className="flex items-center gap-2">
              <CalendarIcon className="shrink-0 text-navy" />
              <dt className="text-muted">دوره مالی</dt>
              <dd className="font-semibold text-ink">
                {context.fiscalPeriod.title}
                <span className="ms-2 text-xs font-normal tabular-nums text-muted">
                  ({formatPersianDate(context.fiscalPeriod.startDate)} تا {formatPersianDate(context.fiscalPeriod.endDate)})
                </span>
              </dd>
            </div>
          </dl>
          <div>
            <Button variant="secondary" onClick={onChangeContext}>
              <SwapIcon />
              تغییر شرکت یا دوره
            </Button>
          </div>
        </div>
      </div>

      {logoutError ? (
        <div role="alert" className="bg-danger-soft px-4 py-2 text-center text-sm text-danger">
          {logoutError}
        </div>
      ) : null}

      <div className="mx-auto flex max-w-7xl flex-col gap-6 px-4 py-6 sm:px-6 lg:flex-row">
        <nav aria-label="منوی اصلی" className="lg:w-56 lg:shrink-0">
          <ul className="flex gap-2 overflow-x-auto lg:flex-col">
            <li>
              <a
                href="/app/"
                aria-current="page"
                className="block whitespace-nowrap rounded-lg bg-white px-4 py-2 font-medium text-navy shadow-sm ring-1 ring-line"
              >
                داشبورد
              </a>
            </li>
            <li>
              <a
                href="/"
                className="block whitespace-nowrap rounded-lg px-4 py-2 text-muted hover:bg-white hover:text-navy"
              >
                نسخه قبلی (Razor)
              </a>
            </li>
          </ul>
        </nav>

        <main id="main" className="min-w-0 flex-1">
          {children}
        </main>
      </div>
    </div>
  );
}
