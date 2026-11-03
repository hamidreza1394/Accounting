import type { ReactNode } from 'react';
import { cn } from '@/lib/utils';
import { formatDateTime } from '@/lib/format';

export type AuditTone = 'success' | 'danger' | 'warning' | 'neutral';

export type AuditTimelineEvent = {
  /** Stable, unique key of the event. */
  id: string;
  /** What happened, in words. The meaning is always in this text and in the icon shape, never in colour alone. */
  label: string;
  tone: AuditTone;
  /** A decorative icon whose shape reinforces the label. It is hidden from assistive technology. */
  icon: ReactNode;
  /** ISO date-time; shown as a Jalali date and time. */
  occurredAt: string;
  /** Technical detail such as an IP address; always rendered left-to-right. */
  detail?: string | null;
};

type AuditTimelineProps = {
  /** Accessible name of the list. */
  label: string;
  events: readonly AuditTimelineEvent[];
  /** Spoken name of the detail value, for example "نشانی IP". */
  detailLabel: string;
  /** Shown instead of the detail when an event has none. */
  detailFallback: string;
};

const tones: Record<AuditTone, string> = {
  success: 'border-success/30 bg-success-soft text-success',
  danger: 'border-danger/30 bg-danger-soft text-danger',
  warning: 'border-gold-deep/40 bg-warning-soft text-warning',
  neutral: 'border-line bg-cool text-sub',
};

/** A read-only, newest-first list of audit events (login history today, user audit trails later). */
export function AuditTimeline({ label, events, detailLabel, detailFallback }: AuditTimelineProps) {
  return (
    <ol aria-label={label} className="space-y-2">
      {events.map((event) => (
        <li
          key={event.id}
          className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-line bg-white p-4"
        >
          <div className="flex min-w-0 items-center gap-3">
            <span
              aria-hidden="true"
              className={cn('flex size-10 shrink-0 items-center justify-center rounded-lg border', tones[event.tone])}
            >
              {event.icon}
            </span>
            <div className="min-w-0">
              <strong className="block text-sm text-ink">{event.label}</strong>
              <time dateTime={event.occurredAt} className="mt-1 block text-xs text-sub">
                {formatDateTime(event.occurredAt)}
              </time>
            </div>
          </div>
          <span className="rounded-md bg-cool px-3 py-1.5 text-xs font-medium text-sub">
            <span className="sr-only">{detailLabel}: </span>
            {event.detail ? <span dir="ltr">{event.detail}</span> : detailFallback}
          </span>
        </li>
      ))}
    </ol>
  );
}
