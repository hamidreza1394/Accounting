import { BarChart3, BookOpen, Boxes, Building2, CalendarDays, CheckCircle2, CircleHelp, Package, ShieldCheck, ShoppingCart, Sparkles, Wallet, type LucideIcon } from 'lucide-react';
import type { SelectedContext, Session } from '../api/types';
import { formatPersianDate } from '../lib/date';
import { CompanyOverviewPanel } from './CompanyOverviewPanel';

type DashboardProps = {
  session: Session;
  context: SelectedContext;
  onUnauthenticated: (error: unknown) => boolean;
};

type ModuleItem = { label: string; description: string; icon: LucideIcon; tone: string };
const modules: ModuleItem[] = [
  { label: 'حسابداری', description: 'اسناد و دفاتر مالی', icon: BookOpen, tone: 'violet' },
  { label: 'انبار', description: 'کالا و موجودی', icon: Boxes, tone: 'cyan' },
  { label: 'فروش', description: 'فاکتور و مشتریان', icon: ShoppingCart, tone: 'blue' },
  { label: 'خزانه‌داری', description: 'دریافت و پرداخت', icon: Wallet, tone: 'amber' },
  { label: 'بازرگانی خارجی', description: 'پرونده‌ها و عملیات واردات', icon: Package, tone: 'teal' },
  { label: 'گزارش‌ها', description: 'گزارش‌های مدیریتی', icon: BarChart3, tone: 'indigo' },
];
export function Dashboard({ session, context, onUnauthenticated }: DashboardProps) {
  const period = context.fiscalPeriod;
  const readOnly = period.accessMode === 'readOnly';

  return (
    <div className="flex flex-col gap-5 sm:gap-6">
      <section className="faranou-welcome relative isolate flex min-h-[220px] items-center overflow-hidden rounded-[22px] border border-white/80 px-6 py-7 text-white sm:min-h-[230px] sm:px-10 xl:px-14">
        <img
          src="/images/dashboard-tehran-panorama.svg"
          alt=""
          aria-hidden="true"
          decoding="async"
          fetchPriority="high"
          className="faranou-welcome-image"
        />
        <div className="faranou-welcome-shade" />
        <div className="pointer-events-none absolute -right-16 -top-28 size-72 rounded-full border border-white/15" />
        <div className="pointer-events-none absolute -right-7 -top-20 size-56 rounded-full border border-white/15" />
        <div className="relative z-10 max-w-[660px]">
          <span className="inline-flex items-center gap-2 rounded-full border border-white/25 bg-white/10 px-3 py-1.5 text-[11px] font-semibold text-blue-50">
            <Sparkles size={14} className="text-amber-200" />فضای کاری یکپارچه فرانو
          </span>
          <h1 className="mt-3 text-2xl font-black leading-tight sm:text-3xl xl:text-[34px]">به سامانه یکپارچه فرانو خوش آمدید</h1>
          <p className="mt-2 max-w-xl text-sm leading-7 text-blue-50/90 sm:text-base">
            مدیریت هوشمند کسب‌وکار با دقت، شفافیت و امنیت بیشتر.
          </p>
        </div>
      </section>

      <section aria-label="خلاصه محیط کاری" className="grid gap-3 sm:grid-cols-2 2xl:grid-cols-4">
        <article className="faranou-summary-card"><span className="faranou-summary-icon bg-sky-100 text-sky-700"><Building2 size={22} /></span><div className="min-w-0"><p className="text-xs text-muted">شرکت فعال</p><p className="mt-1 truncate text-base font-extrabold text-navy">{context.company.name}</p><p className="mt-1 text-[11px] text-muted">{context.company.roleName}</p></div><CheckCircle2 className="ms-auto shrink-0 text-emerald-500" size={18} /></article>
        <article className="faranou-summary-card"><span className="faranou-summary-icon bg-blue-100 text-blue-700"><CalendarDays size={22} /></span><div className="min-w-0"><p className="text-xs text-muted">دوره مالی جاری</p><p className="mt-1 truncate text-base font-extrabold text-navy">{period.title}</p><p className="mt-1 text-[11px] text-muted">{formatPersianDate(period.startDate)} تا {formatPersianDate(period.endDate)}</p></div></article>
        <article className="faranou-summary-card"><span className="faranou-summary-icon bg-teal-100 text-teal-700"><ShieldCheck size={22} /></span><div className="min-w-0"><p className="text-xs text-muted">سطح دسترسی</p><p className="mt-1 text-base font-extrabold text-navy">{context.company.roleName}</p><p className="mt-1 text-[11px] text-muted">{session.isAdministrator ? 'مدیر سامانه' : 'عضو شرکت'}</p></div></article>
        <article className={'faranou-summary-card ' + (readOnly ? 'border-amber-200' : 'border-emerald-100')}><span className={'faranou-summary-icon ' + (readOnly ? 'bg-amber-100 text-amber-700' : 'bg-emerald-100 text-emerald-700')}><CheckCircle2 size={22} /></span><div className="min-w-0"><p className="text-xs text-muted">وضعیت دوره مالی</p><p className="mt-1 text-base font-extrabold text-navy">{readOnly ? 'فقط مشاهده و گزارش‌گیری' : 'دوره باز'}</p><p className="mt-1 text-[11px] text-muted">{readOnly ? 'ثبت عملیات در این دوره مجاز نیست' : 'وضعیت بر اساس اطلاعات سرور'}</p></div></article>
      </section>

      <section className="grid gap-4 xl:grid-cols-[1fr_1.18fr_.9fr]">
        <article className="faranou-panel">
          <div className="flex items-center justify-between gap-2"><div><h2 className="text-sm font-extrabold text-navy">زیرسیستم‌های فرانو</h2><p className="mt-1 text-[11px] text-muted">وضعیت دسترسی بخش‌های سامانه</p></div><span className="faranou-panel-icon text-blue-600"><Boxes size={20} /></span></div>
          <div className="mt-4 space-y-2.5">
            {modules.map(({ label, icon: Icon }) => (
              <div key={label} aria-disabled="true" className="faranou-module-row">
                <span className="faranou-module-row-icon"><Icon size={17} /></span>
                <span className="min-w-0 flex-1 text-xs font-bold text-navy">{label}</span>
                <span className="rounded-full bg-slate-100 px-2.5 py-1 text-[10px] font-semibold text-slate-600">در حال توسعه</span>
              </div>
            ))}
          </div>
        </article>

        <article className="faranou-panel">
          <div className="flex items-center justify-between gap-2"><div><h2 className="text-sm font-extrabold text-navy">روند عملکرد مالی</h2><p className="mt-1 text-[11px] text-muted">نمایش شاخص‌ها پس از اتصال داده‌های مالی</p></div><span className="faranou-panel-icon text-blue-600"><BarChart3 size={20} /></span></div>
          <div className="faranou-empty-state mt-5 flex min-h-[220px] flex-col items-center justify-center rounded-xl border border-dashed border-blue-200 px-4 text-center">
            <BarChart3 size={34} className="text-blue-400" />
            <p className="mt-3 text-xs font-bold text-slate-700">داده‌ای برای ترسیم نمودار در دسترس نیست</p>
            <p className="mt-1 max-w-xs text-[10px] leading-5 text-muted">با فعال‌شدن ماژول‌های مالی، روندها و شاخص‌های واقعی در این قسمت نمایش داده می‌شوند.</p>
          </div>
        </article>

        <article className="faranou-panel">
          <div className="flex items-center justify-between gap-2"><div><h2 className="text-sm font-extrabold text-navy">اطلاعات محیط کاری</h2><p className="mt-1 text-[11px] text-muted">مشخصات نشست جاری</p></div><span className="faranou-panel-icon text-teal-600"><ShieldCheck size={20} /></span></div>
          <dl className="mt-4 divide-y divide-slate-100 text-xs">
            <div className="flex justify-between gap-3 py-3"><dt className="text-muted">نام کاربر</dt><dd className="max-w-[65%] truncate font-bold text-navy">{session.fullName}</dd></div>
            <div className="flex justify-between gap-3 py-3"><dt className="text-muted">نام کاربری</dt><dd className="max-w-[65%] truncate font-bold text-navy" dir="ltr">{session.username}</dd></div>
            <div className="flex justify-between gap-3 py-3"><dt className="text-muted">نقش در شرکت</dt><dd className="max-w-[65%] truncate font-bold text-navy">{context.company.roleName}</dd></div>
            <div className="flex justify-between gap-3 py-3"><dt className="text-muted">وضعیت مجوز دوره</dt><dd className="font-bold text-emerald-700">{readOnly ? 'فقط مشاهده' : 'فعال'}</dd></div>
          </dl>
          <div className="mt-3 flex items-center gap-2 rounded-xl border border-emerald-100 bg-emerald-50 px-3 py-3 text-[11px] font-semibold text-emerald-800"><ShieldCheck size={19} />اطلاعات شرکت و دوره از سرور دریافت شده است.</div>
        </article>
      </section>

      <section aria-labelledby="company-data-title" className="overflow-hidden rounded-2xl border border-white/90 bg-white/95 shadow-[0_8px_28px_rgba(31,65,111,.06)]">
        <div className="flex flex-wrap items-center justify-between gap-2 border-b border-slate-100 px-5 py-4"><div><h2 id="company-data-title" className="text-sm font-extrabold text-navy">اطلاعات شرکت و دوره‌های مالی</h2><p className="mt-1 text-[11px] text-muted">اطلاعات معتبر دریافت‌شده از سامانه</p></div><span className="inline-flex items-center gap-1.5 rounded-full bg-emerald-50 px-3 py-1 text-[10px] font-bold text-emerald-700"><CheckCircle2 size={13} />متصل به داده واقعی</span></div>
        <div className="p-4 sm:p-5"><CompanyOverviewPanel companyId={context.company.id} currentPeriodId={period.id} onUnauthenticated={onUnauthenticated} /></div>
      </section>

      <div className="flex flex-wrap items-center justify-between gap-2 px-1 text-[10px] text-muted"><span className="inline-flex items-center gap-1.5"><ShieldCheck size={13} className="text-emerald-600" />اطلاعات شرکت و دوره از سرور دریافت می‌شود.</span><span className="inline-flex items-center gap-1.5"><CircleHelp size={13} />بخش‌های در حال توسعه با برچسب «در حال توسعه» مشخص شده‌اند.</span></div>
    </div>
  );
}

