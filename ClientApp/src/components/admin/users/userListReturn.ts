/**
 * The last location of the users list, remembered in memory only — never in `localStorage` or
 * `sessionStorage` — so the user detail (and edit) pages can return to the list with the
 * visitor's search, filters and paging intact. A refresh or a new tab resets it to the plain
 * list, which is the predictable fallback; no user data is stored, only the list URL.
 */
let lastListLocation: string | null = null;

/** Called by the users list whenever its query state (URL search params) changes. */
export function rememberUserListLocation(search: string): void {
  const query = search.trim();
  lastListLocation = query ? `/admin/users?${query}` : '/admin/users';
}

/** The list path to return to: the remembered one, or the plain list when nothing is known. */
export function userListBackPath(): string {
  return lastListLocation ?? '/admin/users';
}

/** Test-only: the memory is module-wide, so tests start from a clean state. */
export function resetUserListReturnForTests(): void {
  lastListLocation = null;
}
