import type { ReactNode } from 'react';
import { Ban, CircleCheck, Clock, Eye, XCircle } from 'lucide-react';
import { Badge, type BadgeTone } from './Badge';

export type Status = 'active' | 'inactive' | 'pending' | 'rejected' | 'readonly';

const statuses: Record<Status, { tone: BadgeTone; label: string; icon: ReactNode }> = {
  active: { tone: 'success', label: 'فعال', icon: <CircleCheck size={14} /> },
  inactive: { tone: 'neutral', label: 'غیرفعال', icon: <Ban size={14} /> },
  pending: { tone: 'warning', label: 'در انتظار', icon: <Clock size={14} /> },
  rejected: { tone: 'danger', label: 'ردشده', icon: <XCircle size={14} /> },
  readonly: { tone: 'info', label: 'فقط‌خواندنی', icon: <Eye size={14} /> },
};

/** A status label: always icon plus text, never colour alone. `children` may replace the default text. */
export function StatusBadge({ status, children }: { status: Status; children?: ReactNode }) {
  const { tone, label, icon } = statuses[status];
  return (
    <Badge tone={tone} icon={icon}>
      {children ?? label}
    </Badge>
  );
}
