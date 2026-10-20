import { useCallback, useEffect, useState } from 'react';
import { UnauthenticatedError, buildLoginUrl, getCompanies, getSession, loginPath, logout } from './api/client';
import type { CompanySummary, Session } from './api/types';
import { AppShell } from './components/AppShell';
import { Dashboard } from './components/Dashboard';
import { ErrorState, LoadingState } from './components/Feedback';

type AppState =
  | { kind: 'loading' }
  | { kind: 'redirecting' }
  | { kind: 'error'; message: string }
  | { kind: 'ready'; session: Session; companies: CompanySummary[] };

export type Navigate = (url: string) => void;

const defaultNavigate: Navigate = (url) => window.location.assign(url);

export function App({ navigate = defaultNavigate }: { navigate?: Navigate }) {
  const [state, setState] = useState<AppState>({ kind: 'loading' });
  const [attempt, setAttempt] = useState(0);

  // The sign-in stays on the existing Razor page; an expired session is sent there and
  // comes back to the current client route afterwards.
  const handleUnauthenticated = useCallback(
    (error: unknown): boolean => {
      if (error instanceof UnauthenticatedError) {
        setState({ kind: 'redirecting' });
        navigate(buildLoginUrl(window.location));
        return true;
      }
      return false;
    },
    [navigate],
  );

  useEffect(() => {
    const controller = new AbortController();
    setState({ kind: 'loading' });

    Promise.all([getSession(controller.signal), getCompanies(controller.signal)])
      .then(([session, companies]) => setState({ kind: 'ready', session, companies }))
      .catch((error: unknown) => {
        if (controller.signal.aborted || handleUnauthenticated(error)) {
          return;
        }
        setState({
          kind: 'error',
          message: error instanceof Error ? error.message : 'بارگذاری داشبورد با خطا مواجه شد.',
        });
      });

    return () => controller.abort();
  }, [attempt, handleUnauthenticated]);

  const handleLogout = useCallback(async () => {
    await logout();
    navigate(loginPath);
  }, [navigate]);

  if (state.kind === 'loading') {
    return (
      <div className="mx-auto max-w-xl p-6">
        <LoadingState label="در حال بارگذاری داشبورد…" />
      </div>
    );
  }

  if (state.kind === 'redirecting') {
    return (
      <div className="mx-auto max-w-xl p-6">
        <LoadingState label="در حال انتقال به صفحه ورود…" />
      </div>
    );
  }

  if (state.kind === 'error') {
    return (
      <div className="mx-auto max-w-xl p-6">
        <ErrorState message={state.message} onRetry={() => setAttempt((value) => value + 1)} />
      </div>
    );
  }

  return (
    <AppShell session={state.session} onLogout={handleLogout}>
      <Dashboard session={state.session} companies={state.companies} onUnauthenticated={handleUnauthenticated} />
    </AppShell>
  );
}
