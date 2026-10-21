import { useCallback, useEffect, useRef, useState } from 'react';
import {
  UnauthenticatedError,
  buildLoginUrl,
  getSession,
  getWorkContext,
  loginPath,
  logout,
  selectWorkContext,
} from './api/client';
import type { SelectedContext, Session, WorkContext } from './api/types';
import { AppShell } from './components/AppShell';
import { BlockedAccess } from './components/BlockedAccess';
import { Dashboard } from './components/Dashboard';
import { ErrorState, LoadingState } from './components/Feedback';
import { WorkContextSelector } from './components/WorkContextSelector';

type AppState =
  | { kind: 'loading' }
  | { kind: 'entering' }
  | { kind: 'redirecting' }
  | { kind: 'error'; message: string }
  | { kind: 'ready'; session: Session; context: WorkContext; selecting: boolean };

export type Navigate = (url: string) => void;

const defaultNavigate: Navigate = (url) => window.location.assign(url);

function Centered({ children }: { children: React.ReactNode }) {
  return <div className="mx-auto max-w-xl p-6">{children}</div>;
}

export function App({ navigate = defaultNavigate }: { navigate?: Navigate }) {
  const [state, setState] = useState<AppState>({ kind: 'loading' });
  const [attempt, setAttempt] = useState(0);
  // The automatic entry is attempted once per load; a refused attempt falls back to the
  // selection screen instead of looping.
  const enteredAutomatically = useRef(false);
  // Set when the user asked to change the company or period, so the reload that fetches fresh
  // choices does not send them back to the dashboard.
  const wantsSelection = useRef(false);

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
    enteredAutomatically.current = false;

    Promise.all([getSession(controller.signal), getWorkContext(controller.signal)])
      .then(async ([session, context]) => {
        if (controller.signal.aborted) {
          return;
        }

        // The server found exactly one company with exactly one open period: confirm that
        // single choice (the server validates it again) and enter without asking.
        if (context.current === null && context.entry === 'directEntry' && context.directSelection) {
          enteredAutomatically.current = true;
          setState({ kind: 'entering' });
          try {
            const current = await selectWorkContext(
              context.directSelection.companyId,
              context.directSelection.fiscalPeriodId,
            );
            if (!controller.signal.aborted) {
              setState({ kind: 'ready', session, context: { ...context, current }, selecting: false });
            }
          } catch (error: unknown) {
            if (controller.signal.aborted || handleUnauthenticated(error)) {
              return;
            }
            // The choice changed between the two requests: let the user choose.
            setState({ kind: 'ready', session, context, selecting: true });
          }
          return;
        }

        setState({
          kind: 'ready',
          session,
          context,
          selecting:
            wantsSelection.current || (context.current === null && context.entry === 'selectionRequired'),
        });
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted || handleUnauthenticated(error)) {
          return;
        }
        setState({
          kind: 'error',
          message: error instanceof Error ? error.message : 'بارگذاری برنامه با خطا مواجه شد.',
        });
      });

    return () => controller.abort();
  }, [attempt, handleUnauthenticated]);

  const reload = useCallback(() => setAttempt((value) => value + 1), []);

  const handleLogout = useCallback(async () => {
    await logout();
    navigate(loginPath);
  }, [navigate]);

  const handleLogoutFromScreen = useCallback(() => {
    void handleLogout().catch((error: unknown) => {
      if (!handleUnauthenticated(error)) {
        setState({
          kind: 'error',
          message: error instanceof Error ? error.message : 'خروج با خطا مواجه شد.',
        });
      }
    });
  }, [handleLogout, handleUnauthenticated]);

  const handleSelected = useCallback((selected: SelectedContext) => {
    wantsSelection.current = false;
    setState((previous) =>
      previous.kind === 'ready'
        ? { ...previous, context: { ...previous.context, current: selected }, selecting: false }
        : previous,
    );
  }, []);

  if (state.kind === 'loading') {
    return (
      <Centered>
        <LoadingState label="در حال بارگذاری…" />
      </Centered>
    );
  }

  if (state.kind === 'entering') {
    return (
      <Centered>
        <LoadingState label="در حال ورود به محیط کاری…" />
      </Centered>
    );
  }

  if (state.kind === 'redirecting') {
    return (
      <Centered>
        <LoadingState label="در حال انتقال به صفحه ورود…" />
      </Centered>
    );
  }

  if (state.kind === 'error') {
    return (
      <Centered>
        <ErrorState message={state.message} onRetry={reload} />
      </Centered>
    );
  }

  const { session, context, selecting } = state;

  if (context.entry === 'noCompany' || (context.entry === 'noEligiblePeriod' && context.current === null)) {
    return (
      <BlockedAccess
        session={session}
        reason={context.entry === 'noCompany' ? 'noCompany' : 'noEligiblePeriod'}
        companies={context.companies}
        onRetry={reload}
        onLogout={handleLogoutFromScreen}
      />
    );
  }

  if (context.current === null || selecting) {
    return (
      <WorkContextSelector
        session={session}
        context={context}
        current={context.current}
        onSelected={handleSelected}
        onCancel={
          context.current
            ? () => {
                wantsSelection.current = false;
                setState({ ...state, selecting: false });
              }
            : undefined
        }
        onLogout={handleLogoutFromScreen}
        onRefresh={reload}
        onUnauthenticated={handleUnauthenticated}
      />
    );
  }

  return (
    <AppShell
      session={session}
      context={context.current}
      onChangeContext={() => {
        // Fresh choices from the server: memberships and periods may have changed.
        wantsSelection.current = true;
        reload();
      }}
      onLogout={handleLogout}
    >
      <Dashboard session={session} context={context.current} onUnauthenticated={handleUnauthenticated} />
    </AppShell>
  );
}
