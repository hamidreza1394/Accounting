import type { MembershipRequestStatus } from '../../api/types';
import { StatusBadge } from '../ui/StatusBadge';

/** The status of a membership request: icon plus text, never colour alone. */
export function MembershipRequestStatusBadge({ status }: { status: MembershipRequestStatus }) {
  if (status === 'Approved') return <StatusBadge status="active">تأییدشده</StatusBadge>;
  if (status === 'Rejected') return <StatusBadge status="rejected">ردشده</StatusBadge>;
  return <StatusBadge status="pending">در انتظار تأیید</StatusBadge>;
}
