import { createContext, lazy, useContext } from 'react';
import type { SelectedContext, Session } from '../api/types';
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
  return <LazyUsersListPage onUnauthenticated={useAppRouteData().onUnauthenticated} />;
}

function UserDetailRoute() {
  return <LazyUserDetailPage onUnauthenticated={useAppRouteData().onUnauthenticated} />;
}

/**
 * Routes under `/app`. Admin pages are lazy chunks so the dashboard bundle stays small. The
 * `permission` is a UI hint only; the server decides (D-064). List specific paths first.
 */
export const appRoutes: readonly Route[] = [
  { path: '/', element: DashboardRoute, title: 'داشبورد' },
  { path: '/admin/users', element: UsersListRoute, title: 'مدیریت کاربران', permission: 'admin.users.view' },
  { path: '/admin/users/:id', element: UserDetailRoute, title: 'جزئیات کاربر', permission: 'admin.users.view' },
];
