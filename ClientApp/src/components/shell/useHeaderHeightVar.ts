import { useLayoutEffect, type RefObject } from 'react';

/** The CSS custom property the sidebar frame, the drawer and the scrim read to sit under the header. */
export const headerHeightVar = '--faranou-header-height';

/**
 * Publishes the REAL height of the sticky header as `--faranou-header-height` on the workspace
 * root. The sidebar frame is `position: sticky` below the header and as tall as the rest of the
 * viewport; with a hard-coded header height (92/128/150px) a header that wraps or grows pushes the
 * frame's top edge under the header and its bottom edge below the fold, so the border looks cut.
 * CSS keeps its old pixel values as the fallback, which is what applies until the first
 * measurement (and in test environments that have no layout).
 */
export function useHeaderHeightVar(header: RefObject<HTMLElement | null>, root: RefObject<HTMLElement | null>): void {
  useLayoutEffect(() => {
    const headerElement = header.current;
    const rootElement = root.current;
    if (!headerElement || !rootElement) return undefined;

    const apply = () => {
      const height = Math.round(headerElement.getBoundingClientRect().height);
      if (height > 0) rootElement.style.setProperty(headerHeightVar, `${height}px`);
    };
    apply();

    if (typeof ResizeObserver === 'undefined') {
      window.addEventListener('resize', apply);
      return () => window.removeEventListener('resize', apply);
    }
    const observer = new ResizeObserver(apply);
    observer.observe(headerElement);
    return () => observer.disconnect();
  }, [header, root]);
}
