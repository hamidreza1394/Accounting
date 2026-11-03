/**
 * The client's time contract (see docs/TIME_CONTRACT.md).
 *
 * Two kinds of instants reach the client, and they MUST NOT be mixed:
 *  1. UTC instants: fields named `*Utc`, written by the server with `DateTime.UtcNow`. EF reads
 *     `datetime2` back as `Kind=Unspecified`, so the JSON often has NO `Z`/offset. They are UTC by
 *     contract, so {@link asUtcInstant} adds the `Z`. Display uses the browser's time zone.
 *  2. Server-local wall time: the legacy `InsertDateTime`, `UpdateDateTime`, `LastLoginDateTime`,
 *     written with `DateTime.Now`. They are NOT UTC; treating them as UTC would shift them by the
 *     server's offset. They are parsed as they are (see `readServerLocalDateTime` in api/types.ts).
 */

const hasOffset = /(?:Z|[+-]\d{2}:?\d{2})$/i;

/** Marks an offset-less ISO date-time as UTC; a value that already has `Z` or an offset is unchanged. */
export function asUtcInstant(value: string): string {
  return hasOffset.test(value) ? value : `${value}Z`;
}

function toInstant(date: string, time: string): string | null {
  // A calendar day typed by the user, e.g. `2026-11-03`. Years outside 0000-9999 are not days we accept.
  if (!/^\d{4}-\d{2}-\d{2}$/.test(date)) return null;
  const parsed = new Date(`${date}T${time}`);
  return Number.isFinite(parsed.getTime()) ? parsed.toISOString() : null;
}

/** Start of the given calendar day in the BROWSER's time zone, as a UTC instant (`null` if invalid). */
export function dayStartUtc(date: string): string | null {
  return toInstant(date, '00:00:00');
}

/** Last millisecond of the given calendar day in the BROWSER's time zone, as a UTC instant. */
export function dayEndUtc(date: string): string | null {
  return toInstant(date, '23:59:59.999');
}
