import { useState, type ReactNode } from 'react';
import {
  Bell, BookOpen, Boxes, Building2, CalendarDays, ChevronDown, ClipboardList,
  FileBarChart, Headphones, LayoutDashboard, LogOut, Menu, Package,
  Settings2, ShieldCheck, ShoppingCart, Wallet, X,
} from 'lucide-react';
import type { SelectedContext, Session } from '../api/types';
import { FaranouLogo } from './ui/FaranouLogo';

type AppShellProps = {
  session: Session;
  context: SelectedContext;
  onChangeContext: () => void;
  onLogout: () => Promise<void>;
  children: ReactNode;
};

const navigation = [
  { label: 'داشبورد', icon: LayoutDashboard, active: true },
  { label: 'حسابداری', icon: BookOpen },
  { label: 'خزانه‌داری', icon: Wallet },
  { label: 'فروش', icon: ShoppingCart },
  { label: 'انبار', icon: Boxes },
  { label: 'بازرگانی خارجی', icon: Package },
  { label: 'گزارش‌ها', icon: FileBarChart, divider: true },
  { label: 'مدیریت دسترسی', icon: ShieldCheck },
  { label: 'اطلاعات پایه', icon: ClipboardList },
  { label: 'تنظیمات سیستم', icon: Settings2 },
  { label: 'راهنما و پشتیبانی', icon: Headphones },
];

/** Main authenticated frame. The server-validated WorkContext remains authoritative. */
export function AppShell({ session, context, onChangeContext, onLogout, children }: AppShellProps) {
  const [loggingOut, setLoggingOut] = useState(false);
  const [logoutError, setLogoutError] = useState<string | null>(null);
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
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
      <header className="faranou-topbar on-dark sticky top-0 z-40 text-white">
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
            <FaranouLogo size={45} tone="dark" />
          </div>

          <div className="faranou-header-context" aria-label="زمینه کاری جاری">
            <button type="button" onClick={onChangeContext} className="faranou-context-choice" aria-label="تغییر شرکت">
              <ChevronDown className="faranou-context-chevron" size={17} />
              <span className="min-w-0 flex-1 text-right">
                <span className="block text-[10px] text-blue-100/70 sm:text-[11px]">شرکت جاری</span>
                <strong className="block truncate text-xs sm:text-sm">{context.company.name}</strong>
              </span>
              <Building2 className="shrink-0 text-sky-200" size={22} />
            </button>
            <button type="button" onClick={onChangeContext} className="faranou-context-choice" aria-label="تغییر دوره مالی">
              <ChevronDown className="faranou-context-chevron" size={17} />
              <span className="min-w-0 flex-1 text-right">
                <span className="block text-[10px] text-blue-100/70 sm:text-[11px]">دوره مالی</span>
                <strong className="block truncate text-xs sm:text-sm">{context.fiscalPeriod.title}</strong>
              </span>
              <CalendarDays className="shrink-0 text-sky-200" size={22} />
            </button>
            {readOnly ? <span className="faranou-readonly-chip">فقط مشاهده</span> : null}
          </div>

          <div className="faranou-header-user">
            <span className="faranou-user-avatar" aria-label={'تصویر نمایه ' + session.fullName}>
              {session.fullName.trim().slice(0, 1) || 'ف'}
            </span>
            <span className="hidden min-w-0 flex-col leading-tight sm:flex">
              <strong className="max-w-36 truncate text-sm font-bold">{session.fullName}</strong>
              <span className="mt-1 max-w-36 truncate text-[11px] text-blue-100/75">{context.company.roleName}</span>
            </span>
            <span className="faranou-header-icon" title="اعلان‌ها" aria-label="اعلان‌ها"><Bell size={20} /></span>
            <button type="button" onClick={() => void handleLogout()} disabled={loggingOut} className="faranou-logout-button" aria-label="خروج">
              <LogOut size={18} />
              <span>{loggingOut ? 'در حال خروج…' : 'خروج'}</span>
            </button>
          </div>
        </div>
      </header>

      <div className="faranou-workspace-body">
        {mobileMenuOpen ? (
          <button type="button" aria-label="بستن منو" className="faranou-mobile-scrim" onClick={() => setMobileMenuOpen(false)} />
        ) : null}
        <aside
          id="main-navigation"
          className={'faranou-sidebar ' + (mobileMenuOpen ? 'is-open' : '')}
          aria-label="ناوبری فرانو"
        >
          <nav className="min-h-0 flex-1 overflow-y-auto px-3 py-4" aria-label="منوی اصلی">
            <p className="mb-3 px-3 text-[10px] font-bold tracking-wide text-blue-100/55">منوی اصلی</p>
            <ul className="space-y-1.5">
              {navigation.map(({ label, icon: Icon, active, divider }) => (
                <li key={label} className={divider ? 'mt-4 border-t border-white/10 pt-4' : undefined}>
                  {active ? (
                    <a href="/app/" aria-current="page" onClick={() => setMobileMenuOpen(false)} className="faranou-nav-active flex min-h-12 items-center gap-3 rounded-xl px-3.5 text-sm font-bold">
                      <Icon size={20} strokeWidth={2.2} /><span>{label}</span>
                    </a>
                  ) : (
                    <div aria-disabled="true" title="این بخش پس از تکمیل مسیر و مجوز مربوط فعال می‌شود." className="faranou-nav-disabled flex min-h-12 cursor-not-allowed items-center gap-3 rounded-xl px-3.5 text-sm">
                      <Icon size={20} /><span>{label}</span><span className="ms-auto rounded-md border border-white/15 px-1.5 py-0.5 text-[9px]">به‌زودی</span>
                    </div>
                  )}
                </li>
              ))}
            </ul>
          </nav>
          <div className="faranou-sidebar-footer">
            <div className="flex items-center gap-2 text-xs font-semibold text-emerald-100"><ShieldCheck size={17} />محیط کاری امن</div>
            <p className="mt-1 text-[10px] leading-5 text-blue-100/65">اطلاعات این محیط بر اساس دسترسی معتبر شما نمایش داده می‌شود.</p>
          </div>
        </aside>

        <main className="faranou-main-content min-w-0 flex-1">
          {logoutError ? <p role="alert" className="mb-4 rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-800">{logoutError}</p> : null}
          {children}
        </main>
      </div>
    </div>
  );
}
