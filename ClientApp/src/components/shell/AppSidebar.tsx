import { useEffect, useRef, useState } from 'react';
import {
  Activity,
  BadgeCheck,
  BookOpen,
  Boxes,
  Building2,
  ChevronDown,
  ClipboardList,
  FileBarChart,
  Headphones,
  KeyRound,
  LayoutDashboard,
  Package,
  PanelRightClose,
  PanelRightOpen,
  Settings2,
  ShieldCheck,
  ShoppingCart,
  UserCheck,
  Users,
  Wallet,
  type LucideIcon,
} from 'lucide-react';
import { Link, useLocation } from '@/lib/router';
import type { AccessSnapshot, Permission } from '@/lib/permissions';

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
  label: string;
  icon: LucideIcon;
  tone: Tone;
  /** App-relative route; leaves without one are honest "به‌زودی" placeholders, never dead links. */
  to?: string;
};

type NavGroup = {
  label: string;
  icon: LucideIcon;
  tone: Tone;
  children: NavLeaf[];
  /**
   * Groups with real, working pages start open; future subsystems stay folded so the daily
   * menu stays short. The user can fold/unfold every group, and deep links auto-open theirs.
   */
  openByDefault?: boolean;
};

/** Exported for the menu↔router consistency test and docs tooling; presentation-only. */
export type NavEntry = { kind: 'item'; leaf: NavLeaf } | { kind: 'group'; group: NavGroup };
export type NavLeafInfo = NavLeaf;
export type NavGroupInfo = NavGroup;

/**
 * Presentation-only navigation, grouped the way an Iranian accounting ERP is actually used.
 * Only implemented pages carry a route; everything else shows the visible "به‌زودی" state
 * instead of a broken path. Internal English names (hr, trade, roles…) never surface here.
 */
export const navigation: NavEntry[] = [
  { kind: 'item', leaf: { label: 'داشبورد', icon: LayoutDashboard, tone: 'sky', to: '/' } },
  {
    kind: 'group',
    group: {
      label: 'حسابداری و مالی',
      icon: BookOpen,
      tone: 'emerald',
      children: [
        { label: 'حسابداری', icon: BookOpen, tone: 'emerald' },
        { label: 'خزانه‌داری', icon: Wallet, tone: 'amber' },
      ],
    },
  },
  {
    kind: 'group',
    group: {
      label: 'فروش، بازرگانی و انبار',
      icon: ShoppingCart,
      tone: 'rose',
      children: [
        { label: 'فروش', icon: ShoppingCart, tone: 'rose' },
        { label: 'بازرگانی خارجی', icon: Package, tone: 'cyan' },
        { label: 'انبار', icon: Boxes, tone: 'violet' },
      ],
    },
  },
  {
    kind: 'group',
    group: {
      label: 'گزارش‌ها و اطلاعات پایه',
      icon: FileBarChart,
      tone: 'indigo',
      children: [
        { label: 'گزارش‌ها', icon: FileBarChart, tone: 'indigo' },
        { label: 'اطلاعات پایه', icon: ClipboardList, tone: 'teal' },
      ],
    },
  },
  {
    kind: 'group',
    group: {
      label: 'منابع انسانی',
      icon: Users,
      tone: 'blue',
      openByDefault: true,
      children: [
        { label: 'واحدهای سازمانی', icon: Building2, tone: 'blue', to: '/hr/org/units' },
        { label: 'سمت‌های سازمانی', icon: BadgeCheck, tone: 'violet', to: '/hr/org/positions' },
        { label: 'کارکنان', icon: Users, tone: 'emerald', to: '/hr/org/employees' },
      ],
    },
  },
  {
    kind: 'group',
    group: {
      label: 'مدیریت سیستم',
      icon: Settings2,
      tone: 'slate',
      openByDefault: true,
      children: [
        { label: 'مدیریت کاربران', icon: Users, tone: 'sky', to: '/admin/users' },
        { label: 'کاربران شرکت جاری', icon: Users, tone: 'blue', to: '/company/users' },
        { label: 'گروه‌های دسترسی', icon: ShieldCheck, tone: 'violet', to: '/admin/roles' },
        { label: 'کاتالوگ و جزئیات مجوزها', icon: KeyRound, tone: 'amber', to: '/admin/permissions' },
        { label: 'درخواست‌های عضویت', icon: UserCheck, tone: 'emerald', to: '/admin/membership-requests' },
        { label: 'درخواست‌های عضویت من', icon: ClipboardList, tone: 'slate', to: '/account/membership-requests' },
        { label: 'گزارش امنیت و سوابق', icon: Activity, tone: 'emerald', to: '/admin/security/audit' },
        { label: 'سیاست‌های امنیتی', icon: Settings2, tone: 'slate' },
      ],
    },
  },
  { kind: 'item', leaf: { label: 'راهنما و پشتیبانی', icon: Headphones, tone: 'orange' } },
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

/** True when the current location belongs to the given app-relative route (details included). */
export function isActivePath(pathname: string, to: string): boolean {
  if (to === '/') return pathname === '/';
  return pathname === to || pathname.startsWith(`${to}/`);
}

const soonTitle = 'این بخش پس از تکمیل مسیر و مجوز مربوط فعال می‌شود.';

function DisabledLeaf({ leaf, collapsed = false, nested = false }: { leaf: NavLeaf; collapsed?: boolean; nested?: boolean }) {
  if (collapsed) {
    return (
      <div
        aria-disabled="true"
        title={`${leaf.label} — ${soonTitle}`}
        className="faranou-nav-disabled flex cursor-not-allowed items-center justify-center rounded-xl min-h-11"
      >
        <NavIcon icon={leaf.icon} tone={leaf.tone} />
        <span className="sr-only">{`${leaf.label} (به‌زودی)`}</span>
      </div>
    );
  }
  return (
    <div
      aria-disabled="true"
      title={soonTitle}
      className={
        'faranou-nav-disabled flex cursor-not-allowed items-center gap-2.5 rounded-xl ' +
        (nested ? 'min-h-10 ps-3 pe-2 text-[12px]' : 'min-h-11 px-3 text-[13px] font-semibold')
      }
    >
      <NavIcon icon={leaf.icon} tone={leaf.tone} size={nested ? 'sm' : 'md'} />
      <span className="min-w-0 flex-1 whitespace-normal leading-snug">{leaf.label}</span>
      {/* Always-visible text badge: never a hover-only dot (Issue #442 review, Finding 3). */}
      <span className={'ms-auto shrink-0 rounded-md border border-white/15 bg-white/5 font-semibold text-on-navy/70 ' + (nested ? 'px-1 py-0.5 text-[8px]' : 'px-1.5 py-0.5 text-[9px]')}>
        به‌زودی
      </span>
    </div>
  );
}

export type AppSidebarProps = {
  access: AccessSnapshot;
  canAccess: (permission: Permission) => boolean;
  onRetryPermissions: () => void;
  onNavigate: () => void;
  /** Folded icon-rail mode (desktop); the mobile drawer ignores it. */
  collapsed: boolean;
  onToggleCollapsed: () => void;
  mobileOpen: boolean;
};

/**
 * The right-side navigation of the workspace: real right-to-left layout, logical Persian
 * groups, collapsible groups, an icon-only rail for wide tables, and permission-aware
 * entries that hide what the server has not granted (UI hint only — D-064).
 */
export function AppSidebar({ access, canAccess, onRetryPermissions, onNavigate, collapsed, onToggleCollapsed, mobileOpen }: AppSidebarProps) {
  const { pathname } = useLocation();

  // Groups start in their designed state and open automatically when a deep link lands inside.
  const [openGroups, setOpenGroups] = useState<Record<string, boolean>>(() =>
    Object.fromEntries(
      navigation
        .filter((entry): entry is { kind: 'group'; group: NavGroup } => entry.kind === 'group')
        .map((entry) => [entry.group.label, entry.group.openByDefault === true]),
    ),
  );
  const lastPathname = useRef<string | null>(null);
  useEffect(() => {
    if (lastPathname.current === pathname) return;
    lastPathname.current = pathname;
    const owner = navigation.find(
      (entry): entry is { kind: 'group'; group: NavGroup } =>
        entry.kind === 'group' && entry.group.children.some((leaf) => leaf.to !== undefined && isActivePath(pathname, leaf.to)),
    );
    if (owner) setOpenGroups((previous) => (previous[owner.group.label] ? previous : { ...previous, [owner.group.label]: true }));
  }, [pathname]);

  const toggleGroup = (label: string) => setOpenGroups((previous) => ({ ...previous, [label]: !previous[label] }));

  // Company permission codes count only while the answer is known and fresh (fail-closed).
  const companyCodes = access.status === 'ready' ? access.companyCodes : [];
  const visibleLeaves = (leaves: readonly NavLeaf[]) =>
    leaves.filter((leaf) => {
      switch (leaf.to) {
        case '/admin/users':
          return canAccess('admin.users.view');
        case '/admin/roles':
          return canAccess('admin.roles.view');
        case '/admin/permissions':
          return canAccess('admin.permissions.view');
        case '/admin/security/audit':
          return canAccess('admin.security.view');
        case '/company/users':
          return companyCodes.includes('users.list');
        case '/admin/membership-requests':
          return companyCodes.includes('users.membership.approve');
        case '/hr/org/units':
          return canAccess('hr.units.view');
        case '/hr/org/positions':
          return canAccess('hr.positions.view');
        case '/hr/org/employees':
          return canAccess('hr.employees.view');
        default:
          return true;
      }
    });

  return (
    <aside
      id="main-navigation"
      className={
        'faranou-sidebar faranou-sidebar--premium ' +
        (collapsed ? 'faranou-sidebar--collapsed ' : '') +
        (mobileOpen ? 'is-open' : '')
      }
      aria-label="ناوبری فرانو"
    >
      <button
        type="button"
        onClick={onToggleCollapsed}
        className="faranou-sidebar-fold"
        aria-pressed={collapsed}
        aria-label={collapsed ? 'باز کردن کامل منو' : 'جمع‌کردن منو به حالت آیکونی'}
        title={collapsed ? 'باز کردن کامل منو' : 'جمع‌کردن منو به حالت آیکونی'}
      >
        {collapsed ? <PanelRightOpen size={17} /> : <PanelRightClose size={17} />}
        {!collapsed ? <span>جمع‌کردن منو</span> : null}
      </button>
      <nav className="min-h-0 flex-1 overflow-y-auto overflow-x-hidden px-3 py-3" aria-label="منوی اصلی">
        {!collapsed ? <p className="mb-3 px-3 text-[10px] font-bold tracking-wide text-on-navy-accent/70">منوی اصلی</p> : null}
        <ul className="space-y-1">
          {navigation.map((entry, index) => {
            if (entry.kind === 'item') {
              const { leaf } = entry;
              if (!leaf.to) {
                return (
                  <li key={`leaf-${index}`}>
                    <DisabledLeaf leaf={leaf} collapsed={collapsed} />
                  </li>
                );
              }
              const active = isActivePath(pathname, leaf.to);
              return (
                <li key={`leaf-${index}`}>
                  <Link
                    to={leaf.to}
                    aria-current={active ? 'page' : undefined}
                    onClick={onNavigate}
                    title={collapsed ? leaf.label : undefined}
                    className={
                      'flex min-h-11 items-center gap-2.5 rounded-xl px-3 text-[13px] font-semibold ' +
                      (collapsed ? 'justify-center px-0 ' : '') +
                      (active ? 'faranou-nav-active' : 'text-on-navy/80 hover:bg-white/10 hover:text-white focus-visible:outline-2 focus-visible:outline-gold')
                    }
                  >
                    <NavIcon icon={leaf.icon} tone={leaf.tone} />
                    {!collapsed ? <span className="min-w-0 flex-1 whitespace-normal leading-snug">{leaf.label}</span> : <span className="sr-only">{leaf.label}</span>}
                  </Link>
                </li>
              );
            }

            const { group } = entry;
            const leaves = visibleLeaves(group.children);
            // A group whose real pages are all hidden by permissions shows nothing at all.
            if (group.label === 'منابع انسانی' && leaves.length === 0) return null;
            const open = openGroups[group.label] === true;
            const groupId = `nav-group-${index}`;
            const groupActive = group.children.some((leaf) => leaf.to !== undefined && isActivePath(pathname, leaf.to));
            return (
              <li key={group.label} className="space-y-1">
                <button
                  type="button"
                  className={
                    'faranou-nav-group flex min-h-11 w-full items-center gap-2.5 rounded-xl px-3 text-[13px] font-semibold focus-visible:outline-2 focus-visible:outline-gold ' +
                    (collapsed ? 'justify-center px-0' : '')
                  }
                  aria-expanded={open}
                  aria-controls={collapsed ? undefined : groupId}
                  onClick={() => {
                    if (collapsed) {
                      // The rail shows icons only: opening a group expands the sidebar again
                      // and unfolds that group, instead of toggling something invisible.
                      onToggleCollapsed();
                      setOpenGroups((previous) => ({ ...previous, [group.label]: true }));
                      return;
                    }
                    toggleGroup(group.label);
                  }}
                  title={collapsed ? group.label : undefined}
                >
                  <NavIcon icon={group.icon} tone={group.tone} />
                  {!collapsed ? (
                    <>
                      <span className="min-w-0 flex-1 whitespace-normal text-start leading-snug">{group.label}</span>
                      {groupActive && !open ? <span className="faranou-nav-group-dot" aria-hidden="true" /> : null}
                      <ChevronDown
                        size={16}
                        className={'shrink-0 text-on-navy/70 transition-transform ' + (open ? 'rotate-180' : '')}
                        aria-hidden="true"
                      />
                    </>
                  ) : (
                    <>
                      {groupActive && !open ? <span className="faranou-nav-group-dot faranou-nav-group-dot--rail" aria-hidden="true" /> : null}
                      <span className="sr-only">{`${group.label} (${open ? 'باز' : 'بسته'})`}</span>
                    </>
                  )}
                </button>
                {open && !collapsed ? (
                  <ul id={groupId} className="faranou-nav-sub space-y-2 ms-4 ps-2">
                    {group.label === 'مدیریت سیستم' && access.status === 'loading' ? (
                      <li><span role="status">در حال دریافت مجوزهای شرکت…</span></li>
                    ) : null}
                    {group.label === 'مدیریت سیستم' && access.status === 'error' ? (
                      <li>
                        <span role="alert">دریافت مجوزهای شرکت انجام نشد.</span>{' '}
                        <button type="button" onClick={onRetryPermissions}>تلاش دوباره</button>
                      </li>
                    ) : null}
                    {leaves.map((leaf) =>
                      leaf.to ? (
                        <li key={leaf.label}>
                          <Link
                            to={leaf.to}
                            onClick={onNavigate}
                            aria-current={isActivePath(pathname, leaf.to) ? 'page' : undefined}
                            className={
                              'flex min-h-10 w-full items-center gap-2.5 rounded-xl px-3 text-start text-[12px] transition ' +
                              (isActivePath(pathname, leaf.to) ? 'bg-pick/20 text-white ring-1 ring-pick/30' : 'text-on-navy/80 hover:bg-white/10 hover:text-white')
                            }
                          >
                            <NavIcon icon={leaf.icon} tone={leaf.tone} size="sm" />
                            <span className="min-w-0 flex-1">{leaf.label}</span>
                          </Link>
                        </li>
                      ) : (
                        <li key={leaf.label}>
                          <DisabledLeaf leaf={leaf} nested />
                        </li>
                      ),
                    )}
                  </ul>
                ) : null}
              </li>
            );
          })}
        </ul>
      </nav>
      {!collapsed ? (
        <div className="faranou-sidebar-footer">
          <div className="flex items-center gap-2 text-xs font-semibold text-on-navy-ok">
            <ShieldCheck size={17} />
            محیط کاری امن
          </div>
          <p className="mt-1 text-[10px] leading-5 text-on-navy/65">
            اطلاعات این محیط بر اساس دسترسی معتبر شما نمایش داده می‌شود.
          </p>
        </div>
      ) : null}
    </aside>
  );
}
