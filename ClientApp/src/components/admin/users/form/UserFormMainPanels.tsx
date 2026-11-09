import { useId, type ChangeEvent, type ReactNode } from 'react';
import { FormField, Input } from '@/components/ui/FormField';
import { FormSection } from '@/components/ui/FormSection';
import { PasswordInput } from '@/components/ui/PasswordInput';
import { Info, TabDataBoundary, useProfileTabData } from '../profile/profileShared';
import { faDigits, limits, type FieldName } from '../userFormValidation';
import { employeeLinkKey, loadLinkedEmployee, type FormTabCache, type UserFormContext } from './userFormShared';
import type { AdminUserDetails } from '@/api/types';
import type { SuggestionState } from './useSuggestedEmployeeNumber';

type FieldProps = (name: FieldName) => {
  value: string;
  onChange: (event: ChangeEvent<HTMLInputElement>) => void;
  onBlur: () => void;
};

type PanelBase = {
  field: FieldProps;
  errorFor: (name: FieldName) => string | undefined;
};

/** The personnel number a created account may get (create mode): optional, suggested, editable. */
function EmployeeNumberField({ field, errorFor, context, suggestion }: PanelBase & { context: UserFormContext; suggestion: SuggestionState }) {
  const { activeCompanyId, activeCompanyName, can } = context;
  const allowed = activeCompanyId !== null && can('hr.employees.create', activeCompanyId);
  const company = activeCompanyName ? `«${activeCompanyName}»` : 'شرکت فعال';
  let hint: ReactNode;
  if (activeCompanyId === null) hint = 'شرکت کاری فعالی انتخاب نشده است؛ پرونده پرسنلی همراه حساب ساخته نمی‌شود.';
  else if (!allowed) hint = `برای ثبت شماره پرسنلی، مجوز «ایجاد کارمند» در ${company} لازم است؛ این فیلد برای شما غیرفعال است.`;
  else if (suggestion.status === 'loading') hint = 'در حال دریافت شمارهٔ پیشنهادی…';
  else if (suggestion.status === 'error') hint = 'دریافت شمارهٔ پیشنهادی انجام نشد؛ شماره را دستی وارد کنید یا فیلد را خالی بگذارید.';
  else hint = `اختیاری؛ شمارهٔ پیشنهادی ${company} قابل ویرایش است. برای ثبت‌نکردن پرونده پرسنلی، فیلد را خالی بگذارید. شماره پس از ایجاد قابل تغییر نیست.`;
  return (
    <FormField label="شماره پرسنلی" error={errorFor('employeeNumber')} hint={hint}>
      <Input {...field('employeeNumber')} disabled={!allowed} dir="ltr" autoComplete="off" spellCheck={false} maxLength={30} className="text-start" />
    </FormField>
  );
}

/** The personnel number of the linked record (edit mode): read-only, changing it is not implemented. */
function LinkedEmployeeNumber({ user, companyId, cache, onUnauthenticated }: { user: AdminUserDetails; companyId: string; cache: FormTabCache; onUnauthenticated: (error: unknown) => boolean }) {
  const state = useProfileTabData(cache, employeeLinkKey(user.id, companyId), (signal) => loadLinkedEmployee(companyId, user.id, signal), onUnauthenticated);
  return (
    <TabDataBoundary state={state} label="در حال دریافت شمارهٔ پرسنلی…">
      {(employee) => (
        <Info label="شماره پرسنلی (فقط‌خواندنی)">
          {employee ? <span dir="ltr">{employee.employeeNumber}</span> : <span className="text-sub">پرونده پرسنلی متصل نشده است.</span>}
        </Info>
      )}
    </TabDataBoundary>
  );
}

export function MainInfoPanel({
  mode,
  field,
  errorFor,
  isActive,
  onActiveChange,
  context,
  suggestion,
  user,
  cache,
  onUnauthenticated,
}: PanelBase & {
  mode: 'create' | 'edit';
  isActive: boolean;
  onActiveChange: (value: boolean) => void;
  context: UserFormContext;
  suggestion: SuggestionState;
  user?: AdminUserDetails;
  cache: FormTabCache;
  onUnauthenticated: (error: unknown) => boolean;
}) {
  const statusHintId = useId();
  const companyId = context.activeCompanyId;
  const editLinkVisible =
    mode === 'edit' && user !== undefined && companyId !== null && context.can('hr.employees.view', companyId) && user.memberships.some((m) => m.isActive && m.companyId.toLowerCase() === companyId.toLowerCase());
  return (
    <div className="space-y-5">
      <FormSection headingLevel={2} title="اطلاعات هویتی">
        <FormField label="نام" required={mode === 'create'} error={errorFor('firstName')}>
          <Input {...field('firstName')} autoComplete="off" />
        </FormField>
        <FormField label="نام خانوادگی" required={mode === 'create'} error={errorFor('lastName')}>
          <Input {...field('lastName')} autoComplete="off" />
        </FormField>
      </FormSection>

      <FormSection headingLevel={2} title="اطلاعات ورود">
        <FormField label="نام کاربری" required error={errorFor('username')} hint={`${faDigits(limits.usernameMin)} تا ${faDigits(limits.usernameMax)} نویسه؛ برای ورود به سامانه استفاده می‌شود.`}>
          <Input {...field('username')} dir="ltr" autoComplete="off" autoCapitalize="none" spellCheck={false} className="text-start" />
        </FormField>
        {mode === 'create' ? (
          <>
            <FormField label="رمز عبور" required error={errorFor('password')} hint={`دست‌کم ${faDigits(limits.passwordMin)} نویسه؛ رمزهای رایج و قابل‌حدس پذیرفته نمی‌شوند.`}>
              <PasswordInput {...field('password')} fieldName="رمز عبور" togglePlacement="end" autoComplete="new-password" />
            </FormField>
            <FormField label="تکرار رمز عبور" required error={errorFor('confirmPassword')}>
              <PasswordInput {...field('confirmPassword')} fieldName="تکرار رمز عبور" togglePlacement="end" aria-label="تکرار رمز عبور" autoComplete="new-password" />
            </FormField>
          </>
        ) : null}
      </FormSection>

      <FormSection headingLevel={2} title="شماره پرسنلی و وضعیت">
        {mode === 'create' ? (
          <EmployeeNumberField field={field} errorFor={errorFor} context={context} suggestion={suggestion} />
        ) : editLinkVisible && user && companyId ? (
          <LinkedEmployeeNumber user={user} companyId={companyId} cache={cache} onUnauthenticated={onUnauthenticated} />
        ) : (
          <p className="text-xs leading-6 text-sub">شمارهٔ پرسنلی از پروندهٔ کارمند شرکت خوانده می‌شود؛ در تب «اطلاعات استخدامی» ببینید.</p>
        )}
        <div className="sm:col-span-2">
          <label className="flex cursor-pointer items-start gap-3">
            <input
              type="checkbox"
              checked={isActive}
              onChange={(event) => onActiveChange(event.target.checked)}
              aria-describedby={statusHintId}
              className="mt-0.5 size-5 shrink-0 cursor-pointer accent-teal-deep outline-none focus-visible:ring-2 focus-visible:ring-gold"
            />
            <span className="text-sm font-semibold text-ink">حساب فعال باشد</span>
          </label>
          <p id={statusHintId} className="mt-1.5 ps-8 text-xs leading-5 text-sub">
            {isActive ? 'کاربر می‌تواند وارد سامانه شود.' : 'حساب غیرفعال است و کاربر نمی‌تواند وارد سامانه شود.'}
          </p>
        </div>
      </FormSection>
    </div>
  );
}

export function ContactPanel({ field, errorFor }: PanelBase) {
  return (
    <FormSection headingLevel={2} title="اطلاعات تماس">
      <FormField label="ایمیل" required error={errorFor('emailAddress')}>
        <Input {...field('emailAddress')} type="email" dir="ltr" autoComplete="off" spellCheck={false} className="text-start" />
      </FormField>
      <FormField label="شماره موبایل" error={errorFor('cellPhoneNumber')} hint={`اختیاری؛ ${faDigits(limits.mobileLength)} رقم لاتین، مانند 09123456789.`}>
        <Input {...field('cellPhoneNumber')} type="tel" inputMode="tel" dir="ltr" autoComplete="off" className="text-start" />
      </FormField>
    </FormSection>
  );
}
