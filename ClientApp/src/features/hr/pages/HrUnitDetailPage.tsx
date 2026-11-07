import { useCallback, useEffect, useMemo, useState } from 'react';
import { ApiError } from '@/api/client';
import { Alert } from '@/components/ui/Alert';
import { usePageBreadcrumb } from '@/components/shell/PageBreadcrumb';
import type { BreadcrumbItem } from '@/components/ui/Breadcrumb';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/ui/ConfirmDialog';
import { DataTable, type DataTableColumn } from '@/components/ui/DataTable';
import { FormField, Input, Select, Textarea } from '@/components/ui/FormField';
import { FormSection } from '@/components/ui/FormSection';
import { JalaliDateField } from '@/components/ui/JalaliDateField';
import { useNavigate, useDocumentTitle, useParams } from '@/lib/router';
import { formatHrInstant } from '../lib/hrFormat';
import { deactivateHrUnit, getHrUnit, getHrUnitSubtree, listHrUnitEmployees, updateHrUnit } from '../api/hrClient';
import type { HrUnitDetail, HrUnitEmployeeItem, HrUnitSubtreeNode } from '../api/hrTypes';
import { HrActiveBadge } from '../components/HrBadges';
import { HrUnitParentPicker } from '../components/HrUnitParentPicker';
import { HrUnitTree } from '../components/HrUnitTree';
import { validateDescription, validateRequiredName, validateValidityWindow, buildUpdateUnitBody } from '../lib/hrValidation';
import { useCompanyPermissions } from '@/lib/useCompanyPermissions';

type Props = {
  onUnauthenticated: (error: unknown) => boolean;
  companyId: string;
};

const employeePageSizes = [10, 25, 50] as const;

type EditState = {
  name: string;
  description: string;
  parentUnitId: string | null;
  isActive: boolean;
  validFromIsoDay: string;
  validFromTouched: boolean;
  validToIsoDay: string;
};

/**
 * One organizational unit: its data, its subtree (`GET .../subtree` with active assignment
 * counts), its employees (`GET .../employees`) and the edit form. The edit form carries the
 * documented patch semantics: `parentUnitId: null` = root, `validFromUtc: null` = keep (the field
 * cannot be cleared), `validToUtc: null` = clear, and the mandatory `rowVersion` from the last
 * detail read; a stale token answers 409 and the page re-reads the unit.
 */
export function HrUnitDetailPage({ onUnauthenticated, companyId }: Props) {
  const { unitId } = useParams();
  const navigate = useNavigate();
  const { codes } = useCompanyPermissions(companyId, onUnauthenticated);
  const canUpdate = codes.includes('hr.organizational-units.update');

  const [unit, setUnit] = useState<HrUnitDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [failure, setFailure] = useState<{ message: string; status: number | null } | null>(null);
  const [attempt, setAttempt] = useState(0);

  const [subtree, setSubtree] = useState<HrUnitSubtreeNode | null>(null);
  const [subtreeError, setSubtreeError] = useState<string | null>(null);

  const [editing, setEditing] = useState(false);
  const [edit, setEdit] = useState<EditState | null>(null);
  const [editErrors, setEditErrors] = useState<Partial<Record<string, string>>>({});
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [savedOnce, setSavedOnce] = useState(false);

  const [confirmingDeactivate, setConfirmingDeactivate] = useState(false);
  const [deactivating, setDeactivating] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const [employees, setEmployees] = useState<HrUnitEmployeeItem[] | null>(null);
  const [employeeTotal, setEmployeeTotal] = useState(0);
  const [employeePage, setEmployeePage] = useState(1);
  const [employeePageSize, setEmployeePageSize] = useState<number>(25);
  const [employeesError, setEmployeesError] = useState<string | null>(null);

  const readUnit = useCallback(
    (signal: AbortSignal) =>
      getHrUnit(companyId, unitId ?? '', signal).then((detail) => {
        setUnit(detail);
        setEdit({
          name: detail.name,
          description: detail.description ?? '',
          parentUnitId: detail.parentUnitId,
          isActive: detail.isActive,
          validFromIsoDay: toIsoDay(detail.validFromUtc),
          validFromTouched: false,
          validToIsoDay: toIsoDay(detail.validToUtc),
        });
      }),
    [companyId, unitId],
  );

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setFailure(null);
    readUnit(controller.signal)
      .then(() => {
        if (!controller.signal.aborted) setLoading(false);
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || onUnauthenticated(cause)) return;
        const status = cause instanceof ApiError ? cause.status : null;
        setFailure({
          status,
          message:
            status === 403
              ? 'شما مجوز مشاهدهٔ ساختار سازمانی این شرکت را ندارید.'
              : status === 404
                ? 'واحد سازمانی پیدا نشد یا به آن دسترسی ندارید.'
                : cause instanceof ApiError
                  ? cause.message
                  : 'دریافت واحد سازمانی انجام نشد. لطفاً دوباره تلاش کنید.',
        });
        setLoading(false);
      });
    return () => controller.abort();
  }, [companyId, unitId, attempt, readUnit, onUnauthenticated]);

  useEffect(() => {
    const controller = new AbortController();
    setSubtreeError(null);
    getHrUnitSubtree(companyId, unitId ?? '', controller.signal)
      .then((node) => {
        if (!controller.signal.aborted) setSubtree(node);
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || onUnauthenticated(cause)) return;
        setSubtreeError(cause instanceof ApiError ? cause.message : 'دریافت زیردرخت انجام نشد.');
      });
    return () => controller.abort();
  }, [companyId, unitId, attempt, onUnauthenticated]);

  useEffect(() => {
    const controller = new AbortController();
    setEmployeesError(null);
    listHrUnitEmployees(companyId, unitId ?? '', employeePage, employeePageSize, controller.signal)
      .then((result) => {
        if (controller.signal.aborted) return;
        setEmployees([...result.items]);
        setEmployeeTotal(result.totalCount);
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || onUnauthenticated(cause)) return;
        setEmployeesError(cause instanceof ApiError ? cause.message : 'دریافت کارکنان واحد انجام نشد.');
      });
    return () => controller.abort();
  }, [companyId, unitId, employeePage, employeePageSize, attempt, onUnauthenticated, unit]);

  useDocumentTitle(unit ? `واحد ${unit.name}` : null);

  const employeeColumns = useMemo<readonly DataTableColumn<HrUnitEmployeeItem>[]>(
    () => [
      {
        key: 'name',
        header: 'کارمند',
        cell: (item) => (
          <span className="flex flex-col">
            <span className="font-medium">
              {item.firstName} {item.lastName}
            </span>
            <code dir="ltr" className="text-[11px] text-sub">
              {item.employeeNumber}
            </code>
          </span>
        ),
      },
      { key: 'position', header: 'سمت', cell: (item) => item.positionName ?? '—' },
      { key: 'since', header: 'شروع انتساب', cell: (item) => formatHrInstant(item.startOnUtc) },
    ],
    [],
  );

  const startEdit = () => {
    if (unit === null) return;
    setEdit({
      name: unit.name,
      description: unit.description ?? '',
      parentUnitId: unit.parentUnitId,
      isActive: unit.isActive,
      validFromIsoDay: toIsoDay(unit.validFromUtc),
      validFromTouched: false,
      validToIsoDay: toIsoDay(unit.validToUtc),
    });
    setEditErrors({});
    setSaveError(null);
    setSavedOnce(false);
    setEditing(true);
  };

  const saveEdit = async () => {
    if (unit === null || edit === null) return;
    const errors: Partial<Record<string, string>> = {};
    const nameError = validateRequiredName(edit.name, 'عنوان واحد', 200);
    if (nameError) errors.name = nameError;
    const descriptionError = validateDescription(edit.description);
    if (descriptionError) errors.description = descriptionError;
    const windowError = validateValidityWindow(edit.validFromIsoDay, edit.validToIsoDay);
    if (windowError) errors.validToIsoDay = windowError;
    setEditErrors(errors);
    if (Object.keys(errors).length > 0) return;

    setSaving(true);
    setSaveError(null);
    try {
      await updateHrUnit(companyId, unit.id, buildUpdateUnitBody({ ...edit, rowVersion: unit.rowVersion }));
      setEditing(false);
      setSavedOnce(true);
      setActionError(null);
      setAttempt((value) => value + 1); // re-reads the unit (fresh rowVersion) and the panels
    } catch (cause: unknown) {
      if (onUnauthenticated(cause)) return;
      setSaveError(cause instanceof ApiError ? cause.message : 'ذخیرهٔ تغییرات انجام نشد. لطفاً دوباره تلاش کنید.');
      // A stale rowVersion (409) means someone else saved first; the re-read refreshes the token.
      if (cause instanceof ApiError && cause.status === 409) setAttempt((value) => value + 1);
    } finally {
      setSaving(false);
    }
  };

  const deactivate = async () => {
    if (unit === null) return;
    setDeactivating(true);
    setActionError(null);
    try {
      await deactivateHrUnit(companyId, unit.id);
      setConfirmingDeactivate(false);
      setAttempt((value) => value + 1);
    } catch (cause: unknown) {
      if (onUnauthenticated(cause)) return;
      setActionError(cause instanceof ApiError ? cause.message : 'غیرفعال‌سازی انجام نشد.');
      setConfirmingDeactivate(false);
    } finally {
      setDeactivating(false);
    }
  };

  const crumbs: BreadcrumbItem[] = [{ label: 'منابع انسانی' }, { label: 'واحدهای سازمانی', to: '/hr/org/units' }, { label: unit?.name ?? 'جزئیات واحد' }];
  usePageBreadcrumb(crumbs);

  const forbidden = failure?.status === 403 || failure?.status === 404;

  if (loading) {
    return (
      <div className="space-y-4">
        <p role="status" className="rounded-xl border border-line bg-white p-5 text-sub">
          در حال بارگذاری…
        </p>
      </div>
    );
  }

  if (forbidden || unit === null) {
    return (
      <div className="space-y-4">
        <div role="alert" className="rounded-xl border border-line bg-white p-6 text-center">
          <p className="text-lg font-bold text-ink">یافت نشد یا دسترسی ندارید</p>
          <p className="mt-2 text-sm text-sub">{failure?.message}</p>
          <Button type="button" variant="outline" size="lg" className="mt-4" onClick={() => navigate('/hr/org/units')}>
            بازگشت به فهرست
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-5">
      <header className="space-y-2">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex flex-wrap items-center gap-3">
            <h1 className="text-2xl font-extrabold tracking-tight text-title">{unit.name}</h1>
            <code dir="ltr" className="rounded-md bg-cool px-2 py-1 text-xs text-sub">
              {unit.code}
            </code>
            <HrActiveBadge isActive={unit.isActive} />
          </div>
          {canUpdate ? (
            <div className="flex flex-wrap gap-2">
              {!editing ? (
                <Button type="button" variant="outline" size="lg" onClick={startEdit}>
                  ویرایش
                </Button>
              ) : null}
              {unit.isActive ? (
                <Button
                  type="button"
                  variant="outline"
                  size="lg"
                  disabled={deactivating}
                  className="border-danger/40 text-danger hover:bg-danger-soft"
                  onClick={() => setConfirmingDeactivate(true)}
                >
                  غیرفعال‌سازی واحد
                </Button>
              ) : null}
            </div>
          ) : null}
        </div>
        <p className="text-sm text-sub">{unit.description ?? 'بدون توضیح.'}</p>
      </header>

      {savedOnce && !editing ? (
        <Alert tone="info">تغییرات ذخیره شد.</Alert>
      ) : null}
      {actionError ? <Alert tone="error">{actionError}</Alert> : null}

      <FormSection title="اطلاعات واحد">
        <dl className="sm:col-span-2 grid gap-x-6 gap-y-2 text-sm sm:grid-cols-2">
          <div>
            <dt className="text-sub">واحد والد</dt>
            <dd>{unit.parentUnitId === null ? '— (ریشه)' : <code dir="ltr" className="text-xs">{unit.parentUnitId}</code>}</dd>
          </div>
          <div>
            <dt className="text-sub">شروع اعتبار</dt>
            <dd>{formatHrInstant(unit.validFromUtc)}</dd>
          </div>
          <div>
            <dt className="text-sub">پایان اعتبار</dt>
            <dd>{formatHrInstant(unit.validToUtc)}</dd>
          </div>
          <div>
            <dt className="text-sub">واحدهای فرزند فعال</dt>
            <dd>{unit.activeChildCount.toLocaleString('fa-IR')}</dd>
          </div>
          <div>
            <dt className="text-sub">انتساب‌های فعال</dt>
            <dd>{unit.activeAssignmentCount.toLocaleString('fa-IR')}</dd>
          </div>
          <div>
            <dt className="text-sub">توکن هم‌زمانی (rowVersion)</dt>
            <dd>
              <code dir="ltr" className="text-xs text-sub">
                {unit.rowVersion}
              </code>
            </dd>
          </div>
        </dl>
      </FormSection>

      {editing && edit !== null ? (
        <FormSection
          title="ویرایش واحد"
          description="عنوان، توضیح، والد، وضعیت فعال و بازهٔ اعتبار. حذف تاریخ شروع اعتبار از این مسیر ممکن نیست (خالی = حفظ مقدار)؛ پایان اعتبار با خالی‌کردن پاک می‌شود."
        >
          {saveError ? (
            <div className="sm:col-span-2">
              <Alert tone="error">{saveError}</Alert>
            </div>
          ) : null}
          <FormField label="عنوان واحد" required error={editErrors.name}>
            <Input value={edit.name} maxLength={200} onChange={(event) => setEdit({ ...edit, name: event.target.value })} />
          </FormField>
          <FormField label="وضعیت" hint="غیرفعال‌سازی وقتی ممکن است که فرزند فعال یا انتساب فعال وجود نداشته باشد.">
            <Select
              value={edit.isActive ? 'active' : 'inactive'}
              onChange={(event) => setEdit({ ...edit, isActive: event.target.value === 'active' })}
            >
              <option value="active">فعال</option>
              <option value="inactive">غیرفعال</option>
            </Select>
          </FormField>
          <FormField label="واحد والد" hint="خالی = واحد ریشه. خود واحد و زیرمجموعه‌اش انتخاب نمی‌شوند." className="sm:col-span-2">
            <HrUnitParentPicker
              companyId={companyId}
              value={edit.parentUnitId}
              onValueChange={(key) => setEdit({ ...edit, parentUnitId: key })}
              excludeUnitId={unit.id}
              disabled={saving}
            />
          </FormField>
          <FormField label="شروع اعتبار" hint="برای حفظ مقدار فعلی، آن را تغییر ندهید (خالی = حفظ).">
            <JalaliDateField
              value={edit.validFromIsoDay}
              onChange={(value) => setEdit({ ...edit, validFromIsoDay: value, validFromTouched: true })}
            />
          </FormField>
          <FormField label="پایان اعتبار" error={editErrors.validToIsoDay} hint="خالی‌کردن این فیلد، پایان اعتبار را پاک می‌کند.">
            <JalaliDateField value={edit.validToIsoDay} onChange={(value) => setEdit({ ...edit, validToIsoDay: value })} />
          </FormField>
          <FormField label="توضیحات" error={editErrors.description} className="sm:col-span-2">
            <Textarea value={edit.description} maxLength={1000} onChange={(event) => setEdit({ ...edit, description: event.target.value })} />
          </FormField>
          <div className="sm:col-span-2 flex flex-wrap gap-2">
            <Button type="button" size="lg" disabled={saving} onClick={() => void saveEdit()}>
              {saving ? 'در حال ذخیره…' : 'ذخیرهٔ تغییرات'}
            </Button>
            <Button
              type="button"
              variant="outline"
              size="lg"
              disabled={saving}
              onClick={() => {
                setEditing(false);
                setSaveError(null);
              }}
            >
              انصراف
            </Button>
          </div>
        </FormSection>
      ) : null}

      <section className="space-y-3">
        <h2 className="text-lg font-bold text-ink">زیردرخت واحد</h2>
        <p className="text-sm text-sub">ساختار درختی زیرمجموعه‌ها همراه با شمار انتساب‌های فعال هر گره.</p>
        {subtreeError ? (
          <Alert tone="warning">{subtreeError}</Alert>
        ) : (
          <div className="rounded-xl border border-line bg-white p-4">
            <HrUnitTree nodes={subtree === null ? [] : [subtree]} detailHref={(id) => `/hr/org/units/${id}`} />
          </div>
        )}
      </section>

      <section className="space-y-3">
        <h2 className="text-lg font-bold text-ink">کارکنان این واحد</h2>
        <DataTable
          caption="کارکنان واحد سازمانی"
          columns={employeeColumns}
          rows={employees ?? []}
          getRowKey={(item) => item.employeeId}
          loading={employees === null && employeesError === null}
          error={employeesError}
          onRetry={() => setAttempt((value) => value + 1)}
          rowHref={(item) => `/hr/org/employees/${item.employeeId}`}
          emptyTitle="کارمندی در این واحد نیست"
          emptyDescription="کارمندانی که انتساب فعال در این واحد دارند اینجا نمایش داده می‌شوند."
          pagination={{
            page: employeePage,
            pageSize: employeePageSize,
            total: employeeTotal,
            pageSizeOptions: employeePageSizes,
            onPageChange: setEmployeePage,
            onPageSizeChange: (size) => {
              setEmployeePageSize(size);
              setEmployeePage(1);
            },
          }}
        />
      </section>

      <ConfirmDialog
        open={confirmingDeactivate}
        title="غیرفعال‌سازی واحد سازمانی"
        description={
          unit.activeChildCount > 0 || unit.activeAssignmentCount > 0 ? (
            `این واحد ${unit.activeChildCount.toLocaleString('fa-IR')} فرزند فعال و ${unit.activeAssignmentCount.toLocaleString('fa-IR')} انتساب فعال دارد. سرور درخواست را تا انتقال یا پایان آن‌ها با خطا رد می‌کند.`
          ) : (
            'واحد غیرفعال از فهرست تخت حذف می‌شود ولی در درخت با برچسب «غیرفعال» دیده می‌شود و تاریخچه‌اش حفظ می‌شود.'
          )
        }
        confirmLabel="غیرفعال‌سازی"
        tone="danger"
        busy={deactivating}
        onCancel={() => setConfirmingDeactivate(false)}
        onConfirm={() => deactivate()}
      />
    </div>
  );
}

/** The local ISO calendar day of a `*Utc` instant, for the Jalali edit field. */
function toIsoDay(instant: string | null): string {
  if (instant === null) return '';
  const date = new Date(instant);
  if (Number.isNaN(date.getTime())) return '';
  const pad = (value: number): string => String(value).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}
