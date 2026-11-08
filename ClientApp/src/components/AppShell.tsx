import { useCallback, useMemo, useRef, useState, type ReactNode } from 'react';
import { Bell, Building2, CalendarDays, ChevronDown, LogOut, Menu, X } from 'lucide-react';
import type { SelectedContext, Session } from '../api/types';
import { Alert } from './ui/Alert';
import { FaranouLogo } from './ui/FaranouLogo';
import { MyAccountPanel } from './MyAccountPanel';
import { can, deniedAccess, type AccessSnapshot, type Permission } from '../lib/permissions';
import { matchPath, RouterProvider, useLocation, type Route } from '../lib/router';
import { AppSidebar } from './shell/AppSidebar';
import { PageBreadcrumbBar, PageBreadcrumbProvider } from './shell/PageBreadcrumb';
import { WorkspaceTabPanes, WorkspaceTabsBar, WorkspaceTabsProvider } from './shell/WorkspaceTabs';
import { hasAnyProblemForm, problemSummary } from '../lib/formGuard';
import { ConfirmDialog } from './ui/ConfirmDialog';

type AppShellProps = {
  session: Session;
  /**
   * What `/me/permissions` said for the active company. Presentation hint only; omitted or
   * unknown means nothing is granted (fail-closed). Not derived from `session.isAdministrator`.
   */
  access?: AccessSnapshot;
  onRetryPermissions?: () => void;
  context: SelectedContext;
  onChangeContext: () => void;
  onLogout: () => Promise<void>;
  onPasswordChanged?: () => void;
  onUnauthenticated?: (error: unknown) => boolean;
  /** Routes of the app; `children` is normally a `<RouteView />` that renders them. */
  routes?: readonly Route[];
  children: ReactNode;
  /**
   * Multi-tab workspace mode (Issue #444 stage 1): pages open as tabs under the header and
   * stay mounted while hidden. `children` is ignored in this mode.
   */
  workspaceTabs?: boolean;
};

/** Main authenticated frame. The server-validated WorkContext remains authoritative. */
const ignoreUnauthenticated = (_error: unknown) => false;
const noop = () => undefined;

const noRoutes: readonly Route[] = [];

/** The shell owns the router, so the sidebar links and the page area (`children`) share one location. */
export function AppShell({ routes = noRoutes, access = deniedAccess, onRetryPermissions = noop, ...props }: AppShellProps) {
  const canAccess = useCallback((permission: Permission) => can(access, permission), [access]);
  return (
    <RouterProvider routes={routes} canAccess={canAccess} accessStatus={access.status} onRetryAccess={onRetryPermissions}>
      <AppShellFrame {...props} access={access} canAccess={canAccess} onRetryPermissions={onRetryPermissions} routes={routes} />
    </RouterProvider>
  );
}

type AppShellFrameProps = Omit<AppShellProps, 'access'> & {
  access: AccessSnapshot;
  canAccess: (permission: Permission) => boolean;
  routes: readonly Route[];
};

function AppShellFrame({
  session,
  context,
  access,
  canAccess,
  routes,
  onRetryPermissions = noop,
  onChangeContext,
  onLogout,
  onPasswordChanged = noop,
  onUnauthenticated = ignoreUnauthenticated,
  children,
  workspaceTabs = false,
}: AppShellFrameProps) {
  const [loggingOut, setLoggingOut] = useState(false);
  const [logoutError, setLogoutError] = useState<string | null>(null);
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);
  const [accountOpen, setAccountOpen] = useState(false);
  const accountTriggerRef = useRef<HTMLButtonElement>(null);
  const mainRef = useRef<HTMLElement>(null);
  // Dirty forms anywhere in the workspace block logout and context changes until confirmed.
  const [pendingLogout, setPendingLogout] = useState(false);
  const [pendingContextChange, setPendingContextChange] = useState(false);
  const { pathname } = useLocation();
  const readOnly = context.fiscalPeriod.accessMode === 'readOnly';

  // The route title is the real breadcrumb fallback: the shell never invents a path.
  const fallbackCrumbs = useMemo(() => {
    const match = routes.map((route) => ({ route, params: matchPath(route.path, pathname) })).find((entry) => entry.params !== null);
    return [{ label: match ? match.route.title : 'فرانو' }];
  }, [routes, pathname]);

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

  function requestLogout() {
    if (hasAnyProblemForm()) {
      setPendingLogout(true);
      return;
    }
    void handleLogout();
  }

  function requestContextChange() {
    // Company/period data is refetched for the new context; unresolved forms of the old one
    // would silently mix contexts. (PR-3 replaces this confirmation with a hard block.)
    if (hasAnyProblemForm()) {
      setPendingContextChange(true);
      return;
    }
    onChangeContext();
  }

  return (
    <div dir="rtl" className="faranou-workspace min-h-screen text-ink">
      <a
        href="#main-content"
        onClick={(event) => {
          // The router is pathname based: move focus ourselves instead of changing the URL hash.
          event.preventDefault();
          mainRef.current?.focus();
        }}
        className="sr-only focus:not-sr-only focus:fixed focus:start-4 focus:top-3 focus:z-50 focus:rounded-lg focus:bg-white focus:px-4 focus:py-2 focus:text-sm focus:font-bold focus:text-navy focus:shadow-lg"
      >
        پرش به محتوای اصلی
      </a>
      <header className="faranou-topbar faranou-topbar--login-palette on-dark sticky top-0 z-40 text-white">
        <div className="faranou-header-inner">
          <div className="faranou-header-brand">
            <button
              type="button"
              onClick={() => setMobileMenuOpen((open) => !open)}
              className="faranou-header-menu"
              aria-label={mobileMenuOpen ? 'بستن منوی اصلی' : 'باز کردن منوی اصلی'}
              aria-expanded={mobileMenuOpen}
              aria-controls="main-navigation"
            >
              {mobileMenuOpen ? <X size={21} /> : <Menu size={21} />}
            </button>
            <span className="faranou-desktop-menu-mark" aria-hidden="true">
              <Menu size={23} />
            </span>
            <FaranouLogo size={45} tone="dark" />
          </div>

          <div className="faranou-header-context" aria-label="زمینه کاری جاری">
            <button type="button" onClick={requestContextChange} className="faranou-context-choice" aria-label={`شرکت جاری ${context.company.name}، تغییر شرکت`}>
              <ChevronDown className="faranou-context-chevron" size={17} />
              <span className="min-w-0 flex-1 text-start">
                <span className="block text-[10px] text-on-navy/70 sm:text-[11px]">شرکت جاری</span>
                <strong className="block truncate text-xs sm:text-sm">{context.company.name}</strong>
              </span>
              <Building2 className="shrink-0 text-on-navy-accent" size={22} />
            </button>
            <button type="button" onClick={requestContextChange} className="faranou-context-choice" aria-label={`دوره مالی ${context.fiscalPeriod.title}، تغییر دوره مالی`}>
              <ChevronDown className="faranou-context-chevron" size={17} />
              <span className="min-w-0 flex-1 text-start">
                <span className="block text-[10px] text-on-navy/70 sm:text-[11px]">دوره مالی</span>
                <strong className="block truncate text-xs sm:text-sm">{context.fiscalPeriod.title}</strong>
              </span>
              <CalendarDays className="shrink-0 text-on-navy-accent" size={22} />
            </button>
            {readOnly ? <span className="faranou-readonly-chip">فقط مشاهده و گزارش‌گیری</span> : null}
          </div>

          <div className="faranou-header-user">
            <button
              ref={accountTriggerRef}
              type="button"
              onClick={() => setAccountOpen(true)}
              className="rounded-full focus:outline-none focus:ring-2 focus:ring-gold focus:ring-offset-2 focus:ring-offset-night"
              aria-label="حساب کاربری من"
              aria-haspopup="dialog"
              aria-expanded={accountOpen}
            >
              <span className="faranou-user-avatar" aria-hidden="true">
                {session.fullName.trim().slice(0, 1) || 'ف'}
              </span>
            </button>
            <span className="hidden min-w-0 flex-col leading-tight sm:flex">
              <strong className="max-w-36 truncate text-sm font-bold">{session.fullName}</strong>
              <span className="mt-1 max-w-36 truncate text-[11px] text-on-navy/75">{context.company.roleName}</span>
            </span>
            <span className="faranou-header-icon faranou-header-icon--notify" title="اعلان‌ها" role="img" aria-label="اعلان‌ها">
              <Bell size={20} />
              <span className="faranou-notify-dot" aria-hidden="true" />
            </span>
            <button
              type="button"
              onClick={requestLogout}
              disabled={loggingOut}
              className="faranou-logout-button"
              aria-label="خروج"
            >
              <LogOut size={18} />
              <span>{loggingOut ? 'در حال خروج…' : 'خروج'}</span>
            </button>
          </div>
        </div>
      </header>

      <WorkspaceTabsProvider workspaceTabs={workspaceTabs}>
        {workspaceTabs ? <WorkspaceTabsBar /> : null}
        <div className="faranou-workspace-body">
        {mobileMenuOpen ? (
          <button
            type="button"
            aria-label="بستن منو"
            className="faranou-mobile-scrim"
            onClick={() => setMobileMenuOpen(false)}
          />
        ) : null}
        <AppSidebar
          access={access}
          canAccess={canAccess}
          onRetryPermissions={onRetryPermissions}
          onNavigate={() => setMobileMenuOpen(false)}
          collapsed={sidebarCollapsed}
          onToggleCollapsed={() => setSidebarCollapsed((collapsed) => !collapsed)}
          mobileOpen={mobileMenuOpen}
        />

        <main id="main-content" ref={mainRef} tabIndex={-1} className="faranou-main-content min-w-0 flex-1 outline-none">
          {workspaceTabs ? (
            <>
              {logoutError ? (
                <div className="mb-4">
                  <Alert tone="error">{logoutError}</Alert>
                </div>
              ) : null}
              <WorkspaceTabPanes />
            </>
          ) : (
            /* Keyed by path: each navigation starts from the real route title, then the active
                page declares its record-aware breadcrumb into the same bar. */
            <PageBreadcrumbProvider key={pathname} fallback={fallbackCrumbs}>
              <PageBreadcrumbBar />
              {logoutError ? (
                <div className="mb-4">
                  <Alert tone="error">{logoutError}</Alert>
                </div>
              ) : null}
              {children}
            </PageBreadcrumbProvider>
          )}
        </main>
      </div>
      </WorkspaceTabsProvider>
      <ConfirmDialog
        open={pendingLogout}
        title="خروج از حساب کاربری با فرم‌های تعیین‌تکلیف‌نشده"
        description={`${problemSummary() || 'صفحهٔ باز تعیین‌تکلیف‌نشده است.'} با خروج از دست می‌روند.`}
        confirmLabel="خروج و از دست دادن تغییرات"
        cancelLabel="انصراف"
        tone="danger"
        onConfirm={() => {
          setPendingLogout(false);
          void handleLogout();
        }}
        onCancel={() => setPendingLogout(false)}
      />
      <ConfirmDialog
        open={pendingContextChange}
        title="تغییر زمینهٔ کاری با فرم‌های تعیین‌تکلیف‌نشده"
        description={`${problemSummary() || 'صفحهٔ باز تعیین‌تکلیف‌نشده است.'} پیش از تغییر شرکت یا دوره مالی ذخیره کنید؛ ادامه، فرم‌های زمینهٔ قبلی را از دست می‌دهد.`}
        confirmLabel="ادامه و تغییر زمینه"
        cancelLabel="انصراف"
        tone="danger"
        onConfirm={() => {
          setPendingContextChange(false);
          onChangeContext();
        }}
        onCancel={() => setPendingContextChange(false)}
      />
      <MyAccountPanel
        open={accountOpen}
        onClose={() => {
          setAccountOpen(false);
          accountTriggerRef.current?.focus();
        }}
        onPasswordChanged={onPasswordChanged}
        onUnauthenticated={onUnauthenticated}
      />
    </div>
  );
}
