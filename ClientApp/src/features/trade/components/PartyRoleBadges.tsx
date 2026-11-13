import { Badge } from '@/components/ui/Badge';
import { partyRoles } from '../api/tradeTypes';
import { roleLabel } from '../lib/tradeLabels';

/** The roles of a party as neutral badges, in the enum order (never colour-coded: the label is the meaning). */
export function PartyRoleBadges({ roles }: { roles: readonly string[] }) {
  const ordered = [...roles].sort((left, right) => {
    const a = (partyRoles as readonly string[]).indexOf(left);
    const b = (partyRoles as readonly string[]).indexOf(right);
    return (a < 0 ? 99 : a) - (b < 0 ? 99 : b);
  });
  if (ordered.length === 0) return <span className="text-sub">—</span>;
  return (
    <ul className="flex flex-wrap gap-1.5" aria-label="نقش‌ها">
      {ordered.map((role) => (
        <li key={role}>
          <Badge tone="info" icon={null}>
            {roleLabel(role)}
          </Badge>
        </li>
      ))}
    </ul>
  );
}
