import { Activity, BadgeCheck, Building2, ClipboardList, FileText, KeyRound, LayoutDashboard, ShieldCheck, UserCheck, Users, type LucideIcon } from 'lucide-react';

/**
 * The central route → icon mapping of the workspace tabs (Issue #444 stage 1 completion).
 * Every page deserves a meaningful icon; a generic decorative one is only the honest fallback
 * for a route that has no better match yet. Keep this table in sync with `AppRoutes.tsx` — the
 * menu↔router consistency test covers the routes, this mapping only names their icons.
 */

const routeIcons: readonly { prefix: string; icon: LucideIcon }[] = [
  { prefix: '/', icon: LayoutDashboard },
  { prefix: '/admin/users', icon: Users },
  { prefix: '/admin/roles', icon: ShieldCheck },
  { prefix: '/admin/permissions', icon: KeyRound },
  { prefix: '/admin/security', icon: Activity },
  { prefix: '/admin/membership-requests', icon: UserCheck },
  { prefix: '/account', icon: ClipboardList },
  { prefix: '/company/users', icon: Users },
  { prefix: '/hr/org/units', icon: Building2 },
  { prefix: '/hr/org/positions', icon: BadgeCheck },
  { prefix: '/hr/org/employees', icon: Users },
];

/** The icon of a tab (the pathname part of its key), longest matching prefix wins. */
export function routeIconOf(pathname: string): LucideIcon {
  const matches = routeIcons.filter((entry) => entry.prefix === '/' ? pathname === '/' : pathname === entry.prefix || pathname.startsWith(`${entry.prefix}/`) || pathname.startsWith(entry.prefix));
  const best = matches.reduce<{ prefix: string; icon: LucideIcon } | null>(
    (best, entry) => (best === null || entry.prefix.length > best.prefix.length ? entry : best),
    null,
  );
  return best?.icon ?? FileText;
}
