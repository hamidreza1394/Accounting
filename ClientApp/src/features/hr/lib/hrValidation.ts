// Client-side validation of the HR forms. Every rule mirrors a real server check of
// `HrOrganizationalStructureEndpoints.cs` — the client only prevents a guaranteed 400 early;
// the server stays authoritative and its `{ error }` messages are shown as-is.
// Dates: the forms collect Jalali calendar days (ISO `yyyy-MM-dd`) through `JalaliDateField`
// and turn them into UTC instants with `dayStartUtc` (docs/TIME_CONTRACT.md).

import { dayStartUtc } from '@/lib/time';
import { hrCodePattern, hrEmployeeNumberPattern, hrLimits } from './hrLabels';

export type FieldErrors = Partial<Record<string, string>>;

/** Validates the employee number; `required` is true on create (immutable afterwards). */
export function validateEmployeeNumber(value: string, required: boolean): string | null {
  const text = value.trim();
  if (text === '') return required ? 'شماره پرسنلی الزامی است.' : null;
  if (text.length > hrLimits.employeeNumber || !hrEmployeeNumberPattern.test(text)) {
    return 'شماره پرسنلی باید با حرف یا رقم شروع شود و تنها شامل حرف، رقم، نقطه، خط تیره یا زیرخط باشد (حداکثر ۳۰ نویسه).';
  }
  return null;
}

export function validateRequiredName(value: string, label: string, max: number): string | null {
  const text = value.trim();
  if (text === '') return `${label} الزامی است.`;
  if (text.length > max) return `${label} حداکثر می‌تواند ${hrLimits.employeeName === max ? '۱۰۰' : '۲۰۰'} نویسه باشد.`;
  return null;
}

export function validateDescription(value: string): string | null {
  return value.length > hrLimits.description ? 'توضیحات حداکثر می‌تواند ۱۰۰۰ نویسه باشد.' : null;
}

export function validateCode(value: string, label: string): string | null {
  const text = value.trim();
  if (text === '') return `${label} الزامی است.`;
  if (text.length > hrLimits.code || !hrCodePattern.test(text)) {
    return `${label} باید با حرف یا رقم شروع شود و تنها شامل حرف، رقم، نقطه، خط تیره یا زیرخط باشد (حداکثر ۵۰ نویسه).`;
  }
  return null;
}

/** Unit/position validity window: `validToUtc < validFromUtc` is a guaranteed server 400. */
export function validateValidityWindow(fromIso: string, toIso: string): string | null {
  if (fromIso === '' || toIso === '') return null;
  return toIso < fromIso ? 'پایان بازه اعتبار نمی‌تواند قبل از شروع آن باشد.' : null;
}

/** Rank is an optional plain integer on the server; the client only checks the numeric shape. */
export function validateRank(value: string): string | null {
  if (value.trim() === '') return null;
  return /^-?\d{1,9}$/.test(value.trim()) ? null : 'رتبه باید یک عدد صحیح باشد.';
}

/**
 * Turns a picked Jalali calendar day into the UTC instant the API expects (`yyyy-MM-dd` →
 * start of that day in the browser's time zone). Returns `null` for an empty or invalid day.
 */
export function calendarDayToUtcInstant(isoDay: string): string | null {
  if (isoDay.trim() === '') return null;
  return dayStartUtc(isoDay);
}

/**
 * Form values of the create-employee flow, already mapped 1:1 onto `HrCreateEmployeeRequest`
 * (field-by-field, no spread, so nothing beyond the DTO can leak into the request).
 */
export function buildCreateEmployeeBody(values: {
  employeeNumber: string;
  firstName: string;
  lastName: string;
  hiredOnIsoDay: string;
}): { employeeNumber: string; firstName: string; lastName: string; hiredOnUtc: string | null } {
  return {
    employeeNumber: values.employeeNumber.trim(),
    firstName: values.firstName.trim(),
    lastName: values.lastName.trim(),
    hiredOnUtc: calendarDayToUtcInstant(values.hiredOnIsoDay),
  };
}

/** Maps the edit-employee form onto `HrUpdateEmployeeRequest` (`hiredOnUtc: null` = keep). */
export function buildUpdateEmployeeBody(values: {
  firstName: string;
  lastName: string;
  employmentStatus: 1 | 2;
  hiredOnIsoDay: string;
  rowVersion: string;
}): { firstName: string; lastName: string; employmentStatus: 1 | 2; hiredOnUtc: string | null; rowVersion: string } {
  return {
    firstName: values.firstName.trim(),
    lastName: values.lastName.trim(),
    employmentStatus: values.employmentStatus,
    hiredOnUtc: calendarDayToUtcInstant(values.hiredOnIsoDay),
    rowVersion: values.rowVersion,
  };
}

/** Maps the create-unit form onto `HrCreateUnitRequest`. */
export function buildCreateUnitBody(values: {
  code: string;
  name: string;
  description: string;
  parentUnitId: string | null;
  validFromIsoDay: string;
  validToIsoDay: string;
}): {
  code: string;
  name: string;
  description: string | null;
  parentUnitId: string | null;
  validFromUtc: string | null;
  validToUtc: string | null;
} {
  return {
    code: values.code.trim(),
    name: values.name.trim(),
    description: values.description.trim() === '' ? null : values.description.trim(),
    parentUnitId: values.parentUnitId,
    validFromUtc: calendarDayToUtcInstant(values.validFromIsoDay),
    validToUtc: calendarDayToUtcInstant(values.validToIsoDay),
  };
}

/**
 * Maps the edit-unit form onto `HrUpdateUnitRequest` with its documented patch semantics:
 * `parentUnitId: null` = root, `validFromUtc: null` = keep (the field was not changed),
 * `validToUtc: null` = clear.
 */
export function buildUpdateUnitBody(values: {
  name: string;
  description: string;
  parentUnitId: string | null;
  isActive: boolean;
  validFromIsoDay: string;
  validFromTouched: boolean;
  validToIsoDay: string;
  rowVersion: string;
}): {
  name: string;
  description: string | null;
  parentUnitId: string | null;
  isActive: boolean;
  validFromUtc: string | null;
  validToUtc: string | null;
  rowVersion: string;
} {
  return {
    name: values.name.trim(),
    description: values.description.trim() === '' ? null : values.description.trim(),
    parentUnitId: values.parentUnitId,
    isActive: values.isActive,
    validFromUtc: values.validFromTouched ? calendarDayToUtcInstant(values.validFromIsoDay) : null,
    validToUtc: calendarDayToUtcInstant(values.validToIsoDay),
    rowVersion: values.rowVersion,
  };
}

/** Maps the create-position form onto `HrCreatePositionRequest`. */
export function buildCreatePositionBody(values: {
  code: string;
  name: string;
  description: string;
  rank: string;
}): { code: string; name: string; description: string | null; rank: number | null } {
  const rank = values.rank.trim();
  return {
    code: values.code.trim(),
    name: values.name.trim(),
    description: values.description.trim() === '' ? null : values.description.trim(),
    rank: rank === '' ? null : Number(rank),
  };
}

/** Maps the edit-position form onto `HrUpdatePositionRequest` (`rank: null` clears the rank). */
export function buildUpdatePositionBody(values: {
  name: string;
  description: string;
  rank: string;
  isActive: boolean;
  rowVersion: string;
}): { name: string; description: string | null; rank: number | null; isActive: boolean; rowVersion: string } {
  const rank = values.rank.trim();
  return {
    name: values.name.trim(),
    description: values.description.trim() === '' ? null : values.description.trim(),
    rank: rank === '' ? null : Number(rank),
    isActive: values.isActive,
    rowVersion: values.rowVersion,
  };
}

/**
 * The start-assignment date rule of the backend: the start must be "now or in the past"
 * (future-dated starts are rejected) and not before the employee was hired. Both checks need
 * the picked day as an instant, so they run here against real UTC values.
 */
export function validateAssignmentStart(startIsoDay: string, hiredOnUtc: string | null): string | null {
  const instant = calendarDayToUtcInstant(startIsoDay);
  if (instant === null) return 'تاریخ شروع انتساب الزامی است.';
  if (new Date(instant).getTime() > Date.now()) {
    return 'انتساب آینده‌نگر پشتیبانی نمی‌شود؛ تاریخ شروع باید امروز یا گذشته باشد.';
  }
  if (hiredOnUtc !== null && new Date(instant).getTime() < new Date(hiredOnUtc).getTime()) {
    return 'شروع انتساب نمی‌تواند قبل از تاریخ شروع همکاری باشد.';
  }
  return null;
}

/** End-assignment rule: an explicit end day must also not lie in the future. */
export function validateAssignmentEnd(endIsoDay: string): string | null {
  const instant = calendarDayToUtcInstant(endIsoDay);
  if (instant === null) return null; // empty = "end now", always valid
  if (new Date(instant).getTime() > Date.now()) {
    return 'پایان انتساب باید امروز یا در گذشته باشد.';
  }
  return null;
}
