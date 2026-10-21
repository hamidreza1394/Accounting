import type { ReactNode } from 'react';
import { CheckIcon, InfoIcon, LockIcon } from './icons';

type Tone = 'success' | 'warning' | 'neutral' | 'info';

const tones: Record<Tone, string> = {
  success: 'bg-success-soft text-success',
  warning: 'bg-warning-soft text-warning',
  neutral: 'bg-cool text-muted',
  info: 'bg-cool text-navy',
};

const icons: Record<Tone, ReactNode> = {
  success: <CheckIcon width={14} height={14} />,
  warning: <LockIcon width={14} height={14} />,
  neutral: <LockIcon width={14} height={14} />,
  info: <InfoIcon width={14} height={14} />,
};

/**
 * A status label. The meaning is always in the text and in the icon shape; the colour only
 * reinforces it, so the status is not conveyed by colour alone.
 */
export function Badge({ tone, children }: { tone: Tone; children: ReactNode }) {
  return (
    <span className={`inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-xs font-semibold ${tones[tone]}`}>
      {icons[tone]}
      {children}
    </span>
  );
}
