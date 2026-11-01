import { BarChart3, BookOpen, Boxes, Building2, CalendarDays, CheckCircle2, CircleHelp, Clock3, FilePlus2, Package, ShieldCheck, ShoppingCart, Sparkles, Users, Wallet, type LucideIcon } from 'lucide-react';
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
const toneClasses: Record<string, string> = {
  violet: 'bg-sky-100 text-sky-800 ring-sky-200',
  cyan: 'bg-cyan-100 text-cyan-800 ring-cyan-200',
  blue: 'bg-blue-100 text-blue-800 ring-blue-200',
  amber: 'bg-amber-100 text-amber-800 ring-amber-200',
  teal: 'bg-teal-100 text-teal-800 ring-teal-200',
  indigo: 'bg-blue-100 text-blue-800 ring-blue-200',
};

export function Dashboard({ session, context, onUnauthenticated }: DashboardProps) {
  const period = context.fiscalPeriod;
  const readOnly = period.accessMode === 'readOnly';

  return (
    <div className="flex flex-col gap-6">
      <section className="faranou-welcome relative isolate overflow-hidden rounded-[26px] border border-white/80 p-5 text-white shadow-[0_18px_50px_rgba(23,54,91,.16)] sm:p-7 xl:p-8">
        <div className="pointer-events-none absolute -left-12 -top-24 size-72 rounded-full border border-white/10" />
        <div className="pointer-events-none absolute -left-4 -top-16 size-56 rounded-full border border-white/10" />
        <div className="relative z-10 max-w-2xl">
          <span className="inline-flex items-center gap-2 rounded-full border border-white/20 bg-white/10 px-3 py-1.5 text-[11px] font-semibold text-blue-50"><Sparkles size={14} className="text-amber-200" />فضای کاری یکپارچه فرانو</span>
          <h1 className="mt-4 text-2xl font-black leading-tight sm:text-3xl xl:text-4xl">به فرانو <span className="text-amber-300">خوش آمدید</span></h1>
          <p className="mt-3 max-w-xl text-sm leading-7 text-blue-50/80 sm:text-base">{session.fullName}، همه چیز برای مدیریت دقیق‌تر و شفاف‌تر کسب‌وکار شما در یک نگاه آماده است.</p>
          <div className="mt-5 flex flex-wrap items-center gap-2 text-xs">
            <span className="inline-flex items-center gap-1.5 rounded-lg bg-white/10 px-3 py-2"><Building2 size={15} />{context.company.name}</span>
            <span className="inline-flex items-center gap-1.5 rounded-lg bg-white/10 px-3 py-2"><CalendarDays size={15} />{period.title}</span>
          </div>
        </div>
        <div className="pointer-events-none absolute -bottom-24 -left-10 hidden size-80 rounded-full bg-sky-300/10 blur-3xl sm:block" />
        <div className="pointer-events-none absolute -left-16 top-0 hidden h-full w-1/3 bg-gradient-to-l from-transparent via-white/[.06] to-transparent sm:block" />
        <div className="absolute bottom-0 left-0 right-0 h-px bg-gradient-to-l from-transparent via-amber-300/80 to-transparent" />
      </section>

      <section aria-labelledby="modules-title">
        <div className="mb-3 flex items-end justify-between gap-3">
          <div><h2 id="modules-title" className="text-base font-extrabold text-navy sm:text-lg">زیرسیستم‌های فرانو</h2><p className="mt-1 text-xs text-muted">دسترسی یکپارچه به بخش‌های اصلی نرم‌افزار</p></div>
          <span className="rounded-full border border-blue-200 bg-white/70 px-3 py-1 text-[10px] font-semibold text-muted">در حال تکمیل</span>
        </div>
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 xl:grid-cols-6">
          {modules.map(({ label, description, icon: Icon, tone }) => (
            <div key={label} aria-disabled="true" title="این زیرسیستم پس از تکمیل مجوز و صفحات مربوط فعال می‌شود." className="faranou-module-card group relative flex min-h-[132px] cursor-not-allowed flex-col items-start justify-between overflow-hidden rounded-2xl border border-white bg-white/85 p-4 shadow-[0_5px_20px_rgba(31,65,111,.06)] ring-1 ring-blue-100/80 sm:min-h-[148px]">
              <span className={'inline-flex size-11 items-center justify-center rounded-2xl ring-1 ' + toneClasses[tone]}><Icon size={22} strokeWidth={2} /></span>
              <span className="mt-4 block"><strong className="block text-sm font-extrabold text-navy">{label}</strong><span className="mt-1 block text-[10px] leading-5 text-muted">{description}</span></span>
              <span className="absolute left-3 top-3 rounded-full bg-slate-100 px-2 py-0.5 text-[9px] font-bold text-slate-500">به‌زودی</span>
            </div>
          ))}
        </div>
      </section>

      <section aria-label="نمای کلی محیط کاری" className="grid gap-3 sm:grid-cols-2 2xl:grid-cols-4">
        <article className="faranou-summary-card border-sky-100"><span className="faranou-summary-icon bg-sky-100 text-sky-700"><Building2 size={21} /></span><div className="min-w-0"><p className="text-xs text-muted">شرکت فعال</p><p className="mt-1 truncate text-base font-extrabold text-navy">{context.company.name}</p><p className="mt-1 text-[11px] text-muted">{context.company.roleName}</p></div><CheckCircle2 className="ms-auto shrink-0 text-emerald-500" size={18} /></article>
        <article className="faranou-summary-card border-indigo-100"><span className="faranou-summary-icon bg-indigo-100 text-indigo-700"><CalendarDays size={21} /></span><div className="min-w-0"><p className="text-xs text-muted">دوره مالی جاری</p><p className="mt-1 truncate text-base font-extrabold text-navy">{period.title}</p><p className="mt-1 text-[11px] text-muted">{formatPersianDate(period.startDate)} تا {formatPersianDate(period.endDate)}</p></div></article>
        <article className="faranou-summary-card border-emerald-100"><span className="faranou-summary-icon bg-emerald-100 text-emerald-700"><ShieldCheck size={21} /></span><div className="min-w-0"><p className="text-xs text-muted">سطح دسترسی شما</p><p className="mt-1 text-base font-extrabold text-navy">{context.company.roleName}</p><p className="mt-1 text-[11px] text-muted">{session.isAdministrator ? 'مدیر سامانه' : 'عضو شرکت'}</p></div></article>
        <article className={'faranou-summary-card ' + (readOnly ? 'border-amber-200' : 'border-teal-100')}><span className={'faranou-summary-icon ' + (readOnly ? 'bg-amber-100 text-amber-700' : 'bg-teal-100 text-teal-700')}><CalendarDays size={21} /></span><div className="min-w-0"><p className="text-xs text-muted">وضعیت دوره مالی</p><p className="mt-1 text-base font-extrabold text-navy">{readOnly ? 'فقط مشاهده و گزارش‌گیری' : 'دوره باز'}</p><p className="mt-1 text-[11px] text-muted">{readOnly ? 'ثبت عملیات در این دوره مجاز نیست' : 'وضعیت بر اساس اطلاعات سرور'}</p></div></article>
      </section>

      <section className="grid gap-4 xl:grid-cols-[1.05fr_1.2fr_.85fr]">
        <article className="faranou-panel">
          <div className="flex items-center justify-between gap-2"><div><h2 className="text-sm font-extrabold text-navy">روند فعالیت‌های مالی</h2><p className="mt-1 text-[11px] text-muted">نمایش پس از اتصال داده‌های حسابداری</p></div><span className="faranou-panel-icon text-blue-600"><BarChart3 size={20} /></span></div>
          <div className="faranou-empty-state mt-5 flex min-h-[180px] flex-col items-center justify-center rounded-xl border border-dashed border-blue-200 bg-blue-50/50 px-4 text-center"><BarChart3 size={30} className="text-blue-300" /><p className="mt-3 text-xs font-bold text-slate-600">داده‌ای برای ترسیم نمودار در دسترس نیست</p><p className="mt-1 max-w-xs text-[10px] leading-5 text-muted">با فعال‌شدن ماژول‌های مالی، روندها و شاخص‌های واقعی در این قسمت نمایش داده می‌شوند.</p></div>
        </article>
        <article className="faranou-panel">
          <div className="flex items-center justify-between gap-3"><div><h2 className="text-sm font-extrabold text-navy">فعالیت‌های اخیر</h2><p className="mt-1 text-[11px] text-muted">رویدادهای ثبت‌شده در محیط کاری</p></div><span className="faranou-panel-icon text-indigo-600"><Clock3 size={20} /></span></div>
          <div className="faranou-empty-state mt-5 flex min-h-[180px] flex-col items-center justify-center rounded-xl border border-dashed border-indigo-200 bg-indigo-50/40 px-4 text-center"><span className="flex size-12 items-center justify-center rounded-2xl bg-indigo-100 text-indigo-600"><Clock3 size={24} /></span><p className="mt-3 text-xs font-bold text-slate-600">هنوز رویدادی برای نمایش وجود ندارد</p><p className="mt-1 max-w-xs text-[10px] leading-5 text-muted">پس از اتصال سرویس ثبت رویدادها، آخرین فعالیت‌های کاربران اینجا نمایش داده می‌شود.</p></div>
        </article>
        <article className="faranou-panel">
          <div className="flex items-center justify-between gap-2"><div><h2 className="text-sm font-extrabold text-navy">دسترسی سریع</h2><p className="mt-1 text-[11px] text-muted">میان‌برهای پرکاربرد</p></div><span className="faranou-panel-icon text-amber-500"><Sparkles size={20} /></span></div>
          <div className="mt-5 grid grid-cols-2 gap-2"><QuickAction icon={Users} label="تعریف طرف حساب" /><QuickAction icon={Package} label="تعریف کالا" /><QuickAction icon={FilePlus2} label="ثبت سند" /><QuickAction icon={BarChart3} label="گزارش‌گیری" /></div>
          <p className="mt-3 text-center text-[10px] text-muted">با تکمیل صفحات و مجوزهای هر زیرسیستم فعال می‌شود.</p>
        </article>
      </section>

      <section aria-labelledby="company-data-title" className="overflow-hidden rounded-2xl border border-white/90 bg-white/80 shadow-[0_8px_28px_rgba(31,65,111,.06)]">
        <div className="flex flex-wrap items-center justify-between gap-2 border-b border-slate-100 px-5 py-4"><div><h2 id="company-data-title" className="text-sm font-extrabold text-navy">اطلاعات شرکت و دوره‌های مالی</h2><p className="mt-1 text-[11px] text-muted">اطلاعات معتبر دریافت‌شده از سامانه</p></div><span className="inline-flex items-center gap-1.5 rounded-full bg-emerald-50 px-3 py-1 text-[10px] font-bold text-emerald-700"><CheckCircle2 size={13} />متصل به داده واقعی</span></div>
        <div className="p-4 sm:p-5"><CompanyOverviewPanel companyId={context.company.id} currentPeriodId={period.id} onUnauthenticated={onUnauthenticated} /></div>
      </section>

      <div className="flex flex-wrap items-center justify-between gap-2 px-1 text-[10px] text-muted"><span className="inline-flex items-center gap-1.5"><ShieldCheck size={13} className="text-emerald-600" />اطلاعات شرکت و دوره از سرور دریافت می‌شود.</span><span className="inline-flex items-center gap-1.5"><CircleHelp size={13} />بخش‌های در حال توسعه با برچسب «به‌زودی» مشخص شده‌اند.</span></div>
    </div>
  );
}

function QuickAction({ icon: Icon, label }: { icon: LucideIcon; label: string }) {
  return <div aria-disabled="true" title="این میان‌بر پس از فعال‌شدن زیرسیستم مربوط قابل استفاده خواهد بود." className="faranou-quick-action flex min-h-[94px] cursor-not-allowed flex-col items-center justify-center gap-2 rounded-xl border border-blue-100 bg-gradient-to-b from-white to-blue-50/70 p-3 text-center shadow-sm"><span className="flex size-9 items-center justify-center rounded-xl bg-blue-100 text-blue-700"><Icon size={19} /></span><span className="text-[11px] font-bold text-navy">{label}</span><span className="text-[9px] text-muted">به‌زودی</span></div>;
}
