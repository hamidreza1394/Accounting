import { createContext, Suspense, useCallback, useContext, useEffect, useLayoutEffect, useMemo, useReducer, useRef, useState, type ComponentType, type KeyboardEvent, type ReactNode } from 'react';
import { Plus, X } from 'lucide-react';
import { ParamsScope, TabLocationScope, matchPath, setTabTitleSink, titleSuffix, useRouterContext, type Route } from '@/lib/router';
import type { AccessStatus, Permission } from '@/lib/permissions';
import { formStatusLabel, problemSummary, tabStatus } from '@/lib/formGuard';
import { ErrorState, LoadingState } from '@/components/Feedback';
import { ConfirmDialog } from '@/components/ui/ConfirmDialog';
import { initialTabsState, tabLabel, tabsReducer, type TabsState, type WorkspaceTab } from '@/lib/workspaceTabs';
import { tabContextScope, tabKeyPathname } from '@/lib/tabContext';
import { PageBreadcrumbBar, PageBreadcrumbProvider } from './PageBreadcrumb';
import { WorkspaceTabEntry } from './WorkspaceTabEntry';

// The workspace consumes the router context AppShell owns, so the sidebar links, the tab bar
// and the tab panes share one location and one history. No new router, no new dependency.

export type WorkspaceTabsStore = {
  state: TabsState;
  /** Opens a fresh tab for `to` (or activates the existing one) through navigation. */
  openOrActivate: (to: string) => void;
  /** Switches to an already-open tab, preserving its query string and scroll position. */
  activate: (tab: WorkspaceTab) => void;
  /** Closes a tab; closing the active one activates the right neighbour and follows it in the URL. */
  close: (tab: WorkspaceTab) => void;
  /**
   * Closes a tab WITHOUT the unresolved-form dialog — only for the confirmed context change,
   * where the guard already proved every form clean (fail-safe: it closes problem tabs too).
   */
  forceClose: (tab: WorkspaceTab) => void;
  /** The "+ تب جدید" action: opens (or focuses) the dashboard tab. */
  openHome: () => void;
};

const WorkspaceTabsContext = createContext<WorkspaceTabsStore | null>(null);

/**
 * The tab key of the mounted page, consumed by the dirty-form guard (`lib/formGuard.ts`).
 * Null outside the multi-tab shell: the guard then registers under a fixed single-page key,
 * so logout and context-change protection still work without tabs.
 */
export const PaneTabKeyContext = createContext<string | null>(null);

function useWorkspaceTabs(): WorkspaceTabsStore {
  const store = useContext(WorkspaceTabsContext);
  if (!store) throw new Error('WorkspaceTabsProvider is missing above this component.');
  return store;
}

/** Read access to the workspace store for shell dialogs rendered inside the provider. */
export { useWorkspaceTabs };

/** The store when the multi-tab shell is present, `null` in single-page mode (pages that may run in both). */
export function useOptionalWorkspaceTabs(): WorkspaceTabsStore | null {
  return useContext(WorkspaceTabsContext);
}

function routeTitleOf(routes: readonly Route[], pathname: string): string {
  for (const route of routes) {
    if (matchPath(route.path, pathname) !== null) return route.title;
  }
  return 'صفحه پیدا نشد';
}

/**
 * Owns the tab model for the whole shell: the store, the tab-title sink, scroll bookkeeping
 * and the navigation side effects. The bar and the panes consume the same store.
 */
export function WorkspaceTabsProvider({ workspaceTabs = false, contextKey = null, children }: { workspaceTabs?: boolean; contextKey?: string | null; children: ReactNode }) {
  const { routes, location, navigate } = useRouterContext();
  const [state, dispatch] = useReducer(
    tabsReducer,
    undefined,
    () => initialTabsState(location.pathname, location.search, routeTitleOf(routes, location.pathname)),
  );
  const stateRef = useRef<TabsState>(state);
  stateRef.current = state;
  // A tab holding a dirty form closes only after the shared ConfirmDialog says so (PR 5).
  const [pendingClose, setPendingClose] = useState<WorkspaceTab | null>(null);

  // Navigation is the single source of truth: every location change opens or activates the
  // matching tab (same pathname = same tab; a different query = same tab, updated path).
  // Back/forward and refresh are just location changes, so they behave predictably.
  useEffect(() => {
    dispatch({ type: 'open', pathname: location.pathname, search: location.search, title: routeTitleOf(routes, location.pathname) });
  }, [location, routes]);

  // Pages name their tab (record titles) through the decoupled sink of `useDocumentTitle`.
  // In single-page mode the sink stays unregistered: nothing consumes it.
  useEffect(() => {
    if (!workspaceTabs) return undefined;
    setTabTitleSink((title) => {
      if (!stateRef.current.activeKey) return;
      dispatch({ type: 'set-declared-title', key: stateRef.current.activeKey, title });
    });
    return () => setTabTitleSink(null);
  }, [workspaceTabs]);

  // The guard flow bypasses the dirty-check on purpose (the user just confirmed the loss).
  const closeTabNow = (tab: WorkspaceTab) => {
    const current = stateRef.current;
    const closingIndex = current.tabs.findIndex((entry) => entry.key === tab.key);
    if (closingIndex === -1) return;
    const remaining = current.tabs.filter((entry) => entry.key !== tab.key);
    if (current.activeKey !== tab.key) {
      dispatch({ type: 'close', key: tab.key });
      return;
    }
    const previous =
      current.previousActiveKey && remaining.some((entry) => entry.key === current.previousActiveKey)
        ? current.previousActiveKey
        : null;
    const next = previous ?? remaining[closingIndex]?.key ?? remaining[remaining.length - 1]?.key ?? null;
    dispatch({ type: 'close', key: tab.key });
    navigate(next ? (remaining.find((entry) => entry.key === next)?.path ?? '/') : '/');
  };

  const confirmPendingClose = () => {
    const tab = pendingClose;
    setPendingClose(null);
    if (!tab) return;
    closeTabNow(tab);
  };

  const store = useMemo<WorkspaceTabsStore>(
    () => ({
      state,
      openOrActivate: (to: string) => navigate(to),
      activate: (tab: WorkspaceTab) => {
        // The scroll position belongs to the tab we are leaving; the panes restore the new one.
        dispatch({ type: 'capture-scroll', key: stateRef.current.activeKey ?? '', scrollY: window.scrollY });
        navigate(tab.path);
      },
      close: (tab: WorkspaceTab) => {
        const current = stateRef.current;
        const closingIndex = current.tabs.findIndex((entry) => entry.key === tab.key);
        if (closingIndex === -1) return;
        // Never lose a form to a misclick: a Dirty tab asks first; a Saving/Error tab is
        // refused outright (D-076: only a resolved tab may close).
        if (tabStatus(tab.key) !== 'clean') {
          setPendingClose(tab);
          return;
        }
        closeTabNow(tab);
      },
      forceClose: closeTabNow,
      openHome: () => navigate('/'),
    }),
    [state, navigate],
  );


  return (
    <WorkspaceTabsContext.Provider value={store}>
      {children}
      <TabContextJanitor contextKey={workspaceTabs ? contextKey : null} />
      <TabCloseDialog tab={pendingClose} onDismiss={() => setPendingClose(null)} onConfirmClose={confirmPendingClose} />
    </WorkspaceTabsContext.Provider>
  );
}

/**
 * After a CONFIRMED work-context change, closes every tab that depends on the previous
 * company/period and keeps only the proven-global ones (fail-safe: unknown = dependent).
 * Runs on the context key change only — a failed or cancelled change never reaches it, so
 * a server rejection leaves the tabs untouched. Closing the panes also drops every cached
 * list, search state and form instance of the old context with its component.
 */
function TabContextJanitor({ contextKey }: { contextKey: string | null }) {
  const { state, forceClose } = useWorkspaceTabs();
  const stateRef = useRef(state);
  stateRef.current = state;
  const previous = useRef<string | null>(null);
  useEffect(() => {
    if (!contextKey) return;
    if (previous.current === null) {
      previous.current = contextKey;
      return;
    }
    if (previous.current === contextKey) return;
    previous.current = contextKey;
    for (const tab of stateRef.current.tabs) {
      if (tabContextScope(tabKeyPathname(tab.key)) !== 'global') forceClose(tab);
    }
  }, [contextKey, forceClose]);
  return null;
}

/**
 * The close guard of a tab with unresolved forms: `Dirty` asks for confirmation (the user just
 * confirmed the loss), while `Saving` and `Error` are refused outright — only a resolved tab
 * may close (D-076).
 */
function TabCloseDialog({ tab, onDismiss, onConfirmClose }: { tab: WorkspaceTab | null; onDismiss: () => void; onConfirmClose: () => void }) {
  const status = tab ? tabStatus(tab.key) : 'clean';
  const label = tab ? tabLabel(tab) : '';
  if (!tab || status === 'clean') {
    return null;
  }
  if (status === 'dirty') {
    return (
      <ConfirmDialog
        open
        title="بستن تب با تغییرات ذخیره‌نشده"
        description={`${problemSummary() || 'این صفحه تغییرات ذخیره‌نشده دارد.'} با بستن «${label}» از دست می‌روند.`}
        confirmLabel="بستن تب و از دست دادن تغییرات"
        cancelLabel="انصراف"
        tone="danger"
        onConfirm={onConfirmClose}
        onCancel={onDismiss}
      />
    );
  }
  return (
    <ConfirmDialog
      open
      title={status === 'saving' ? 'بستن این تب در حال حاضر ممکن نیست' : 'بستن این تب تا رفع خطا ممکن نیست'}
      description={
        status === 'saving'
          ? `ذخیرهٔ «${label}» در حال انجام است (${formStatusLabel.saving}). تا پایان عملیات منتظر بمانید؛ پس از ذخیرهٔ موفق می‌توانید تب را ببندید.`
          : `«${label}» ${formStatusLabel.error} دارد. ابتدا خطا را برطرف کنید، تغییرات را ذخیره یا آگاهانه لغو کنید؛ سپس تب را ببندید.`
      }
      confirmVisible={false}
      cancelLabel="متوجه شدم"
      onConfirm={onDismiss}
      onCancel={onDismiss}
    />
  );
}

/** The tab strip under the header: one tab per open page, plus the "new tab" action. */
export function WorkspaceTabsBar() {
  const { state, activate, close, openHome } = useWorkspaceTabs();
  const active = state.activeKey;
  const tablistRef = useRef<HTMLDivElement>(null);

  // Roving arrow-key navigation (RTL: ArrowLeft moves visually left = the next tab in DOM order).
  function onKeyDown(event: KeyboardEvent) {
    const tabs = state.tabs;
    if (tabs.length === 0 || !active) return;
    const index = tabs.findIndex((tab) => tab.key === active);
    let nextIndex: number | null = null;
    if (event.key === 'ArrowLeft') nextIndex = (index + 1) % tabs.length;
    else if (event.key === 'ArrowRight') nextIndex = (index - 1 + tabs.length) % tabs.length;
    else if (event.key === 'Home') nextIndex = 0;
    else if (event.key === 'End') nextIndex = tabs.length - 1;
    if (nextIndex === null) return;
    event.preventDefault();
    const next = tabs[nextIndex]!;
    activate(next);
    const buttons = tablistRef.current?.querySelectorAll<HTMLButtonElement>('.faranou-tab-button');
    const nextButton = buttons?.[nextIndex];
    window.setTimeout(() => nextButton?.focus(), 0);
  }

  return (
    <div className="faranou-tabbar">
      {/* A navigation of open pages, not a WAI-ARIA tablist: each entry pairs an activation
         button with its own close button, and a `tablist` would require bare `tab` children
         while a close button inside a `tab` trips the nested-interactive rule. `aria-current`
         names the open page, and the arrow keys still move between entries. */}
      <nav ref={tablistRef} aria-label="تب‌های کاری باز" className="faranou-tabbar-tabs" onKeyDown={onKeyDown}>
        {state.tabs.map((tab) => {
          const isActive = tab.key === active;
          return (
            <div key={tab.key} className={'faranou-tab' + (isActive ? ' faranou-tab--active' : '')}>
              <WorkspaceTabEntry tab={tab} isActive={isActive} onActivate={() => (isActive ? undefined : activate(tab))} />
              <button
                type="button"
                className="faranou-tab-close"
                aria-label={`بستن ${tabLabel(tab)}`}
                title={`بستن ${tabLabel(tab)}`}
                onClick={() => close(tab)}
              >
                <X size={13} aria-hidden="true" />
              </button>
            </div>
          );
        })}
      </nav>
      <button type="button" className="faranou-tabbar-new" aria-label="بازکردن تب جدید" title="بازکردن تب جدید" onClick={openHome}>
        <Plus size={16} aria-hidden="true" />
      </button>
    </div>
  );
}

/**
 * Every open tab stays mounted (hidden) so its filters, search, paging, form state and scroll
 * survive switching; only the active pane is visible. Each pane scopes the tab's own location
 * and params, so a hidden list page never sees another tab's query and never refetches with it.
 */
export function WorkspaceTabPanes() {
  const { routes, canAccess, accessStatus, onRetryAccess, notFound: NotFound, forbidden: Forbidden, loadingLabel } = useRouterContext();
  const { state } = useWorkspaceTabs();
  const activeKey = state.activeKey;

  const activeTab = state.tabs.find((tab) => tab.key === activeKey) ?? null;
  const panesRef = useRef<HTMLDivElement>(null);
  // Only the visible pane may name the document; hidden panes stay silent.
  const setActivePaneTitle = useCallback((title: string) => {
    document.title = `${title} | ${titleSuffix}`;
  }, []);

  // Switching tabs mirrors a route change for assistive tech: the page heading gets focus.
  const lastActiveKey = useRef<string | null>(null);
  useEffect(() => {
    if (lastActiveKey.current === activeKey) return;
    lastActiveKey.current = activeKey;
    if (!activeKey) return;
    const container = panesRef.current;
    if (!container) return;
    const pane = container.querySelector<HTMLElement>(`[data-tab-pane="${CSS.escape(activeKey)}"]`);
    if (!pane) return;
    const focusHeading = (): boolean => {
      const heading = pane.querySelector('h1');
      if (!heading) return false;
      heading.tabIndex = -1;
      heading.focus();
      return true;
    };
    if (focusHeading()) return;
    const observer = new MutationObserver(() => {
      if (focusHeading()) observer.disconnect();
    });
    observer.observe(pane, { childList: true, subtree: true });
    const timer = window.setTimeout(() => observer.disconnect(), 5000);
    return () => {
      observer.disconnect();
      window.clearTimeout(timer);
    };
  }, [activeKey, activeTab]);

  // Scroll restore for the freshly activated tab (best effort; hidden panes keep their DOM).
  const scrollMap = useRef<Record<string, number>>({});
  const lastScrollKey = useRef<string | null>(activeKey);
  useLayoutEffect(() => {
    if (lastScrollKey.current !== activeKey) {
      if (lastScrollKey.current) scrollMap.current[lastScrollKey.current] = window.scrollY;
      lastScrollKey.current = activeKey;
      window.scrollTo(0, (activeKey && scrollMap.current[activeKey]) || 0);
    }
  }, [activeKey]);

  if (!activeTab) {
    // No tabs at all (the last one was closed): the store already navigated home.
    return null;
  }

  return (
    <div ref={panesRef}>
      {state.tabs.map((tab) => {
        const isActive = tab.key === activeKey;
        return (
          <div
            key={tab.key}
            data-tab-pane={tab.key}
            hidden={!isActive}
            tabIndex={-1}
          >
            <TabPane tab={tab} routes={routes} canAccess={canAccess} accessStatus={accessStatus} onRetryAccess={onRetryAccess} NotFound={NotFound} Forbidden={Forbidden} loadingLabel={loadingLabel} onTitle={isActive ? setActivePaneTitle : undefined} />
          </div>
        );
      })}
    </div>
  );
}

type TabPaneProps = {
  tab: WorkspaceTab;
  routes: readonly Route[];
  canAccess: (permission: Permission) => boolean;
  accessStatus: AccessStatus;
  onRetryAccess: () => void;
  NotFound: ComponentType;
  Forbidden: ComponentType;
  loadingLabel: string;
  /** Present only for the visible pane: it owns `document.title`. */
  onTitle?: ((title: string) => void) | undefined;
};

/** One tab's page: its own location scope, params, breadcrumb provider and permission state. */
function TabPane({ tab, routes, canAccess, accessStatus, onRetryAccess, NotFound: NotFoundComponent, Forbidden: ForbiddenComponent, loadingLabel, onTitle }: TabPaneProps) {
  const [pathname, search] = splitPath(tab.path);
  const match = useMemo(() => {
    for (const route of routes) {
      const params = matchPath(route.path, pathname);
      if (params) return { route, params };
    }
    return null;
  }, [routes, pathname]);

  const allowed = match ? !match.route.permission || canAccess(match.route.permission) : true;
  const pending = !allowed && accessStatus === 'loading';
  const failed = !allowed && accessStatus === 'error';

  // Same title rules as the single-page RouteView, scoped to this tab.
  const paneTitle = !match ? 'صفحه پیدا نشد' : pending ? loadingLabel : failed ? 'خطا در دریافت مجوزها' : allowed ? match.route.title : 'دسترسی ندارید';
  useEffect(() => {
    onTitle?.(paneTitle);
  }, [onTitle, paneTitle]);

  let content: ReactNode;
  if (!match) {
    content = <NotFoundComponent />;
  } else if (pending) {
    content = <LoadingState label="در حال دریافت مجوزهای شما…" />;
  } else if (failed) {
    content = <ErrorState message="دریافت مجوزها انجام نشد؛ تا آن زمان دسترسی به این صفحه داده نمی‌شود." onRetry={onRetryAccess} />;
  } else if (!allowed) {
    content = <ForbiddenComponent />;
  } else {
    const Element = match.route.element;
    content = (
      <ParamsScope params={match.params}>
        <Suspense fallback={<LoadingState label={loadingLabel} />}>
          <Element />
        </Suspense>
      </ParamsScope>
    );
  }

  return (
    <TabLocationScope pathname={pathname} search={search} hash="">
      <PaneTabKeyContext.Provider value={tab.key}>
        <PageBreadcrumbProvider key={tab.key} fallback={[{ label: match ? match.route.title : 'صفحه پیدا نشد' }]}>
          <PageBreadcrumbBar />
          {content}
        </PageBreadcrumbProvider>
      </PaneTabKeyContext.Provider>
    </TabLocationScope>
  );
}

function splitPath(path: string): [string, string] {
  const queryIndex = path.indexOf('?');
  return queryIndex === -1 ? [path, ''] : [path.slice(0, queryIndex), path.slice(queryIndex)];
}
