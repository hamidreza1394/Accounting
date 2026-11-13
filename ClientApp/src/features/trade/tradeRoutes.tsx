import { lazy, useContext, type ReactNode } from 'react';
import { AppRouteDataContext } from '@/components/AppRoutes';
import type { Route } from '@/lib/router';
import type { TradePermission } from './access/TradeAccess';
import { TradeRouteGuard } from './components/TradeStates';

// Lazy chunks, exactly like the other feature routes: the dashboard bundle stays small and the offline
// checker already resolves lazy imports (docs/UI_COMPONENTS.md, Issue #462).
const LazyPartiesPage = lazy(() => import('./pages/PartiesPage').then((module) => ({ default: module.PartiesPage })));
const LazyPartyCreatePage = lazy(() => import('./pages/PartyCreatePage').then((module) => ({ default: module.PartyCreatePage })));
const LazyPartyDetailPage = lazy(() => import('./pages/PartyDetailPage').then((module) => ({ default: module.PartyDetailPage })));
const LazyPartyEditPage = lazy(() => import('./pages/PartyEditPage').then((module) => ({ default: module.PartyEditPage })));
const LazyProductsPage = lazy(() => import('./pages/ProductsPage').then((module) => ({ default: module.ProductsPage })));
const LazyProductCreatePage = lazy(() => import('./pages/ProductCreatePage').then((module) => ({ default: module.ProductCreatePage })));
const LazyProductDetailPage = lazy(() => import('./pages/ProductDetailPage').then((module) => ({ default: module.ProductDetailPage })));
const LazyProductEditPage = lazy(() => import('./pages/ProductEditPage').then((module) => ({ default: module.ProductEditPage })));

type PageProps = { onUnauthenticated: (error: unknown) => boolean };

/**
 * Wraps a page in the Trade route guard. The guard reads the Trade capabilities hint (fail-closed); the page is
 * keyed by company and fiscal period so a context change never shows one company's data under another.
 */
function makeRoute(permission: TradePermission, render: (props: PageProps) => ReactNode) {
  function TradeRoute() {
    const data = useContext(AppRouteDataContext);
    if (!data) throw new Error('AppRouteDataContext is missing above the Trade routes.');
    return (
      <TradeRouteGuard permission={permission}>
        <div key={`${data.context.company.id}:${data.context.fiscalPeriod.id}`}>{render({ onUnauthenticated: data.onUnauthenticated })}</div>
      </TradeRouteGuard>
    );
  }
  return TradeRoute;
}

const PartiesRoute = makeRoute('party.view', (props) => <LazyPartiesPage {...props} />);
const PartyCreateRoute = makeRoute('party.create', (props) => <LazyPartyCreatePage {...props} />);
const PartyDetailRoute = makeRoute('party.view', (props) => <LazyPartyDetailPage {...props} />);
const PartyEditRoute = makeRoute('party.update', (props) => <LazyPartyEditPage {...props} />);
const ProductsRoute = makeRoute('product.view', (props) => <LazyProductsPage {...props} />);
const ProductCreateRoute = makeRoute('product.create', (props) => <LazyProductCreatePage {...props} />);
const ProductDetailRoute = makeRoute('product.view', (props) => <LazyProductDetailPage {...props} />);
const ProductEditRoute = makeRoute('product.update', (props) => <LazyProductEditPage {...props} />);

/**
 * The Trade routes under `/app/trade/*`. `/new` sits before `/:id` on purpose (the router matches in array
 * order). No `permission` on the route itself: the router's hint knows only the shell snapshot; Trade hints
 * come from `/trade/capabilities` and are enforced by `TradeRouteGuard` (view -> list/detail, create -> new,
 * update -> edit). The server decides every request.
 */
export const tradeRoutes: readonly Route[] = [
  { path: '/trade/parties', element: PartiesRoute, title: 'طرف‌حساب‌ها' },
  { path: '/trade/parties/new', element: PartyCreateRoute, title: 'طرف‌حساب جدید' },
  { path: '/trade/parties/:id', element: PartyDetailRoute, title: 'جزئیات طرف‌حساب' },
  { path: '/trade/parties/:id/edit', element: PartyEditRoute, title: 'ویرایش طرف‌حساب' },
  { path: '/trade/products', element: ProductsRoute, title: 'کالاها' },
  { path: '/trade/products/new', element: ProductCreateRoute, title: 'کالای جدید' },
  { path: '/trade/products/:id', element: ProductDetailRoute, title: 'جزئیات کالا' },
  { path: '/trade/products/:id/edit', element: ProductEditRoute, title: 'ویرایش کالا' },
];
