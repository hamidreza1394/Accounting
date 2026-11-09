import { useId, useRef, type KeyboardEvent, type ReactNode } from 'react';
import { cn } from '@/lib/utils';

export type TabItem = { id: string; label: ReactNode; disabled?: boolean; content: ReactNode };

type TabsProps = {
  /** Accessible name of the tab list. */
  label: string;
  items: readonly TabItem[];
  value: string;
  onValueChange: (id: string) => void;
  className?: string;
};

/** WAI-ARIA tabs with automatic activation. In RTL, ArrowLeft moves to the next tab. */
export function Tabs({ label, items, value, onValueChange, className }: TabsProps) {
  const baseId = useId();
  const listRef = useRef<HTMLDivElement>(null);
  const enabled = items.filter((item) => !item.disabled);
  const active = items.find((item) => item.id === value) ?? enabled[0];
  const tabId = (id: string) => `${baseId}-tab-${id}`;
  const panelId = (id: string) => `${baseId}-panel-${id}`;

  const move = (event: KeyboardEvent<HTMLButtonElement>, currentId: string) => {
    const rtl = (listRef.current?.closest('[dir]')?.getAttribute('dir') ?? 'rtl') === 'rtl';
    const index = enabled.findIndex((item) => item.id === currentId);
    let nextIndex: number;
    switch (event.key) {
      case 'ArrowRight':
        nextIndex = index + (rtl ? -1 : 1);
        break;
      case 'ArrowLeft':
        nextIndex = index + (rtl ? 1 : -1);
        break;
      case 'Home':
        nextIndex = 0;
        break;
      case 'End':
        nextIndex = enabled.length - 1;
        break;
      default:
        return;
    }
    event.preventDefault();
    const next = enabled[(nextIndex + enabled.length) % enabled.length];
    if (!next) return;
    onValueChange(next.id);
    listRef.current?.querySelector<HTMLElement>(`[id="${tabId(next.id)}"]`)?.focus();
  };

  return (
    <div className={className}>
      <div ref={listRef} role="tablist" aria-label={label} className="flex gap-1 overflow-x-auto border-b border-line">
        {items.map((item) => {
          const selected = item.id === active?.id;
          return (
            <button
              key={item.id}
              id={tabId(item.id)}
              type="button"
              role="tab"
              aria-selected={selected}
              aria-controls={panelId(item.id)}
              disabled={item.disabled}
              tabIndex={selected ? 0 : -1}
              onClick={() => onValueChange(item.id)}
              onKeyDown={(event) => move(event, item.id)}
              className={cn(
                '-mb-px whitespace-nowrap border-b-2 px-4 py-2.5 text-sm font-semibold max-sm:min-h-11 max-sm:min-w-0 max-sm:flex-1 max-sm:basis-0 max-sm:whitespace-normal max-sm:px-1.5 max-sm:text-xs outline-none focus-visible:ring-2 focus-visible:ring-gold disabled:cursor-not-allowed disabled:opacity-50',
                selected ? 'border-gold bg-navy text-white shadow-sm' : 'border-transparent bg-cool text-sub hover:bg-period hover:text-navy',
              )}
            >
              {item.label}
            </button>
          );
        })}
      </div>
      {active ? (
        <div id={panelId(active.id)} role="tabpanel" aria-labelledby={tabId(active.id)} tabIndex={0} className="pt-4 outline-none focus-visible:ring-2 focus-visible:ring-gold">
          {active.content}
        </div>
      ) : null}
    </div>
  );
}
