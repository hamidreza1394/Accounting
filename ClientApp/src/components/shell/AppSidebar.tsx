import { useEffect, useRef, useState } from 'react';
import {
  Activity,
  BadgeCheck,
  Banknote,
  BookOpen,
  Boxes,
  Building2,
  ChevronDown,
  ClipboardList,
  FileBarChart,
  Gem,
  Headphones,
  KeyRound,
  LayoutDashboard,
  PanelRightClose,
  PanelRightOpen,
  Settings2,
  ShieldCheck,
  ShoppingCart,
  Ship,
  UserCheck,
  Users,
  Wallet,
  type LucideIcon,
} from 'lucide-react';
import { Link, useLocation } from '@/lib/router';
import type { AccessSnapshot, Permission } from '@/lib/permissions';
import { Tooltip } from '@/components/ui/Tooltip';

// TEMP: full file restored in next commit from working tree.
export type NavEntry = { kind: 'item'; leaf: NavLeaf } | { kind: 'group'; group: NavGroup };
export type NavLeafInfo = NavLeaf;
export type NavGroupInfo = NavGroup;
type Tone = 'sky' | 'emerald' | 'amber' | 'rose' | 'violet' | 'cyan' | 'indigo' | 'teal' | 'orange' | 'slate' | 'blue' | 'green';
type NavLeaf = { label: string; icon: LucideIcon; tone: Tone; to?: string; section?: string };
type NavGroup = { label: string; icon: LucideIcon; tone: Tone; children: NavLeaf[]; openByDefault?: boolean };
export const navigation: NavEntry[] = [];
export function isActivePath(pathname: string, to: string): boolean {
  if (to === '/') return pathname === '/';
  return pathname === to || pathname.startsWith(`${to}/`);
}
export type AppSidebarProps = {
  access: AccessSnapshot;
  canAccess: (permission: Permission) => boolean;
  onRetryPermissions: () => void;
  onNavigate: () => void;
  collapsed: boolean;
  onToggleCollapsed: () => void;
  mobileOpen: boolean;
};
export function AppSidebar(_props: AppSidebarProps) {
  return <aside id="main-navigation" className="faranou-sidebar" aria-label="ناوبری فرانو" />;
}
