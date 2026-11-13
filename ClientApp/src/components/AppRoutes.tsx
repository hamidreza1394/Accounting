import { createContext, lazy, useCallback, useContext, useMemo } from 'react';
import type { SelectedContext, Session } from '../api/types';
import { hrRoutes } from '../features/hr/hrRoutes';
import { tradeRoutes } from '../features/trade/tradeRoutes';
import { can, isInstallationAdministrator, type AccessSnapshot, type CanHint, type Permission } from '../lib/permissions';
import type { Route } from '../lib/router';
import type { UserFormContext } from './admin/users/form/userFormShared';
import { Dashboard } from './Dashboard';

/** What the route pages need from the signed-in shell; supplied once by `App`. */
export type AppRouteData = {
  session: Session;
  context: SelectedContext;
  /** What `/me/permissions` said for the active company; a UI hint only, fail-closed (D-071). */
  access: AccessSnapshot;
  /** Reloads the hints, e.g. after the server answered 403 to something the hints allowed. */
  refreshAccess: () => void;
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
const LazyAccessPermissionsPage = lazy(() =>
  import('./admin/access/AccessPermissionsPage').then((module) => ({ default: module.AccessPermissionsPage })),
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

const LazySecurityAuditPage = lazy(() =>
  import('./admin/security/SecurityAuditPage').then((module) => ({ default: module.SecurityAuditPage })),
);

const LazyCompanyUsersPage = lazy(() =>
  import('./company/CompanyUsersPage').then((module) => ({ default: module.CompanyUsersPage })),
);

function useCanHint(): CanHint {
  const { access } = useAppRouteData();
  return useCallback((permission, companyId) => can(access, permission, companyId), [access]);
}

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
  const { access, onUnauthenticated } = useAppRouteData();
  return <LazyUsersListPage onUnauthenticated={onUnauthenticated} canCreate={can(access, 'admin.users.create')} />;
}

/** The work-context company and the permission hints the user form needs (UI hints only; the server decides). */
function useUserFormContext(): UserFormContext {
  const { context } = useAppRouteData();
  const canHint = useCanHint();
  return useMemo(
    () => ({ activeCompanyId: context.company.id, activeCompanyName: context.company.name, can: canHint }),
    [context.company.id, context.company.name, canHint],
  );
}

function UserCreateRoute() {
  return <LazyUserCreatePage onUnauthenticated={useAppRouteData().onUnauthenticated} workContext={useUserFormContext()} />;
}

function UserDetailRoute() {
  const { access, onUnauthenticated } = useAppRouteData();
  return <LazyUserDetailPage onUnauthenticated={onUnauthenticated} canUpdate={can(access, 'admin.users.update')} />;
}

function UserEditRoute() {
  return <LazyUserEditPage onUnauthenticated={useAppRouteData().onUnauthenticated} workContext={useUserFormContext()} />;
}

function PermissionCatalogRoute() {
  return <LazyPermissionCatalogPage onUnauthenticated={useAppRouteData().onUnauthenticated} />;
}

function AccessPermissionsRoute() {
  const { access, context, onUnauthenticated, refreshAccess } = useAppRouteData();
  const hint = useCanHint();
  return (
    <LazyAccessPermissionsPage
      key={context.company.id}
      onUnauthenticated={onUnauthenticated}
      defaultCompanyId={context.company.id}
      can={hint}
      isInstallationAdministrator={isInstallationAdministrator(access)}
      onForbidden={refreshAccess}
    />
  );
}

function RolesListRoute() {
  const { context, onUnauthenticated, refreshAccess } = useAppRouteData();
  const hint = useCanHint();
  return <LazyRolesListPage key={context.company.id} onUnauthenticated={onUnauthenticated} defaultCompanyId={context.company.id} can={hint} onForbidden={refreshAccess} />;
}

function RoleCreateRoute() {
  const { access, context, onUnauthenticated, refreshAccess } = useAppRouteData();
  return (
    <LazyRoleCreatePage
      key={context.company.id}
      onUnauthenticated={onUnauthenticated}
      defaultCompanyId={context.company.id}
      isInstallationAdministrator={isInstallationAdministrator(access)}
      onForbidden={refreshAccess}
    />
  );
}

function RoleDetailRoute() {
  const { access, context, onUnauthenticated, refreshAccess } = useAppRouteData();
  const hint = useCanHint();
  return (
    <LazyRoleDetailPage
      key={context.company.id}
      onUnauthenticated={onUnauthenticated}
      defaultCompanyId={context.company.id}
      can={hint}
      isInstallationAdministrator={isInstallationAdministrator(access)}
      onForbidden={refreshAccess}
    />
  );
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

function SecurityAuditRoute() {
  return <LazySecurityAuditPage onUnauthenticated={useAppRouteData().onUnauthenticated} />;
}

function CompanyUsersRoute() {
  const { access, context, onUnauthenticated } = useAppRouteData();
  return (
    <LazyCompanyUsersPage
      key={context.company.id}
      onUnauthenticated={onUnauthenticated}
      companyId={context.company.id}
      companyName={context.company.name}
      canViewRoles={can(access, 'admin.roles.view', context.company.id)}
    />
  );
}

/**
 * Routes under `/app`. Admin pages are lazy chunks so the dashboard bundle stays small. The
 * `permission` is a UI hint only; the server decides (D-064). List specific paths first.
 */
const baseRoutes: readonly Route[] = [
  { path: '/', element: DashboardRoute, title: 'داشبورد' },
  { path: '/admin/users', element: UsersListRoute, title: 'مدیریت کاربران', permission: 'admin.users.view' },
  // `/new` must stay before `/:id`, otherwise "new" would be read as a user id.
  { path: '/admin/users/new', element: UserCreateRoute, title: 'ایجاد کاربر', permission: 'admin.users.create' },
  { path: '/admin/users/:id', element: UserDetailRoute, title: 'جزئیات کاربر', permission: 'admin.users.view' },
  { path: '/admin/users/:id/edit', element: UserEditRoute, title: 'ویرایش کاربر', permission: 'admin.users.update' },
  // The standalone access page (subsystem and form permissions of a role); `admin.roles.view` is a hint, the server decides.
  { path: '/admin/access', element: AccessPermissionsRoute, title: 'مجوزهای دسترسی', permission: 'admin.roles.view' },
  { path: '/admin/permissions', element: PermissionCatalogRoute, title: 'کاتالوگ مجوزها', permission: 'admin.permissions.view' },
  { path: '/admin/roles', element: RolesListRoute, title: 'گروه‌های دسترسی', permission: 'admin.roles.view' },
  // `/new` must stay before `/:roleId`, otherwise "new" would be read as a role id.
  { path: '/admin/roles/new', element: RoleCreateRoute, title: 'ایجاد گروه دسترسی', permission: 'admin.roles.create' },
  { path: '/admin/roles/:roleId', element: RoleDetailRoute, title: 'جزئیات گروه دسترسی', permission: 'admin.roles.view' },
  // No UI permission hint: the company permission users.list is not in the session; the server decides (403/404).
  { path: '/company/users', element: CompanyUsersRoute, title: 'کاربران شرکت' },
  { path: '/admin/security/audit', element: SecurityAuditRoute, title: 'گزارش امنیت و سوابق', permission: 'admin.security.view' },
  // No UI permission hint: the approver permission is per company and not in the session; the server decides (404).
  { path: '/admin/membership-requests', element: PendingMembershipRequestsRoute, title: 'درخواست‌های عضویت' },
  { path: '/account/membership-requests', element: MyMembershipRequestsRoute, title: 'درخواست‌های عضویت من', permission: 'account.view' },
];

/**
 * The finished HR routes (`features/hr/hrRoutes.tsx`, read-only) get the list permission of their family as UI hint, so the
 * router never shows an HR page while the company permissions are loading, failed or lack the code (fail-closed). The
 * server still authorizes every request (D-064), so the shared hint is deliberately the family's list code.
 */
function hrPermission(path: string): Permission {
  if (path.startsWith('/hr/org/units')) return 'hr.units.view';
  if (path.startsWith('/hr/org/positions')) return 'hr.positions.view';
  return 'hr.employees.view';
}

/**
 * Specific paths first inside each group; HR and Trade paths do not overlap the shared ones. The Trade routes carry no
 * `permission` here: their hints come from `/trade/capabilities` and are enforced by the route guard of the feature.
 */
export const appRoutes: readonly Route[] = [
  ...baseRoutes,
  ...hrRoutes.map((route): Route => ({ ...route, permission: hrPermission(route.path) })),
  ...tradeRoutes,
];
