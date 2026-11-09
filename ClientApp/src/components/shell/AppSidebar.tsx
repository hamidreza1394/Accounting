import { useState } from 'react';
import {
  Activity,
  Banknote,
  BookOpen,
  Boxes,
  Building2,
  BriefcaseBusiness,
  ChevronDown,
  ClipboardList,
  FileBarChart,
  Gem,
  Headphones,
  KeyRound,
  LayoutDashboard,
  LockKeyhole,
  LogOut,
  Network,
  Settings2,
  ShieldCheck,
  ShoppingCart,
  Ship,
  UserCheck,
  Users,
  Wallet,
  type LucideIcon,
} from 'lucide-react';
import { Link, useLocation } from '@/lib/router';
import type { AccessSnapshot, Permission } from '@/lib/permissions';
import { NestedNavSections, SubNavIcon } from './NestedNavSections';

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
  /** Chip colour: only top-level entries and groups carry one; sub-menu leaves are uniform. */
  tone?: Tone;
  to?: string;
  /** Consecutive leaves with the same name form one collapsible section; any of them may carry its icon. */
  section?: string;
  sectionIcon?: LucideIcon;
};

type NavGroup = {
  label: string;
  icon: LucideIcon;
  tone: Tone;
  children: NavLeaf[];
};

export type NavEntry = { kind: 'item'; leaf: NavLeaf } | { kind: 'group'; group: NavGroup };
export type NavLeafInfo = NavLeaf;
export type NavGroupInfo = NavGroup;

export const navigation: NavEntry[] = [
  { kind: 'item', leaf: { label: 'داشبورد', icon: LayoutDashboard, tone: 'sky', to: '/' } },
  {
    kind: 'group',
    group: {
      label: 'حسابداری',
      icon: BookOpen,
      tone: 'emerald',
      children: [{ label: 'اسناد و دفاتر', icon: BookOpen }],
    },
  },
  {
    kind: 'group',
    group: {
      label: 'انبار',
      icon: Boxes,
      tone: 'violet',
      children: [{ label: 'کالا و انبارها', icon: Boxes }],
    },
  },
  {
    kind: 'group',
    group: {
      label: 'فروش',
      icon: ShoppingCart,
      tone: 'rose',
      children: [{ label: 'مشتریان و فروش', icon: ShoppingCart }],
    },
  },
  {
    kind: 'group',
    group: {
      label: 'خزانه‌داری',
      icon: Wallet,
      tone: 'amber',
      children: [{ label: 'بانک و صندوق', icon: Wallet }],
    },
  },
  {
    kind: 'group',
    group: {
      label: 'حقوق و دستمزد',
      icon: Banknote,
      tone: 'teal',
      children: [{ label: 'لیست حقوق و مزایا', icon: Banknote }],
    },
  },
  {
    kind: 'group',
    group: {
      label: 'دارایی ثابت',
      icon: Gem,
      tone: 'indigo',
      children: [{ label: 'اموال و دارایی‌ها', icon: Gem }],
    },
  },
  {
    kind: 'group',
    group: {
      label: 'بازرگانی خارجی',
      icon: Ship,
      tone: 'cyan',
      children: [{ label: 'واردات و صادرات', icon: Ship }],
    },
  },
  {
    kind: 'group',
    group: {
      label: 'عمومی',
      icon: FileBarChart,
      tone: 'slate',
      children: [
        { label: 'گزارش‌ها', icon: FileBarChart },
        { label: 'اطلاعات پایه', icon: ClipboardList },
      ],
    },
  },
  {
    kind: 'group',
    group: {
      label: 'مدیریت سیستم',
      icon: Settings2,
      tone: 'slate',
      children: [
        { label: 'مدیریت کاربران', icon: Users, to: '/admin/users' },
        { label: 'واحدهای سازمانی', icon: Building2, to: '/hr/org/units', section: 'ساختار سازمانی', sectionIcon: Network },
        { label: 'سمت‌های سازمانی', icon: BriefcaseBusiness, to: '/hr/org/positions', section: 'ساختار سازمانی', sectionIcon: Network },
        { label: 'کاربران شرکت جاری', icon: Users, to: '/company/users', section: 'شرکت‌ها و عضویت‌ها', sectionIcon: UserCheck },
        { label: 'درخواست‌های عضویت', icon: UserCheck, to: '/admin/membership-requests', section: 'شرکت‌ها و عضویت‌ها', sectionIcon: UserCheck },
        { label: 'درخواست‌های عضویت من', icon: ClipboardList, to: '/account/membership-requests', section: 'شرکت‌ها و عضویت‌ها', sectionIcon: UserCheck },
        { label: 'گزارش امنیت و سوابق', icon: Activity, to: '/admin/security/audit', section: 'سوابق امنیتی و مدیریتی', sectionIcon: Activity },
        { label: 'سیاست‌های امنیتی', icon: Settings2, section: 'سوابق امنیتی و مدیریتی', sectionIcon: Activity },
        { label: 'گروه‌های دسترسی', icon: ShieldCheck, section: 'دسترسی و مجوزها', sectionIcon: KeyRound },
        { label: 'کاتالوگ و جزئیات مجوزها', icon: KeyRound, section: 'دسترسی و مجوزها', sectionIcon: KeyRound },
        { label: 'مجوزهای دسترسی', icon: LockKeyhole, section: 'دسترسی و مجوزها', sectionIcon: KeyRound },
        { label: 'فهرست کارکنان', icon: Users, section: 'کارکنان', sectionIcon: Users },
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

function NavIcon({ icon: Icon, tone = 'slate' }: { icon: LucideIcon; tone?: Tone }) {
  return (
    <span className={'faranou-nav-icon ' + toneClass[tone]} aria-hidden="true">
      <Icon size={16} strokeWidth={2.2} />
    </span>
  );
}

export function isActivePath(pathname: string, to: string): boolean {
  if (to === '/') return pathname === '/';
  return pathname === to || pathname.startsWith(`${to}/`);
}

const soonTitle = 'این بخش پس از تکمیل مسیر و مجوز مربوط فعال می‌شود.';

function DisabledLeaf({ leaf, nested = false }: { leaf: NavLeaf; nested?: boolean }) {
  return (
    <div
      aria-disabled="true"
      title={soonTitle}
      className={'faranou-nav-disabled ' + (nested ? 'faranou-nav-leaf faranou-nav-leaf--sub' : 'faranou-nav-row')}
    >
      {nested ? <SubNavIcon icon={leaf.icon} /> : <NavIcon icon={leaf.icon} {...(leaf.tone ? { tone: leaf.tone } : {})} />}
      <span className="min-w-0 flex-1 whitespace-normal leading-snug">{leaf.label}</span>
      <span className="faranou-nav-soon-badge">به‌زودی</span>
    </div>
  );
}

export type AppSidebarProps = {
  access: AccessSnapshot;
  canAccess: (permission: Permission) => boolean;
  onRetryPermissions: () => void;
  onNavigate: () => void;
  mobileOpen: boolean;
  /** The shell's existing logout flow (dirty-form guard included); the sidebar only triggers it. */
  onLogout?: () => void;
  loggingOut?: boolean;
};

export function AppSidebar({ access, canAccess, onRetryPermissions, onNavigate, mobileOpen, onLogout, loggingOut = false }: AppSidebarProps) {
  const { pathname } = useLocation();

  // Two independent concepts: the ACTIVE ROUTE (which page is open; drives only the highlight,
  // `aria-current` and the dot on a folded group) and the EXPANDED GROUPS (which groups the user
  // opened by an explicit click). Every group starts folded and navigation never opens one.
  const [openGroups, setOpenGroups] = useState<Record<string, boolean>>({});

  const toggleGroup = (label: string) => setOpenGroups((previous) => ({ ...previous, [label]: !previous[label] }));

  const companyCodes = access.status === 'ready' ? access.companyCodes : [];
  const visibleLeaves = (leaves: readonly NavLeaf[]) =>
    leaves.filter((leaf) => {
      switch (leaf.to) {
        case '/admin/users':
          return canAccess('admin.users.view');
        case '/admin/access':
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
      className={'faranou-sidebar faranou-sidebar--premium ' + (mobileOpen ? 'is-open' : '')}
      aria-label="ناوبری فرانو"
    >
      <nav className="faranou-sidebar-nav" aria-label="منوی اصلی">
        <p className="faranou-sidebar-title">منوی اصلی</p>
        <ul className="faranou-nav-list">
          {navigation.map((entry, index) => {
            if (entry.kind === 'item') {
              const { leaf } = entry;
              if (!leaf.to) {
                return (
                  <li key={`leaf-${index}`}>
                    <DisabledLeaf leaf={leaf} />
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
                    className={'faranou-nav-row ' + (active ? 'faranou-nav-active' : 'faranou-nav-link')}
                  >
                    <NavIcon icon={leaf.icon} {...(leaf.tone ? { tone: leaf.tone } : {})} />
                    <span className="min-w-0 flex-1 whitespace-normal leading-snug">{leaf.label}</span>
                  </Link>
                </li>
              );
            }

            const { group } = entry;
            const leaves = visibleLeaves(group.children);
            if (group.children.some((leaf) => leaf.to !== undefined) && leaves.length === 0) return null;
            const open = openGroups[group.label] === true;
            const groupId = `nav-group-${index}`;
            const groupActive = group.children.some((leaf) => leaf.to !== undefined && isActivePath(pathname, leaf.to));
            return (
              <li key={group.label} className={'faranou-nav-group-card' + (open ? ' faranou-nav-group-card--open' : '')}>
                <button
                  type="button"
                  className={'faranou-nav-group faranou-nav-row' + (open ? ' faranou-nav-group--open' : '')}
                  aria-expanded={open}
                  aria-controls={groupId}
                  onClick={() => toggleGroup(group.label)}
                >
                  <NavIcon icon={group.icon} tone={group.tone} />
                  <span className="min-w-0 flex-1 whitespace-normal text-start leading-snug">{group.label}</span>
                  {groupActive && !open ? <span className="faranou-nav-group-dot" aria-hidden="true" /> : null}
                  <ChevronDown size={16} className={'faranou-nav-chevron' + (open ? ' faranou-nav-chevron--open' : '')} aria-hidden="true" />
                </button>
                {open ? (
                  <ul id={groupId} className="faranou-nav-sub">
                    {group.label === 'مدیریت سیستم' && access.status === 'loading' ? (
                      <li><span role="status">در حال دریافت مجوزهای شرکت…</span></li>
                    ) : null}
                    {group.label === 'مدیریت سیستم' && access.status === 'error' ? (
                      <li>
                        <span role="alert">دریافت مجوزهای شرکت انجام نشد.</span>{' '}
                        <button type="button" onClick={onRetryPermissions}>تلاش دوباره</button>
                      </li>
                    ) : null}
                    <NestedNavSections
                      groupLabel={group.label}
                      leaves={leaves}
                      pathname={pathname}
                      onNavigate={onNavigate}
                      DisabledLeaf={({ leaf, nested }) => <DisabledLeaf leaf={leaf as NavLeaf} nested={nested === true} />}
                    />
                  </ul>
                ) : null}
              </li>
            );
          })}
        </ul>
      </nav>
      <div className="faranou-sidebar-footer">
        <div className="faranou-sidebar-secure">
          <ShieldCheck size={17} aria-hidden="true" />
          محیط کاری امن
        </div>
        <p>اطلاعات این محیط بر اساس دسترسی معتبر شما نمایش داده می‌شود.</p>
        {onLogout ? (
          <button type="button" className="faranou-sidebar-logout" onClick={onLogout} disabled={loggingOut}>
            <LogOut size={16} aria-hidden="true" />
            <span>{loggingOut ? 'در حال خروج…' : 'خروج از حساب'}</span>
          </button>
        ) : null}
      </div>
    </aside>
  );
}
