import { useEffect, useRef } from 'react';
import { CircleAlert, LoaderCircle } from 'lucide-react';
import { formStatusLabel, tabStatus, useFormRegistryVersion, type FormStatus } from '@/lib/formGuard';
import { routeIconOf } from '@/lib/routeIcons';
import { tabLabel, type WorkspaceTab } from '@/lib/workspaceTabs';

/**
 * One tab entry of the strip: route icon + Persian label + a lifecycle status marker that is
 * never colour-only (icon + accessible Persian text), and the independent close button.
 */

function splitPathname(tabKey: string): string {
  const queryIndex = tabKey.indexOf('?');
  return queryIndex === -1 ? tabKey : tabKey.slice(0, queryIndex);
}

export function TabStatusMarker({ status }: { status: FormStatus }) {
  if (status === 'clean') return null;
  if (status === 'dirty') {
    return (
      <span className="faranou-tab-dot" aria-hidden="true" />
    );
  }
  if (status === 'saving') {
    return <LoaderCircle size={12} aria-hidden="true" className="faranou-tab-saving" />;
  }
  return <CircleAlert size={13} aria-hidden="true" className="faranou-tab-erroricon" />;
}

/** Visible and screen-reader text of the status; empty for `clean`. */
export function tabStatusText(status: FormStatus): string {
  return status === 'clean' ? '' : formStatusLabel[status];
}

export function WorkspaceTabEntry({
  tab,
  isActive,
  onActivate,
}: {
  tab: WorkspaceTab;
  isActive: boolean;
  onActivate: () => void;
}) {
  // The registry is a module store: subscribing keeps the marker live while forms change.
  useFormRegistryVersion();
  const status = tabStatus(tab.key);
  const statusText = tabStatusText(status);
  const Icon = routeIconOf(splitPathname(tab.key));
  const buttonRef = useRef<HTMLButtonElement>(null);

  // With many tabs the active one must stay visible inside the horizontal strip.
  useEffect(() => {
    if (!isActive) return;
    // jsdom (tests) does not implement scrollIntoView; real browsers always do.
    if (typeof buttonRef.current?.scrollIntoView !== 'function') return;
    buttonRef.current.scrollIntoView({ inline: 'nearest', block: 'nearest' });
  }, [isActive]);

  const tooltip = statusText ? `${tabLabel(tab)} — ${statusText}` : tabLabel(tab);
  return (
    <button
      ref={buttonRef}
      type="button"
      aria-current={isActive ? 'page' : undefined}
      title={tooltip}
      className="faranou-tab-button"
      style={{
        backgroundColor: isActive ? 'var(--color-navy)' : 'var(--color-period)',
        color: isActive ? 'var(--color-primary-foreground)' : 'var(--color-sub)',
        borderRadius: "0.45rem",
      }}
      onClick={onActivate}
    >
      <Icon size={14} aria-hidden="true" className="faranou-tab-icon" />
      <span className="faranou-tab-label">{tabLabel(tab)}</span>
      {statusText ? <span className="sr-only">{`، ${statusText}`}</span> : null}
      <TabStatusMarker status={status} />
    </button>
  );
}
