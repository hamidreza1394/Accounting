import { useEffect, useId, useMemo, useRef, useState, type ReactNode } from 'react';
import { ApiError, previewCompanyRolePermissions, updateCompanyRole } from '../../../api/client';
import type { CompanyRole, PermissionCatalogItem, RolePermissionPreview } from '../../../api/types';
import {
  buildFormTable,
  buildSubsystemEntries,
  disableSubsystem,
  enableSubsystem,
  heldCodes,
  isSubsystemEnabled,
  readCodesOf,
  verbLabels,
  type SubsystemEntry,
} from '../../../lib/accessSubsystems';
import { diffCodes } from '../../../lib/permissionTree';
import { Alert } from '../../ui/Alert';
import { Badge } from '../../ui/Badge';
import { ConfirmDialog } from '../../ui/ConfirmDialog';
import { FormField, Select } from '../../ui/FormField';
import { Tabs } from '../../ui/Tabs';
import { Button } from '../../ui/button';
import { sameCodes } from '../roles/roleValidation';
import type { SubsystemCatalogState } from './useAccessSubsystems';

type Props = {
  role: CompanyRole;
  companyId: string;
  /** The real permission catalog (`GET /admin/permission-catalog`); a company manager only receives their own ceiling. */
  permissions: PermissionCatalogItem[];
  subsystems: SubsystemCatalogState;
  onRetrySubsystems: () => void;
  /** UI hint only (`roles.update`); the server decides. */
  canUpdate: boolean;
  isInstallationAdministrator: boolean;
  onUnauthenticated: (error: unknown) => boolean;
  onForbidden?: (() => void) | undefined;
  /** The save succeeded: the page reloads the roles (and the user's effective permissions). */
  onSaved: (savedCodes: string[]) => void;
  /** 409: the role changed meanwhile; the page reloads it so the editor shows the real state. */
  onReload: () => void;
  onDirtyChange: (dirty: boolean) => void;
};

const number = new Intl.NumberFormat('fa-IR');
const fallbackMessage = 'ذخیرهٔ مجوزهای نقش انجام نشد. لطفاً دوباره تلاش کنید.';

const entitlementTexts: Record<string, string> = {
  none: 'بدون قید لایسنس',
  installation: 'وابسته به لایسنس نصب',
  company: 'وابسته به لایسنس شرکت',
};

type ConfirmState = { preview: RolePermissionPreview | null; previewMessage: string | null };

function CodeList({ title, codes, names, tone }: { title: string; codes: readonly string[]; names: ReadonlyMap<string, string>; tone: 'add' | 'remove' }) {
  if (codes.length === 0) return null;
  return (
    <section className="mt-3">
      <h3 className={tone === 'add' ? 'text-sm font-bold text-success' : 'text-sm font-bold text-danger'}>
        {title} ({number.format(codes.length)})
      </h3>
      <ul tabIndex={0} aria-label={title} className="mt-1 max-h-40 space-y-1 overflow-y-auto rounded-lg border border-line p-2 outline-none focus-visible:ring-2 focus-visible:ring-gold">
        {codes.map((code) => (
          <li key={code} className="text-xs leading-5 text-ink">
            <span aria-hidden="true">{tone === 'add' ? '+ ' : '− '}</span>
            {names.get(code) ?? 'مجوز ناشناخته'} <bdi dir="ltr" className="text-sub">({code})</bdi>
          </li>
        ))}
      </ul>
    </section>
  );
}

/**
 * The two tabs of the access page for ONE role: «زیرسیستم‌ها» and «دسترسی فرم‌ها». Both edit the same draft
 * permission set; saving sends the full replacement through the existing `PUT roles/{roleId}` after a
 * server-computed impact preview. Nothing is stored in the browser and no permission engine exists here.
 */
export function AccessRoleEditor({
  role,
  companyId,
  permissions,
  subsystems,
  onRetrySubsystems,
  canUpdate,
  isInstallationAdministrator,
  onUnauthenticated,
  onForbidden,
  onSaved,
  onReload,
  onDirtyChange,
}: Props) {
  const alertRef = useRef<HTMLDivElement>(null);
  const busyRef = useRef(false);
  const doneRef = useRef(false);
  const selectId = useId();
  const [tab, setTab] = useState('subsystems');
  const [selection, setSelection] = useState<string[]>(role.permissionCodes);
  const [formsSubsystem, setFormsSubsystem] = useState('');
  const [pendingDisable, setPendingDisable] = useState<SubsystemEntry | null>(null);
  const [confirm, setConfirm] = useState<ConfirmState | null>(null);
  const [checking, setChecking] = useState(false);
  const [saving, setSaving] = useState(false);
  const [alertMessage, setAlertMessage] = useState<string | null>(null);
  const [conflict, setConflict] = useState(false);

  const catalogCodes = useMemo(() => new Set(permissions.map((item) => item.code)), [permissions]);
  const names = useMemo(() => new Map(permissions.map((item) => [item.code, item.name])), [permissions]);
  const outsideCodes = useMemo(() => role.permissionCodes.filter((code) => !catalogCodes.has(code)), [role.permissionCodes, catalogCodes]);
  // A company manager can only edit a role whose codes are all within their ceiling (D-071), otherwise the
  // full replacement would silently drop what they cannot see.
  const exceedsCeiling = !isInstallationAdministrator && outsideCodes.length > 0;
  const editable = canUpdate && !role.isSystemRole && !exceedsCeiling;
  const busy = checking || saving;

  const entries = useMemo(
    () => buildSubsystemEntries(subsystems.kind === 'ready' ? subsystems.subsystems : null, permissions),
    [subsystems, permissions],
  );
  const selected = useMemo(() => new Set(selection), [selection]);
  const dirty = editable && !sameCodes(role.permissionCodes, selection);

  useEffect(() => {
    onDirtyChange(dirty);
  }, [dirty, onDirtyChange]);
  useEffect(() => () => onDirtyChange(false), [onDirtyChange]);

  useEffect(() => {
    if (!dirty) return undefined;
    const warn = (event: BeforeUnloadEvent) => {
      if (doneRef.current) return;
      event.preventDefault();
      event.returnValue = '';
    };
    window.addEventListener('beforeunload', warn);
    return () => window.removeEventListener('beforeunload', warn);
  }, [dirty]);

  useEffect(() => {
    if (alertMessage) alertRef.current?.focus();
  }, [alertMessage]);

  // The forms tab only offers subsystems that are enabled for the role (or have no permission codes at all, to say so).
  const selectable = (entry: SubsystemEntry) => entry.permissions.length === 0 || isSubsystemEnabled(entry, selected);
  const activeFormsEntry =
    entries.find((entry) => entry.code === formsSubsystem && selectable(entry)) ?? entries.find((entry) => isSubsystemEnabled(entry, selected));

  const toggle = (entry: SubsystemEntry, on: boolean) => {
    if (!editable || busy) return;
    if (on) setSelection((current) => enableSubsystem(current, entry));
    else setPendingDisable(entry);
  };

  const requestSave = async () => {
    if (!editable || !dirty || busyRef.current || confirm !== null) return;
    busyRef.current = true;
    setChecking(true);
    setAlertMessage(null);
    setConflict(false);
    try {
      // The impact preview is read-only; the save re-runs every security control on the server.
      const preview = await previewCompanyRolePermissions(companyId, role.id, selection);
      setConfirm({ preview, previewMessage: null });
    } catch (cause) {
      if (onUnauthenticated(cause)) return;
      if (cause instanceof ApiError && [403, 404, 409].includes(cause.status)) {
        if (cause.status === 403) onForbidden?.();
        setConflict(cause.status === 409);
        setAlertMessage(cause.message);
        return;
      }
      // Any other failure only loses the advisory numbers: the warning falls back to the list's member count.
      setConfirm({ preview: null, previewMessage: cause instanceof ApiError ? cause.message : 'محاسبهٔ اثر تغییرات انجام نشد؛ شمار اعضا از آخرین فهرست نقش‌ها نمایش داده می‌شود.' });
    } finally {
      busyRef.current = false;
      setChecking(false);
    }
  };

  const save = async () => {
    if (busyRef.current) return;
    busyRef.current = true;
    setSaving(true);
    setAlertMessage(null);
    setConflict(false);
    try {
      // The role name and status are re-sent unchanged; only the permission set is edited on this page.
      await updateCompanyRole(companyId, role.id, { name: role.name, isActive: role.isActive, permissionCodes: selection });
      doneRef.current = true;
      onSaved(selection);
    } catch (cause) {
      if (onUnauthenticated(cause)) return;
      if (cause instanceof ApiError && cause.status === 403) onForbidden?.();
      setConflict(cause instanceof ApiError && cause.status === 409);
      setAlertMessage(cause instanceof ApiError ? cause.message : fallbackMessage);
    } finally {
      busyRef.current = false;
      setSaving(false);
    }
  };

  const memberCount = confirm?.preview?.activeMemberCount ?? role.activeMemberCount;
  const shared = memberCount > 1;
  const changes = useMemo(() => diffCodes(role.permissionCodes, selection), [role.permissionCodes, selection]);

  let confirmBody: ReactNode = null;
  if (confirm) {
    const { preview } = confirm;
    confirmBody = (
      <div>
        {shared ? (
          <p role="alert" className="rounded-lg border border-danger/30 bg-danger-soft p-3 text-sm font-semibold text-danger">
            نقش «{role.name}» مشترک است: این تغییر بلافاصله دسترسی {number.format(memberCount)} کاربر فعال را تغییر می‌دهد، نه فقط یک کاربر.
          </p>
        ) : (
          <p className="text-sm text-sub">اعضای فعال تحت تأثیر: {number.format(memberCount)} نفر.</p>
        )}
        <CodeList title="مجوزهای افزوده‌شده" codes={changes.added} names={names} tone="add" />
        <CodeList title="مجوزهای حذف‌شده" codes={changes.removed} names={names} tone="remove" />
        <p className="mt-3 text-sm">
          تعداد مجوزها: {number.format(role.permissionCodes.length)} ← {number.format(selection.length)}
        </p>
        {confirm.previewMessage ? <p role="alert" className="mt-3 text-sm text-danger">{confirm.previewMessage}</p> : null}
        {preview && preview.sensitiveChangedPermissionCodes.length > 0 ? (
          <p className="mt-3 text-sm font-semibold text-danger">
            مجوزهای حساس تغییرکرده: {preview.sensitiveChangedPermissionCodes.map((code) => names.get(code) ?? code).join('، ')}
          </p>
        ) : null}
        {preview && preview.ceilingViolatedPermissionCodes.length > 0 ? (
          <p className="mt-3 text-sm font-semibold text-danger">
            این کدها خارج از سقف تفویض شما است و ذخیره از سمت سرور رد می‌شود: {preview.ceilingViolatedPermissionCodes.join('، ')}
          </p>
        ) : null}
      </div>
    );
  }

  const reason = role.isSystemRole
    ? 'این نقش سیستمی است و قابل ویرایش نیست؛ سرور هر تغییر آن را رد می‌کند. مجوزها فقط نمایش داده می‌شوند.'
    : !canUpdate
      ? 'شما مجوز ویرایش نقش‌ها را ندارید؛ مجوزهای نقش فقط نمایش داده می‌شوند.'
      : exceedsCeiling
        ? 'این نقش مجوزهایی دارد که خارج از سقف مجوزهای شما است؛ برای جلوگیری از حذف ناخواستهٔ آن‌ها فقط نمایش داده می‌شود.'
        : null;

  const subsystemsPanel = (
    <div className="space-y-3">
      <Alert tone="info">
        سرور برای هر شرکت پرچم مستقلی برای «فعال‌بودن زیرسیستم» ندارد. در این صفحه یک زیرسیستم فقط وقتی «فعال» است که نقش انتخاب‌شده دست‌کم یکی از مجوزهای واقعی آن را داشته باشد. غیرفعال‌کردن همهٔ مجوزهای آن زیرسیستم را از نقش حذف می‌کند و انتخاب‌های جزئی فرم‌ها ذخیره نمی‌ماند؛ فعال‌سازی دوباره فقط مجوزهای مشاهده و فهرست را برمی‌گرداند و بقیه را باید در تب «دسترسی فرم‌ها» دوباره بدهید. لایسنس زیرسیستم از مجوز جداست.
      </Alert>
      {subsystems.kind === 'limited' ? (
        <Alert
          tone="warning"
          action={
            !subsystems.forbidden ? (
              <Button type="button" variant="outline" size="sm" onClick={onRetrySubsystems}>
                تلاش دوباره
              </Button>
            ) : undefined
          }
        >
          {subsystems.message}
        </Alert>
      ) : null}
      {subsystems.kind === 'loading' ? <p role="status" className="text-sm text-sub">در حال دریافت کاتالوگ زیرسیستم‌ها…</p> : null}
      {entries.length === 0 && subsystems.kind !== 'loading' ? (
        <p className="rounded-xl border border-dashed border-line bg-white p-5 text-center text-sm text-sub">زیرسیستمی برای نمایش وجود ندارد.</p>
      ) : (
        <div className="overflow-x-auto rounded-xl border border-line bg-white">
          <table className="w-full min-w-[40rem] border-collapse text-sm">
            <caption className="sr-only">زیرسیستم‌های نقش و مجوزهای واقعی هر کدام</caption>
            <thead>
              <tr className="border-b border-line bg-cool text-start text-xs text-sub">
                <th scope="col" className="px-3 py-2 text-start font-semibold">زیرسیستم</th>
                <th scope="col" className="px-3 py-2 text-start font-semibold">لایسنس (جدا از مجوز)</th>
                <th scope="col" className="px-3 py-2 text-start font-semibold">مجوزهای تعریف‌شده</th>
                <th scope="col" className="px-3 py-2 text-start font-semibold">مجوزهای نقش</th>
                <th scope="col" className="px-3 py-2 text-start font-semibold">وضعیت در این نقش</th>
              </tr>
            </thead>
            <tbody>
              {entries.map((entry) => {
                const enabled = isSubsystemEnabled(entry, selected);
                const held = heldCodes(entry, selected).length;
                const noCodes = entry.permissions.length === 0;
                const noReadCode = !enabled && !noCodes && readCodesOf(entry).length === 0;
                const note = noCodes
                  ? 'برای این زیرسیستم هنوز مجوزی در کاتالوگ سرور تعریف نشده است.'
                  : noReadCode
                    ? 'مجوز مشاهده یا فهرست ندارد؛ فعال‌سازی از این صفحه ممکن نیست.'
                    : null;
                const noteId = `${selectId}-${entry.code}-note`;
                return (
                  <tr key={entry.code} className="border-b border-line last:border-b-0">
                    <th scope="row" className="px-3 py-2 text-start font-semibold text-ink">
                      {entry.title}
                      {entry.title !== entry.code ? <bdi dir="ltr" className="ms-2 text-xs font-normal text-sub">({entry.code})</bdi> : null}
                    </th>
                    <td className="px-3 py-2 text-xs text-sub">{entry.entitlementScope ? (entitlementTexts[entry.entitlementScope] ?? entry.entitlementScope) : '—'}</td>
                    <td className="px-3 py-2">{number.format(entry.permissions.length)}</td>
                    <td className="px-3 py-2">{number.format(held)}</td>
                    <td className="px-3 py-2">
                      <label className="flex items-center gap-2">
                        <input
                          type="checkbox"
                          checked={enabled}
                          disabled={!editable || busy || noCodes || noReadCode}
                          aria-label={`فعال بودن زیرسیستم ${entry.title}`}
                          aria-describedby={note ? noteId : undefined}
                          onChange={(event) => toggle(entry, event.target.checked)}
                          className="size-5 shrink-0 accent-teal-deep outline-none focus-visible:ring-2 focus-visible:ring-gold"
                        />
                        <Badge tone={enabled ? 'success' : 'neutral'} icon={null}>
                          {enabled ? 'فعال' : 'غیرفعال'}
                        </Badge>
                      </label>
                      {note ? <p id={noteId} className="mt-1 text-xs text-sub">{note}</p> : null}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );

  const table = activeFormsEntry ? buildFormTable(activeFormsEntry.permissions) : null;
  const formsPanel = (
    <div className="space-y-3">
      <Alert tone="info">
        فقط فرم‌ها و عملیات تعریف‌شده در کاتالوگ واقعی مجوزهای سرور نمایش داده می‌شوند؛ این صفحه مجوز تازه‌ای نمی‌سازد. فقط زیرسیستم‌های فعال در نقش قابل انتخاب‌اند؛ زیرسیستم غیرفعال را از تب «زیرسیستم‌ها» فعال کنید.
      </Alert>
      <FormField label="زیرسیستم" className="w-full sm:w-80">
        <Select
          className="h-11 sm:h-10"
          value={activeFormsEntry?.code ?? ''}
          disabled={entries.length === 0}
          onChange={(event) => setFormsSubsystem(event.target.value)}
        >
          {!activeFormsEntry ? <option value="">زیرسیستم فعالی وجود ندارد</option> : null}
          {entries.map((entry) => (
            <option key={entry.code} value={entry.code} disabled={!selectable(entry)}>
              {entry.title}
              {entry.permissions.length === 0 ? ' (بدون مجوز تعریف‌شده)' : !isSubsystemEnabled(entry, selected) ? ' (غیرفعال)' : ''}
            </option>
          ))}
        </Select>
      </FormField>
      {!activeFormsEntry ? (
        <p className="rounded-xl border border-dashed border-line bg-white p-5 text-center text-sm text-sub">
          هیچ زیرسیستمی برای این نقش فعال نیست. ابتدا در تب «زیرسیستم‌ها» یک زیرسیستم را فعال کنید.
        </p>
      ) : activeFormsEntry.permissions.length === 0 || !table ? (
        <p role="status" className="rounded-xl border border-dashed border-line bg-white p-5 text-center text-sm text-sub">
          برای زیرسیستم «{activeFormsEntry.title}» هنوز هیچ مجوز فرم یا عملیاتی در کاتالوگ سرور تعریف نشده است؛ چیزی برای تنظیم وجود ندارد و در این صفحه مجوز ساخته نمی‌شود.
        </p>
      ) : (
        <div className="overflow-x-auto rounded-xl border border-line bg-white">
          <table className="w-full min-w-[32rem] border-collapse text-sm">
            <caption className="sr-only">مجوزهای فرم‌های زیرسیستم {activeFormsEntry.title}</caption>
            <thead>
              <tr className="border-b border-line bg-cool text-xs text-sub">
                <th scope="col" className="px-3 py-2 text-start font-semibold">فرم</th>
                {table.verbs.map((verb) => (
                  <th key={verb} scope="col" className="px-3 py-2 text-center font-semibold">{verbLabels[verb]}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {table.rows.map((row) => (
                <tr key={row.resource} className="border-b border-line last:border-b-0">
                  <th scope="row" className="px-3 py-2 text-start font-semibold text-ink">
                    {row.resource ? <bdi dir="ltr">{row.resource}</bdi> : 'عمومی'}
                  </th>
                  {table.verbs.map((verb) => (
                    <td key={verb} className="px-3 py-2 text-center">
                      {(row.cells[verb] ?? []).length === 0 ? (
                        <span className="text-muted"><span aria-hidden="true">—</span><span className="sr-only">تعریف نشده</span></span>
                      ) : (
                        <span className="inline-flex flex-wrap items-center justify-center gap-2">
                          {(row.cells[verb] ?? []).map((permission) => (
                            <input
                              key={permission.code}
                              type="checkbox"
                              checked={selected.has(permission.code)}
                              disabled={!editable || busy}
                              aria-label={permission.name}
                              title={`${permission.code}${permission.description ? ` — ${permission.description}` : ''}`}
                              onChange={(event) =>
                                setSelection((current) =>
                                  event.target.checked ? [...current.filter((code) => code !== permission.code), permission.code] : current.filter((code) => code !== permission.code),
                                )
                              }
                              className="size-5 shrink-0 accent-teal-deep outline-none focus-visible:ring-2 focus-visible:ring-gold"
                            />
                          ))}
                        </span>
                      )}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );

  return (
    <div className="space-y-4">
      {alertMessage ? (
        <Alert
          ref={alertRef}
          tone="error"
          action={
            conflict ? (
              <Button type="button" variant="outline" size="sm" onClick={onReload}>
                دریافت آخرین اطلاعات (تغییرات ذخیره‌نشده کنار گذاشته می‌شود)
              </Button>
            ) : undefined
          }
        >
          {alertMessage}
        </Alert>
      ) : null}
      {reason ? <Alert tone="info">{reason}</Alert> : null}
      {outsideCodes.length > 0 ? (
        <Alert tone="info">{number.format(outsideCodes.length)} مجوز این نقش در فهرست قابل‌نمایش شما نیست.{' '}
          {exceedsCeiling
            ? 'به همین دلیل این نقش برای شما فقط‌خواندنی است.'
            : 'سرور هنگام ذخیره فقط کدهای ثبت‌شده در کاتالوگ را می‌پذیرد.'}
        </Alert>
      ) : null}
      <Tabs
        label="بخش‌های مجوزهای دسترسی"
        value={tab}
        onValueChange={setTab}
        items={[
          { id: 'subsystems', label: 'زیرسیستم‌ها', content: subsystemsPanel },
          { id: 'forms', label: 'دسترسی فرم‌ها', content: formsPanel },
        ]}
      />
      {editable ? (
        <div className="flex flex-wrap items-center gap-2 border-t border-line pt-4">
          <Button type="button" size="lg" disabled={busy || !dirty} aria-busy={busy || undefined} onClick={requestSave}>
            {saving ? 'در حال ذخیره…' : checking ? 'در حال محاسبهٔ اثر…' : 'بررسی و ذخیرهٔ مجوزهای نقش'}
          </Button>
          <Button type="button" variant="outline" size="lg" disabled={busy || !dirty} onClick={() => setSelection(role.permissionCodes)}>
            انصراف از تغییرات
          </Button>
          {dirty ? (
            <span role="status" className="text-xs font-semibold text-warning">تغییرات ذخیره‌نشده</span>
          ) : (
            <span className="text-xs text-sub">تغییری برای ذخیره وجود ندارد.</span>
          )}
        </div>
      ) : null}

      <ConfirmDialog
        open={pendingDisable !== null}
        tone="danger"
        title="غیرفعال‌کردن زیرسیستم در نقش"
        description={
          pendingDisable
            ? `با غیرفعال‌کردن «${pendingDisable.title}»، ${number.format(heldCodes(pendingDisable, selected).length)} مجوز این زیرسیستم از نقش «${role.name}» حذف می‌شود و دسترسی مؤثر آن پس از ذخیره لغو می‌شود. انتخاب‌های جزئی فرم‌ها ذخیره نمی‌ماند. تا پیش از ذخیره می‌توانید با «انصراف از تغییرات» برگردید.`
            : ''
        }
        confirmLabel="غیرفعال‌کردن"
        cancelLabel="انصراف"
        onConfirm={() => {
          if (pendingDisable) setSelection((current) => disableSubsystem(current, pendingDisable));
          setPendingDisable(null);
        }}
        onCancel={() => setPendingDisable(null)}
      />
      <ConfirmDialog
        open={confirm !== null}
        tone={shared ? 'danger' : 'default'}
        title={shared ? 'تغییر نقش مشترک' : 'پیش‌نمایش تغییرات نقش'}
        description={confirmBody}
        confirmLabel={shared ? `تأیید و اعمال برای ${number.format(memberCount)} کاربر` : 'تأیید و ذخیره'}
        cancelLabel="بازگشت به ویرایش"
        busy={saving}
        onConfirm={async () => {
          // Close first so a failure can move focus to the message box.
          setConfirm(null);
          await save();
        }}
        onCancel={() => setConfirm(null)}
      />
    </div>
  );
}
