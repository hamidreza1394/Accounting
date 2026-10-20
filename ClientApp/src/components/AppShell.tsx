import { useState, type ReactNode } from 'react';
import type { Session } from '../api/types';

type AppShellProps = {
  session: Session;
  onLogout: () => Promise<void>;
  children: ReactNode;
};

/**
 * The right-to-left frame of the client: brand, the signed-in user, sign-out and the main
 * navigation. Only screens that exist are listed; future accounting modules are not shown
 * as if they were available.
 */
export function AppShell({ session, onLogout, children }: AppShellProps) {
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
