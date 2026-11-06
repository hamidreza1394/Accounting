/**
 * Persian labels for the audit event codes, used when the server's event-type list is not
 * (yet) available. The wording mirrors the server registry in
 * `SecurityAuditAdministrationEndpoints` exactly, plus the codes the backend writes but the
 * registry does not label. The server stores the exact code; an unknown code is shown as it is
 * (never hidden), so a new event type needs no UI change.
 */
const labels: Readonly<Record<string, string>> = {
  LoginSucceeded: 'ورود موفق',
  LoginFailed: 'ورود ناموفق',
  LoginDeniedInactive: 'رد ورود حساب غیرفعال',
  LoginDeniedEnrollmentPending: 'رد ورود تا تکمیل ثبت‌نام',
  LogoutSucceeded: 'خروج موفق',
  UserSessionRevoked: 'ابطال نشست کاربر',
  UserSessionsRevokedOthers: 'ابطال سایر نشست‌های کاربر',
  AdminUserSessionRevoked: 'ابطال نشست کاربر توسط مدیر نصب',
  AdminUserSessionsRevoked: 'ابطال نشست‌های کاربر توسط مدیر نصب',
  PasswordChanged: 'تغییر رمز عبور',
  UserCreated: 'ایجاد کاربر',
  UserUpdated: 'ویرایش کاربر',
  CompanyRoleCreated: 'ایجاد نقش شرکت',
  CompanyRoleUpdated: 'ویرایش نقش شرکت',
  UserCompanyRoleAssigned: 'تخصیص نقش به کاربر',
  WorkContextSelected: 'انتخاب شرکت و دوره مالی',
  WorkContextChanged: 'تغییر شرکت یا دوره مالی',
  WorkContextSelectionDenied: 'رد انتخاب زمینه کاری',
  CorporateGroupCreated: 'ایجاد گروه شرکتی',
  CorporateGroupMemberAdded: 'افزودن شرکت به گروه شرکتی',
  CorporateGroupMemberEnded: 'پایان عضویت شرکت در گروه شرکتی',
  CorporateGroupAccessDenied: 'رد دسترسی گروه شرکتی',
  CustomerBoundaryProvisioned: 'ثبت مرز مشتری',
  CustomerBoundaryProvisionRejected: 'رد ثبت مرز مشتری',
  ProvisioningApplied: 'اعمال بسته تأمین',
  ProvisioningRejected: 'رد بسته تأمین',
  ProvisioningEnrollmentCompleted: 'تکمیل ثبت‌نام',
  ProvisioningEnrollmentFailed: 'شکست ثبت‌نام',
  ProvisioningEnrollmentTokenReissued: 'صدور مجدد کد ثبت‌نام',
  CompanyMembershipRequested: 'درخواست عضویت شرکت',
  CompanyMembershipApproved: 'تأیید عضویت شرکت',
  CompanyMembershipRejected: 'رد درخواست عضویت شرکت',
  SecurityAuditExported: 'خروجی گزارش امنیتی',
  LicenseImported: 'ورود مجوز',
  InstallationIdentityEnrolled: 'ثبت هویت نصب',
};

/** The known event codes, for the filter suggestions when the server list is unavailable. */
export const knownEventTypes: readonly string[] = Object.keys(labels);

/** The Persian label of an event code, or `null` when the code is not known to this client. */
export function eventTypeLabel(code: string): string | null {
  return Object.prototype.hasOwnProperty.call(labels, code) ? (labels[code] ?? null) : null;
}

/** The export limits the server enforces: a mandatory range of at most 31 days. */
const maxExportRangeMs = 31 * 24 * 60 * 60 * 1000;

/**
 * The early validation of the CSV export range, before anything is sent: the server answers 400
 * for the same cases, so the message only saves a round trip. `null` means the range is exportable.
 */
export function exportRangeProblem(fromUtc: string | null, toUtc: string | null): string | null {
  if (!fromUtc || !toUtc) return 'برای خروجی CSV ابتدا بازهٔ زمانی (از تاریخ و تا تاریخ) را اعمال کنید.';
  if (Date.parse(toUtc) - Date.parse(fromUtc) > maxExportRangeMs) return 'بازهٔ زمانی خروجی حداکثر ۳۱ روز است؛ بازه را کوچک‌تر کنید.';
  return null;
}
