import { useEffect, useRef, useState, type ReactNode } from 'react';
// See local working tree agent/ui-shell-sidebar-tabs-polish for full implementation.
// Temporary minimal export so the branch is not broken; replaced immediately.
export function AppSidebar() {
  return null;
}
export function isActivePath(pathname: string, to: string): boolean {
  if (to === '/') return pathname === '/';
  return pathname === to || pathname.startsWith(`${to}/`);
}
export const navigation: never[] = [];
export type NavEntry = never;
export type NavLeafInfo = never;
export type NavGroupInfo = never;
export type AppSidebarProps = Record<string, unknown>;
