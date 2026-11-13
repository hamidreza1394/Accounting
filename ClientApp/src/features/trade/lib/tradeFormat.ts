import { asUtcInstant } from '@/lib/time';
import { formatDateTime, notRecorded } from '@/lib/format';

/**
 * Formats one `*Utc` instant for display (Jalali date and time). The backend can send instants without the `Z`
 * (docs/TIME_CONTRACT.md); they are UTC by contract, so `asUtcInstant` marks them before the formatting.
 */
export function formatTradeInstant(value: string | null | undefined, fallback: string = notRecorded): string {
  if (value === null || value === undefined || value === '') return fallback;
  return formatDateTime(asUtcInstant(value), fallback);
}
