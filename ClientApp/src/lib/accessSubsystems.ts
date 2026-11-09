import type { AccessSubsystem, PermissionCatalogItem } from '../api/types';

/**
 * PRESENTATION-ONLY mapping between the permission codes the server registers and the subsystems of the
 * D-075 access catalog (docs/SUBSYSTEM_CATALOG.md section 2, column «کدهای مجوز (پیشوند)»).
 *
 * The server has NO per-company subsystem flag and no endpoint that links a permission to a subsystem, so
 * this prefix table is the only bridge. It is never sent to the server and it grants nothing: a subsystem
 * counts as «enabled» for a role only because the role really holds permission codes of that prefix. An
 * unknown prefix is kept as its own entry (titled by the raw code) instead of being dropped or guessed.
 */
const prefixToSubsystem: Readonly<Record<string, string>> = {
  hr: 'hr',
  roles: 'access-admin',
  users: 'membership',
  trade: 'TRADE_IMPORT',
  'corporate-groups': 'corporate-groups',
  consolidation: 'corporate-groups',
  reporting: 'reporting',
};

export function subsystemCodeOfPermission(code: string): string {
  const prefix = code.split('.')[0] ?? code;
  return prefixToSubsystem[prefix] ?? prefix;
}

/** One subsystem row: its catalog data (when the catalog was readable) and the REAL permissions that belong to it. */
export type SubsystemEntry = {
  code: string;
  title: string;
  /** `none | installation | company` from the catalog; `null` when the catalog is not available. */
  entitlementScope: string | null;
  permissions: PermissionCatalogItem[];
};

const byCode = (a: { code: string }, b: { code: string }): number => (a.code < b.code ? -1 : a.code > b.code ? 1 : 0);

/**
 * Joins the subsystem catalog (installation administrators only; `null` = not readable) with the real
 * permission catalog. Subsystems without permission codes stay listed (honestly empty). Permissions whose
 * subsystem is not in the catalog get their own entry titled by the raw code.
 */
export function buildSubsystemEntries(subsystems: readonly AccessSubsystem[] | null, permissions: readonly PermissionCatalogItem[]): SubsystemEntry[] {
  const entries = new Map<string, SubsystemEntry>();
  for (const subsystem of subsystems ?? []) {
    entries.set(subsystem.code, { code: subsystem.code, title: subsystem.title, entitlementScope: subsystem.entitlementScope, permissions: [] });
  }
  for (const permission of [...permissions].sort(byCode)) {
    const code = subsystemCodeOfPermission(permission.code);
    let entry = entries.get(code);
    if (!entry) {
      entry = { code, title: code, entitlementScope: null, permissions: [] };
      entries.set(code, entry);
    }
    entry.permissions.push(permission);
  }
  return [...entries.values()];
}

export function isReadCode(code: string): boolean {
  const last = code.split('.').pop();
  return last === 'view' || last === 'list';
}

/** A subsystem is «enabled» for a role exactly when the role holds at least one of its permission codes. */
export function isSubsystemEnabled(entry: SubsystemEntry, selection: ReadonlySet<string>): boolean {
  return entry.permissions.some((permission) => selection.has(permission.code));
}

export function heldCodes(entry: SubsystemEntry, selection: ReadonlySet<string>): string[] {
  return entry.permissions.filter((permission) => selection.has(permission.code)).map((permission) => permission.code);
}

/** Read-only (`view`/`list`) codes: what re-enabling a subsystem grants. Earlier form-level choices cannot be restored. */
export function readCodesOf(entry: SubsystemEntry): string[] {
  return entry.permissions.filter((permission) => isReadCode(permission.code)).map((permission) => permission.code);
}

/** Removes every code of the subsystem and keeps all others (also codes unknown to the catalog). */
export function disableSubsystem(selection: readonly string[], entry: SubsystemEntry): string[] {
  const own = new Set(entry.permissions.map((permission) => permission.code));
  return selection.filter((code) => !own.has(code));
}

/** Adds the read-only codes of the subsystem to the selection. */
export function enableSubsystem(selection: readonly string[], entry: SubsystemEntry): string[] {
  const next = new Set(selection);
  for (const code of readCodesOf(entry)) next.add(code);
  return [...next];
}

// --- the form/operation table of one subsystem ---

const verbOrder = ['list', 'view', 'create', 'update', 'delete', 'approve', 'close', 'export', 'manage', 'run', 'assign'] as const;
export const otherVerb = 'other';

export const verbLabels: Readonly<Record<string, string>> = {
  list: 'فهرست',
  view: 'مشاهده',
  create: 'ایجاد',
  update: 'ویرایش',
  delete: 'حذف',
  approve: 'تأیید',
  close: 'بستن',
  export: 'خروجی',
  manage: 'مدیریت',
  run: 'اجرا',
  assign: 'تخصیص',
  [otherVerb]: 'سایر',
};

export function verbOf(code: string): string {
  const segments = code.split('.');
  const last = segments.length >= 2 ? segments[segments.length - 1] : undefined;
  return last !== undefined && (verbOrder as readonly string[]).includes(last) ? last : otherVerb;
}

/** The segments between the subsystem prefix and the last one; empty for two-segment codes. */
export function resourceOfCode(code: string): string {
  const segments = code.split('.');
  return segments.length >= 3 ? segments.slice(1, -1).join('.') : '';
}

export type FormRow = { resource: string; cells: Readonly<Record<string, PermissionCatalogItem[]>> };
export type FormTable = { verbs: string[]; rows: FormRow[] };

/** Rows = resources (forms) derived from the real codes, columns = the operations that really exist in this subsystem. */
export function buildFormTable(permissions: readonly PermissionCatalogItem[]): FormTable {
  const rows = new Map<string, Record<string, PermissionCatalogItem[]>>();
  const present = new Set<string>();
  for (const permission of [...permissions].sort(byCode)) {
    const resource = resourceOfCode(permission.code);
    const verb = verbOf(permission.code);
    present.add(verb);
    const row = rows.get(resource) ?? {};
    (row[verb] ??= []).push(permission);
    rows.set(resource, row);
  }
  const verbs = [...verbOrder, otherVerb].filter((verb) => present.has(verb));
  return {
    verbs,
    rows: [...rows.entries()].sort(([a], [b]) => (a < b ? -1 : a > b ? 1 : 0)).map(([resource, cells]) => ({ resource, cells })),
  };
}
