import { CircleAlert, LoaderCircle, PencilLine } from 'lucide-react';
import { formStatusLabel, problemForms, useFormRegistryVersion, worstFormStatus, type FormStatus } from '@/lib/formGuard';
import { routeIconOf } from '@/lib/routeIcons';
import { tabLabel } from '@/lib/workspaceTabs';
import { ConfirmDialog } from '@/components/ui/ConfirmDialog';
import { useWorkspaceTabs } from './WorkspaceTabs';

/**
 * The Persian dialog that names EVERY workspace tab holding an unresolved form (dirty,
 * saving or failed) and lets the user jump straight to it — the tab activates with its own
 * query string, scroll and form state intact (Issue #444 stage 1 completion; D-076 item 9).
 * Must be rendered inside `WorkspaceTabsProvider` so it can resolve the tab titles and the
 * caller's `onGoToTab` can navigate through the shared store.
 */

const faNumber = new Intl.NumberFormat('fa-IR');

type ProblemTabGroup = {
  tabKey: string;
  status: FormStatus;
  /** One Persian message per problem form, worst status first. */
  messages: readonly string[];
  /** How many unresolved forms this tab holds (the title is shown only once per tab). */
  count: number;
};

/** Groups every unresolved form by its workspace tab, worst status first. */
export function groupProblemTabs(): readonly ProblemTabGroup[] {
  const byTab = new Map<string, ProblemTabGroup>();
  for (const form of problemForms()) {
    const group = byTab.get(form.tabKey) ?? { tabKey: form.tabKey, status: 'clean', messages: [], count: 0 };
    group.status = worstFormStatus(group.status, form.status);
    if (form.message) group.messages = [...group.messages, form.message];
    group.count += 1;
    byTab.set(form.tabKey, group);
  }
  return [...byTab.values()].sort((left, right) => {
    const rank = (status: FormStatus) => (status === 'error' ? 3 : status === 'saving' ? 2 : 1);
    return rank(right.status) - rank(left.status);
  });
}

function statusIcon(status: FormStatus) {
  if (status === 'error') return <CircleAlert size={15} aria-hidden="true" className="faranou-problemtab-statusicon faranou-problemtab-statusicon--error" />;
  if (status === 'saving') return <LoaderCircle size={15} aria-hidden="true" className="faranou-problemtab-statusicon faranou-problemtab-statusicon--saving" />;
  return <PencilLine size={15} aria-hidden="true" className="faranou-problemtab-statusicon faranou-problemtab-statusicon--dirty" />;
}

export type ProblemTabsDialogProps = {
  open: boolean;
  /** Persian headline, e.g. «تغییر زمینهٔ کاری ممکن نیست». */
  title: string;
  /** Persian intro above the list. */
  description: string;
  closeLabel?: string;
  /** Closes the dialog without doing anything (nothing must change on cancel). */
  onClose: () => void;
  /** Optional observer after a tab was activated through the shared store (tests, telemetry). */
  onGoToTab?: (tabKey: string) => void;
};

export function ProblemTabsDialog({ open, title, description, closeLabel = 'انصراف', onClose, onGoToTab }: ProblemTabsDialogProps) {
  useFormRegistryVersion();
  const { state, activate } = useWorkspaceTabs();
  if (!open) return null;

  const groups = groupProblemTabs();
  const labelOf = (tabKey: string): string => {
    const tab = state.tabs.find((entry) => entry.key === tabKey);
    return tab ? tabLabel(tab) : 'صفحهٔ باز';
  };

  // Activating through the store keeps the tab's own query string, scroll and mounted form
  // state; the pane's focus effect then moves focus to the page heading.
  const goTo = (tabKey: string) => {
    const tab = state.tabs.find((entry) => entry.key === tabKey);
    if (tab) activate(tab);
    onGoToTab?.(tabKey);
    onClose();
  };

  const list =
    groups.length > 0 ? (
      <ul className="faranou-problemtabs">
        {groups.map((group) => {
          const label = labelOf(group.tabKey);
          const Icon = routeIconOf(group.tabKey);
          return (
            <li key={group.tabKey} className="faranou-problemtab-row">
              <button type="button" className="faranou-problemtab" onClick={() => goTo(group.tabKey)} aria-label={`رفتن به تب ${label}`}>
                <Icon size={15} aria-hidden="true" className="faranou-problemtab-routeicon" />
                <span className="faranou-problemtab-title">{label}</span>
                {statusIcon(group.status)}
                <span className={'faranou-problemtab-status' + (group.status === 'error' ? ' faranou-problemtab-status--error' : '')}>
                  {formStatusLabel[group.status]}
                </span>
                {group.count > 1 ? <span className="faranou-problemtab-count">{`${faNumber.format(group.count)} فرم`}</span> : null}
              </button>
              {group.messages.length > 0 ? <p className="faranou-problemtab-message">{group.messages[0]}</p> : null}
            </li>
          );
        })}
      </ul>
    ) : null;

  return (
    <ConfirmDialog
      open={open}
      title={title}
      description={
        <>
          {description}
          {list}
        </>
      }
      confirmVisible={false}
      cancelLabel={closeLabel}
      onConfirm={onClose}
      onCancel={onClose}
    />
  );
}
