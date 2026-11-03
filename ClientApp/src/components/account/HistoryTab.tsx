import { useEffect, useState } from 'react';
import { Ban, History, LogIn, LogOut, ShieldAlert, UserX } from 'lucide-react';
import { getMyLoginHistory, UnauthenticatedError } from '../../api/client';
import type { LoginHistoryItem } from '../../api/types';
import { AuditTimeline, type AuditTimelineEvent } from '../ui/AuditTimeline';
import { EmptyState, ErrorState, LoadingState } from '../Feedback';

const initialTake = 50;
/** The API clamps the page size to 100; this is the most history it can return. */
const maxTake = 100;

function toEvent(item: LoginHistoryItem, index: number): AuditTimelineEvent {
  const base = { id: `${item.eventType}-${item.occurredAtUtc}-${index}`, occurredAt: item.occurredAtUtc, detail: item.ipAddress };
  switch (item.eventType) {
    case 'LoginSucceeded':
      return { ...base, label: 'ورود موفق', tone: 'success', icon: <LogIn size={17} /> };
    case 'LoginFailed':
      return { ...base, label: 'ورود ناموفق', tone: 'danger', icon: <ShieldAlert size={17} /> };
    case 'LoginDeniedInactive':
      return { ...base, label: 'حساب غیرفعال', tone: 'warning', icon: <Ban size={17} /> };
    case 'LoginDeniedEnrollmentPending':
      return { ...base, label: 'فعال‌سازی تکمیل نشده', tone: 'warning', icon: <UserX size={17} /> };
    case 'LogoutSucceeded':
      return { ...base, label: 'خروج از حساب', tone: 'neutral', icon: <LogOut size={17} /> };
  }
}

type HistoryTabProps = { onUnauthenticated: (error: unknown) => boolean };

export function HistoryTab({ onUnauthenticated }: HistoryTabProps) {
  const [take, setTake] = useState(initialTake);
  const [reloadKey, setReloadKey] = useState(0);
  const [items, setItems] = useState<LoginHistoryItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError(null);
    getMyLoginHistory(controller.signal, take)
      .then((result) => {
        if (!controller.signal.aborted) setItems(result);
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted) return;
        if (cause instanceof UnauthenticatedError && onUnauthenticated(cause)) return;
        setError(cause instanceof Error ? cause.message : 'دریافت سوابق ورود ناموفق بود.');
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [take, reloadKey, onUnauthenticated]);

  const mayHaveMore = items.length >= take;
  const capped = take >= maxTake && items.length >= maxTake;

  return (
    <div className="space-y-4">
      <div>
        <h3 className="font-extrabold text-ink">سوابق ورود و خروج من</h3>
        <p className="mt-1 text-xs leading-6 text-sub">
          فقط رخدادهای قابل انتساب به همین حساب نمایش داده می‌شوند. پایان ناشناخته نشست، خروج موفق تلقی نمی‌شود.
        </p>
      </div>
      {error ? <ErrorState message={error} onRetry={() => setReloadKey((key) => key + 1)} /> : null}
      {loading && items.length === 0 ? <LoadingState label="در حال دریافت سوابق…" /> : null}
      {!loading && !error && items.length === 0 ? (
        <EmptyState title="سابقه‌ای ثبت نشده است">
          <History aria-hidden="true" className="mx-auto mb-2 text-muted" size={28} />
          رویدادهای ورود موفق، ورود ناموفق و خروج صریح در این بخش دیده می‌شوند.
        </EmptyState>
      ) : null}
      {items.length > 0 ? (
        <AuditTimeline label="سوابق ورود و خروج" events={items.map(toEvent)} detailLabel="نشانی IP" detailFallback="IP ثبت نشده" />
      ) : null}
      {items.length > 0 && loading && !error ? <LoadingState label="در حال دریافت سوابق…" /> : null}
      {!error && !loading && items.length > 0 && mayHaveMore && !capped ? (
        <button
          type="button"
          onClick={() => setTake(maxTake)}
          className="w-full rounded-lg border border-line bg-white px-4 py-2.5 text-xs font-bold text-teal-deep outline-none hover:bg-cool focus-visible:ring-2 focus-visible:ring-gold"
        >
          نمایش بیشتر
        </button>
      ) : null}
      {!error && !loading && capped ? (
        <p role="note" className="text-xs leading-6 text-sub">
          حداکثر {maxTake.toLocaleString('fa-IR')} رویداد اخیر نمایش داده می‌شود؛ رویدادهای قدیمی‌تر در این بخش در دسترس نیستند.
        </p>
      ) : null}
    </div>
  );
}
