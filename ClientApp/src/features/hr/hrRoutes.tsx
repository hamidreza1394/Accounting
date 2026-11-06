import { lazy, useContext, type ReactNode } from 'react';
import { AppRouteDataContext } from '@/components/AppRoutes';
import type { Route } from '@/lib/router';

// Lazy chunks, exactly like the shared routes: the dashboard bundle stays small and the offline
// checker already resolves lazy imports (docs/UI_COMPONENTS.md, Issue #462).
const LazyHrUnitsPage = lazy(() => import('./pages/HrUnitsPage').then((module) => ({ default: module.HrUnitsPage })));
const LazyHrUnitCreatePage = lazy(() => import('./pages/HrUnitCreatePage').then((module) => ({ default: module.HrUnitCreatePage })));
const LazyHrUnitDetailPage = lazy(() => import('./pages/HrUnitDetailPage').then((module) => ({ default: module.HrUnitDetailPage })));
const LazyHrPositionsPage = lazy(() => import('./pages/HrPositionsPage').then((module) => ({ default: module.HrPositionsPage })));
const LazyHrPositionCreatePage = lazy(() => import('./pages/HrPositionCreatePage').then((module) => ({ default: module.HrPositionCreatePage })));
const LazyHrPositionDetailPage = lazy(() =>
  import('./pages/HrPositionDetailPage').then((module) => ({ default: module.HrPositionDetailPage })),
);
const LazyHrEmployeesPage = lazy(() => import('./pages/HrEmployeesPage').then((module) => ({ default: module.HrEmployeesPage })));
const LazyHrEmployeeCreatePage = lazy(() =>
  import('./pages/HrEmployeeCreatePage').then((module) => ({ default: module.HrEmployeeCreatePage })),
);
const LazyHrEmployeeDetailPage = lazy(() =>
  import('./pages/HrEmployeeDetailPage').then((module) => ({ default: module.HrEmployeeDetailPage })),
);
const LazyHrEmployeeEditPage = lazy(() => import('./pages/HrEmployeeEditPage').then((module) => ({ default: module.HrEmployeeEditPage })));

function useHrRouteData() {
  const data = useContext(AppRouteDataContext);
  if (!data) throw new Error('AppRouteDataContext is missing above the HR routes.');
  return data;
}

function makeRouteWrapper(Page: (props: { onUnauthenticated: (error: unknown) => boolean; companyId: string }) => ReactNode) {
  function HrRoute() {
    const { context, onUnauthenticated } = useHrRouteData();
    return <Page onUnauthenticated={onUnauthenticated} companyId={context.company.id} />;
  }
  return HrRoute;
}

// Detail routes need a stable identity per id, so the pages re-read when the param changes
// (they already key their data effects on companyId + id).
const HrUnitsRoute = makeRouteWrapper((props) => <LazyHrUnitsPage {...props} />);
const HrUnitCreateRoute = makeRouteWrapper((props) => <LazyHrUnitCreatePage {...props} />);
const HrUnitDetailRoute = makeRouteWrapper((props) => <LazyHrUnitDetailPage {...props} />);
const HrPositionsRoute = makeRouteWrapper((props) => <LazyHrPositionsPage {...props} />);
const HrPositionCreateRoute = makeRouteWrapper((props) => <LazyHrPositionCreatePage {...props} />);
const HrPositionDetailRoute = makeRouteWrapper((props) => <LazyHrPositionDetailPage {...props} />);
const HrEmployeesRoute = makeRouteWrapper((props) => <LazyHrEmployeesPage {...props} />);
const HrEmployeeCreateRoute = makeRouteWrapper((props) => <LazyHrEmployeeCreatePage {...props} />);
const HrEmployeeDetailRoute = makeRouteWrapper((props) => <LazyHrEmployeeDetailPage {...props} />);
const HrEmployeeEditRoute = makeRouteWrapper((props) => <LazyHrEmployeeEditPage {...props} />);

/**
 * The HR routes under `/app/hr/org/*`, ready to be concatenated after `appRoutes` in
 * `AppRoutes.tsx` (see docs/handoffs/HR_FRONTEND_INTEGRATION.md). `/new` sits before `/:id` on
 * purpose (the router matches in array order). No `permission` hint: the `hr.*` codes are
 * company-scoped and live in `/me/permissions`, not in the session union; the server decides
 * every request (D-064) and the pages show 403/404 honestly.
 */
export const hrRoutes: readonly Route[] = [
  { path: '/hr/org/units', element: HrUnitsRoute, title: 'واحدهای سازمانی' },
  { path: '/hr/org/units/new', element: HrUnitCreateRoute, title: 'ایجاد واحد سازمانی' },
  { path: '/hr/org/units/:unitId', element: HrUnitDetailRoute, title: 'جزئیات واحد سازمانی' },
  { path: '/hr/org/positions', element: HrPositionsRoute, title: 'سمت‌های سازمانی' },
  { path: '/hr/org/positions/new', element: HrPositionCreateRoute, title: 'ایجاد سمت سازمانی' },
  { path: '/hr/org/positions/:positionId', element: HrPositionDetailRoute, title: 'جزئیات سمت' },
  { path: '/hr/org/employees', element: HrEmployeesRoute, title: 'کارکنان' },
  { path: '/hr/org/employees/new', element: HrEmployeeCreateRoute, title: 'ثبت کارمند' },
  { path: '/hr/org/employees/:employeeId', element: HrEmployeeDetailRoute, title: 'پروندهٔ کارمند' },
  { path: '/hr/org/employees/:employeeId/edit', element: HrEmployeeEditRoute, title: 'ویرایش کارمند' },
];
