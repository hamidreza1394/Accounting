// Dates are stored and transferred as calendar dates (ISO `yyyy-MM-dd`, Gregorian).
// The Persian (Jalali) calendar is a presentation concern only and is applied here,
// with the platform's Intl implementation; no date is ever stored in Jalali form.

const persianDate = new Intl.DateTimeFormat('fa-IR-u-ca-persian-nu-arabext', {
  timeZone: 'UTC',
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
});

const isoDate = /^(\d{4})-(\d{2})-(\d{2})$/;

/** Formats an ISO calendar date as a Persian-calendar date, e.g. `۱۴۰۵/۰۱/۰۱`. */
export function formatPersianDate(iso: string): string {
  const match = isoDate.exec(iso);
  if (!match) {
    throw new RangeError(`Invalid ISO date: ${iso}`);
  }
  const [, year, month, day] = match;
  // UTC midnight of the calendar date, formatted in UTC: no time-zone shift is possible.
  const date = new Date(Date.UTC(Number(year), Number(month) - 1, Number(day)));
  if (Number.isNaN(date.getTime()) || date.getUTCDate() !== Number(day)) {
    throw new RangeError(`Invalid ISO date: ${iso}`);
  }
  // Intl may emit right-to-left marks and the era-less parts with "/" separators;
  // the parts are joined explicitly so the output is stable across engines.
  const parts = persianDate.formatToParts(date);
  const part = (type: Intl.DateTimeFormatPartTypes): string =>
    parts.find((candidate) => candidate.type === type)?.value ?? '';
  return `${part('year')}/${part('month')}/${part('day')}`;
}
