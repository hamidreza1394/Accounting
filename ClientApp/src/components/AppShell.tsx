import { useState, type ReactNode } from 'react';
import { Activity, Bell, BookOpen, Boxes, Building2, CalendarDays, ChevronDown, CircleHelp, ClipboardList, FileBarChart, Headphones, LayoutDashboard, LogOut, Menu, Package, PanelRightClose, Settings2, ShieldCheck, ShoppingCart, Wallet, X } from 'lucide-react';
import type { SelectedContext, Session } from '../api/types';
import { Button } from './ui/FaranouButton';
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
  { label: 'انبار', icon: Boxes },
  { label: 'فروش', icon: ShoppingCart },
  { label: 'خزانه‌داری', icon: Wallet },
  { label: 'بازرگانی خارجی', icon: Package },
  { label: 'گزارش‌ها', icon: FileBarChart, divider: true },
  { label: 'مدیریت دسترسی', icon: ShieldCheck },
  { label: 'اطلاعات پایه', icon: ClipboardList },
  { label: 'تنظیمات', icon: Settings2 },
  { label: 'راهنما و پشتیبانی', icon: Headphones },
];

/** Main authenticated frame. The server-validated WorkContext remains authoritative. */
export function AppShell({ session, context, onChangeContext, onLogout, children }: AppShellProps) {
  const [loggingOut, setLoggingOut] = useState(false);
  const [logoutError, setLogoutError] = useState<string | null>(null);
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

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

  const readOnly = context.fiscalPeriod.accessMode === 'readOnly';

  return (
    <div dir="rtl" className="min-h-screen bg-[#edf3fc] text-ink">
      <header className="faranou-topbar on-dark sticky top-0 z-40 border-b border-white/10 text-white shadow-xl">
        <div className="flex min-h-[76px] items-center justify-between gap-3 px-4 sm:px-6 xl:px-8">
          <div className="flex min-w-0 items-center gap-3">
            <button type="button" onClick={() => setMobileMenuOpen((open) => !open)} className="inline-flex size-10 shrink-0 items-center justify-center rounded-xl border border-white/15 bg-white/10 text-white transition hover:bg-white/15 lg:hidden" aria-label={mobileMenuOpen ? 'بستن منوی اصلی' : 'باز کردن منوی اصلی'} aria-expanded={mobileMenuOpen} aria-controls="main-navigation">
              {mobileMenuOpen ? <X size={20} /> : <Menu size={20} />}
            </button>
            <div className="hidden items-center gap-3 sm:flex">
              <span className="flex size-11 items-center justify-center rounded-full border border-white/20 bg-white/10 text-lg font-black text-amber-200">{session.fullName.trim().slice(0, 1) || 'ف'}</span>
              <span className="hidden min-w-0 flex-col leading-tight md:flex">
                <strong className="max-w-40 truncate text-sm font-bold">{session.fullName}</strong>
                <span className="mt-1 text-xs text-blue-100/70">{session.isAdministrator ? 'مدیر سیستم' : 'کاربر سامانه'}</span>
              </span>
            </div>
          </div>

          <div className="flex min-w-0 flex-1 items-center justify-center gap-2 sm:gap-3">
            <button type="button" onClick={onChangeContext} className="group flex min-w-0 max-w-[48%] items-center gap-2 rounded-2xl border border-white/15 bg-white/[0.08] px-3 py-2 text-start transition hover:border-amber-200/50 hover:bg-white/[0.13] sm:px-4" aria-label="تغییر شرکت یا دوره مالی">
              <Building2 className="hidden shrink-0 text-amber-200 sm:block" size={19} />
              <span className="min-w-0"><span className="block text-[10px] text-blue-100/65 sm:text-[11px]">شرکت جاری</span><strong className="block truncate text-xs sm:text-sm">{context.company.name}</strong></span>
              <ChevronDown className="shrink-0 text-blue-100/70" size={16} />
            </button>
            <button type="button" onClick={onChangeContext} className="group flex min-w-0 max-w-[48%] items-center gap-2 rounded-2xl border border-white/15 bg-white/[0.08] px-3 py-2 text-start transition hover:border-amber-200/50 hover:bg-white/[0.13] sm:px-4" aria-label="تغییر دوره مالی">
              <CalendarDays className="hidden shrink-0 text-amber-200 sm:block" size={19} />
              <span className="min-w-0"><span className="block text-[10px] text-blue-100/65 sm:text-[11px]">دوره مالی</span><strong className="block truncate text-xs sm:text-sm">{context.fiscalPeriod.title}</strong></span>
              <ChevronDown className="shrink-0 text-blue-100/70" size={16} />
            </button>
          </div>

          <div className="flex shrink-0 items-center gap-1 sm:gap-2">
            <span className="hidden items-center gap-2 rounded-full border border-emerald-300/20 bg-emerald-300/10 px-3 py-2 text-xs font-medium text-emerald-100 xl:inline-flex"><span className="size-2 rounded-full bg-emerald-300" />سامانه فعال</span>
            <span className="hidden size-10 items-center justify-center rounded-xl text-white/75 sm:inline-flex" title="راهنما"><CircleHelp size={20} /></span>
            <span className="hidden size-10 items-center justify-center rounded-xl text-white/75 sm:inline-flex" title="اعلان‌ها"><Bell size={19} /></span>
            <button type="button" onClick={() => void handleLogout()} disabled={loggingOut} className="inline-flex size-10 items-center justify-center rounded-xl border border-white/15 bg-white/10 text-white transition hover:bg-rose-400/15 disabled:opacity-60 sm:w-auto sm:gap-2 sm:px-3" aria-label="خروج از حساب" title="خروج">
              <LogOut size={18} /><span className="hidden text-xs font-semibold sm:inline">{loggingOut ? 'در حال خروج…' : 'خروج'}</span>
            </button>
          </div>
        </div>
        <div className="h-px bg-gradient-to-l from-transparent via-amber-300/80 to-transparent" />
      </header>

      <div className="flex min-h-[calc(100vh-77px)]">
        {mobileMenuOpen ? <button type="button" aria-label="بستن منو" className="fixed inset-0 z-30 bg-slate-950/50 lg:hidden" onClick={() => setMobileMenuOpen(false)} /> : null}
        <aside id="main-navigation" className={'faranou-sidebar fixed inset-y-[77px] right-0 z-40 flex w-[278px] flex-col text-white shadow-2xl transition-transform duration-200 lg:sticky lg:top-[77px] lg:z-20 lg:h-[calc(100vh-77px)] lg:translate-x-0 lg:shrink-0 ' + (mobileMenuOpen ? 'translate-x-0' : 'translate-x-full')} aria-label="ناوبری فرانو">
          <div className="border-b border-white/10 px-6 py-6">
            <div className="flex items-center justify-between gap-3">
              <FaranouLogo size={43} tone="dark" />
              <button type="button" onClick={() => setMobileMenuOpen(false)} className="rounded-lg p-2 text-white/60 hover:bg-white/10 hover:text-white lg:hidden" aria-label="بستن منو"><PanelRightClose size={19} /></button>
            </div>
            <p className="mt-4 text-[11px] tracking-wide text-blue-100/55">مدیریت یکپارچه کسب‌وکار</p>
          </div>
          <nav className="min-h-0 flex-1 overflow-y-auto px-3 py-5" aria-label="منوی اصلی">
            <p className="mb-3 px-3 text-[10px] font-bold tracking-[.16em] text-blue-100/45">محیط کاری</p>
            <ul className="space-y-1.5">
              {navigation.map(({ label, icon: Icon, active, divider }) => (
                <li key={label} className={divider ? 'mt-5 border-t border-white/10 pt-5' : undefined}>
                  {active ? (
                    <a href="/app/" aria-current="page" onClick={() => setMobileMenuOpen(false)} className="faranou-nav-active flex min-h-11 items-center gap-3 rounded-xl px-3.5 text-sm font-bold text-[#152b47] shadow-lg">
                      <Icon size={19} strokeWidth={2.2} /><span>{label}</span><span className="ms-auto size-1.5 rounded-full bg-[#173454]" />
                    </a>
                  ) : (
                    <div aria-disabled="true" title="این بخش در مرحله بعدی توسعه فعال می‌شود." className="flex min-h-11 cursor-not-allowed items-center gap-3 rounded-xl px-3.5 text-sm text-blue-50/55">
                      <Icon size={19} /><span>{label}</span><span className="ms-auto rounded-md border border-white/10 px-1.5 py-0.5 text-[9px] text-blue-100/40">به‌زودی</span>
                    </div>
                  )}
                </li>
              ))}
            </ul>
          </nav>
          <div className="border-t border-white/10 p-4">
            <div className="rounded-2xl border border-white/10 bg-white/[0.055] p-3">
              <div className="flex items-center gap-2 text-xs font-semibold text-emerald-200"><ShieldCheck size={16} />اتصال امن به سامانه</div>
              <p className="mt-2 text-[10px] leading-5 text-blue-100/50">نسخه تحت توسعه · محیط کاری محافظت‌شده</p>
            </div>
            <p className="mt-4 text-center text-[10px] text-blue-100/35">FARANOU · نرم‌افزار حسابداری هوشمند</p>
          </div>
        </aside>

        <main id="main" className="min-w-0 flex-1">
          <div className="mx-auto w-full max-w-[1800px] px-4 py-5 sm:px-6 sm:py-7 xl:px-9 xl:py-8">
            <div className="mb-5 flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-white/80 bg-white/65 px-4 py-3 shadow-sm backdrop-blur sm:px-5">
              <div className="flex min-w-0 items-center gap-2 text-xs text-muted"><Building2 size={16} className="shrink-0 text-navy" /><span className="truncate font-semibold text-ink">{context.company.name}</span><span className="text-slate-300">/</span><span className="truncate">{context.fiscalPeriod.title}</span></div>
              <div className="flex items-center gap-2">
                {readOnly ? <span className="rounded-full border border-amber-200 bg-amber-50 px-3 py-1 text-[11px] font-bold text-amber-800">دوره فقط‌خواندنی</span> : <span className="rounded-full border border-emerald-200 bg-emerald-50 px-3 py-1 text-[11px] font-bold text-emerald-800">دوره باز</span>}
                <Button variant="secondary" onClick={onChangeContext}>تغییر زمینه کاری</Button>
              </div>
            </div>
            {logoutError ? <div role="alert" className="mb-5 rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-danger">{logoutError}</div> : null}
            {children}
            <footer className="mt-8 flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-blue-200/70 bg-white/55 px-4 py-3 text-[11px] text-muted shadow-sm">
              <span className="flex items-center gap-2"><ShieldCheck size={15} className="text-emerald-600" />محیط کاری شما با کنترل دسترسی سمت سرور محافظت می‌شود.</span>
              <span className="flex items-center gap-2"><Activity size={15} className="text-teal" />{readOnly ? 'دسترسی فقط مشاهده و گزارش‌گیری' : 'دوره مالی با امکان ثبت عملیات'}</span>
              <span dir="ltr" className="font-semibold tracking-wider text-navy/55">FARANOU</span>
            </footer>
          </div>
        </main>
      </div>
    </div>
  );
}
