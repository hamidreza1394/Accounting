/**
 * Context dependency of a workspace tab (Issue #444 stage 1 completion; D-076 flow step 10):
 * after a CONFIRMED company/fiscal-period change the workspace closes every tab that depends
 * on the previous context, and keeps only the proven-global ones. Anything unknown is treated
 * as context-dependent (fail-safe) — a page is never assumed global without evidence.
 *
 * The classification is presentation-only: the server revalidates the context on every
 * request (D-013), so this only decides which tabs stay open, never what data is allowed.
 */

export type TabContextScope = 'global' | 'company' | 'company-period';

const globalPrefixes: readonly string[] = [
  // Installation-wide user administration (D-064): not company-scoped.
  '/admin/users',
  // The permission catalog of the installation.
  '/admin/permissions',
  // Installation-level security and management audit (installation administrator).
  '/admin/security',
  // The user's own account data and membership requests.
  '/account',
];

const companyPrefixes: readonly string[] = [
  // Company roles/access groups, membership approval queue, company directory.
  '/admin/roles',
  '/admin/membership-requests',
  '/company',
  // The whole HR organizational structure (company-scoped by D-072).
  '/hr',
];

/** The strip's pathname part of a tab key (the key is the route pathname). */
export function tabKeyPathname(tabKey: string): string {
  const queryIndex = tabKey.indexOf('?');
  return queryIndex === -1 ? tabKey : tabKey.slice(0, queryIndex);
}

function matches(pathname: string, prefixes: readonly string[]): boolean {
  return prefixes.some((prefix) => pathname === prefix || pathname.startsWith(`${prefix}/`));
}

/** The context scope of a tab (the pathname part of its key). Unknown means context-dependent. */
export function tabContextScope(pathname: string): TabContextScope {
  if (matches(pathname, globalPrefixes)) return 'global';
  // The dashboard renders the company overview of the selected period.
  if (pathname === '/') return 'company-period';
  if (matches(pathname, companyPrefixes)) return 'company';
  // Fail-safe: an unclassified page is treated as dependent on the work context.
  return 'company';
}

/** True when the tab must close after a confirmed context change. */
export function tabDependsOnContext(tabKey: string): boolean {
  return tabContextScope(tabKeyPathname(tabKey)) !== 'global';
}
