/**
 * The lightweight Persian tooltip of the workspace navigation (Issue #444 stage 1 completion).
 * No new dependency: hover AND keyboard focus open it, Escape dismisses it, and it renders in
 * a portal with fixed positioning so the sidebar's scroll never clips it. RTL-correct by
 * construction — it always opens toward the page content (inline-end of the trigger).
 */
import { useCallback, useEffect, useId, useRef, useState, type ReactNode } from 'react';
import { createPortal } from 'react-dom';

export type TooltipTriggerProps = {
  onMouseEnter: () => void;
  onMouseLeave: () => void;
  onFocus: () => void;
  onBlur: () => void;
  ref: (node: HTMLElement | null) => void;
  'aria-describedby'?: string;
};

export function Tooltip({ label, children }: { label: string; children: (triggerProps: TooltipTriggerProps) => ReactNode }) {
  const [open, setOpen] = useState(false);
  const [position, setPosition] = useState<{ top: number; left: number } | null>(null);
  const triggerRef = useRef<HTMLElement | null>(null);
  const tooltipId = useId();

  const show = useCallback(() => {
    const trigger = triggerRef.current;
    if (!trigger) return;
    const rect = trigger.getBoundingClientRect();
    setPosition({ top: Math.max(8, rect.top + rect.height / 2), left: Math.max(8, rect.left - 10) });
    setOpen(true);
  }, []);
  const hide = useCallback(() => setOpen(false), []);

  // Escape dismisses wherever the focus is; moving the focus away hides it as well.
  useEffect(() => {
    if (!open) return undefined;
    const onKeyDown = (event: globalThis.KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.stopPropagation();
        hide();
      }
    };
    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  }, [open, hide]);

  return (
    <>
      {children({
        onMouseEnter: show,
        onMouseLeave: hide,
        onFocus: show,
        onBlur: hide,
        ref: (node: HTMLElement | null) => {
          triggerRef.current = node;
        },
        ...(open ? { 'aria-describedby': tooltipId } : {}),
      })}
      {open && position
        ? createPortal(
            <span id={tooltipId} role="tooltip" style={{ top: position.top, left: position.left }} className="faranou-tooltip">
              {label}
            </span>,
            document.body,
          )
        : null}
    </>
  );
}
