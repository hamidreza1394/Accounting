import { createContext, lazy, useContext } from 'react';
import type { SelectedContext, Session } from '../api/types';
import { can } from '../lib/permissions';
import type { Route } from '../lib/router';
import { Dashboard } from './Dashboard';

/** What the route pages need from the signed-in shell; supplied once by `App`. */
export type AppRouteData = {
  session: Session;
  context: SelectedContext;
  onUnauthenticated: (error: unknown) => boolean;
};

export const AppRouteDataContext = createContext<AppRouteData | null>(null);

function useAppRouteData(): AppRouteData {
  const data = useContext(AppRouteDataContext);
  if (!data) throw new Error('AppRouteDataContext is missing above the routes.');
  return data;
}

const LazyUsersListPage = lazy(() =>
  import('./admin/users/UsersListPage').then((module) => ({ default: module.UsersListPage })),
);
const LazyUserDetailPage = lazy(() =>
  import('./admin/users/UserDetailPage').then((module) => ({ default: module.UserDetailPage })),
);

const LazyUserCreatePage = lazy(() =>
  import('./admin/users/UserCreatePage').then((module) => ({ default: module.UserCreatePage })),
);
const LazyUserEditPage = lazy(() =>
  import('./admin/users/UserEditPage').then((module) => ({ default: module.UserEditPage })),
);

const LazyPermissionCatalogPage = lazy(() =>
  import('./admin/roles/PermissionCatalogPage').then((module) => ({ default: module.PermissionCatalogPage })),
);
const LazyRolesListPage = lazy(() => import('./admin/roles/RolesListPage').then((module) => ({ default: module.RolesListPage })));
const LazyRoleCreatePage = lazy(() => import('./admin/roles/RoleCreatePage').then((module) => ({ default: module.RoleCreatePage })));
const LazyRoleDetailPage = lazy(() => import('./admin/roles/RoleDetailPage').then((module) => ({ default: module.RoleDetailPage })));

const LazyMyMembershipRequestsPage = lazy(() =>
  import('./membership/MyMembershipRequestsPage').then((module) => ({ default: module.MyMembershipRequestsPage })),
);
const LazyPendingMembershipRequestsPage = lazy(() =>
  import('./membership/PendingMembershipRequestsPage').then((module) => ({ default: module.PendingMembershipRequestsPage })),
);

function DashboardRoute() {
  const { session, context, onUnauthenticated } = useAppRouteData();
  return (
    <Dashboard
      key={`${context.company.id}:${context.fiscalPeriod.id}`}
      session={session}
      context={context}
      onUnauthenticated={onUnauthenticated}
    />
  );
}

function UsersListRoute() {
  const { session, onUnauthenticated } = useAppRouteData();
  return <LazyUsersListPage onUnauthenticated={onUnauthenticated} canCreate={can(session, 'admin.users.create')} />;
}

function UserCreateRoute() {
  return <LazyUserCreatePage onUnauthenticated={useAppRouteData().onUnauthenticated} />;
}

function UserDetailRoute() {
  const { session, onUnauthenticated } = useAppRouteData();
  return <LazyUserDetailPage onUnauthenticated={onUnauthenticated} canUpdate={can(session, 'admin.users.update')} />;
}

function UserEditRoute() {
  return <LazyUserEditPage onUnauthenticated={useAppRouteData().onUnauthenticated} />;
}

function PermissionCatalogRoute() {
  return <LazyPermissionCatalogPage onUnauthenticated={useAppRouteData().onUnauthenticated} />;
}

function RolesListRoute() {
  const { session, context, onUnauthenticated } = useAppRouteData();
  return <LazyRolesListPage onUnauthenticated={onUnauthenticated} defaultCompanyId={context.company.id} canManage={can(session, 'admin.roles.manage')} />;
}

function RoleCreateRoute() {
  const { context, onUnauthenticated } = useAppRouteData();
  return <LazyRoleCreatePage onUnauthenticated={onUnauthenticated} defaultCompanyId={context.company.id} />;
}

function RoleDetailRoute() {
  const { session, context, onUnauthenticated } = useAppRouteData();
  return <LazyRoleDetailPage onUnauthenticated={onUnauthenticated} defaultCompanyId={context.company.id} canManage={can(session, 'admin.roles.manage')} />;
}

function MyMembershipRequestsRoute() {
  return <LazyMyMembershipRequestsPage onUnauthenticated={useAppRouteData().onUnauthenticated} />;
}

function PendingMembershipRequestsRoute() {
  const { context, onUnauthenticated } = useAppRouteData();
  return (
    <LazyPendingMembershipRequestsPage
      key={context.company.id}
      onUnauthenticated={onUnauthenticated}
      companyId={context.company.id}
      companyName={context.company.name}
    />
  );
}

/**
 * Routes under `/app`. Admin pages are lazy chunks so the dashboard bundle stays small. The
 * `permission` is a UI hint only; the server decides (D-064). List specific paths first.
 */
export const appRoutes: readonly Route[] = [
  { path: '/', element: DashboardRoute, title: 'داشبورد' },
  { path: '/admin/users', element: UsersListRoute, title: 'مدیریت کاربران', permission: 'admin.users.view' },
  // `/new` must stay before `/:id`, otherwise "new" would be read as a user id.
  { path: '/admin/users/new', element: UserCreateRoute, title: 'ایجاد کاربر', permission: 'admin.users.create' },
  { path: '/admin/users/:id', element: UserDetailRoute, title: 'جزئیات کاربر', permission: 'admin.users.view' },
  { path: '/admin/users/:id/edit', element: UserEditRoute, title: 'ویرایش کاربر', permission: 'admin.users.update' },
  { path: '/admin/permissions', element: PermissionCatalogRoute, title: 'کاتالوگ مجوزها', permission: 'admin.permissions.view' },
  { path: '/admin/roles', element: RolesListRoute, title: 'گروه‌های دسترسی', permission: 'admin.roles.view' },
  // `/new` must stay before `/:roleId`, otherwise "new" would be read as a role id.
  { path: '/admin/roles/new', element: RoleCreateRoute, title: 'ایجاد گروه دسترسی', permission: 'admin.roles.manage' },
  { path: '/admin/roles/:roleId', element: RoleDetailRoute, title: 'جزئیات گروه دسترسی', permission: 'admin.roles.view' },
  // No UI permission hint: the approver permission is per company and not in the session; the server decides (404).
  { path: '/admin/membership-requests', element: PendingMembershipRequestsRoute, title: 'درخواست‌های عضویت' },
  { path: '/account/membership-requests', element: MyMembershipRequestsRoute, title: 'درخواست‌های عضویت من', permission: 'account.view' },
];
