import { useEffect, useMemo, useState } from 'react';
import { Alert } from '@/components/ui/Alert';
import { Badge } from '@/components/ui/Badge';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/ui/ConfirmDialog';
import { FormField } from '@/components/ui/FormField';
import { FormSection } from '@/components/ui/FormSection';
import { JalaliDateField } from '@/components/ui/JalaliDateField';
import { ApiError } from '@/api/client';
import { dayStartUtc } from '@/lib/time';
import { listHrEmployees, startHrAssignment, changeHrAssignment, endHrAssignment, listHrPositions } from '../api/hrClient';
import type { HrEmployeeDetail } from '../api/hrTypes';
import { formatHrInstant } from '../lib/hrFormat';
import { hrFullName } from '../lib/hrLabels';
import { validateAssignmentEnd, validateAssignmentStart } from '../lib/hrValidation';
import { HrSearchSelect } from './HrSearchSelect';
import { unitTreeLoader } from './HrUnitParentPicker';

type Props = {
  companyId: string;
  employee: HrEmployeeDetail;
  onUnauthenticated: (error: unknown) => boolean;
  /** Reloads the employee detail after a successful change. */
  onChanged: () => void;
};

type Mode = 'view' | 'change' | 'start';

const utcTodayIso = (): string => {
  // "Today" for the Jalali picker is the browser's local day (D-068 presentation concern).
  const now = new Date();
  const pad = (value: number): string => String(value).padStart(2, '0');
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
};

/**
 * The organizational-assignment area of the employee page: the current assignment, the atomic
 * transfer form (`assignment-change`, null = keep), the first assignment form (`POST
 * /org/assignments`) and ending the active assignment. All date rules of the backend
 * (no future starts, not before `hiredOnUtc`) are checked in `hrValidation` and the server
 * answers remain authoritative.
 */
export function HrAssignmentSection({ companyId, employee, onUnauthenticated, onChanged }: Props) {
  const active = employee.activeAssignment;
  const terminated = employee.employmentStatus === 3;
  const [mode, setMode] = useState<Mode>('view');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [unitId, setUnitId] = useState<string | null>(null);
  const [positionId, setPositionId] = useState<string | null>(null);
  const [managerId, setManagerId] = useState<string | null>(null);
  const [startDay, setStartDay] = useState('');
  const [startError, setStartError] = useState<string | null>(null);

  // The end-assignment flow asks for an optional explicit day; empty means "end now".
  const [confirmingEnd, setConfirmingEnd] = useState(false);
  const [endDay, setEndDay] = useState('');
  const [endError, setEndError] = useState<string | null>(null);

  useEffect(() => {
    setMode('view');
    setError(null);
    setSuccess(null);
    setStartDay('');
    setUnitId(null);
    setPositionId(null);
    setManagerId(null);
  }, [employee.id]);

  const showSuccess = (message: string) => {
    setSuccess(message);
    onChanged();
  };

  const runAction = async (action: () => Promise<void>, successMessage: string) => {
    setSubmitting(true);
    setError(null);
    try {
      await action();
      showSuccess(successMessage);
      return true;
    } catch (cause: unknown) {
      if (onUnauthenticated(cause)) return false;
      setError(cause instanceof ApiError ? cause.message : 'عملیات انجام نشد. لطفاً دوباره تلاش کنید.');
      return false;
    } finally {
      setSubmitting(false);
    }
  };

  const submitStart = async () => {
    const startErrorOr = validateAssignmentStart(startDay, employee.hiredOnUtc);
    setStartError(startErrorOr);
    if (startErrorOr !== null) return;
    const startOnUtc = dayStartUtc(startDay);
    if (startOnUtc === null) {
      setStartError('تاریخ شروع معتبر نیست.');
      return;
    }
    const done = await runAction(
      () =>
        startHrAssignment(companyId, {
          employeeId: employee.id,
          organizationalUnitId: unitId,
          positionId,
          managerEmployeeId: managerId,
          startOnUtc,
          endActive: false,
        }).then(() => undefined),
      'انتساب جدید ثبت شد.',
    );
    if (done) {
      setMode('view');
      setStartDay('');
      setUnitId(null);
      setPositionId(null);
      setManagerId(null);
    }
  };

  const submitChange = async () => {
    const done = await runAction(
      () =>
        changeHrAssignment(companyId, employee.id, {
          organizationalUnitId: unitId,
          positionId,
          managerEmployeeId: managerId,
        }).then(() => undefined),
      'انتقال کارمند انجام شد؛ انتساب جاری پایان و انتساب جدید آغاز شد.',
    );
    if (done) {
      setMode('view');
      setUnitId(null);
      setPositionId(null);
      setManagerId(null);
    }
  };

  const submitEnd = async () => {
    const endErrorOr = validateAssignmentEnd(endDay);
    setEndError(endErrorOr);
    if (endErrorOr !== null) return;
    const endOnUtc = endDay === '' ? null : dayStartUtc(endDay);
    if (endDay !== '' && endOnUtc === null) {
      setEndError('تاریخ پایان معتبر نیست.');
      return;
    }
    if (active === null) return;
    const done = await runAction(
      () => endHrAssignment(companyId, active.id, endOnUtc),
      'انتساب جاری پایان یافت.',
    );
    if (done) setConfirmingEnd(false);
  };

  const loadUnitOptions = useMemo(() => unitTreeLoader(companyId), [companyId]);

  const loadPositionOptions = useMemo(
    () => async (search: string, signal: AbortSignal) => {
      const positions = await listHrPositions(
        companyId,
        { ...(search.trim() === '' ? {} : { search: search.trim() }), page: 1, pageSize: 25 },
        signal,
      );
      return positions.items
        .filter((position) => position.isActive)
        .map((position) => ({ key: position.id, label: position.name, detail: position.code }));
    },
    [companyId],
  );

  const loadManagerOptions = useMemo(
    () => async (search: string, signal: AbortSignal) => {
      const employees = await listHrEmployees(
        companyId,
        { ...(search.trim() === '' ? {} : { search: search.trim() }), status: 1, page: 1, pageSize: 25 },
        signal,
      );
      return employees.items
        .filter((candidate) => candidate.id !== employee.id)
        .map((candidate) => ({
          key: candidate.id,
          label: hrFullName(candidate),
          detail: candidate.employeeNumber,
        }));
    },
    [companyId, employee.id],
  );

  return (
    <FormSection
      title="ساختار سازمانی"
      description="واحد، سمت و مدیر از انتساب جاری خوانده می‌شوند. انتقال کارمند اتمی است: انتساب جاری پایان و انتساب جدید در یک تراکنش آغاز می‌شود و تاریخچه بازنویسی نمی‌شود."
    >
      {error ? (
        <div className="sm:col-span-2">
          <Alert tone="error">{error}</Alert>
        </div>
      ) : null}
      {success ? (
        <div className="sm:col-span-2">
          <Alert tone="info">{success}</Alert>
        </div>
      ) : null}

      {active !== null ? (
        <div className="sm:col-span-2 rounded-lg border border-line bg-cool/60 p-4">
          <div className="flex flex-wrap items-center gap-2">
            <h4 className="text-sm font-bold text-ink">انتساب جاری</h4>
            <Badge tone="success" icon={null}>
              فعال
            </Badge>
          </div>
          <dl className="mt-3 grid gap-x-6 gap-y-2 text-sm sm:grid-cols-2">
            <div>
              <dt className="text-sub">واحد سازمانی</dt>
              <dd>{active.unitName ?? '—'}</dd>
            </div>
            <div>
              <dt className="text-sub">سمت</dt>
              <dd>{active.positionName ?? '—'}</dd>
            </div>
            <div>
              <dt className="text-sub">مدیر</dt>
              <dd>
                {active.managerEmployeeId ? (
                  <code dir="ltr" className="text-xs">
                    {active.managerEmployeeId}
                  </code>
                ) : (
                  '—'
                )}
              </dd>
            </div>
            <div>
              <dt className="text-sub">شروع انتساب</dt>
              <dd>{formatHrInstant(active.startOnUtc)}</dd>
            </div>
          </dl>
        </div>
      ) : (
        <div className="sm:col-span-2">
          <Alert tone="info">
            {terminated ? 'این کارمند خاتمه‌همکاری است و انتساب جدید برای او ثبت نمی‌شود.' : 'این کارمند هنوز انتساب فعالی ندارد.'}
          </Alert>
        </div>
      )}

      {mode === 'view' ? (
        <div className="sm:col-span-2 flex flex-wrap gap-2">
          {active !== null ? (
            <>
              <Button type="button" variant="outline" size="lg" disabled={submitting} onClick={() => setMode('change')}>
                انتقال کارمند
              </Button>
              <Button
                type="button"
                variant="outline"
                size="lg"
                disabled={submitting}
                className="border-danger/40 text-danger hover:bg-danger-soft"
                onClick={() => setConfirmingEnd(true)}
              >
                پایان انتساب جاری
              </Button>
            </>
          ) : !terminated ? (
            <Button type="button" size="lg" disabled={submitting} onClick={() => setMode('start')}>
              شروع انتساب
            </Button>
          ) : null}
        </div>
      ) : null}

      {mode === 'start' ? (
        <div className="sm:col-span-2 space-y-4 rounded-lg border border-line bg-white p-4">
          <h4 className="text-sm font-bold text-ink">شروع انتساب جدید</h4>
          <p className="text-xs leading-6 text-sub">
            تاریخ شروع باید امروز یا گذشته باشد و نمی‌تواند قبل از تاریخ شروع همکاری کارمند باشد. همهٔ فیلدها اختیاری‌اند مگر تاریخ شروع.
          </p>
          <div className="grid gap-4 sm:grid-cols-2">
            <FormField label="واحد سازمانی">
              <HrSearchSelect
                searchLabel="جستجوی واحد سازمانی"
                value={unitId}
                onValueChange={setUnitId}
                load={loadUnitOptions}
                placeholder="بدون واحد"
                clearLabel="بدون واحد"
              />
            </FormField>
            <FormField label="سمت">
              <HrSearchSelect
                searchLabel="جستجوی سمت"
                value={positionId}
                onValueChange={setPositionId}
                load={loadPositionOptions}
                placeholder="بدون سمت"
                clearLabel="بدون سمت"
              />
            </FormField>
            <FormField label="مدیر">
              <HrSearchSelect
                searchLabel="جستجوی مدیر"
                value={managerId}
                onValueChange={setManagerId}
                load={loadManagerOptions}
                placeholder="بدون مدیر"
                clearLabel="بدون مدیر"
              />
            </FormField>
            <FormField label="تاریخ شروع" required error={startError}>
              <JalaliDateField
                value={startDay}
                onChange={(value) => {
                  setStartDay(value);
                  setStartError(null);
                }}
                max={utcTodayIso()}
              />
            </FormField>
          </div>
          <div className="flex flex-wrap gap-2">
            <Button type="button" size="lg" disabled={submitting} onClick={() => void submitStart()}>
              {submitting ? 'در حال ثبت…' : 'ثبت انتساب'}
            </Button>
            <Button
              type="button"
              variant="outline"
              size="lg"
              disabled={submitting}
              onClick={() => {
                setMode('view');
                setStartError(null);
              }}
            >
              انصراف
            </Button>
          </div>
        </div>
      ) : null}

      {mode === 'change' && active !== null ? (
        <div className="sm:col-span-2 space-y-4 rounded-lg border border-line bg-white p-4">
          <h4 className="text-sm font-bold text-ink">انتقال کارمند</h4>
          <p className="text-xs leading-6 text-sub">
            هر فیلد خالی یعنی «مقدار فعلی حفظ شود». مقدار فعلی — واحد: {active.unitName ?? '—'} · سمت: {active.positionName ?? '—'}.
          </p>
          <div className="grid gap-4 sm:grid-cols-2">
            <FormField label="واحد سازمانی جدید" hint="خالی = حفظ واحد فعلی">
              <HrSearchSelect
                searchLabel="جستجوی واحد سازمانی"
                value={unitId}
                onValueChange={setUnitId}
                load={loadUnitOptions}
                placeholder="حفظ واحد فعلی"
                clearLabel="حفظ واحد فعلی"
              />
            </FormField>
            <FormField label="سمت جدید" hint="خالی = حفظ سمت فعلی">
              <HrSearchSelect
                searchLabel="جستجوی سمت"
                value={positionId}
                onValueChange={setPositionId}
                load={loadPositionOptions}
                placeholder="حفظ سمت فعلی"
                clearLabel="حفظ سمت فعلی"
              />
            </FormField>
            <FormField label="مدیر جدید" hint="خالی = حفظ مدیر فعلی">
              <HrSearchSelect
                searchLabel="جستجوی مدیر"
                value={managerId}
                onValueChange={setManagerId}
                load={loadManagerOptions}
                placeholder="حفظ مدیر فعلی"
                clearLabel="حفظ مدیر فعلی"
              />
            </FormField>
          </div>
          <div className="flex flex-wrap gap-2">
            <Button type="button" size="lg" disabled={submitting} onClick={() => void submitChange()}>
              {submitting ? 'در حال انتقال…' : 'ثبت انتقال'}
            </Button>
            <Button
              type="button"
              variant="outline"
              size="lg"
              disabled={submitting}
              onClick={() => {
                setMode('view');
                setError(null);
              }}
            >
              انصراف
            </Button>
          </div>
        </div>
      ) : null}

      <ConfirmDialog
        open={confirmingEnd}
        title="پایان انتساب جاری"
        description={
          <>
            <p>
              با پایان انتساب، کارمند از واحد و سمت جاری خارج می‌شود. این عمل در تاریخچه ثبت می‌شود و بازنویسی نمی‌شود. تاریخ خالی یعنی پایان از همین
              حالا.
            </p>
            <div className="mt-3">
              <FormField label="تاریخ پایان (اختیاری)" error={endError}>
                <JalaliDateField
                  value={endDay}
                  onChange={(value) => {
                    setEndDay(value);
                    setEndError(null);
                  }}
                  max={utcTodayIso()}
                  clearLabel="پایان از همین حالا"
                />
              </FormField>
            </div>
          </>
        }
        confirmLabel="پایان انتساب"
        tone="danger"
        busy={submitting}
        onCancel={() => {
          setConfirmingEnd(false);
          setEndError(null);
        }}
        onConfirm={() => submitEnd()}
      />
    </FormSection>
  );
}
