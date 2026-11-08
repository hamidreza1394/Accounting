import { useContext, useEffect, useMemo, useSyncExternalStore } from 'react';
import { PaneTabKeyContext } from '@/components/shell/WorkspaceTabs';

/**
 * The shared form-lifecycle registry (Issue #444 stage 1; four-state contract of D-076):
 * a small, lightweight module store, deliberately NOT a form framework.
 *
 * Every form declares its lifecycle status under its page's workspace tab key:
 *
 * - `clean`  — in sync with the last saved state; nothing is registered.
 * - `dirty`  — at least one real change against the baseline.
 * - `saving` — a save request is running; the tab must not close or switch context.
 * - `error`  — the save failed or its outcome is unknown; blocked until the user resolves it.
 *
 * The workspace asks the registry before closing a tab, before signing out and before a
 * company/fiscal-period change. Tab status is the WORST status of its forms
 * (`error > saving > dirty > clean`). Nothing is written to web storage and no form content
 * is remembered — only a status record per form instance. The server never sees any of this;
 * it is pure client-side loss protection.
 */

/** The four lifecycle states of a form, in D-076 order of severity. */
export type FormStatus = 'clean' | 'dirty' | 'saving' | 'error';

/** Persian label of every status, shared by tab markers, dialogs and screen-reader texts. */
export const formStatusLabel: Record<FormStatus, string> = {
  clean: 'بدون تغییر ذخیره‌نشده',
  dirty: 'دارای تغییر ذخیره‌نشده',
  saving: 'در حال ذخیره',
  error: 'خطا در ذخیره',
};

/** Severity for the tab/workspace roll-up; higher wins. */
export function formStatusRank(status: FormStatus): number {
  switch (status) {
    case 'error':
      return 3;
    case 'saving':
      return 2;
    case 'dirty':
      return 1;
    default:
      return 0;
  }
}

/** The worst of two statuses (`error > saving > dirty > clean`). */
export function worstFormStatus(left: FormStatus, right: FormStatus): FormStatus {
  return formStatusRank(left) >= formStatusRank(right) ? left : right;
}

export type FormGuardEntry = {
  /** Stable per form instance. */
  id: string;
  /** The workspace tab the form lives on; `single-page` outside the multi-tab shell. */
  tabKey: string;
  /** Persian title of the form for dialogs and lists. */
  title: string;
  /** Current status; only non-clean forms are registered. */
  status: Exclude<FormStatus, 'clean'>;
  /** Persian message shown by the dialogs (unsaved-changes note or the failure message). */
  message: string | null;
  /** Identifies the running save attempt, so a late answer cannot resolve a newer one. */
  saveToken: string | null;
};

/** One problem form as the shell's dialogs consume it. */
export type ProblemForm = {
  id: string;
  tabKey: string;
  title: string;
  status: Exclude<FormStatus, 'clean'>;
  message: string | null;
};

const registry = new Map<string, FormGuardEntry>();
const listeners = new Set<() => void>();
let version = 0;

function emit(): void {
  version += 1;
  for (const listener of listeners) listener();
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

/** Live subscription counter for React components (`useSyncExternalStore`). */
export function useFormRegistryVersion(): number {
  return useSyncExternalStore(subscribe, () => version, () => 0);
}

/** All registered (non-clean) forms right now (dialogs, tests and the shell's summaries). */
export function formEntries(): readonly FormGuardEntry[] {
  return [...registry.values()];
}

/** The worst status among the forms of one workspace tab (`clean` when the tab holds none). */
export function tabStatus(tabKey: string): FormStatus {
  let worst: FormStatus = 'clean';
  for (const entry of registry.values()) {
    if (entry.tabKey === tabKey) worst = worstFormStatus(worst, entry.status);
  }
  return worst;
}

/** The worst status across every mounted form (logout, context change, unload). */
export function workspaceStatus(): FormStatus {
  let worst: FormStatus = 'clean';
  for (const entry of registry.values()) worst = worstFormStatus(worst, entry.status);
  return worst;
}

/** True when any mounted form is dirty, saving or failed (anything that blocks leaving). */
export function hasAnyProblemForm(): boolean {
  return registry.size > 0;
}

/** Problem forms of one tab, worst first. */
export function tabProblemForms(tabKey: string): readonly ProblemForm[] {
  return [...registry.values()]
    .filter((entry) => entry.tabKey === tabKey)
    .sort((left, right) => formStatusRank(right.status) - formStatusRank(left.status));
}

/** All problem forms, worst status first (the problem-tabs dialog). */
export function problemForms(): readonly ProblemForm[] {
  return [...registry.values()].sort((left, right) => formStatusRank(right.status) - formStatusRank(left.status));
}

const faNumber = new Intl.NumberFormat('fa-IR');

/** Persian summary of what would be lost, used by the confirming dialogs. */
export function problemSummary(): string {
  const entries = formEntries();
  if (entries.length === 0) return '';
  const distinctTabs = new Set(entries.map((entry) => entry.tabKey));
  if (distinctTabs.size <= 1) {
    const first = entries[0] as FormGuardEntry;
    const sameStatus = entries.every((entry) => entry.status === first.status);
    if (sameStatus && first.message) return first.message;
    if (sameStatus) return `${faNumber.format(entries.length)} فرم «${formStatusLabel[first.status]}» است.`;
    return `${faNumber.format(entries.length)} فرم نیاز به تعیین تکلیف دارد.`;
  }
  return `${faNumber.format(entries.length)} فرم نیاز به تعیین تکلیف در ${faNumber.format(distinctTabs.size)} تب باز دارد.`;
}

/** Test-only: empty the registry between tests. */
export function resetFormsForTests(): void {
  if (registry.size === 0) return;
  registry.clear();
  emit();
}

/** Identifies one save attempt; a late answer never resolves a different attempt. */
export type SaveToken = string;

export type FormGuardInput = {
  /** The form holds at least one real change against its baseline. */
  dirty: boolean;
  /** A save request is currently running; takes precedence over `dirty`. */
  saving?: boolean;
  /** Persian failure message of the last save attempt; presence means the `error` status. */
  errorMessage?: string | null;
  /** Persian title of the form for dialogs and lists (defaults to a generic label). */
  title?: string;
  /** Persian note shown while merely dirty (defaults to the shared text). */
  dirtyMessage?: string;
};

export type FormGuard = {
  /** The derived lifecycle status of this form. */
  status: FormStatus;
  /**
   * Declares this instance clean again (explicit user cancel, or the brief moment between a
   * confirmed save and the page unmounting). Blocks nothing else; the form's own state stays
   * authoritative for its data.
   */
  markClean: () => void;
};

let saveCounter = 0;
const defaultTitle = 'فرم';
const defaultDirtyMessage = 'این فرم تغییرات ذخیره‌نشده دارد.';

/**
 * Declares this form's lifecycle status to the workspace. The status is derived from the
 * form's own state (`saving` > `error` > `dirty` > `clean`); the registration follows it and
 * is removed when the form reaches `clean` or unmounts. `beforeunload` is armed exactly while
 * the form is not clean.
 */
export function useFormGuard(input: FormGuardInput): FormGuard {
  const tabKey = useContext(PaneTabKeyContext) ?? 'single-page';
  const { dirty, saving = false, errorMessage = null, title = defaultTitle, dirtyMessage = defaultDirtyMessage } = input;
  const id = useMemo(() => `form-${Math.random().toString(36).slice(2)}-${Date.now().toString(36)}`, []);

  const status: FormStatus = saving ? 'saving' : errorMessage ? 'error' : dirty ? 'dirty' : 'clean';
  const message = status === 'error' ? errorMessage || defaultDirtyMessage : status === 'dirty' ? dirtyMessage : null;

  useEffect(() => {
    if (status === 'clean') {
      if (registry.delete(id)) emit();
      return () => {
        if (registry.delete(id)) emit();
      };
    }
    const token = status === 'saving' ? `save-${(saveCounter += 1).toString(36)}` : registry.get(id)?.saveToken ?? null;
    const previous = registry.get(id);
    if (!(previous && previous.status === status && previous.message === message && previous.title === title && previous.tabKey === tabKey)) {
      registry.set(id, { id, tabKey, title, status, message, saveToken: token });
      emit();
    }
    // Unmount (and Strict Mode's unmount/remount cycle) removes exactly this form's record;
    // other forms on the same tab are never touched.
    return () => {
      if (registry.delete(id)) emit();
    };
  }, [status, message, title, tabKey, id]);

  // The browser warning is armed for every non-clean state (dirty, saving and error alike).
  useEffect(() => {
    if (status === 'clean') return undefined;
    const beforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = '';
    };
    window.addEventListener('beforeunload', beforeUnload);
    return () => window.removeEventListener('beforeunload', beforeUnload);
  }, [status]);

  return useMemo<FormGuard>(
    () => ({
      status,
      markClean: () => {
        // Declared clean (explicit cancel, or the confirmed-save moment before the page
        // unmounts). A save still running in the form's own state re-registers on its next
        // status change; the registry never invents a clean state by itself.
        if (registry.delete(id)) emit();
      },
    }),
    [status, id],
  );
}
