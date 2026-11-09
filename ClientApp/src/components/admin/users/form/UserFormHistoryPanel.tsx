import { useId } from 'react';
import type { AdminUserDetails } from '@/api/types';
import { StatusBadge } from '@/components/ui/StatusBadge';
import { formatDateTime } from '@/lib/format';
import { Info } from '../profile/profileShared';
import { ProfileAuditTab, ProfileLoginHistoryTab } from '../profile/ProfileTabs';
import type { FormTabCache, UserFormContext } from './userFormShared';

type Props = {
  user: AdminUserDetails;
  cache: FormTabCache;
  context: UserFormContext;
  onUnauthenticated: (error: unknown) => boolean;
};

/** Tab 5 (edit mode): account dates and status plus the existing login / audit history tabs, reused. */
export function HistoryPanel({ user, cache, context, onUnauthenticated }: Props) {
  const id = useId();
  const shared = { user, cache, onUnauthenticated };
  return (
    <div className="space-y-6">
      <section aria-labelledby={`${id}-account`}>
        <h2 id={`${id}-account`} className="mb-3 text-base font-bold text-ink">
          وضعیت حساب
        </h2>
        <dl className="grid gap-3 sm:grid-cols-2">
          <Info label="وضعیت حساب">
            <StatusBadge status={user.isActive ? 'active' : 'inactive'} />
          </Info>
          <Info label="تاریخ ایجاد حساب">{formatDateTime(user.insertDateTime)}</Info>
          <Info label="آخرین تغییر">{formatDateTime(user.updateDateTime)}</Info>
          <Info label="آخرین ورود موفق">{formatDateTime(user.lastLoginDateTime, 'هرگز')}</Info>
        </dl>
      </section>
      <section aria-labelledby={`${id}-logins`}>
        <h2 id={`${id}-logins`} className="mb-3 text-base font-bold text-ink">
          سوابق ورود و خروج
        </h2>
        <ProfileLoginHistoryTab {...shared} />
      </section>
      <section aria-labelledby={`${id}-audit`}>
        <h2 id={`${id}-audit`} className="mb-3 text-base font-bold text-ink">
          سوابق تغییرات مدیریتی
        </h2>
        {context.can('admin.security.view') ? (
          <ProfileAuditTab {...shared} />
        ) : (
          <p className="text-sm text-sub">نمایش سوابق تغییرات مدیریتی برای شما مجاز نیست.</p>
        )}
      </section>
    </div>
  );
}
