import { useContext, useEffect, useMemo } from 'react';
import { PaneTabKeyContext } from '@/components/shell/WorkspaceTabs';

/**
 * The shared dirty-form registry (Issue #444 stage 1, PR 5) — a small, lightweight module
 * store, deliberately NOT a form framework:
 *
 * - every form instance registers its dirty state under its page's workspace tab key,
 * - the workspace asks the registry before closing a tab, before signing out and before a
 *   company/fiscal-period change, and shows the shared `ConfirmDialog` on refusal,
 * - each dirty form keeps its own `beforeunload` warning (same as before this module).
 *
 * Nothing is written to web storage and no form content is remembered — only a boolean per
 * form instance. The server never sees any of this; it is pure client-side loss protection.
 */

export type DirtyFormEntry = {
  /** Stable per form instance. */
  id: string;
  /** The workspace tab the form lives on; `single-page` outside the multi-tab shell. */
  tabKey: string;
  /** Persian message shown by the confirming dialog. */
  message: string;
};

const registry = new Map<string, DirtyFormEntry>();
const listeners = new Set<() => void>();

function emit(): void {
  for (const listener of listeners) listener();
}

/** All dirty forms right now (test/debug and the shell's summaries). */
export function dirtyForms(): readonly DirtyFormEntry[] {
  return [...registry.values()];
}

/** True when any form on the given workspace tab holds unsaved changes. */
export function isTabDirty(tabKey: string): boolean {
  for (const entry of registry.values()) if (entry.tabKey === tabKey) return true;
  return false;
}

/** True when any mounted form anywhere holds unsaved changes (logout, context change, unload). */
export function hasAnyDirtyForm(): boolean {
  return registry.size > 0;
}

/** Persian summary of what would be lost, used by the confirming dialogs. */
export function dirtySummary(): string {
  const entries = dirtyForms();
  if (entries.length === 0) return '';
  const distinctTabs = new Set(entries.map((entry) => entry.tabKey));
  if (distinctTabs.size <= 1) return entries[0]?.message ?? '';
  const faNumber = new Intl.NumberFormat('fa-IR');
  return `${faNumber.format(entries.length)} فرم دارای تغییر ذخیره‌نشده در ${faNumber.format(distinctTabs.size)} تب باز است.`;
}

/** Test-only: empty the registry between tests. */
export function resetDirtyFormsForTests(): void {
  if (registry.size === 0) return;
  registry.clear();
  emit();
}

export type FormGuard = {
  /** True while this form holds unsaved changes (mirrors the page's own state). */
  dirty: boolean;
  /** Called by the page after a successful save so the tab can close without a warning. */
  markClean: () => void;
};

/**
 * Declares this form's dirty state to the workspace. `dirty` may change over time; the
 * registration follows it and is removed when the page unmounts.
 */
export function useFormGuard(dirty: boolean, message = 'این فرم تغییرات ذخیره‌نشده دارد.'): FormGuard {
  const tabKey = useContext(PaneTabKeyContext) ?? 'single-page';
  const id = useMemo(() => `form-${Math.random().toString(36).slice(2)}-${Date.now().toString(36)}`, []);

  useEffect(() => {
    if (!dirty) {
      if (registry.delete(id)) emit();
      return undefined;
    }
    registry.set(id, { id, tabKey, message });
    emit();
    const beforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = '';
    };
    window.addEventListener('beforeunload', beforeUnload);
    return () => {
      registry.delete(id);
      emit();
      window.removeEventListener('beforeunload', beforeUnload);
    };
  }, [dirty, id, tabKey, message]);

  return useMemo<FormGuard>(
    () => ({
      dirty,
      markClean: () => {
        if (registry.delete(id)) emit();
      },
    }),
    [dirty, id],
  );
}
