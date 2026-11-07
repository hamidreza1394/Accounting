/**
 * The multi-tab workspace model (Issue #444 stage 1, PR 4).
 *
 * A tab is one open page. Its identity is the route PATHNAME, so the same list opened twice
 * is one tab, and two records of a parameterised route are two tabs. The current query string
 * travels WITH the tab (path), so filters, search and paging survive switching back and forth.
 *
 * The model is kept free of React and DOM so the tricky rules are unit-testable:
 * dedupe, activation on navigation, the next-active choice when a tab closes, and the
 * scroll restore bookkeeping. Storage: none — tabs live in memory only, a refresh leaves
 * exactly the page you were on (predictable, and nothing sensitive ever hits web storage).
 */

export type WorkspaceTab = {
  /** Unique per page: the route pathname (includes the record id, if any). */
  key: string;
  /** The tab's own pathname + query string; restored verbatim on activation. */
  path: string;
  /** The matched route's Persian title; pages refine it with the record name via the sink. */
  title: string;
  /** Window scroll position captured when the tab was last deactivated. */
  scrollY: number;
  /** Record title the page declared (via `useDocumentTitle`); null keeps the route title. */
  declaredTitle: string | null;
};

export type TabsState = {
  tabs: readonly WorkspaceTab[];
  /** Key of the visible tab. */
  activeKey: string | null;
  /** Key of the tab that was visible just before the current one (preferred on close). */
  previousActiveKey: string | null;
};

export type TabsAction =
  | { type: 'open'; pathname: string; search: string; title: string }
  | { type: 'activate'; key: string }
  | { type: 'close'; key: string }
  | { type: 'set-declared-title'; key: string; title: string | null }
  | { type: 'capture-scroll'; key: string; scrollY: number };

/** The dashboard path every workspace can fall back to. */
export const homePath = '/';

export function initialTabsState(pathname: string, search: string, title: string): TabsState {
  return {
    tabs: [{ key: pathname, path: `${pathname}${search}`, title, scrollY: 0, declaredTitle: null }],
    activeKey: pathname,
    previousActiveKey: null,
  };
}

/** The tab label: the page-declared record name wins over the route title. */
export function tabLabel(tab: WorkspaceTab): string {
  return tab.declaredTitle?.trim() ? tab.declaredTitle : tab.title;
}

export function tabsReducer(state: TabsState, action: TabsAction): TabsState {
  switch (action.type) {
    case 'open': {
      // A tab already opened for this page (same pathname, any query): refresh its query and
      // activate it — never a duplicate.
      const existing = state.tabs.find((tab) => tab.key === action.pathname);
      const path = `${action.pathname}${action.search}`;
      if (existing) {
        if (state.activeKey === existing.key && existing.path === path) return state;
        const tabs = state.tabs.map((tab) => (tab.key === existing.key ? { ...tab, path } : tab));
        return { tabs, activeKey: existing.key, previousActiveKey: state.activeKey };
      }
      const tab: WorkspaceTab = { key: action.pathname, path, title: action.title, scrollY: 0, declaredTitle: null };
      return { tabs: [...state.tabs, tab], activeKey: tab.key, previousActiveKey: state.activeKey };
    }
    case 'activate': {
      if (state.activeKey === action.key || !state.tabs.some((tab) => tab.key === action.key)) return state;
      return { ...state, activeKey: action.key, previousActiveKey: state.activeKey };
    }
    case 'close': {
      const closing = state.tabs.find((tab) => tab.key === action.key);
      if (!closing) return state;
      const tabs = state.tabs.filter((tab) => tab.key !== action.key);
      // Zero tabs left: the workspace falls back to the dashboard route (the caller navigates).
      if (tabs.length === 0) return { tabs, activeKey: null, previousActiveKey: null };
      if (state.activeKey !== action.key) return { ...state, tabs };
      const nextActive =
        (state.previousActiveKey && state.previousActiveKey !== action.key && tabs.some((tab) => tab.key === state.previousActiveKey)
          ? state.previousActiveKey
          : null) ??
        tabs[state.tabs.findIndex((tab) => tab.key === action.key)]?.key ??
        tabs[tabs.length - 1]!.key;
      return { tabs, activeKey: nextActive, previousActiveKey: null };
    }
    case 'set-declared-title':
      return {
        ...state,
        tabs: state.tabs.map((tab) =>
          tab.key === action.key && tab.declaredTitle !== action.title ? { ...tab, declaredTitle: action.title } : tab,
        ),
      };
    case 'capture-scroll':
      return {
        ...state,
        tabs: state.tabs.map((tab) => (tab.key === action.key && tab.scrollY !== action.scrollY ? { ...tab, scrollY: action.scrollY } : tab)),
      };
    default:
      return state;
  }
}
