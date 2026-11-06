import { asUtcInstant } from '@/lib/time';
import { formatDateTime, notRecorded } from '@/lib/format';

/**
 * Formats one HR `*Utc` instant for display. The backend often sends instants without the `Z`
 * (EF datetime2, docs/TIME_CONTRACT.md); they are UTC by contract, so `asUtcInstant` marks them
 * before the browser-time-zone formatting of `formatDateTime`.
 */
export function formatHrInstant(value: string | null | undefined, fallback: string = notRecorded): string {
  if (value === null || value === undefined || value === '') return fallback;
  return formatDateTime(asUtcInstant(value), fallback);
}
