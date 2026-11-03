import {
  BarChart3,
  BookOpen,
  Boxes,
  Building2,
  CalendarDays,
  CheckCircle2,
  CircleHelp,
  Package,
  ShieldCheck,
  ShoppingCart,
  Sparkles,
  Wallet,
  type LucideIcon,
} from 'lucide-react';
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
      <section className="faranou-welcome faranou-welcome--milad relative isolate flex min-h-[240px] items-center overflow-hidden rounded-[24px] border border-white/80 px-6 py-8 text-white sm:min-h-[260px] sm:px-10 xl:px-14">
        <img
          src="/images/dashboard-tehran-panorama.svg"
          alt=""
          aria-hidden="true"
          decoding="async"
          fetchPriority="high"
          className="faranou-welcome-image"
        />
        <div className="faranou-welcome-shade" />
        <div className="pointer-events-none absolute -start-16 -top-28 size-72 rounded-full border border-white/15" />
        <div className="pointer-events-none absolute -start-7 -top-20 size-56 rounded-full border border-white/15" />
        <div className="pointer-events-none absolute bottom-6 end-10 size-40 rounded-full border border-white/10 opacity-60" />
        <div className="relative z-10 max-w-[680px]">
          <span className="inline-flex items-center gap-2 rounded-full border border-white/25 bg-white/10 px-3.5 py-1.5 text-[11px] font-semibold text-on-navy shadow-[0_4px_18px_rgba(8,40,60,.18)] backdrop-blur-sm">
            <Sparkles size={14} className="text-cta-soft" />
            فضای کاری یکپارچه فرانو
          </span>
          <h1 className="mt-3.5 text-2xl font-black leading-tight tracking-tight sm:text-3xl xl:text-[34px]">
            به سامانه یکپارچه فرانو خوش آمدید
          </h1>
          <p className="mt-2.5 max-w-xl text-sm leading-7 text-on-navy/90 sm:text-base">
            مدیریت هوشمند کسب‌وکار با دقت، شفافیت و امنیت بیشتر.
          </p>
          <div className="mt-5 flex flex-wrap items-center gap-2.5">
            <span className="inline-flex items-center gap-2 rounded-xl border border-white/20 bg-white/10 px-3.5 py-2 text-[11px] font-semibold text-white/95 backdrop-blur-sm">
              <Building2 size={15} className="text-on-navy-accent" />
              {context.company.name}
            </span>
            <span className="inline-flex items-center gap-2 rounded-xl border border-white/20 bg-white/10 px-3.5 py-2 text-[11px] font-semibold text-white/95 backdrop-blur-sm">
              <CalendarDays size={15} className="text-on-navy-accent" />
              {period.title}
            </span>
          </div>
        </div>
      </section>

      <section aria-label="خلاصه محیط کاری" className="grid gap-3 sm:grid-cols-2 2xl:grid-cols-4">
        <article className="faranou-summary-card">
          <span className="faranou-summary-icon bg-azure-chip text-azure-text">
            <Building2 size={22} />
          </span>
          <div className="min-w-0">
            <p className="text-xs text-muted">شرکت فعال</p>
            <p className="mt-1 truncate text-base font-extrabold text-navy">{context.company.name}</p>
            <p className="mt-1 text-[11px] text-muted">{context.company.roleName}</p>
          </div>
          <CheckCircle2 className="ms-auto shrink-0 text-success" size={18} />
        </article>

        <article className="faranou-summary-card">
          <span className="faranou-summary-icon bg-period text-azure">
            <CalendarDays size={22} />
          </span>
          <div className="min-w-0">
            <p className="text-xs text-muted">دوره مالی جاری</p>
            <p className="mt-1 truncate text-base font-extrabold text-navy">{period.title}</p>
            <p className="mt-1 text-[11px] text-muted">
              {formatPersianDate(period.startDate)} تا {formatPersianDate(period.endDate)}
            </p>
          </div>
        </article>

        <article className="faranou-summary-card">
          <span className="faranou-summary-icon bg-teal-soft text-teal-deep">
            <ShieldCheck size={22} />
          </span>
          <div className="min-w-0">
            <p className="text-xs text-muted">سطح دسترسی</p>
            <p className="mt-1 text-base font-extrabold text-navy">{context.company.roleName}</p>
            <p className="mt-1 text-[11px] text-muted">
              {session.isAdministrator ? 'مدیر سامانه' : 'عضو شرکت'}
            </p>
          </div>
        </article>

        <article
          className={
            'faranou-summary-card ' + (readOnly ? 'border-gold/40' : 'border-success/20')
          }
        >
          <span
            className={
              'faranou-summary-icon ' +
              (readOnly ? 'bg-warning-soft text-warning' : 'bg-success-soft text-success')
            }
          >
            <CheckCircle2 size={22} />
          </span>
          <div className="min-w-0">
            <p className="text-xs text-muted">وضعیت دوره مالی</p>
            <p className="mt-1 text-base font-extrabold text-navy">
              {readOnly ? 'فقط مشاهده و گزارش‌گیری' : 'دوره باز'}
            </p>
            <p className="mt-1 text-[11px] text-muted">
              {readOnly ? 'ثبت عملیات در این دوره مجاز نیست' : 'وضعیت بر اساس اطلاعات سرور'}
            </p>
          </div>
        </article>
      </section>

      <section className="grid gap-4 xl:grid-cols-[1fr_1.18fr_.9fr]">
        <article className="faranou-panel">
          <div className="flex items-center justify-between gap-2">
            <div>
              <h2 className="text-sm font-extrabold text-navy">زیرسیستم‌های فرانو</h2>
              <p className="mt-1 text-[11px] text-muted">وضعیت دسترسی بخش‌های سامانه</p>
            </div>
            <span className="faranou-panel-icon text-azure">
              <Boxes size={20} />
            </span>
          </div>
          <div className="mt-4 space-y-2.5">
            {modules.map(({ label, icon: Icon }) => (
              <div key={label} aria-disabled="true" className="faranou-module-row">
                <span className="faranou-module-row-icon">
                  <Icon size={17} />
                </span>
                <span className="min-w-0 flex-1 text-xs font-bold text-navy">{label}</span>
                <span className="rounded-full bg-cool px-2.5 py-1 text-[10px] font-semibold text-sub">
                  در حال توسعه
                </span>
              </div>
            ))}
          </div>
        </article>

        <article className="faranou-panel">
          <div className="flex items-center justify-between gap-2">
            <div>
              <h2 className="text-sm font-extrabold text-navy">روند عملکرد مالی</h2>
              <p className="mt-1 text-[11px] text-muted">نمایش شاخص‌ها پس از اتصال داده‌های مالی</p>
            </div>
            <span className="faranou-panel-icon text-azure">
              <BarChart3 size={20} />
            </span>
          </div>
          <div className="faranou-empty-state mt-5 flex min-h-[220px] flex-col items-center justify-center rounded-xl border border-dashed border-period-line px-4 text-center">
            <BarChart3 size={34} className="text-pick" />
            <p className="mt-3 text-xs font-bold text-ink">داده‌ای برای ترسیم نمودار در دسترس نیست</p>
            <p className="mt-1 max-w-xs text-[10px] leading-5 text-muted">
              با فعال‌شدن ماژول‌های مالی، روندها و شاخص‌های واقعی در این قسمت نمایش داده می‌شوند.
            </p>
          </div>
        </article>

        <article className="faranou-panel">
          <div className="flex items-center justify-between gap-2">
            <div>
              <h2 className="text-sm font-extrabold text-navy">اطلاعات محیط کاری</h2>
              <p className="mt-1 text-[11px] text-muted">مشخصات نشست جاری</p>
            </div>
            <span className="faranou-panel-icon text-teal-deep">
              <ShieldCheck size={20} />
            </span>
          </div>
          <dl className="mt-4 divide-y divide-cool text-xs">
            <div className="flex justify-between gap-3 py-3">
              <dt className="text-muted">نام کاربر</dt>
              <dd className="max-w-[65%] truncate font-bold text-navy">{session.fullName}</dd>
            </div>
            <div className="flex justify-between gap-3 py-3">
              <dt className="text-muted">نام کاربری</dt>
              <dd className="max-w-[65%] truncate font-bold text-navy" dir="ltr">
                {session.username}
              </dd>
            </div>
            <div className="flex justify-between gap-3 py-3">
              <dt className="text-muted">نقش در شرکت</dt>
              <dd className="max-w-[65%] truncate font-bold text-navy">{context.company.roleName}</dd>
            </div>
            <div className="flex justify-between gap-3 py-3">
              <dt className="text-muted">وضعیت مجوز دوره</dt>
              <dd className="font-bold text-success">{readOnly ? 'فقط مشاهده' : 'فعال'}</dd>
            </div>
          </dl>
          <div className="mt-3 flex items-center gap-2 rounded-xl border border-success/20 bg-success-soft px-3 py-3 text-[11px] font-semibold text-success">
            <ShieldCheck size={19} />
            اطلاعات شرکت و دوره از سرور دریافت شده است.
          </div>
        </article>
      </section>

      <section
        aria-labelledby="company-data-title"
        className="overflow-hidden rounded-2xl border border-white/90 bg-white/95 shadow-[0_8px_28px_rgba(31,65,111,.06)]"
      >
        <div className="flex flex-wrap items-center justify-between gap-2 border-b border-cool px-5 py-4">
          <div>
            <h2 id="company-data-title" className="text-sm font-extrabold text-navy">
              اطلاعات شرکت و دوره‌های مالی
            </h2>
            <p className="mt-1 text-[11px] text-muted">اطلاعات معتبر دریافت‌شده از سامانه</p>
          </div>
          <span className="inline-flex items-center gap-1.5 rounded-full bg-success-soft px-3 py-1 text-[10px] font-bold text-success">
            <CheckCircle2 size={13} />
            متصل به داده واقعی
          </span>
        </div>
        <div className="p-4 sm:p-5">
          <CompanyOverviewPanel
            companyId={context.company.id}
            currentPeriodId={period.id}
            onUnauthenticated={onUnauthenticated}
          />
        </div>
      </section>

      <div className="flex flex-wrap items-center justify-between gap-2 px-1 text-[10px] text-muted">
        <span className="inline-flex items-center gap-1.5">
          <ShieldCheck size={13} className="text-success" />
          اطلاعات شرکت و دوره از سرور دریافت می‌شود.
        </span>
        <span className="inline-flex items-center gap-1.5">
          <CircleHelp size={13} />
          بخش‌های در حال توسعه با برچسب «در حال توسعه» مشخص شده‌اند.
        </span>
      </div>
    </div>
  );
}
