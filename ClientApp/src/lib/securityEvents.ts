/**
 * Persian labels for the audit event types the server writes today. The server stores the exact
 * code; an unknown code is shown as it is (never hidden), so a new event type needs no UI change.
 */
const labels: Readonly<Record<string, string>> = {
  CompanyRoleCreated: 'ایجاد گروه دسترسی',
  CompanyRoleUpdated: 'ویرایش گروه دسترسی',
  UserCompanyRoleAssigned: 'تغییر گروه دسترسی کاربر در شرکت',
  CompanyMembershipRequested: 'ثبت درخواست عضویت در شرکت',
  CompanyMembershipApproved: 'تأیید درخواست عضویت',
  CompanyMembershipRejected: 'رد درخواست عضویت',
  UserCreated: 'ایجاد کاربر',
  UserUpdated: 'ویرایش کاربر',
  PasswordChanged: 'تغییر رمز عبور',
  LogoutSucceeded: 'خروج موفق',
  LoginSucceeded: 'ورود موفق',
  LoginFailed: 'ورود ناموفق',
  LoginDeniedInactive: 'رد ورود (حساب غیرفعال)',
  LoginDeniedEnrollmentPending: 'رد ورود (ثبت‌نام مدیر اولیه ناتمام)',
  WorkContextSelected: 'انتخاب شرکت و دوره مالی',
  WorkContextChanged: 'تغییر شرکت یا دوره مالی',
  WorkContextSelectionDenied: 'رد انتخاب شرکت یا دوره مالی',
  CorporateGroupCreated: 'ایجاد گروه شرکتی',
  CorporateGroupMemberAdded: 'افزودن شرکت به گروه شرکتی',
  CorporateGroupMemberEnded: 'پایان عضویت شرکت در گروه شرکتی',
  CorporateGroupAccessDenied: 'رد دسترسی به گروه شرکتی',
  ProvisioningApplied: 'اعمال بستهٔ راه‌اندازی مشتری',
  ProvisioningRejected: 'رد بستهٔ راه‌اندازی مشتری',
  CustomerBoundaryProvisioned: 'راه‌اندازی مرز مشتری',
  CustomerBoundaryProvisionRejected: 'رد راه‌اندازی مرز مشتری',
  ProvisioningEnrollmentCompleted: 'تکمیل ثبت‌نام مدیر اولیه',
  ProvisioningEnrollmentFailed: 'ناموفق‌بودن ثبت‌نام مدیر اولیه',
  ProvisioningEnrollmentTokenReissued: 'صدور مجدد توکن ثبت‌نام',
};

/** The known event codes, for the filter suggestions. */
export const knownEventTypes: readonly string[] = Object.keys(labels);

/** The Persian label of an event code, or `null` when the code is not known to this client. */
export function eventTypeLabel(code: string): string | null {
  return Object.prototype.hasOwnProperty.call(labels, code) ? (labels[code] ?? null) : null;
}
