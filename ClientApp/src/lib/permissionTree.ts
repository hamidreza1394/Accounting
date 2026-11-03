import { normalizeSearchText } from './search';

/** The part of a catalog entry the tree needs. */
export type PermissionItem = { code: string; name: string; description: string | null };

/**
 * UI-DERIVED metadata. The server sends only `code`, `name`, `description` and `subsystemCode`;
 * the resource grouping and the operation badge below are derived here from the dotted code
 * (`<subsystem>.<resource...>.<operation>`) purely to make a long list scannable. They carry no
 * meaning for authorization and must never be sent to the server.
 */
export type Operation = 'view' | 'list' | 'create' | 'update' | 'delete' | 'manage';

export const operationLabels: Record<Operation, string> = {
  view: 'مشاهده',
  list: 'فهرست',
  create: 'ایجاد',
  update: 'ویرایش',
  delete: 'حذف',
  manage: 'مدیریت',
};

const operations = Object.keys(operationLabels) as Operation[];

/** Known subsystem names; an unknown subsystem is shown by its code. */
const subsystemLabels: Readonly<Record<string, string>> = {
  admin: 'مدیریت سیستم',
  trade: 'بازرگانی',
};

export function subsystemLabel(subsystemCode: string): string | null {
  return subsystemLabels[subsystemCode] ?? null;
}

export function subsystemOf(code: string): string {
  return code.split('.')[0] ?? code;
}

/** The segments between the subsystem and the last segment; empty for codes with fewer than 3 segments. */
export function resourceOf(code: string): string {
  const segments = code.split('.');
  return segments.length >= 3 ? segments.slice(1, -1).join('.') : '';
}

/** The last segment when it is one of the six common operations (and the code has 2+ segments). */
export function operationOf(code: string): Operation | null {
  const segments = code.split('.');
  if (segments.length < 2) return null;
  const last = segments[segments.length - 1];
  return operations.find((operation) => operation === last) ?? null;
}

export type PermissionResourceGroup = {
  /** Unique within the tree: `<subsystem>/<resource>`. */
  key: string;
  resource: string;
  items: readonly PermissionItem[];
};

export type PermissionSubsystemGroup = {
  key: string;
  count: number;
  resources: readonly PermissionResourceGroup[];
};

/** Groups items by subsystem and then by resource; both levels keep a stable, code-sorted order. */
export function buildPermissionGroups(items: readonly PermissionItem[]): PermissionSubsystemGroup[] {
  const bySubsystem = new Map<string, Map<string, PermissionItem[]>>();
  for (const item of items) {
    const subsystem = subsystemOf(item.code);
    const resource = resourceOf(item.code);
    let resources = bySubsystem.get(subsystem);
    if (!resources) {
      resources = new Map();
      bySubsystem.set(subsystem, resources);
    }
    const list = resources.get(resource);
    if (list) list.push(item);
    else resources.set(resource, [item]);
  }
  const compare = (a: string, b: string) => (a < b ? -1 : a > b ? 1 : 0);
  return [...bySubsystem.entries()]
    .sort(([a], [b]) => compare(a, b))
    .map(([subsystem, resources]) => ({
      key: subsystem,
      count: [...resources.values()].reduce((total, list) => total + list.length, 0),
      resources: [...resources.entries()]
        .sort(([a], [b]) => compare(a, b))
        .map(([resource, list]) => ({
          key: `${subsystem}/${resource}`,
          resource,
          items: [...list].sort((a, b) => compare(a.code, b.code)),
        })),
    }));
}

/** Items whose code, name or description contains every word of the query (Persian-normalized). */
export function filterPermissions<T extends PermissionItem>(items: readonly T[], query: string): readonly T[] {
  const words = normalizeSearchText(query).split(' ').filter(Boolean);
  if (words.length === 0) return items;
  return items.filter((item) => {
    const haystack = normalizeSearchText(`${item.code} ${item.name} ${item.description ?? ''}`);
    return words.every((word) => haystack.includes(word));
  });
}

export type CheckState = 'checked' | 'mixed' | 'unchecked';

export function checkStateOf(selectedCount: number, total: number): CheckState {
  if (total === 0 || selectedCount === 0) return 'unchecked';
  return selectedCount === total ? 'checked' : 'mixed';
}

/** Codes added to and removed from `before` to reach `after` (each sorted). */
export function diffCodes(before: readonly string[], after: readonly string[]): { added: string[]; removed: string[] } {
  const beforeSet = new Set(before);
  const afterSet = new Set(after);
  return {
    added: after.filter((code) => !beforeSet.has(code)).sort(),
    removed: before.filter((code) => !afterSet.has(code)).sort(),
  };
}
