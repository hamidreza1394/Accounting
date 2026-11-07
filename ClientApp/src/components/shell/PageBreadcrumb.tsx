import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { Breadcrumb, type BreadcrumbItem } from '@/components/ui/Breadcrumb';

/**
 * The shell renders ONE breadcrumb — in its own bar above the page content — while pages
 * *declare* their real, record-aware path instead of rendering one inline. The declaration is
 * per mounted page instance, which is what makes the bar per workspace tab once the multi-tab
 * shell gives every open tab its own provider (Issue #444 stage 1).
 *
 * `fallback` is the real route title derived from the matched route — never an invented path.
 */

type PageBreadcrumbApi = {
  /** A page announces its breadcrumb; equal declarations do not re-render the bar. */
  declare: (items: readonly BreadcrumbItem[]) => void;
  /** The declaring page is gone (or reset): the bar falls back to the route title. */
  clear: () => void;
};

const PageBreadcrumbApiContext = createContext<PageBreadcrumbApi | null>(null);
const PageBreadcrumbItemsContext = createContext<readonly BreadcrumbItem[] | null>(null);

const sameItems = (left: readonly BreadcrumbItem[], right: readonly BreadcrumbItem[]): boolean =>
  left === right ||
  (left.length === right.length &&
    left.every((item, index) => item.label === right[index]?.label && item.to === right[index]?.to));

export function PageBreadcrumbProvider({
  fallback,
  children,
}: {
  fallback: readonly BreadcrumbItem[];
  children: ReactNode;
}) {
  const [items, setItems] = useState<readonly BreadcrumbItem[] | null>(null);
  const declared = useRef<readonly BreadcrumbItem[] | null>(null);
  const declare = useCallback((next: readonly BreadcrumbItem[]) => {
    if (declared.current && sameItems(declared.current, next)) return;
    declared.current = next;
    setItems(next);
  }, []);
  const clear = useCallback(() => {
    declared.current = null;
    setItems(null);
  }, []);
  const api = useMemo<PageBreadcrumbApi>(() => ({ declare, clear }), [declare, clear]);
  const value = items ?? fallback;
  return (
    <PageBreadcrumbApiContext.Provider value={api}>
      <PageBreadcrumbItemsContext.Provider value={value}>{children}</PageBreadcrumbItemsContext.Provider>
    </PageBreadcrumbApiContext.Provider>
  );
}

/**
 * Pages call this instead of rendering `<Breadcrumb>`; the shell draws it. `items` may change
 * over time (e.g. once the record name is known) — updates flow to the bar automatically.
 */
export function usePageBreadcrumb(items: readonly BreadcrumbItem[]): void {
  const api = useContext(PageBreadcrumbApiContext);
  const last = useRef<readonly BreadcrumbItem[] | null>(null);
  useEffect(() => {
    last.current = items;
    api?.declare(items);
  }, [api, items]);
  useEffect(
    () => () => {
      // Only clear when the bar still shows what THIS page declared (per-tab providers in the
      // multi-tab shell keep every tab's declaration independent anyway).
      if (last.current) api?.clear();
    },
    [api],
  );
}

/** The single shell-level breadcrumb bar; shows the active page's declaration or the route title. */
export function PageBreadcrumbBar() {
  const items = useContext(PageBreadcrumbItemsContext);
  if (!items || items.length === 0) return null;
  return (
    <div className="faranou-breadcrumbbar">
      <Breadcrumb items={items} />
    </div>
  );
}
