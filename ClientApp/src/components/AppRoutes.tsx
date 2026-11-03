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
];
