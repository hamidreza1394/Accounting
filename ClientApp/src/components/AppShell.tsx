import { useRef, useState, type ReactNode } from 'react';
import {
  Activity,
  Bell,
  BookOpen,
  Boxes,
  Building2,
  CalendarDays,
  ChevronDown,
  ClipboardList,
  FileBarChart,
  Headphones,
  KeyRound,
  LayoutDashboard,
  LogOut,
  Menu,
  Package,
  Settings2,
  ShieldCheck,
  ShoppingCart,
  UserCheck,
  Users,
  Wallet,
  X,
  type LucideIcon,
} from 'lucide-react';
import type { SelectedContext, Session } from '../api/types';
import { Alert } from './ui/Alert';
import { FaranouLogo } from './ui/FaranouLogo';
import { MyAccountPanel } from './MyAccountPanel';
import type { Permission } from '../lib/permissions';
import { Link, RouterProvider, useLocation, type Route } from '../lib/router';

type AppShellProps = {
  session: Session;
  companyPermissions?: readonly string[];
  permissionsStatus?: 'loading' | 'ready' | 'error';
  onRetryPermissions?: () => void;
  context: SelectedContext;
  onChangeContext: () => void;
  onLogout: () => Promise<void>;
  onPasswordChanged?: () => void;
  onUnauthenticated?: (error: unknown) => boolean;
  /** Routes of the app; `children` is normally a `<RouteView />` that renders them. */
  routes?: readonly Route[];
  /** UI permission hint for guarded routes; the server decides. */
  canAccess?: (permission: Permission) => boolean;
  children: ReactNode;
};

type Tone =
  | 'sky'
  | 'emerald'
  | 'amber'
  | 'rose'
  | 'violet'
  | 'cyan'
  | 'indigo'
  | 'teal'
  | 'orange'
  | 'slate'
  | 'blue'
  | 'green';

type NavLeaf = {
  kind: 'item';
  label: string;
  icon: LucideIcon;
  tone: Tone;
  /** App-relative route; items without one stay disabled placeholders. */
  to?: string;
};

type NavGroup = {
  kind: 'group';
  label: string;
  icon: LucideIcon;
  tone: Tone;
  children: NavLeaf[];
};

type NavEntry = NavLeaf | NavGroup | { kind: 'divider' };

/** Presentation-only navigation. Unfinished routes stay disabled until real paths and server permissions exist. */
const navigation: NavEntry[] = [
  { kind: 'item', label: 'داشبورد', icon: LayoutDashboard, tone: 'sky', to: '/' },
  { kind: 'item', label: 'حسابداری', icon: BookOpen, tone: 'emerald' },
  { kind: 'item', label: 'خزانه‌داری', icon: Wallet, tone: 'amber' },
  { kind: 'item', label: 'فروش', icon: ShoppingCart, tone: 'rose' },
  { kind: 'item', label: 'انبار', icon: Boxes, tone: 'violet' },
  { kind: 'item', label: 'بازرگانی خارجی', icon: Package, tone: 'cyan' },
  { kind: 'item', label: 'گزارش‌ها', icon: FileBarChart, tone: 'indigo' },
  { kind: 'divider' },
  {
    kind: 'group',
    label: 'مدیریت سیستم',
    icon: Settings2,
    tone: 'slate',
    children: [],
  },
  { kind: 'item', label: 'اطلاعات پایه', icon: ClipboardList, tone: 'teal' },
  { kind: 'item', label: 'راهنما و پشتیبانی', icon: Headphones, tone: 'orange' },
];

const toneClass: Record<Tone, string> = {
  sky: 'faranou-nav-icon--sky',
  emerald: 'faranou-nav-icon--emerald',
  amber: 'faranou-nav-icon--amber',
  rose: 'faranou-nav-icon--rose',
  violet: 'faranou-nav-icon--violet',
  cyan: 'faranou-nav-icon--cyan',
  indigo: 'faranou-nav-icon--indigo',
  teal: 'faranou-nav-icon--teal',
  orange: 'faranou-nav-icon--orange',
  slate: 'faranou-nav-icon--slate',
  blue: 'faranou-nav-icon--blue',
  green: 'faranou-nav-icon--green',
};

function NavIcon({ icon: Icon, tone, size = 'md' }: { icon: LucideIcon; tone: Tone; size?: 'md' | 'sm' }) {
  return (
    <span className={'faranou-nav-icon ' + toneClass[tone] + (size === 'sm' ? ' faranou-nav-icon--sm' : '')} aria-hidden="true">
      <Icon size={size === 'sm' ? 14 : 16} strokeWidth={2.2} />
    </span>
  );
}

function DisabledItem({ label, icon, tone, nested }: { label: string; icon: LucideIcon; tone: Tone; nested?: boolean }) {
  return (
    <div
      aria-disabled="true"
      title="این بخش پس از تکمیل مسیر و مجوز مربوط فعال می‌شود."
      className={
        'faranou-nav-disabled flex cursor-not-allowed items-center gap-2.5 rounded-xl ' +
        (nested ? 'min-h-10 ps-3 pe-2 text-[12px]' : 'min-h-11 px-3 text-[13px] font-semibold')
      }
    >
      <NavIcon icon={icon} tone={tone} size={nested ? 'sm' : 'md'} />
      <span className="min-w-0 flex-1 whitespace-normal leading-snug">{label}</span>
      {nested ? (
        /* Issue #442 review (Finding 3): a bare dot relying on `title` is invisible to
           touch users (no hover) and conveys nothing on its own to sighted users who
           aren't using a screen reader. Keep a compact-but-visible text badge instead,
           smaller than the top-level one to fit the narrower nested row. */
        <span className="ms-auto shrink-0 rounded-md border border-white/15 bg-white/5 px-1 py-0.5 text-[8px] font-semibold text-on-navy/70">
          به‌زودی
        </span>
      ) : (
        <span className="ms-auto shrink-0 rounded-md border border-white/15 bg-white/5 px-1.5 py-0.5 text-[9px] font-semibold text-on-navy/70">
          به‌زودی
        </span>
      )}
    </div>
  );
}

/** Main authenticated frame. The server-validated WorkContext remains authoritative. */
const ignoreUnauthenticated = (_error: unknown) => false;
const noop = () => undefined;

const noRoutes: readonly Route[] = [];

/** The shell owns the router, so the sidebar links and the page area (`children`) share one location. */
export function AppShell({ routes = noRoutes, canAccess, ...props }: AppShellProps) {
  return (
    <RouterProvider routes={routes} {...(canAccess ? { canAccess } : {})}>
      <AppShellFrame {...props} />
    </RouterProvider>
  );
}

type AppShellFrameProps = Omit<AppShellProps, 'routes' | 'canAccess'>;

function AppShellFrame({ session, context, companyPermissions = [], permissionsStatus = 'ready', onRetryPermissions = noop, onChangeContext, onLogout, onPasswordChanged = noop, onUnauthenticated = ignoreUnauthenticated, children }: AppShellFrameProps) {
  const [loggingOut, setLoggingOut] = useState(false);
  const [logoutError, setLogoutError] = useState<string | null>(null);
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const [accountOpen, setAccountOpen] = useState(false);
  const accountTriggerRef = useRef<HTMLButtonElement>(null);
  const mainRef = useRef<HTMLElement>(null);
  const [settingsOpen, setSettingsOpen] = useState(true);
  const { pathname } = useLocation();
  const dashboardActive = pathname === '/';
  const usersActive = pathname === '/admin/users' || pathname.startsWith('/admin/users/');
  const rolesActive = pathname === '/admin/roles' || pathname.startsWith('/admin/roles/');
  const permissionsActive = pathname === '/admin/permissions';
  const companyUsersActive = pathname === '/company/users';
  const securityAuditActive = pathname === '/admin/security/audit';
  const membershipRequestsActive = pathname === '/admin/membership-requests';
  const myMembershipRequestsActive = pathname === '/account/membership-requests';
  const readOnly = context.fiscalPeriod.accessMode === 'readOnly';

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
            <button type="button" onClick={onChangeContext} className="faranou-context-choice" aria-label={`شرکت جاری ${context.company.name}، تغییر شرکت`}>
              <ChevronDown className="faranou-context-chevron" size={17} />
              <span className="min-w-0 flex-1 text-start">
                <span className="block text-[10px] text-on-navy/70 sm:text-[11px]">شرکت جاری</span>
                <strong className="block truncate text-xs sm:text-sm">{context.company.name}</strong>
              </span>
              <Building2 className="shrink-0 text-on-navy-accent" size={22} />
            </button>
            <button type="button" onClick={onChangeContext} className="faranou-context-choice" aria-label={`دوره مالی ${context.fiscalPeriod.title}، تغییر دوره مالی`}>
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
              onClick={() => void handleLogout()}
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

      <div className="faranou-workspace-body">
        {mobileMenuOpen ? (
          <button
            type="button"
            aria-label="بستن منو"
            className="faranou-mobile-scrim"
            onClick={() => setMobileMenuOpen(false)}
          />
        ) : null}
        <aside
          id="main-navigation"
          className={'faranou-sidebar faranou-sidebar--premium ' + (mobileMenuOpen ? 'is-open' : '')}
          aria-label="ناوبری فرانو"
        >
          <nav className="min-h-0 flex-1 overflow-y-auto px-3 py-4" aria-label="منوی اصلی">
            <p className="mb-3 px-3 text-[10px] font-bold tracking-wide text-on-navy-accent/70">منوی اصلی</p>
            <ul className="space-y-1">
              {navigation.map((entry, index) => {
                if (entry.kind === 'divider') {
                  return (
                    <li key={'divider-' + index} className="faranou-nav-divider" aria-hidden="true">
                      <span />
                    </li>
                  );
                }

                if (entry.kind === 'group' && entry.label === 'مدیریت سیستم') {
                  return (
                    <li key={entry.label} className="space-y-1">
                      <button
                        type="button"
                        className="faranou-nav-group flex min-h-11 w-full items-center gap-2.5 rounded-xl px-3 text-[13px] font-semibold"
                        aria-expanded={settingsOpen}
                        onClick={() => setSettingsOpen((open) => !open)}
                      >
                        <NavIcon icon={entry.icon} tone={entry.tone} />
                        <span className="min-w-0 flex-1 whitespace-normal text-start leading-snug">{entry.label}</span>
                        <ChevronDown
                          size={16}
                          className={'shrink-0 text-on-navy/70 transition-transform ' + (settingsOpen ? 'rotate-180' : '')}
                          aria-hidden="true"
                        />
                      </button>
                      {settingsOpen ? (
                        <ul className="faranou-nav-sub space-y-2 ms-4 ps-2">
                          {permissionsStatus === 'loading' ? <li><span role="status">در حال دریافت مجوزهای شرکت…</span></li> : null}
                          {permissionsStatus === 'error' ? <li><span role="alert">دریافت مجوزهای شرکت انجام نشد.</span> <button type="button" onClick={onRetryPermissions}>تلاش دوباره</button></li> : null}
                          {session.isAdministrator ? (
                          <li>
                            <Link to="/admin/users" onClick={() => setMobileMenuOpen(false)} aria-current={usersActive ? 'page' : undefined} className={'flex min-h-10 w-full items-center gap-2.5 rounded-xl px-3 text-start text-[12px] transition ' + (usersActive ? 'bg-pick/20 text-white ring-1 ring-pick/30' : 'text-on-navy/80 hover:bg-white/10 hover:text-white')}>
                              <NavIcon icon={Users} tone="sky" size="sm" />
                              <span className="min-w-0 flex-1">مدیریت کاربران</span>
                            </Link>
                          </li>
                          ) : null}
                          {companyPermissions.includes('users.list') ? (
                          <li>
                            <Link to="/company/users" onClick={() => setMobileMenuOpen(false)} aria-current={companyUsersActive ? 'page' : undefined} className={'flex min-h-10 w-full items-center gap-2.5 rounded-xl px-3 text-start text-[12px] transition ' + (companyUsersActive ? 'bg-pick/20 text-white ring-1 ring-pick/30' : 'text-on-navy/80 hover:bg-white/10 hover:text-white')}>
                              <NavIcon icon={Users} tone="blue" size="sm" />
                              <span className="min-w-0 flex-1">کاربران شرکت جاری</span>
                            </Link>
                          </li>
                          ) : null}
                          {session.isAdministrator ? <li><DisabledItem label="ساختار سازمانی و سمت‌ها" icon={Building2} tone="blue" nested /></li> : null}
                          {session.isAdministrator ? (
                          <li>
                            <Link to="/admin/roles" onClick={() => setMobileMenuOpen(false)} aria-current={rolesActive ? 'page' : undefined} className={'flex min-h-10 w-full items-center gap-2.5 rounded-xl px-3 text-start text-[12px] transition ' + (rolesActive ? 'bg-pick/20 text-white ring-1 ring-pick/30' : 'text-on-navy/80 hover:bg-white/10 hover:text-white')}>
                              <NavIcon icon={ShieldCheck} tone="violet" size="sm" />
                              <span className="min-w-0 flex-1">گروه‌های دسترسی</span>
                            </Link>
                          </li>
                          ) : null}
                          {session.isAdministrator ? (
                          <li>
                            <Link to="/admin/permissions" onClick={() => setMobileMenuOpen(false)} aria-current={permissionsActive ? 'page' : undefined} className={'flex min-h-10 w-full items-center gap-2.5 rounded-xl px-3 text-start text-[12px] transition ' + (permissionsActive ? 'bg-pick/20 text-white ring-1 ring-pick/30' : 'text-on-navy/80 hover:bg-white/10 hover:text-white')}>
                              <NavIcon icon={KeyRound} tone="amber" size="sm" />
                              <span className="min-w-0 flex-1">کاتالوگ و جزئیات مجوزها</span>
                            </Link>
                          </li>
                          ) : null}
                          {companyPermissions.includes('users.membership.approve') ? (
                          <li>
                            <Link to="/admin/membership-requests" onClick={() => setMobileMenuOpen(false)} aria-current={membershipRequestsActive ? 'page' : undefined} className={'flex min-h-10 w-full items-center gap-2.5 rounded-xl px-3 text-start text-[12px] transition ' + (membershipRequestsActive ? 'bg-pick/20 text-white ring-1 ring-pick/30' : 'text-on-navy/80 hover:bg-white/10 hover:text-white')}>
                              <NavIcon icon={UserCheck} tone="emerald" size="sm" />
                              <span className="min-w-0 flex-1">درخواست‌های عضویت</span>
                            </Link>
                          </li>
                          ) : null}
                          <li>
                            <Link to="/account/membership-requests" onClick={() => setMobileMenuOpen(false)} aria-current={myMembershipRequestsActive ? 'page' : undefined} className={'flex min-h-10 w-full items-center gap-2.5 rounded-xl px-3 text-start text-[12px] transition ' + (myMembershipRequestsActive ? 'bg-pick/20 text-white ring-1 ring-pick/30' : 'text-on-navy/80 hover:bg-white/10 hover:text-white')}>
                              <NavIcon icon={ClipboardList} tone="slate" size="sm" />
                              <span className="min-w-0 flex-1">درخواست‌های عضویت من</span>
                            </Link>
                          </li>
                          {session.isAdministrator ? (
                          <li>
                            <Link to="/admin/security/audit" onClick={() => setMobileMenuOpen(false)} aria-current={securityAuditActive ? 'page' : undefined} className={'flex min-h-10 w-full items-center gap-2.5 rounded-xl px-3 text-start text-[12px] transition ' + (securityAuditActive ? 'bg-pick/20 text-white ring-1 ring-pick/30' : 'text-on-navy/80 hover:bg-white/10 hover:text-white')}>
                              <NavIcon icon={Activity} tone="emerald" size="sm" />
                              <span className="min-w-0 flex-1">گزارش امنیت و سوابق</span>
                            </Link>
                          </li>
                          ) : null}
                          {session.isAdministrator ? <li><DisabledItem label="سیاست‌های امنیتی" icon={Settings2} tone="slate" nested /></li> : null}
                        </ul>
                      ) : null}
                    </li>
                  );
                }

                if (entry.kind !== 'item') {
                  return null;
                }

                if (entry.to) {
                  const active = dashboardActive;
                  return (
                    <li key={entry.label}>
                      <Link
                        to={entry.to}
                        aria-current={active ? 'page' : undefined}
                        onClick={() => setMobileMenuOpen(false)}
                        className={
                          'flex min-h-11 items-center gap-2.5 rounded-xl px-3 text-[13px] font-semibold ' +
                          (active ? 'faranou-nav-active' : 'text-on-navy/80 hover:bg-white/10 hover:text-white')
                        }
                      >
                        <NavIcon icon={entry.icon} tone={entry.tone} />
                        <span className="min-w-0 flex-1 whitespace-normal leading-snug">{entry.label}</span>
                      </Link>
                    </li>
                  );
                }

                return (
                  <li key={entry.label}>
                    <DisabledItem label={entry.label} icon={entry.icon} tone={entry.tone} />
                  </li>
                );
              })}
            </ul>
          </nav>
          <div className="faranou-sidebar-footer">
            <div className="flex items-center gap-2 text-xs font-semibold text-on-navy-ok">
              <ShieldCheck size={17} />
              محیط کاری امن
            </div>
            <p className="mt-1 text-[10px] leading-5 text-on-navy/65">
              اطلاعات این محیط بر اساس دسترسی معتبر شما نمایش داده می‌شود.
            </p>
          </div>
        </aside>

        <main id="main-content" ref={mainRef} tabIndex={-1} className="faranou-main-content min-w-0 flex-1 outline-none">
          {logoutError ? (
            <div className="mb-4">
              <Alert tone="error">{logoutError}</Alert>
            </div>
          ) : null}
          {children}
        </main>
      </div>
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
