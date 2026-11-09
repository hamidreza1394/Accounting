import { useEffect } from 'react';
import { usePageBreadcrumb } from '@/components/shell/PageBreadcrumb';
import { UserForm } from './UserFormFields';
import { clearUserFlash, setUserListFlash } from './userFlash';
import { useLeaveToList } from './useLeaveToList';
import { createUserWithEmployee } from './userCreateFlow';
import { noUserFormContext, type UserFormContext } from './form/userFormShared';
import { emptyUserForm } from './userFormValidation';

type Props = {
  onUnauthenticated: (error: unknown) => boolean;
  /** Work-context company and permission hints (UI only); the default grants nothing. */
  workContext?: UserFormContext;
};

export function UserCreatePage({ onUnauthenticated, workContext = noUserFormContext }: Props) {
  const leaveToList = useLeaveToList();
  // A message nobody consumed must not appear after this form is saved later.
  useEffect(() => {
    clearUserFlash();
  }, []);
  usePageBreadcrumb([{ label: 'مدیریت سیستم' }, { label: 'کاربران', to: '/admin/users' }, { label: 'ایجاد کاربر' }]);
  return (
    <div className="space-y-5">
      <header className="space-y-2">
        <h1 className="text-2xl font-extrabold tracking-tight text-title">ایجاد کاربر</h1>
        <p className="text-sm text-sub">یک حساب کاربری جدید برای این نصب بسازید. عضویت در شرکت‌ها پس از ایجاد حساب تعیین می‌شود.</p>
      </header>
      <UserForm
        mode="create"
        initialValues={emptyUserForm}
        onUnauthenticated={onUnauthenticated}
        workContext={workContext}
        onCancel={leaveToList}
        onSubmit={async (values, meta) => {
          // The account is created first; a failed personnel record never hides it (see the flow).
          const result = await createUserWithEmployee({
            values,
            companyId: workContext.activeCompanyId,
            suggestedEmployeeNumber: meta.suggestedEmployeeNumber,
            photo: meta.photo,
            onUnauthenticated,
          });
          setUserListFlash(result.flash);
          if (result.sessionExpired) return;
          // The list shows the new account first: plain first page, refetched, form tab closed.
          leaveToList();
        }}
      />
    </div>
  );
}
