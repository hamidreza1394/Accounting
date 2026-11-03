import {
  Suspense,
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type AnchorHTMLAttributes,
  type ComponentType,
  type MouseEvent,
  type ReactNode,
} from 'react';
import { LoadingState } from '../components/Feedback';
import type { Permission } from './permissions';

// A minimal History-API router for the pages under the base path `/app` (the server falls back
// `/app/{*path}` to index.html). It is in-house on purpose: no new dependency without an ADR;
// revisit if needs grow (nested layouts, data loaders, search-param state).
// All paths given to this module are app-relative (`/admin/users`); the base is added/removed here.

/**
 * Routes are matched in array order and the first match wins, so list specific paths before
 * parameterised ones (`/admin/users/new` before `/admin/users/:id`).
 */
export type Route = {
  /** App-relative pattern; `:name` segments become params. */
  path: string;
  /** A component, including a `React.lazy` one (it is rendered inside Suspense). */
  element: ComponentType;
  /** Page title; the tab title becomes `<title> | فرانو`. */
  title: string;
  /** UI hint only; the server is authoritative (D-064). */
  permission?: Permission;
};

export type AppLocation = { pathname: string; search: string; hash: string };
export type NavigateOptions = { replace?: boolean };
export type Params = Record<string, string>;

const defaultBase = '/app';

/** Only a path like `/x` is internal; `https://…`, `//host` and `mailto:` are left to the browser. */
function isInternalPath(to: string): boolean {
  return to.startsWith('/') && !to.startsWith('//');
}
const titleSuffix = 'فرانو';

function normalize(path: string): string {
  const trimmed = path.replace(/\/+$/, '');
  return trimmed === '' ? '/' : trimmed;
}

function stripBase(pathname: string, base: string): string {
  if (pathname === base || pathname === `${base}/`) return '/';
  if (pathname.startsWith(`${base}/`)) return normalize(pathname.slice(base.length));
  return normalize(pathname);
}

function readLocation(base: string): AppLocation {
  const { pathname, search, hash } = window.location;
  return { pathname: stripBase(pathname, base), search, hash };
}

function safeDecode(value: string): string {
  try {
    return decodeURIComponent(value);
  } catch {
    return value;
  }
}

/** Matches an exact pattern; returns the params or null. */
export function matchPath(pattern: string, pathname: string): Params | null {
  const patternParts = normalize(pattern).split('/').filter(Boolean);
  const pathParts = normalize(pathname).split('/').filter(Boolean);
  if (patternParts.length !== pathParts.length) return null;
  const params: Params = {};
  for (let index = 0; index < patternParts.length; index += 1) {
    const expected = patternParts[index] as string;
    const actual = pathParts[index] as string;
    if (expected.startsWith(':')) {
      params[expected.slice(1)] = safeDecode(actual);
    } else if (expected !== actual) {
      return null;
    }
  }
  return params;
}

type RouterContextValue = {
  location: AppLocation;
  navigate: (to: string, options?: NavigateOptions) => void;
  hrefFor: (to: string) => string;
  routes: readonly Route[];
  canAccess: (permission: Permission) => boolean;
  notFound: ComponentType;
  forbidden: ComponentType;
  loadingLabel: string;
};

const RouterContext = createContext<RouterContextValue | null>(null);

function useRouterContext(): RouterContextValue {
  const context = useContext(RouterContext);
  if (!context) throw new Error('RouterProvider is missing above this component.');
  return context;
}

function DefaultNotFound() {
  return (
    <div role="alert" className="rounded-xl border border-line bg-white p-6 text-center">
      <h1 className="text-lg font-bold text-ink">صفحه پیدا نشد</h1>
      <p className="mt-2 text-sm text-sub">نشانی درخواست‌شده وجود ندارد.</p>
    </div>
  );
}

function DefaultForbidden() {
  return (
    <div role="alert" className="rounded-xl border border-danger/30 bg-danger-soft p-6 text-center text-danger">
      <h1 className="text-lg font-bold">دسترسی ندارید</h1>
      <p className="mt-2 text-sm">شما مجوز مشاهدهٔ این صفحه را ندارید.</p>
    </div>
  );
}

type RouterProviderProps = {
  routes: readonly Route[];
  /** Base path of the app on the server; defaults to `/app`. */
  base?: string;
  /** UI permission hint; by default everything is allowed. The server still decides. */
  canAccess?: (permission: Permission) => boolean;
  notFound?: ComponentType;
  forbidden?: ComponentType;
  loadingLabel?: string;
  /** Defaults to `<RouteView />`. */
  children?: ReactNode;
};

const allowAll = () => true;

export function RouterProvider({
  routes,
  base = defaultBase,
  canAccess = allowAll,
  notFound = DefaultNotFound,
  forbidden = DefaultForbidden,
  loadingLabel = 'در حال بارگذاری…',
  children,
}: RouterProviderProps) {
  const normalizedBase = normalize(base) === '/' ? '' : normalize(base);
  const [location, setLocation] = useState<AppLocation>(() => readLocation(normalizedBase));

  useEffect(() => {
    const onPopState = () => setLocation(readLocation(normalizedBase));
    window.addEventListener('popstate', onPopState);
    return () => window.removeEventListener('popstate', onPopState);
  }, [normalizedBase]);

  const hrefFor = useCallback(
    (to: string) => (isInternalPath(to) ? `${normalizedBase}${to === '/' ? '/' : to}` : to),
    [normalizedBase],
  );

  const navigate = useCallback(
    (to: string, options?: NavigateOptions) => {
      const href = hrefFor(to);
      const current = `${window.location.pathname}${window.location.search}${window.location.hash}`;
      if (options?.replace || href === current) {
        window.history.replaceState(null, '', href);
      } else {
        window.history.pushState(null, '', href);
      }
      setLocation(readLocation(normalizedBase));
    },
    [hrefFor, normalizedBase],
  );

  const value = useMemo<RouterContextValue>(
    () => ({ location, navigate, hrefFor, routes, canAccess, notFound, forbidden, loadingLabel }),
    [location, navigate, hrefFor, routes, canAccess, notFound, forbidden, loadingLabel],
  );

  return <RouterContext.Provider value={value}>{children ?? <RouteView />}</RouterContext.Provider>;
}

type Match = { route: Route; params: Params } | null;

function findMatch(routes: readonly Route[], pathname: string): Match {
  for (const route of routes) {
    const params = matchPath(route.path, pathname);
    if (params) return { route, params };
  }
  return null;
}

const ParamsContext = createContext<Params>({});

/** Renders the route matching the current location (NotFound when none; Forbidden without permission). */
export function RouteView() {
  const { location, routes, canAccess, notFound: NotFound, forbidden: Forbidden, loadingLabel } = useRouterContext();
  const match = useMemo(() => findMatch(routes, location.pathname), [routes, location.pathname]);
  const allowed = match ? !match.route.permission || canAccess(match.route.permission) : true;
  const title = match ? (allowed ? match.route.title : 'دسترسی ندارید') : 'صفحه پیدا نشد';

  useEffect(() => {
    document.title = `${title} | ${titleSuffix}`;
  }, [title]);

  // After a route change (not on the first load) focus moves to the page heading, so keyboard and
  // screen-reader users are not left on the link they activated. Lazy pages render later, so the
  // heading is awaited briefly. Query-only changes (filters, paging) keep the focus where it is.
  const containerRef = useRef<HTMLDivElement>(null);
  const lastPathname = useRef(location.pathname);
  useEffect(() => {
    if (lastPathname.current === location.pathname) return;
    lastPathname.current = location.pathname;
    const container = containerRef.current;
    if (!container) return;
    const focusHeading = (): boolean => {
      const heading = container.querySelector('h1');
      if (!heading) return false;
      heading.tabIndex = -1;
      heading.focus();
      return true;
    };
    if (focusHeading()) return;
    const observer = new MutationObserver(() => {
      if (focusHeading()) observer.disconnect();
    });
    observer.observe(container, { childList: true, subtree: true });
    const timer = window.setTimeout(() => observer.disconnect(), 5000);
    return () => {
      observer.disconnect();
      window.clearTimeout(timer);
    };
  }, [location.pathname]);

  let content: ReactNode;
  if (!match) {
    content = <NotFound />;
  } else if (!allowed) {
    content = <Forbidden />;
  } else {
    const Element = match.route.element;
    content = (
      <ParamsContext.Provider value={match.params}>
        <Suspense fallback={<LoadingState label={loadingLabel} />}>
          <Element />
        </Suspense>
      </ParamsContext.Provider>
    );
  }
  return <div ref={containerRef}>{content}</div>;
}

/** Refines the tab title once a page knows its subject (e.g. a user's name); `null` leaves it as is. */
export function useDocumentTitle(title: string | null): void {
  useEffect(() => {
    if (title) document.title = `${title} | ${titleSuffix}`;
  }, [title]);
}

export function useNavigate(): (to: string, options?: NavigateOptions) => void {
  return useRouterContext().navigate;
}

/** The current app-relative location. */
export function useLocation(): AppLocation {
  return useRouterContext().location;
}

/** Params of the matched route (e.g. `:id`). */
export function useParams(): Params {
  return useContext(ParamsContext);
}

/**
 * The query string of the current location as `URLSearchParams`, plus a setter that navigates to
 * the same path with a new query (so the state lives in the URL: back/forward and refresh work).
 * Empty values are dropped; pass `{ replace: true }` for changes that should not add a history entry.
 */
export function useSearchParams(): [URLSearchParams, (next: URLSearchParams, options?: NavigateOptions) => void] {
  const { location, navigate } = useRouterContext();
  const params = useMemo(() => new URLSearchParams(location.search), [location.search]);
  const pathname = location.pathname;
  const setParams = useCallback(
    (next: URLSearchParams, options?: NavigateOptions) => {
      const query = next.toString();
      navigate(query ? `${pathname}?${query}` : pathname, options);
    },
    [navigate, pathname],
  );
  return [params, setParams];
}

type LinkProps = Omit<AnchorHTMLAttributes<HTMLAnchorElement>, 'href'> & {
  /** App-relative path, e.g. `/admin/users`. */
  to: string;
  replace?: boolean;
};

function isModifiedEvent(event: MouseEvent): boolean {
  return event.metaKey || event.altKey || event.ctrlKey || event.shiftKey;
}

export function Link({ to, replace, onClick, target, download, children, ...rest }: LinkProps) {
  const { navigate, hrefFor } = useRouterContext();

  const handleClick = (event: MouseEvent<HTMLAnchorElement>) => {
    onClick?.(event);
    if (
      !isInternalPath(to) ||
      event.defaultPrevented ||
      event.button !== 0 ||
      isModifiedEvent(event) ||
      (target !== undefined && target !== '_self') ||
      download !== undefined
    ) {
      return;
    }
    event.preventDefault();
    navigate(to, replace ? { replace: true } : undefined);
  };

  return (
    <a
      {...rest}
      href={hrefFor(to)}
      {...(target !== undefined ? { target } : {})}
      {...(download !== undefined ? { download } : {})}
      onClick={handleClick}
    >
      {children}
    </a>
  );
}
