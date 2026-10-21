import type { CompanyChoice, Session } from '../api/types';
import { Badge } from './ui/Badge';
import { Button } from './ui/Button';
import { BuildingIcon, LockIcon } from './ui/icons';

type BlockedAccessProps = {
  session: Session;
  reason: 'noCompany' | 'noEligiblePeriod';
  companies: CompanyChoice[];
  onRetry: () => void;
  onLogout: () => void;
};

/**
 * Shown when the server refuses entry: the user has no company, or no company has a fiscal
 * period that can be entered. The accounting environment is not opened in either case.
 */
export function BlockedAccess({ session, reason, companies, onRetry, onLogout }: BlockedAccessProps) {
  const noCompany = reason === 'noCompany';
  const infrastructure = session.accountKind === 'Infrastructure';

  return (
    <div className="flex min-h-screen items-center justify-center px-4 py-10">
      <main id="main" className="w-full max-w-xl rounded-2xl border border-line bg-white p-6 shadow-sm sm:p-8">
        <div className="flex flex-col items-center gap-4 text-center">
          <span aria-hidden="true" className="inline-flex size-14 items-center justify-center rounded-full bg-warning-soft text-warning">
            <LockIcon width={28} height={28} />
          </span>
          <h1 className="text-xl font-bold text-navy">
            {noCompany ? 'شرکتی برای ورود در دسترس نیست' : 'دوره مالی بازی برای ورود وجود ندارد'}
          </h1>
          <p className="text-sm leading-7 text-muted">
            {noCompany
              ? infrastructure
                ? 'حساب زیرساختی به شرکتی تعلق ندارد. برای کار با اطلاعات یک شرکت، با حساب کاربری عضو آن شرکت وارد شوید.'
                : 'عضویت فعالی در هیچ شرکتی ندارید یا عضویت شما غیرفعال شده است. برای دریافت دسترسی با مدیر سیستم یا مدیر شرکت تماس بگیرید.'
              : 'شما در شرکت‌های زیر عضو هستید، اما هیچ‌کدام دوره مالی باز ندارند. برای ادامه، از مدیر شرکت بخواهید یک دوره مالی باز تعریف یا فعال کند.'}
          </p>
        </div>

        {!noCompany && companies.length > 0 ? (
          <ul className="mt-6 flex flex-col gap-2" aria-label="شرکت‌های شما">
            {companies.map((company) => (
              <li key={company.id} className="flex items-center justify-between gap-3 rounded-xl border border-line p-3 text-sm">
                <span className="flex items-center gap-2 font-medium">
                  <BuildingIcon className="text-navy" />
                  {company.name}
                </span>
                <Badge tone="warning">بدون دوره مالی باز</Badge>
              </li>
            ))}
          </ul>
        ) : null}

        <div className="mt-8 flex flex-col-reverse gap-3 sm:flex-row sm:justify-center">
          <Button variant="secondary" onClick={onLogout}>
            خروج
          </Button>
          <Button onClick={onRetry}>بررسی دوباره</Button>
        </div>
      </main>
    </div>
  );
}
