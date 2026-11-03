import { memo, useCallback, useEffect, useId, useMemo, useRef, useState, type KeyboardEvent } from 'react';
import { ChevronDown, Search, X } from 'lucide-react';
import { cn } from '@/lib/utils';
import {
  buildPermissionGroups,
  checkStateOf,
  filterPermissions,
  operationLabels,
  operationOf,
  subsystemLabel,
  type CheckState,
  type Operation,
  type PermissionItem,
} from '../../lib/permissionTree';
import { Badge, type BadgeTone } from './Badge';
import { Input } from './FormField';
import { Button } from './button';

export type PermissionTreeProps = {
  /** The whole catalog (or any list of permissions) to choose from. */
  permissions: readonly PermissionItem[];
  /** The selected permission codes. */
  value: readonly string[];
  /** Receives the complete next selection (catalog order; codes unknown to `permissions` are kept). */
  onChange?: (next: string[]) => void;
  /** Shows the selection without allowing changes (e.g. a system role). */
  readOnly?: boolean;
  /** Locks the selection while a request is running. */
  disabled?: boolean;
  /** Accessible name of the whole control. */
  label?: string;
};

const searchDebounceMs = 150;
/** Up to this many permissions every group starts open; larger catalogs start collapsed. */
const openAllLimit = 40;
const number = new Intl.NumberFormat('fa-IR');

const operationTones: Record<Operation, BadgeTone> = {
  view: 'neutral',
  list: 'neutral',
  create: 'info',
  update: 'info',
  delete: 'danger',
  manage: 'warning',
};

/** A checkbox with a mixed state: native `indeterminate` plus `aria-checked="mixed"`. */
function TriStateCheckbox({
  state,
  label,
  disabled,
  readOnly,
  onToggle,
}: {
  state: CheckState;
  label: string;
  disabled: boolean;
  readOnly: boolean;
  /** `true` selects everything in the group, `false` clears it. */
  onToggle: (select: boolean) => void;
}) {
  const ref = useRef<HTMLInputElement>(null);
  useEffect(() => {
    if (ref.current) ref.current.indeterminate = state === 'mixed';
  }, [state]);
  return (
    <label className={cn('flex size-11 shrink-0 items-center justify-center sm:size-9', disabled ? 'cursor-not-allowed' : 'cursor-pointer')}>
      <input
        ref={ref}
        type="checkbox"
        checked={state === 'checked'}
        aria-label={label}
        disabled={disabled}
        aria-readonly={readOnly || undefined}
        onChange={() => {
          if (!readOnly) onToggle(state !== 'checked');
        }}
        className="size-5 accent-teal-deep outline-none focus-visible:ring-2 focus-visible:ring-gold disabled:cursor-not-allowed"
      />
    </label>
  );
}

const PermissionRow = memo(function PermissionRow({
  item,
  checked,
  disabled,
  readOnly,
  onToggle,
}: {
  item: PermissionItem;
  checked: boolean;
  disabled: boolean;
  readOnly: boolean;
  onToggle: (code: string, select: boolean) => void;
}) {
  const id = useId();
  const operation = operationOf(item.code);
  return (
    <li>
      <label className={cn('flex min-h-11 items-start gap-3 rounded-lg px-2 py-2 sm:min-h-9', disabled || readOnly ? 'cursor-default' : 'cursor-pointer hover:bg-cool/60')}>
        <input
          type="checkbox"
          checked={checked}
          disabled={disabled}
          aria-readonly={readOnly || undefined}
          aria-labelledby={`${id}-name`}
          aria-describedby={item.description ? `${id}-code ${id}-desc` : `${id}-code`}
          onChange={(event) => {
            if (!readOnly) onToggle(item.code, event.target.checked);
          }}
          className="mt-0.5 size-5 shrink-0 accent-teal-deep outline-none focus-visible:ring-2 focus-visible:ring-gold disabled:cursor-not-allowed"
        />
        <span className="min-w-0 flex-1 space-y-1">
          <span className="flex flex-wrap items-center gap-2">
            <span id={`${id}-name`} className="text-sm font-semibold text-ink">
              {item.name}
            </span>
            {operation ? (
              <Badge tone={operationTones[operation]} icon={null}>
                {operationLabels[operation]}
              </Badge>
            ) : null}
          </span>
          <span id={`${id}-code`} dir="ltr" className="block break-all text-start text-xs text-sub">
            {item.code}
          </span>
          {item.description ? (
            <span id={`${id}-desc`} className="block text-xs leading-5 text-sub">
              {item.description}
            </span>
          ) : null}
        </span>
      </label>
    </li>
  );
});

/**
 * Chooses permissions from the catalog. Permissions are grouped by subsystem and then by resource
 * (both derived from the dotted code, UI-derived only). It is a grouped checkbox list built from
 * native controls: Tab and Space work as for any checkbox, group headers are disclosure buttons
 * (Enter/Space) and ArrowUp/ArrowDown/Home/End move between them. Parent checkboxes are
 * tri-state. While a search is active, parent checkboxes and bulk actions act only on the
 * permissions that match it.
 */
export function PermissionTree({ permissions, value, onChange, readOnly = false, disabled = false, label = 'مجوزها' }: PermissionTreeProps) {
  const searchId = useId();
  const rootRef = useRef<HTMLDivElement>(null);
  const [searchInput, setSearchInput] = useState('');
  const [query, setQuery] = useState('');
  const [expanded, setExpanded] = useState<ReadonlySet<string>>(() =>
    permissions.length <= openAllLimit ? new Set(buildPermissionGroups(permissions).map((group) => group.key)) : new Set(),
  );

  // Debounced so typing does not re-filter ~200 rows on every keystroke.
  useEffect(() => {
    const timer = window.setTimeout(() => setQuery(searchInput), searchDebounceMs);
    return () => window.clearTimeout(timer);
  }, [searchInput]);

  const locked = readOnly || disabled;
  const selected = useMemo(() => new Set(value), [value]);
  const searching = query.trim() !== '';
  const unknownCodes = useMemo(() => {
    const known = new Set(permissions.map((item) => item.code));
    return value.filter((code) => !known.has(code));
  }, [permissions, value]);
  const visible = useMemo(() => filterPermissions(permissions, query), [permissions, query]);
  const groups = useMemo(() => buildPermissionGroups(visible), [visible]);

  // A search opens the groups that contain matches, so results are never hidden in a closed group.
  useEffect(() => {
    if (!searching) return;
    setExpanded((current) => {
      const next = new Set(current);
      for (const group of groups) next.add(group.key);
      return next.size === current.size ? current : next;
    });
  }, [groups, searching]);

  const latest = useRef({ value, onChange, permissions, selected });
  latest.current = { value, onChange, permissions, selected };

  const emit = useCallback((next: ReadonlySet<string>) => {
    const { onChange: notify, permissions: all, value: current } = latest.current;
    if (!notify) return;
    const known = new Set(all.map((item) => item.code));
    notify([...all.filter((item) => next.has(item.code)).map((item) => item.code), ...current.filter((code) => !known.has(code) && next.has(code))]);
  }, []);

  const setMany = useCallback(
    (codes: readonly string[], select: boolean) => {
      const next = new Set(latest.current.selected);
      for (const code of codes) {
        if (select) next.add(code);
        else next.delete(code);
      }
      emit(next);
    },
    [emit],
  );

  const toggleOne = useCallback((code: string, select: boolean) => setMany([code], select), [setMany]);

  const visibleCodes = useMemo(() => visible.map((item) => item.code), [visible]);
  const selectedCount = useMemo(() => permissions.filter((item) => selected.has(item.code)).length, [permissions, selected]);
  const visibleSelectedCount = useMemo(() => visible.filter((item) => selected.has(item.code)).length, [visible, selected]);

  const toggleGroup = (key: string) =>
    setExpanded((current) => {
      const next = new Set(current);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });

  const onToggleKeyDown = (event: KeyboardEvent<HTMLElement>) => {
    const keys = ['ArrowDown', 'ArrowUp', 'Home', 'End'];
    if (!keys.includes(event.key) || !(event.target instanceof HTMLElement) || !event.target.hasAttribute('data-group-toggle')) return;
    const buttons = Array.from(rootRef.current?.querySelectorAll<HTMLElement>('[data-group-toggle]') ?? []);
    const index = buttons.indexOf(event.target);
    if (index < 0) return;
    event.preventDefault();
    const target =
      event.key === 'Home' ? 0 : event.key === 'End' ? buttons.length - 1 : event.key === 'ArrowDown' ? Math.min(buttons.length - 1, index + 1) : Math.max(0, index - 1);
    buttons[target]?.focus();
  };

  const summaryId = `${searchId}-summary`;

  return (
    <div ref={rootRef} role="group" aria-label={label} onKeyDown={onToggleKeyDown} className="space-y-3">
      <div className="flex flex-wrap items-end gap-3">
        <div className="flex min-w-60 flex-1 flex-col gap-1.5">
          <label htmlFor={searchId} className="text-sm font-semibold text-ink">
            جستجوی مجوزها
          </label>
          <div className="relative">
            <Search aria-hidden="true" size={16} className="pointer-events-none absolute start-3 top-1/2 -translate-y-1/2 text-muted" />
            <Input
              id={searchId}
              type="search"
              value={searchInput}
              onChange={(event) => setSearchInput(event.target.value)}
              placeholder="کد، عنوان یا توضیح…"
              autoComplete="off"
              aria-describedby={summaryId}
              className="h-11 ps-9 pe-11 sm:h-10 [&::-webkit-search-cancel-button]:appearance-none"
            />
            {searchInput ? (
              <button
                type="button"
                aria-label="پاک‌کردن جستجوی مجوزها"
                onClick={() => {
                  setSearchInput('');
                  setQuery('');
                }}
                className="absolute inset-y-0 end-0 flex w-11 items-center justify-center rounded-e-lg text-sub outline-none hover:text-ink focus-visible:ring-2 focus-visible:ring-gold"
              >
                <X aria-hidden="true" size={16} />
              </button>
            ) : null}
          </div>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button type="button" variant="outline" size="lg" onClick={() => setExpanded(new Set(groups.map((group) => group.key)))}>
            بازکردن همه
          </Button>
          <Button type="button" variant="outline" size="lg" onClick={() => setExpanded(new Set())}>
            بستن همه
          </Button>
          <Button type="button" variant="outline" size="lg" disabled={locked || visibleCodes.length === 0} onClick={() => setMany(visibleCodes, true)}>
            {searching ? 'انتخاب نتایج' : 'انتخاب همه'}
          </Button>
          <Button type="button" variant="outline" size="lg" disabled={locked || visibleSelectedCount === 0} onClick={() => setMany(visibleCodes, false)}>
            {searching ? 'پاک‌کردن نتایج' : 'پاک‌کردن همه'}
          </Button>
        </div>
      </div>

      {/* Visible count: not a live region, so toggling a checkbox does not announce every change. */}
      <p id={summaryId} className="text-sm text-sub">
        {number.format(selectedCount)} از {number.format(permissions.length)} مجوز انتخاب شده است
        {searching ? ` — ${number.format(visible.length)} مجوز با جستجو مطابقت دارد.` : '.'}
      </p>
      {/* Only the number of search matches is announced. */}
      <p role="status" className="sr-only">
        {searching ? `${number.format(visible.length)} مجوز با جستجو مطابقت دارد.` : ''}
      </p>

      {readOnly ? <p className="text-xs text-sub">این فهرست فقط‌خواندنی است و قابل تغییر نیست.</p> : null}

      {unknownCodes.length > 0 && !searching ? (
        <section className="rounded-xl border border-gold/50 bg-warning-soft p-3">
          <h3 className="text-sm font-bold text-warning">مجوزهای ناشناخته ({number.format(unknownCodes.length)})</h3>
          <p className="mt-1 text-xs leading-5 text-sub">این کدها در کاتالوگ نیستند و ذخیرهٔ گروه را رد می‌کنند؛ برای ذخیره، تیک آن‌ها را بردارید.</p>
          <ul>
            {unknownCodes.map((code) => (
              <PermissionRow key={code} item={{ code, name: 'مجوز ناشناخته', description: null }} checked disabled={disabled} readOnly={readOnly} onToggle={toggleOne} />
            ))}
          </ul>
        </section>
      ) : null}

      {groups.length === 0 ? (
        <p className="rounded-xl border border-dashed border-line bg-white p-6 text-center text-sm text-muted">
          {permissions.length === 0 ? 'مجوزی در کاتالوگ ثبت نشده است.' : 'مجوزی با این جستجو پیدا نشد.'}
        </p>
      ) : (
        <ul className="space-y-2">
          {groups.map((group) => {
            const codes = group.resources.flatMap((resource) => resource.items.map((item) => item.code));
            const groupSelected = codes.filter((code) => selected.has(code)).length;
            const state = checkStateOf(groupSelected, codes.length);
            const open = expanded.has(group.key);
            const title = subsystemLabel(group.key) ?? group.key;
            const panelId = `${searchId}-panel-${group.key}`;
            return (
              <li key={group.key} className="rounded-xl border border-line bg-white">
                <div className="flex items-center gap-1 ps-2 pe-1">
                  <TriStateCheckbox state={state} label={`انتخاب همهٔ مجوزهای ${title}`} disabled={disabled} readOnly={readOnly} onToggle={(select) => setMany(codes, select)} />
                  <button
                    type="button"
                    data-group-toggle=""
                    aria-expanded={open}
                    aria-controls={open ? panelId : undefined}
                    onClick={() => toggleGroup(group.key)}
                    className="flex min-h-11 min-w-0 flex-1 items-center justify-between gap-2 rounded-lg px-2 text-start outline-none focus-visible:ring-2 focus-visible:ring-gold sm:min-h-10"
                  >
                    <span className="min-w-0 space-y-0.5">
                      <span className="block text-sm font-bold text-ink">{title}</span>
                      {subsystemLabel(group.key) ? (
                        <span dir="ltr" className="block text-start text-xs text-sub">
                          {group.key}
                        </span>
                      ) : null}
                    </span>
                    <span className="flex shrink-0 items-center gap-2 text-xs text-sub">
                      {number.format(groupSelected)} از {number.format(codes.length)}{searching ? ' نتیجه' : ''}
                      <ChevronDown aria-hidden="true" size={16} className={cn('transition-transform', open && 'rotate-180')} />
                    </span>
                  </button>
                </div>
                {open ? (
                  <div id={panelId} className="space-y-3 border-t border-line px-3 py-3">
                    {group.resources.map((resource) => {
                      const resourceCodes = resource.items.map((item) => item.code);
                      const resourceSelected = resourceCodes.filter((code) => selected.has(code)).length;
                      return (
                        <div key={resource.key} role="group" aria-label={resource.resource || `${title} — عمومی`}>
                          {resource.resource ? (
                            <div className="flex items-center gap-1">
                              <TriStateCheckbox
                                state={checkStateOf(resourceSelected, resourceCodes.length)}
                                label={`انتخاب همهٔ مجوزهای ${resource.resource}`}
                                disabled={disabled}
                                readOnly={readOnly}
                                onToggle={(select) => setMany(resourceCodes, select)}
                              />
                              <h3 dir="ltr" className="text-start text-sm font-semibold text-title">
                                {resource.resource}
                              </h3>
                              <span className="ms-auto text-xs text-sub">
                                {number.format(resourceSelected)} از {number.format(resourceCodes.length)}
                              </span>
                            </div>
                          ) : null}
                          <ul className={cn(resource.resource && 'ms-4 border-s border-line ps-2')}>
                            {resource.items.map((item) => (
                              <PermissionRow key={item.code} item={item} checked={selected.has(item.code)} disabled={disabled} readOnly={readOnly} onToggle={toggleOne} />
                            ))}
                          </ul>
                        </div>
                      );
                    })}
                  </div>
                ) : null}
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
