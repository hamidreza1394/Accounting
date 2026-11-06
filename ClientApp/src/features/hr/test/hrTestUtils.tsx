import { render } from '@testing-library/react';
import { RouteView, RouterProvider, type Route } from '@/lib/router';
import type { ReactElement } from 'react';
import { companyId } from './hrFixtures';

/** The `/me/permissions` body the shared `useCompanyPermissions` hook consumes. */
export function hrPermissionRoute(codes: readonly string[], id: string = companyId) {
  return { status: 200, body: { isInstallationAdministrator: false, company: { id, permissionCodes: codes } } };
}

/** Renders a page inside the real in-house router so `Link`/`useNavigate`/`useParams` work. */
export function renderHrPage(element: ReactElement, route: Pick<Route, 'path' | 'title'>, appPath: string) {
  const routes: Route[] = [{ ...route, element: () => element }];
  window.history.pushState(null, '', `/app${appPath}`);
  return render(
    <RouterProvider routes={routes}>
      <RouteView />
    </RouterProvider>,
  );
}
