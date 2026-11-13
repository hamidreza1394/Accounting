import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
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
import { TradeAccessProvider } from './features/trade/access/TradeAccess';
import { BlockedAccess } from './components/BlockedAccess';
import { AppRouteDataContext, appRoutes, type AppRouteData } from './components/AppRoutes';
import { ErrorState, LoadingScreen } from './components/Feedback';
import { SelectionLayout } from './components/SelectionLayout';
import { WorkContextSelector } from './components/WorkContextSelector';
import { useCompanyPermissions } from './lib/useCompanyPermissions';
import { RouteView } from './lib/router';

type AppState =
  | { kind: 'loading' }
  | { kind: 'entering' }
  | { kind: 'redirecting' }
  | { kind: 'error'; message: string }
  | { kind: 'ready'; session: Session; context: WorkContext; selecting: boolean };

export type Navigate = (url: string) => void;

const defaultNavigate: Navigate = (url) => window.location.assign(url);

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
    // A reload that starts from an already-entered workspace must not swap the shell for the
    // full-page loading screen: that unmounts AppShell and silently discards its open tabs.
    // Keep the previous ready state until the fresh choices arrive; every other case (first
    // load, retry after an error, no active context) still shows the loading screen.
    setState((previous) =>
      previous.kind === 'ready' && previous.context.current !== null ? previous : { kind: 'loading' },
    );
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

  // The confirmed context change (Issue #444 stage 1 completion) refetches ONLY the work
  // context: the workspace stays mounted the whole time, so the open tabs and their global
  // forms survive the round trip. A failure lands in the shared error state; the previous
  // context and tabs remain untouched until the server accepts the new selection.
  const changeContext = useCallback(() => {
    wantsSelection.current = true;
    const controller = new AbortController();
    getWorkContext(controller.signal)
      .then((fresh) => {
        if (controller.signal.aborted) return;
        setState((previous) => (previous.kind === 'ready' ? { ...previous, context: fresh, selecting: true } : previous));
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted || handleUnauthenticated(error)) return;
        setState((previous) =>
          previous.kind === 'ready'
            ? { ...previous, selecting: true, context: previous.context }
            : { kind: 'error', message: error instanceof Error ? error.message : 'بارگذاری برنامه با خطا مواجه شد.' },
        );
      });
  }, [handleUnauthenticated]);

  const handleLogout = useCallback(async () => {
    await logout();
    navigate(loginPath);
  }, [navigate]);

  const handlePasswordChanged = useCallback(() => {
    setState({ kind: 'redirecting' });
    navigate(buildLoginUrl(window.location));
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

  const showsSelection =
    state.kind === 'ready' && (state.context.current === null || state.selecting);
  // The tab title names the screen, so several open tabs and the history stay distinguishable.
  // Inside the shell the router names the page (`<title> | فرانو`).
  useEffect(() => {
    if (showsSelection) document.title = 'فرانو — انتخاب دوره مالی';
  }, [showsSelection]);

  const activeCompanyId = state.kind === 'ready' && !state.selecting ? state.context.current?.company.id : undefined;
  const companyPermissions = useCompanyPermissions(activeCompanyId, handleUnauthenticated);

  const readySession = state.kind === 'ready' ? state.session : null;
  const readyContext = state.kind === 'ready' ? state.context.current : null;
  const routeData = useMemo<AppRouteData | null>(
    () =>
      readySession && readyContext
        ? {
            session: readySession,
            context: readyContext,
            access: companyPermissions.access,
            refreshAccess: companyPermissions.refresh,
            onUnauthenticated: handleUnauthenticated,
          }
        : null,
    [readySession, readyContext, companyPermissions.access, companyPermissions.refresh, handleUnauthenticated],
  );

  if (state.kind === 'loading') {
    return (
      <SelectionLayout>
        <LoadingScreen label="در حال بارگذاری…" />
      </SelectionLayout>
    );
  }

  if (state.kind === 'entering') {
    return (
      <SelectionLayout>
        <LoadingScreen label="در حال ورود به محیط کاری…" />
      </SelectionLayout>
    );
  }

  if (state.kind === 'redirecting') {
    return (
      <SelectionLayout>
        <LoadingScreen label="در حال انتقال به صفحه ورود…" />
      </SelectionLayout>
    );
  }

  if (state.kind === 'error') {
    return (
      <SelectionLayout width="narrow">
        <ErrorState message={state.message} onRetry={reload} />
      </SelectionLayout>
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

  const selectorScreen = (
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

  if (context.current === null) {
    // The first entry (or a forgotten context): there is no workspace to preserve yet.
    return (
      <WorkContextSelector
        session={session}
        context={context}
        current={context.current}
        onSelected={handleSelected}
        onLogout={handleLogoutFromScreen}
        onRefresh={reload}
        onUnauthenticated={handleUnauthenticated}
      />
    );
  }

  // The selection screen rides ABOVE the workspace: the shell keeps its exact tree position
  // (only `inert` and the overlay toggle), so the open tabs and their state never remount.
  const changingContext = selecting;
  return (
    <AppRouteDataContext.Provider value={routeData}>
      {/* The Trade capability hints are scoped to the work context (company + fiscal period): a change drops them. */}
      <TradeAccessProvider scope={`${context.current.company.id}:${context.current.fiscalPeriod.id}`} onUnauthenticated={handleUnauthenticated}>
        <div inert={changingContext ? true : undefined}>
          <AppShell
            routes={appRoutes}
            workspaceTabs
            access={companyPermissions.access}
            onRetryPermissions={companyPermissions.retry}
            session={session}
            context={context.current}
            contextChangePending={changingContext}
            onChangeContext={changeContext}
            onLogout={handleLogout}
            onPasswordChanged={handlePasswordChanged}
            onUnauthenticated={handleUnauthenticated}
          >
            <RouteView />
          </AppShell>
        </div>
        {changingContext ? <div className="faranou-context-change-overlay">{selectorScreen}</div> : null}
      </TradeAccessProvider>
    </AppRouteDataContext.Provider>
  );
}
