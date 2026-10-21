import type { ReactNode } from 'react';
import { CheckIcon, InfoIcon, LockIcon } from './icons';

type Tone = 'success' | 'warning' | 'neutral' | 'info';

const tones: Record<Tone, string> = {
  success: 'border-ws-ok/30 bg-ws-ok-soft text-ws-ok',
  warning: 'border-ws-warn/30 bg-ws-warn-soft text-ws-warn',
  neutral: 'border-slate-300 bg-ws-off-soft text-ws-off',
  info: 'border-ws-blue/25 bg-ws-blue-soft text-ws-blue-deep',
};

const defaultIcons: Record<Tone, ReactNode> = {
  success: <CheckIcon width={14} height={14} />,
  warning: <LockIcon width={14} height={14} />,
  neutral: <LockIcon width={14} height={14} />,
  info: <InfoIcon width={14} height={14} />,
};

/**
 * A status label. The meaning is always in the text and in the icon shape; the colour only
 * reinforces it, so the status is not conveyed by colour alone. Pass `icon={null}` for none.
 */
export function Badge({ tone, icon, children }: { tone: Tone; icon?: ReactNode; children: ReactNode }) {
  return (
    <span
      className={`inline-flex items-center gap-1.5 whitespace-nowrap rounded-md border px-2.5 py-0.5 text-xs font-semibold ${tones[tone]}`}
    >
      {icon === undefined ? defaultIcons[tone] : icon}
      {children}
    </span>
  );
}
