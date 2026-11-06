// Persian display labels and validation rules of the HR feature. The wording follows the
// backend's own Persian error messages (code is the source of truth) and D-072 terminology.
// Status is always icon + text (never colour alone) — see `ui/Badge`.

import type { HrAssignmentStatus, HrEmploymentStatus } from '../api/hrTypes';

export const hrEmploymentStatusLabel: Record<HrEmploymentStatus, string> = {
  1: 'فعال',
  2: 'معلق',
  3: 'خاتمه‌همکاری',
};

export const hrAssignmentStatusLabel: Record<HrAssignmentStatus, string> = {
  1: 'فعال',
  2: 'پایان‌یافته',
};

/** Persian labels for the audit event types the employee history may return (allowlist only). */
export const hrEventTypeLabel: Record<string, string> = {
  HrEmployeeCreated: 'ایجاد کارمند',
  HrEmployeeUpdated: 'ویرایش کارمند',
  HrEmployeeTerminated: 'خاتمه همکاری',
  HrEmployeeUserLinked: 'اتصال حساب کاربری',
  HrEmployeeUserUnlinked: 'جداسازی حساب کاربری',
  HrAssignmentStarted: 'شروع انتساب',
  HrAssignmentEnded: 'پایان انتساب',
};

export function hrEventTypeTitle(eventType: string): string {
  return hrEventTypeLabel[eventType] ?? eventType;
}

/** Server pattern: `^[A-Za-z0-9][A-Za-z0-9._-]{0,29}$` (Constants of the HR endpoints). */
export const hrEmployeeNumberPattern = /^[A-Za-z0-9][A-Za-z0-9._-]{0,29}$/;

/** Server pattern for unit/position codes: `^[A-Za-z0-9][A-Za-z0-9._-]{0,49}$`. */
export const hrCodePattern = /^[A-Za-z0-9][A-Za-z0-9._-]{0,49}$/;

/** The exact server limits, so the client never lets the user type into a guaranteed 400. */
export const hrLimits = {
  employeeName: 100,
  unitPositionName: 200,
  description: 1000,
  search: 120,
  employeeNumber: 30,
  code: 50,
} as const;

export function hrFullName(employee: { firstName: string; lastName: string }): string {
  return `${employee.firstName} ${employee.lastName}`.trim();
}
