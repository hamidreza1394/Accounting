import { Badge } from '@/components/ui/Badge';
import { Ban, CircleCheck, CirclePause } from 'lucide-react';
import type { ReactNode } from 'react';
import type { HrAssignmentStatus, HrEmploymentStatus } from '../api/hrTypes';
import { hrAssignmentStatusLabel, hrEmploymentStatusLabel } from '../lib/hrLabels';

/**
 * Status chips of the HR feature. Meaning is always in the text and icon shape (never colour
 * alone); the tones reuse the shared Badge tokens.
 */

const employmentTone: Record<HrEmploymentStatus, 'success' | 'warning' | 'danger'> = {
  1: 'success',
  2: 'warning',
  3: 'danger',
};

const employmentIcon: Record<HrEmploymentStatus, ReactNode> = {
  1: <CircleCheck size={14} />,
  2: <CirclePause size={14} />,
  3: <Ban size={14} />,
};

export function HrEmploymentStatusBadge({ status }: { status: HrEmploymentStatus }) {
  return (
    <Badge tone={employmentTone[status]} icon={employmentIcon[status]}>
      {hrEmploymentStatusLabel[status]}
    </Badge>
  );
}

export function HrActiveBadge({ isActive }: { isActive: boolean }) {
  return (
    <Badge tone={isActive ? 'success' : 'neutral'} icon={isActive ? <CircleCheck size={14} /> : <Ban size={14} />}>
      {isActive ? 'فعال' : 'غیرفعال'}
    </Badge>
  );
}

const assignmentTone: Record<HrAssignmentStatus, 'success' | 'neutral'> = { 1: 'success', 2: 'neutral' };
const assignmentIcon: Record<HrAssignmentStatus, ReactNode> = {
  1: <CircleCheck size={14} />,
  2: <Ban size={14} />,
};

export function HrAssignmentStatusBadge({ status }: { status: HrAssignmentStatus }) {
  return (
    <Badge tone={assignmentTone[status]} icon={assignmentIcon[status]}>
      {hrAssignmentStatusLabel[status]}
    </Badge>
  );
}
