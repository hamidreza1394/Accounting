import type { ReactNode } from 'react';
import { CalendarClock, KeyRound, Mail, Phone, UserRound } from 'lucide-react';
import type { MyProfile } from '../../api/types';
import { formatDateTime, notRecorded } from '../../lib/format';
import { Alert } from '../ui/Alert';
import { StatusBadge } from '../ui/StatusBadge';
import { ErrorState, LoadingState } from '../Feedback';

function InfoCard({ icon: Icon, label, value, verified }: {
  icon: typeof UserRound;
  label: string;
  value: string | null;
  /** When given and the field has a value, a verified / not verified badge is shown. */
  verified?: boolean;
}) {
  return (
    <div className="min-w-0 rounded-xl border border-line bg-white p-4">
      <div className="mb-2 flex items-center gap-2 text-xs font-semibold text-sub">
        <Icon size={15} aria-hidden="true" />
        <span>{label}</span>
      </div>
      <div className="flex min-w-0 flex-wrap items-center justify-between gap-2">
        <strong className="min-w-0 break-words text-sm text-ink">{value || notRecorded}</strong>
        {verified === undefined || !value ? null : verified ? (
          <StatusBadge status="active">تأیید شده</StatusBadge>
        ) : (
          <StatusBadge status="pending">تأیید نشده</StatusBadge>
        )}
      </div>
    </div>
  );
}

type ProfileTabProps = {
  profile: MyProfile | null;
  loading: boolean;
  error: string | null;
  onRetry: () => void;
};

export function ProfileTab({ profile, loading, error, onRetry }: ProfileTabProps): ReactNode {
  return (
    <div className="space-y-4">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h3 className="font-extrabold text-ink">مشخصات حساب</h3>
          <p className="mt-1 text-xs text-sub">این اطلاعات فقط برای حساب واردشده نمایش داده می‌شود.</p>
        </div>
        <StatusBadge status="readonly" />
      </div>
      <Alert tone="info">تغییر ایمیل و شماره همراه در این صفحه در دسترس نیست.</Alert>
      {loading ? <LoadingState label="در حال دریافت اطلاعات…" /> : null}
      {error ? <ErrorState message={error} onRetry={onRetry} /> : null}
      {profile ? (
        <div className="grid gap-3 sm:grid-cols-2">
          <InfoCard icon={UserRound} label="نام و نام خانوادگی" value={[profile.firstName, profile.lastName].filter(Boolean).join(' ') || null} />
          <InfoCard icon={KeyRound} label="نام کاربری" value={profile.username} />
          <InfoCard icon={Mail} label="ایمیل" value={profile.emailAddress} verified={profile.isEmailAddressVerified} />
          <InfoCard icon={Phone} label="شماره همراه" value={profile.cellPhoneNumber} verified={profile.isCellPhoneNumberVerified} />
          <InfoCard icon={CalendarClock} label="آخرین ورود ثبت‌شده" value={formatDateTime(profile.lastLoginDateTime)} />
          <InfoCard icon={CalendarClock} label="تاریخ ایجاد حساب" value={formatDateTime(profile.insertDateTime)} />
        </div>
      ) : null}
    </div>
  );
}
