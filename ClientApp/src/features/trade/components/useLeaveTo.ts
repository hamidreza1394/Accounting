import { useCallback, useContext } from 'react';
import { PaneTabKeyContext, useOptionalWorkspaceTabs } from '@/components/shell/WorkspaceTabs';
import { useNavigate } from '@/lib/router';

/**
 * Leaves a finished or cancelled form for `path`. In the multi-tab workspace the form's own tab is closed
 * first (the caller has saved, or the user confirmed discarding), so no stale form tab stays behind and the
 * target tab becomes active exactly once; the navigation then replaces the entry the closing produced.
 * Outside the workspace it is a plain navigation.
 */
export function useLeaveTo(): (path: string) => void {
  const navigate = useNavigate();
  const tabs = useOptionalWorkspaceTabs();
  const paneKey = useContext(PaneTabKeyContext);
  return useCallback(
    (path: string) => {
      const own = tabs !== null && paneKey !== null ? tabs.state.tabs.find((tab) => tab.key === paneKey) : undefined;
      if (own && tabs) tabs.forceClose(own);
      navigate(path, own ? { replace: true } : undefined);
    },
    [navigate, tabs, paneKey],
  );
}
