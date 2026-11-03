import { formatPersianDate } from './date';

// One presentation implementation for dates: the Jalali helper of ./date.ts (fa-IR, Persian
// digits). Values stay ISO on the wire; this only formats for display.

const isoCalendarDate = /^\d{4}-\d{2}-\d{2}$/;

const timeFormat = new Intl.DateTimeFormat('fa-IR-u-nu-arabext', {
  hour: '2-digit',
  minute: '2-digit',
  hour12: false,
});

export const notRecorded = 'ثبت نشده';

function pad(value: number): string {
  return String(value).padStart(2, '0');
}

function toDate(value: string | null | undefined): Date | null {
  if (!value) return null;
  const date = new Date(value);
  return Number.isFinite(date.getTime()) ? date : null;
}

/** The local calendar date of an instant as ISO `yyyy-MM-dd`, so the Jalali helper can format it. */
function localIsoDate(date: Date): string {
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

/** A Jalali date, e.g. `۱۴۰۵/۰۸/۱۱`. Accepts an ISO calendar date or an ISO date-time. */
export function formatDate(value: string | null | undefined, fallback: string = notRecorded): string {
  if (value && isoCalendarDate.test(value)) {
    try {
      return formatPersianDate(value);
    } catch {
      return fallback;
    }
  }
  const date = toDate(value);
  return date ? formatPersianDate(localIsoDate(date)) : fallback;
}

/** A Jalali date and the local time, e.g. `۱۴۰۵/۰۸/۱۱ ۱۱:۳۰`. */
export function formatDateTime(value: string | null | undefined, fallback: string = notRecorded): string {
  const date = toDate(value);
  if (!date) return fallback;
  return `${formatPersianDate(localIsoDate(date))} ${timeFormat.format(date)}`;
}
