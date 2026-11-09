import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { ApiError, getAdminUser, updateAdminUser } from '../../../api/client';
import type { AdminUserDetails } from '../../../api/types';
import { Link, useDocumentTitle, useNavigate, useParams } from '../../../lib/router';
import { ErrorState, LoadingState } from '../../Feedback';
import { usePageBreadcrumb } from '@/components/shell/PageBreadcrumb';
import { UserForm } from './UserFormFields';
import { fullName } from './userDisplay';
import { userListBackPath } from './userListReturn';
import { clearUserFlash, setUserFlash } from './userFlash';
import { toUpdateRequest, type UserFormValues } from './userFormValidation';
import { noUserFormContext, type UserFormContext } from './form/userFormShared';

type Props = {
  onUnauthenticated: (error: unknown) => boolean;
  /** Work-context company and permission hints (UI only); the default grants nothing. */
  workContext?: UserFormContext;
};

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
    employeeNumber: '',
  };
}

export function UserEditPage({ onUnauthenticated, workContext = noUserFormContext }: Props) {
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

  /** Reloads the user after a role assignment; the form keeps what was typed and the page never flashes a loader. */
  const refreshUser = () => {
    getAdminUser(id)
      .then((fresh) => setState((current) => (current.kind === 'ready' ? { kind: 'ready', user: fresh } : current)))
      .catch((cause: unknown) => {
        onUnauthenticated(cause);
      });
  };

  const user = state.kind === 'ready' ? state.user : null;
  const initialValues = useMemo(() => (user ? initialValuesOf(user) : null), [user]);
  const name = user ? fullName(user) : 'کاربر';
  useDocumentTitle(user ? `ویرایش ${name}` : null);
  const detailPath = `/admin/users/${encodeURIComponent(id)}`;
  const crumbs = [{ label: 'مدیریت سیستم' }, { label: 'کاربران', to: '/admin/users' }, { label: name, to: detailPath }, { label: 'ویرایش' }];

  usePageBreadcrumb(crumbs);
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
        user={state.user}
        workContext={workContext}
        onMembershipsChanged={refreshUser}
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
        {heading}
        {user ? <p className="text-sm text-sub">ویرایش اطلاعات «{name}». رمز عبور از این صفحه تغییر نمی‌کند.</p> : null}
      </header>
      {body}
    </div>
  );
}
