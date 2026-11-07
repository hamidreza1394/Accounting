import { useEffect } from 'react';
import { createAdminUser } from '../../../api/client';
import { useNavigate } from '../../../lib/router';
import { usePageBreadcrumb } from '@/components/shell/PageBreadcrumb';
import { UserForm } from './UserFormFields';
import { clearUserFlash, setUserFlash } from './userFlash';
import { emptyUserForm, toCreateRequest } from './userFormValidation';

type Props = { onUnauthenticated: (error: unknown) => boolean };

export function UserCreatePage({ onUnauthenticated }: Props) {
  const navigate = useNavigate();
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
        onCancel={() => navigate('/admin/users')}
        onSubmit={async (values) => {
          const created = await createAdminUser(toCreateRequest(values));
          setUserFlash(created.id, 'created');
          navigate(`/admin/users/${encodeURIComponent(created.id)}`);
        }}
      />
    </div>
  );
}
