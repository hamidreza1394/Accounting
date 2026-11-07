import { useCallback, useEffect, useMemo, useState } from 'react';
import { ApiError } from '@/api/client';
import { Alert } from '@/components/ui/Alert';
import { Breadcrumb } from '@/components/ui/Breadcrumb';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/ui/ConfirmDialog';
import { DataTable, type DataTableColumn } from '@/components/ui/DataTable';
import { FormField, Input, Select, Textarea } from '@/components/ui/FormField';
import { FormSection } from '@/components/ui/FormSection';
import { useDocumentTitle, useNavigate, useParams } from '@/lib/router';
import { useCompanyPermissions } from '@/lib/useCompanyPermissions';
import { can } from '@/lib/permissions';
import { formatHrInstant } from '../lib/hrFormat';
import { deactivateHrPosition, getHrPosition, listHrPositionEmployees, updateHrPosition } from '../api/hrClient';
import type { HrPositionDetail, HrPositionEmployeeItem } from '../api/hrTypes';
import { HrActiveBadge } from '../components/HrBadges';
import { validateDescription, validateRank, validateRequiredName, buildUpdatePositionBody } from '../lib/hrValidation';

type Props = {
  onUnauthenticated: (error: unknown) => boolean;
  companyId: string;
};

const holderPageSizes = [10, 25, 50] as const;

type EditState = { name: string; description: string; rank: string; isActive: boolean };

/**
 * One position: data, edit form (mandatory `rowVersion`; a stale token answers 409 and the page
 * re-reads), the holders list (`GET .../employees`) and deactivation (blocked with 409 while
 * active assignments exist).
 */
export function HrPositionDetailPage({ onUnauthenticated, companyId }: Props) {
  const { positionId } = useParams();
  const navigate = useNavigate();
  const { access } = useCompanyPermissions(companyId, onUnauthenticated);
  // The standard hint (Issue #444 stage 1): a ready installation administrator passes without a
  // company code — the same dual-mode rule the server enforces for every `hr.*` operation.
  const canUpdate = can(access, 'hr.positions.update');

  const [position, setPosition] = useState<HrPositionDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [failure, setFailure] = useState<{ message: string; status: number | null } | null>(null);
  const [attempt, setAttempt] = useState(0);

  const [editing, setEditing] = useState(false);
  const [edit, setEdit] = useState<EditState | null>(null);
  const [editErrors, setEditErrors] = useState<Partial<Record<string, string>>>({});
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [savedOnce, setSavedOnce] = useState(false);

  const [confirmingDeactivate, setConfirmingDeactivate] = useState(false);
  const [deactivating, setDeactivating] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const [holders, setHolders] = useState<HrPositionEmployeeItem[] | null>(null);
  const [holderTotal, setHolderTotal] = useState(0);
  const [holderPage, setHolderPage] = useState(1);
  const [holderPageSize, setHolderPageSize] = useState<number>(25);
  const [holdersError, setHoldersError] = useState<string | null>(null);

  const readPosition = useCallback(
    (signal: AbortSignal) =>
      getHrPosition(companyId, positionId ?? '', signal).then((detail) => {
        setPosition(detail);
        setEdit({
          name: detail.name,
          description: detail.description ?? '',
          rank: detail.rank === null ? '' : String(detail.rank),
          isActive: detail.isActive,
        });
      }),
    [companyId, positionId],
  );

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setFailure(null);
    readPosition(controller.signal)
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
              ? 'شما مجوز مشاهدهٔ سمت‌های این شرکت را ندارید.'
              : status === 404
                ? 'سمت پیدا نشد یا به آن دسترسی ندارید.'
                : cause instanceof ApiError
                  ? cause.message
                  : 'دریافت سمت انجام نشد. لطفاً دوباره تلاش کنید.',
        });
        setLoading(false);
      });
    return () => controller.abort();
  }, [companyId, positionId, attempt, readPosition, onUnauthenticated]);

  useEffect(() => {
    const controller = new AbortController();
    setHoldersError(null);
    listHrPositionEmployees(companyId, positionId ?? '', holderPage, holderPageSize, controller.signal)
      .then((result) => {
        if (controller.signal.aborted) return;
        setHolders([...result.items]);
        setHolderTotal(result.totalCount);
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || onUnauthenticated(cause)) return;
        setHoldersError(cause instanceof ApiError ? cause.message : 'دریافت دارندگان سمت انجام نشد.');
      });
    return () => controller.abort();
  }, [companyId, positionId, holderPage, holderPageSize, attempt, onUnauthenticated, position]);

  useDocumentTitle(position ? `سمت ${position.name}` : null);

  const holderColumns = useMemo<readonly DataTableColumn<HrPositionEmployeeItem>[]>(
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
      { key: 'unit', header: 'واحد سازمانی', cell: (item) => item.organizationalUnitName ?? '—' },
      { key: 'since', header: 'شروع انتساب', cell: (item) => formatHrInstant(item.startOnUtc) },
    ],
    [],
  );

  const startEdit = () => {
    if (position === null) return;
    setEdit({
      name: position.name,
      description: position.description ?? '',
      rank: position.rank === null ? '' : String(position.rank),
      isActive: position.isActive,
    });
    setEditErrors({});
    setSaveError(null);
    setSavedOnce(false);
    setEditing(true);
  };

  const saveEdit = async () => {
    if (position === null || edit === null) return;
    const errors: Partial<Record<string, string>> = {};
    const nameError = validateRequiredName(edit.name, 'عنوان سمت', 200);
    if (nameError) errors.name = nameError;
    const descriptionError = validateDescription(edit.description);
    if (descriptionError) errors.description = descriptionError;
    const rankError = validateRank(edit.rank);
    if (rankError) errors.rank = rankError;
    setEditErrors(errors);
    if (Object.keys(errors).length > 0) return;

    setSaving(true);
    setSaveError(null);
    try {
      await updateHrPosition(companyId, position.id, buildUpdatePositionBody({ ...edit, rowVersion: position.rowVersion }));
      setEditing(false);
      setSavedOnce(true);
      setActionError(null);
      setAttempt((value) => value + 1);
    } catch (cause: unknown) {
      if (onUnauthenticated(cause)) return;
      setSaveError(cause instanceof ApiError ? cause.message : 'ذخیرهٔ تغییرات انجام نشد. لطفاً دوباره تلاش کنید.');
      if (cause instanceof ApiError && cause.status === 409) setAttempt((value) => value + 1);
    } finally {
      setSaving(false);
    }
  };

  const deactivate = async () => {
    if (position === null) return;
    setDeactivating(true);
    setActionError(null);
    try {
      await deactivateHrPosition(companyId, position.id);
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

  const forbidden = failure?.status === 403 || failure?.status === 404;

  if (loading) {
    return (
      <div className="space-y-4">
        <Breadcrumb items={[{ label: 'منابع انسانی' }, { label: 'سمت‌های سازمانی', to: '/hr/org/positions' }, { label: 'جزئیات سمت' }]} />
        <p role="status" className="rounded-xl border border-line bg-white p-5 text-sub">
          در حال بارگذاری…
        </p>
      </div>
    );
  }

  if (forbidden || position === null) {
    return (
      <div className="space-y-4">
        <Breadcrumb items={[{ label: 'منابع انسانی' }, { label: 'سمت‌های سازمانی', to: '/hr/org/positions' }, { label: 'جزئیات سمت' }]} />
        <div role="alert" className="rounded-xl border border-line bg-white p-6 text-center">
          <p className="text-lg font-bold text-ink">یافت نشد یا دسترسی ندارید</p>
          <p className="mt-2 text-sm text-sub">{failure?.message}</p>
          <Button type="button" variant="outline" size="lg" className="mt-4" onClick={() => navigate('/hr/org/positions')}>
            بازگشت به فهرست
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-5">
      <header className="space-y-2">
        <Breadcrumb items={[{ label: 'منابع انسانی' }, { label: 'سمت‌های سازمانی', to: '/hr/org/positions' }, { label: position.name }]} />
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex flex-wrap items-center gap-3">
            <h1 className="text-2xl font-extrabold tracking-tight text-title">{position.name}</h1>
            <code dir="ltr" className="rounded-md bg-cool px-2 py-1 text-xs text-sub">
              {position.code}
            </code>
            <HrActiveBadge isActive={position.isActive} />
          </div>
          {canUpdate ? (
            <div className="flex flex-wrap gap-2">
              {!editing ? (
                <Button type="button" variant="outline" size="lg" onClick={startEdit}>
                  ویرایش
                </Button>
              ) : null}
              {position.isActive ? (
                <Button
                  type="button"
                  variant="outline"
                  size="lg"
                  disabled={deactivating}
                  className="border-danger/40 text-danger hover:bg-danger-soft"
                  onClick={() => setConfirmingDeactivate(true)}
                >
                  غیرفعال‌سازی سمت
                </Button>
              ) : null}
            </div>
          ) : null}
        </div>
        <p className="text-sm text-sub">{position.description ?? 'بدون توضیح.'}</p>
      </header>

      {savedOnce && !editing ? <Alert tone="info">تغییرات ذخیره شد.</Alert> : null}
      {actionError ? <Alert tone="error">{actionError}</Alert> : null}

      <FormSection title="اطلاعات سمت" description="سمت مستقل از واحد سازمانی و از گروه دسترسی است؛ یک سمت می‌تواند در چند واحد استفاده شود.">
        <dl className="sm:col-span-2 grid gap-x-6 gap-y-2 text-sm sm:grid-cols-2">
          <div>
            <dt className="text-sub">رتبه</dt>
            <dd>{position.rank === null ? '—' : position.rank.toLocaleString('fa-IR')}</dd>
          </div>
          <div>
            <dt className="text-sub">انتساب‌های فعال</dt>
            <dd>{position.activeAssignmentCount.toLocaleString('fa-IR')}</dd>
          </div>
          <div>
            <dt className="text-sub">توکن هم‌زمانی (rowVersion)</dt>
            <dd>
              <code dir="ltr" className="text-xs text-sub">
                {position.rowVersion}
              </code>
            </dd>
          </div>
        </dl>
      </FormSection>

      {editing && edit !== null ? (
        <FormSection title="ویرایش سمت" description="عنوان، توضیح، رتبه و وضعیت فعال. رتبهٔ خالی یعنی بدون رتبه.">
          {saveError ? (
            <div className="sm:col-span-2">
              <Alert tone="error">{saveError}</Alert>
            </div>
          ) : null}
          <FormField label="عنوان سمت" required error={editErrors.name}>
            <Input value={edit.name} maxLength={200} onChange={(event) => setEdit({ ...edit, name: event.target.value })} />
          </FormField>
          <FormField label="رتبه" error={editErrors.rank}>
            <Input value={edit.rank} dir="ltr" inputMode="numeric" autoComplete="off" onChange={(event) => setEdit({ ...edit, rank: event.target.value })} />
          </FormField>
          <FormField label="وضعیت">
            <Select value={edit.isActive ? 'active' : 'inactive'} onChange={(event) => setEdit({ ...edit, isActive: event.target.value === 'active' })}>
              <option value="active">فعال</option>
              <option value="inactive">غیرفعال</option>
            </Select>
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
        <h2 className="text-lg font-bold text-ink">دارندگان این سمت</h2>
        <DataTable
          caption="کارکنان دارای این سمت"
          columns={holderColumns}
          rows={holders ?? []}
          getRowKey={(item) => item.employeeId}
          loading={holders === null && holdersError === null}
          error={holdersError}
          onRetry={() => setAttempt((value) => value + 1)}
          rowHref={(item) => `/hr/org/employees/${item.employeeId}`}
          emptyTitle="کارمندی این سمت را ندارد"
          emptyDescription="کارمندانی که انتساب فعال با این سمت دارند اینجا نمایش داده می‌شوند."
          pagination={{
            page: holderPage,
            pageSize: holderPageSize,
            total: holderTotal,
            pageSizeOptions: holderPageSizes,
            onPageChange: setHolderPage,
            onPageSizeChange: (size) => {
              setHolderPageSize(size);
              setHolderPage(1);
            },
          }}
        />
      </section>

      <ConfirmDialog
        open={confirmingDeactivate}
        title="غیرفعال‌سازی سمت"
        description={
          position.activeAssignmentCount > 0
            ? `این سمت ${position.activeAssignmentCount.toLocaleString('fa-IR')} انتساب فعال دارد. سرور درخواست را تا پایان آن انتساب‌ها با خطا رد می‌کند.`
            : 'سمت غیرفعال در انتساب‌های جدید قابل انتخاب نیست و تاریخچه‌اش حفظ می‌شود.'
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
