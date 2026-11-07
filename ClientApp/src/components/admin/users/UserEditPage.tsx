import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { ApiError, getAdminUser, updateAdminUser } from '../../../api/client';
import type { AdminUserDetails } from '../../../api/types';
import { Link, useDocumentTitle, useNavigate, useParams } from '../../../lib/router';
import { ErrorState, LoadingState } from '../../Feedback';
import { Breadcrumb } from '../../ui/Breadcrumb';
import { UserForm } from './UserFormFields';
import { fullName } from './userDisplay';
import { userListBackPath } from './userListReturn';
import { clearUserFlash, setUserFlash } from './userFlash';
import { toUpdateRequest, type UserFormValues } from './userFormValidation';

type Props = { onUnauthenticated: (error: unknown) => boolean };

type State =
  | { kind: 'loading' }
  | { kind: 'ready'; user: AdminUserDetails }
  | { kind: 'error'; status: number | null; message: string };

const heading = <h1 className="text-2xl font-extrabold tracking-tight text-title">ویرایش کاربر</h1>;

/** Same back affordance as the detail page: the remembered list query, not a dead plain list. */
function BackToList() {
  return (
    <Link
      to={userListBackPath()}
      className="mt-3 inline-block rounded font-medium text-teal-deep underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-gold"
    >
      بازگشت به فهرست کاربران
    </Link>
  );
}

function initialValuesOf(user: AdminUserDetails): UserFormValues {
  return {
    firstName: user.firstName ?? '',
    lastName: user.lastName ?? '',
    username: user.username ?? '',
    password: '',
    confirmPassword: '',
    emailAddress: user.emailAddress,
    cellPhoneNumber: user.cellPhoneNumber ?? '',
    isActive: user.isActive,
  };
}

export function UserEditPage({ onUnauthenticated }: Props) {
  const { id = '' } = useParams();
  const navigate = useNavigate();
  const [state, setState] = useState<State>({ kind: 'loading' });
  const [attempt, setAttempt] = useState(0);
  // A message nobody consumed must not appear after this form is saved later.
  useEffect(() => {
    clearUserFlash();
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    setState({ kind: 'loading' });
    getAdminUser(id, controller.signal)
      .then((user) => {
        if (!controller.signal.aborted) setState({ kind: 'ready', user });
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || onUnauthenticated(cause)) return;
        setState({
          kind: 'error',
          status: cause instanceof ApiError ? cause.status : null,
          message: cause instanceof ApiError ? cause.message : 'دریافت اطلاعات کاربر انجام نشد. لطفاً دوباره تلاش کنید.',
        });
      });
    return () => controller.abort();
  }, [id, attempt, onUnauthenticated]);

  const user = state.kind === 'ready' ? state.user : null;
  const initialValues = useMemo(() => (user ? initialValuesOf(user) : null), [user]);
  const name = user ? fullName(user) : 'کاربر';
  useDocumentTitle(user ? `ویرایش ${name}` : null);
  const detailPath = `/admin/users/${encodeURIComponent(id)}`;
  const crumbs = [{ label: 'مدیریت سیستم' }, { label: 'کاربران', to: '/admin/users' }, { label: name, to: detailPath }, { label: 'ویرایش' }];

  let body: ReactNode;
  if (state.kind === 'loading') {
    body = <LoadingState label="در حال دریافت اطلاعات کاربر…" />;
  } else if (state.kind === 'error') {
    const missing = state.status === 404 || state.status === 400;
    body = (
      <div className="space-y-4">
        {missing ? (
          <div role="alert" className="rounded-xl border border-line bg-white p-6 text-center">
            <p className="text-lg font-bold text-ink">کاربر پیدا نشد</p>
            <p className="mt-2 text-sm text-sub">کاربری با این شناسه وجود ندارد یا به آن دسترسی ندارید.</p>
          </div>
        ) : state.status === 403 ? (
          <ErrorState message="شما مجوز ویرایش اطلاعات این کاربر را ندارید." />
        ) : (
          <ErrorState message={state.message} onRetry={() => setAttempt((value) => value + 1)} />
        )}
        {BackToList()}
      </div>
    );
  } else if (initialValues) {
    body = (
      <UserForm
        key={state.user.id}
        mode="edit"
        initialValues={initialValues}
        onUnauthenticated={onUnauthenticated}
        onCancel={() => navigate(detailPath)}
        onSubmit={async (values) => {
          await updateAdminUser(id, toUpdateRequest(values));
          setUserFlash(id, 'updated');
          navigate(detailPath);
        }}
      />
    );
  }

  return (
    <div className="space-y-5">
      <header className="space-y-2">
        <Breadcrumb items={crumbs} />
        {heading}
        {user ? <p className="text-sm text-sub">ویرایش اطلاعات «{name}». رمز عبور از این صفحه تغییر نمی‌کند.</p> : null}
      </header>
      {body}
    </div>
  );
}
