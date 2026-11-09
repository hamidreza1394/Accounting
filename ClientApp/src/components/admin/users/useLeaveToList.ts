import { useCallback, useContext } from 'react';
import { PaneTabKeyContext, useOptionalWorkspaceTabs } from '@/components/shell/WorkspaceTabs';
import { useNavigate } from '@/lib/router';
import { invalidateUserList } from './userListRefresh';
import { resetUserListLocation } from './userListReturn';

/** The list a finished or cancelled user form returns to: always the plain list, never `history.back()`. */
export const usersListPath = '/admin/users';

/**
 * Leaves a user form for the plain users list and makes the list load again.
 *
 * In the multi-tab workspace the form's own tab is closed (the caller has already saved, or the user
 * confirmed discarding), so no stale "create user" tab stays behind and the list tab - an existing
 * one or a new one - becomes the active tab exactly once. The navigation replaces the entry that
 * closing the tab produced, so one step of history is added, not two. Outside the workspace it is a
 * plain navigation.
 */
export function useLeaveToList(): () => void {
  const navigate = useNavigate();
  const tabs = useOptionalWorkspaceTabs();
  const paneKey = useContext(PaneTabKeyContext);
  return useCallback(() => {
    resetUserListLocation();
    invalidateUserList();
    const own = tabs !== null && paneKey !== null ? tabs.state.tabs.find((tab) => tab.key === paneKey) : undefined;
    if (own && tabs) tabs.forceClose(own);
    navigate(usersListPath, own ? { replace: true } : undefined);
  }, [navigate, tabs, paneKey]);
}
