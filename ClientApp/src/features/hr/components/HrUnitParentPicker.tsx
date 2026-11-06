import { getHrUnitTree } from '../api/hrClient';
import type { HrUnitSubtreeNode } from '../api/hrTypes';
import { HrSearchSelect, type HrPickerOption } from './HrSearchSelect';

type TreeLike = {
  id: string;
  code: string;
  name: string;
  isActive: boolean;
  parentUnitId: string | null;
  validFromUtc: string | null;
  validToUtc: string | null;
  children: readonly TreeLike[];
};

function toSubtree(node: TreeLike): HrUnitSubtreeNode {
  return {
    id: node.id,
    code: node.code,
    name: node.name,
    isActive: node.isActive,
    parentUnitId: node.parentUnitId,
    activeAssignmentCount: 0,
    children: node.children.map(toSubtree),
    validFromUtc: node.validFromUtc,
    validToUtc: node.validToUtc,
  };
}

function flattenActive(
  nodes: readonly HrUnitSubtreeNode[],
  depth: number,
  excludeSubtreeId: string | null,
  out: HrPickerOption[],
): void {
  for (const node of nodes) {
    if (node.id === excludeSubtreeId) continue; // a unit can never be its own parent (or a descendant's)
    if (node.isActive) {
      out.push({ key: node.id, label: `${'\u2003'.repeat(depth)}${node.name}`, detail: node.code });
      flattenActive(node.children, depth + 1, excludeSubtreeId, out);
    }
  }
}

/**
 * The loader behind unit pickers: the active units of the company, indented by hierarchy, with an
 * optional self-subtree exclusion (editing a unit's parent). The full tree is one request; the
 * picker filters it client-side, matching the server's own tree read model.
 */
export function unitTreeLoader(
  companyId: string,
  excludeSubtreeId: string | null = null,
): (search: string, signal: AbortSignal) => Promise<readonly HrPickerOption[]> {
  return async (search: string, signal: AbortSignal) => {
    const tree = await getHrUnitTree(companyId, signal);
    const options: HrPickerOption[] = [];
    flattenActive(tree.roots.map(toSubtree), 0, excludeSubtreeId, options);
    const term = search.trim();
    return term === '' ? options : options.filter((option) => option.label.includes(term) || (option.detail ?? '').includes(term));
  };
}

type HrUnitParentPickerProps = {
  companyId: string;
  value: string | null;
  onValueChange: (key: string | null) => void;
  /** Excluded with its whole subtree (the unit being edited). */
  excludeUnitId?: string | null;
  disabled?: boolean;
};

/**
 * Parent-unit selector for the create/edit unit forms. The server accepts only ACTIVE parents of
 * the same company and rejects cycles; this picker already shows only active units and (when
 * editing) hides the unit's own subtree, so the guaranteed 409s never happen by accident.
 */
export function HrUnitParentPicker({ companyId, value, onValueChange, excludeUnitId = null, disabled = false }: HrUnitParentPickerProps) {
  return (
    <HrSearchSelect
      searchLabel="جستجوی واحد والد"
      value={value}
      onValueChange={onValueChange}
      load={unitTreeLoader(companyId, excludeUnitId)}
      placeholder="بدون والد (واحد ریشه)"
      clearLabel="بدون والد (ریشه)"
      emptyLabel="واحد فعالی پیدا نشد"
      disabled={disabled}
    />
  );
}
