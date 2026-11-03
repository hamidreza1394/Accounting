import { afterEach, describe, expect, it, vi } from 'vitest';
import { asUtcInstant, dayEndUtc, dayStartUtc } from './time';

afterEach(() => {
  vi.unstubAllEnvs();
  vi.resetModules();
});

describe('asUtcInstant', () => {
  it('adds Z to an offset-less value, including fractional seconds', () => {
    expect(asUtcInstant('2026-11-03T10:00:00')).toBe('2026-11-03T10:00:00Z');
    expect(asUtcInstant('2026-11-03T10:00:00.1234567')).toBe('2026-11-03T10:00:00.1234567Z');
  });

  it('leaves values that already carry Z or an offset unchanged, so nothing is converted twice', () => {
    expect(asUtcInstant('2026-11-03T10:00:00Z')).toBe('2026-11-03T10:00:00Z');
    expect(asUtcInstant('2026-11-03T10:00:00z')).toBe('2026-11-03T10:00:00z');
    expect(asUtcInstant('2026-11-03T13:30:00+03:30')).toBe('2026-11-03T13:30:00+03:30');
    expect(asUtcInstant('2026-11-03T08:00:00-0200')).toBe('2026-11-03T08:00:00-0200');
    expect(asUtcInstant(asUtcInstant('2026-11-03T10:00:00'))).toBe('2026-11-03T10:00:00Z');
  });

  it('survives a JSON round trip of a real instant', () => {
    const instant = new Date(Date.UTC(2026, 10, 3, 10, 15, 30, 123));
    const wire = JSON.parse(JSON.stringify({ at: instant })).at as string; // ends with Z
    expect(Date.parse(asUtcInstant(wire))).toBe(instant.getTime());
    // The offset-less form EF/System.Text.Json produces for Kind=Unspecified reads back as the same instant.
    expect(Date.parse(asUtcInstant(wire.replace('Z', '')))).toBe(instant.getTime());
  });
});

describe('day boundaries of the user time zone', () => {
  it('UTC: the day is exactly 00:00:00.000 to 23:59:59.999Z', () => {
    vi.stubEnv('TZ', 'UTC');
    expect(dayStartUtc('2026-11-03')).toBe('2026-11-03T00:00:00.000Z');
    expect(dayEndUtc('2026-11-03')).toBe('2026-11-03T23:59:59.999Z');
  });

  it('Iran (UTC+03:30, no DST)', () => {
    vi.stubEnv('TZ', 'Asia/Tehran');
    expect(dayStartUtc('2026-11-03')).toBe('2026-11-02T20:30:00.000Z');
    expect(dayEndUtc('2026-11-03')).toBe('2026-11-03T20:29:59.999Z');
    expect(dayStartUtc('2026-07-01')).toBe('2026-06-30T20:30:00.000Z');
  });

  it('Germany: winter, summer, and both daylight-saving transition days', () => {
    vi.stubEnv('TZ', 'Europe/Berlin');
    expect(dayStartUtc('2026-01-15')).toBe('2026-01-14T23:00:00.000Z'); // CET, UTC+1
    expect(dayStartUtc('2026-07-15')).toBe('2026-07-14T22:00:00.000Z'); // CEST, UTC+2
    // 2026-03-29: clocks go forward at 02:00, a 23-hour day.
    expect(dayStartUtc('2026-03-29')).toBe('2026-03-28T23:00:00.000Z');
    expect(dayEndUtc('2026-03-29')).toBe('2026-03-29T21:59:59.999Z');
    // 2026-10-25: clocks go back at 03:00, a 25-hour day.
    expect(dayStartUtc('2026-10-25')).toBe('2026-10-24T22:00:00.000Z');
    expect(dayEndUtc('2026-10-25')).toBe('2026-10-25T22:59:59.999Z');
  });

  it('a midnight instant belongs to exactly one day', () => {
    vi.stubEnv('TZ', 'Asia/Tehran');
    const lastOfThird = Date.parse('2026-11-03T20:29:59.999Z'); // Tehran 23:59:59.999 on Nov 3
    const firstOfFourth = Date.parse('2026-11-03T20:30:00.000Z'); // Tehran 00:00:00.000 on Nov 4
    const [start3, end3] = [Date.parse(dayStartUtc('2026-11-03') ?? ''), Date.parse(dayEndUtc('2026-11-03') ?? '')];
    const [start4, end4] = [Date.parse(dayStartUtc('2026-11-04') ?? ''), Date.parse(dayEndUtc('2026-11-04') ?? '')];
    expect(lastOfThird >= start3 && lastOfThird <= end3).toBe(true);
    expect(lastOfThird >= start4 && lastOfThird <= end4).toBe(false);
    expect(firstOfFourth >= start3 && firstOfFourth <= end3).toBe(false);
    expect(firstOfFourth >= start4 && firstOfFourth <= end4).toBe(true);
    expect(end3 + 1).toBe(start4);
  });

  it('returns null instead of throwing for anything that is not a calendar day', () => {
    expect(dayStartUtc('275760-09-14')).toBeNull();
    expect(dayEndUtc('not-a-date')).toBeNull();
    expect(dayStartUtc('')).toBeNull();
    expect(dayStartUtc('2026-13-45')).toBeNull();
    expect(dayStartUtc('2026-11-03T10:00')).toBeNull();
  });
});

describe('display follows the viewer time zone', () => {
  async function show(zone: string, value: string) {
    vi.stubEnv('TZ', zone);
    vi.resetModules();
    const { formatDateTime } = await import('./format');
    return formatDateTime(value);
  }

  it('shows the same UTC instant as different local times, and the next local day across midnight', async () => {
    const instant = '2026-11-03T20:30:00Z';
    const tehran = await show('Asia/Tehran', instant);
    const berlin = await show('Europe/Berlin', instant);
    const utc = await show('UTC', instant);
    expect(tehran.endsWith('۰۰:۰۰')).toBe(true); // 00:00 on the next day
    expect(berlin.endsWith('۲۱:۳۰')).toBe(true); // 21:30
    expect(utc.endsWith('۲۰:۳۰')).toBe(true);
    // Tehran is already on the next calendar day.
    expect(tehran.split(' ')[0]).not.toBe(berlin.split(' ')[0]);
    expect(berlin.split(' ')[0]).toBe(utc.split(' ')[0]);
  });
});
