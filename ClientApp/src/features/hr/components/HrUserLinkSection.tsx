import { useEffect, useMemo, useState } from 'react';
import { Alert } from '@/components/ui/Alert';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/ui/ConfirmDialog';
import { FormSection } from '@/components/ui/FormSection';
import { Badge } from '@/components/ui/Badge';
import { ApiError, getCompanyUsers } from '@/api/client';
import { useCompanyPermissions } from '@/lib/useCompanyPermissions';
import { linkHrEmployeeUser, unlinkHrEmployeeUser } from '../api/hrClient';
import type { HrEmployeeDetail } from '../api/hrTypes';
import { HrSearchSelect } from './HrSearchSelect';

type Props = {
  companyId: string;
  employee: HrEmployeeDetail;
  onUnauthenticated: (error: unknown) => boolean;
  onChanged: () => void;
};

/**
 * The optional association between an employee and an application user (D-072). An employee never
 * needs a user account; linking resolves the chosen user's ACTIVE membership of THIS company on
 * the server. The picker needs the company's `users.list` permission: the backend has no
 * HR-scoped user lookup endpoint, so without it the association workflow cannot search users and
 * the page says so instead of pretending (see docs/handoffs/HR_FRONTEND_INTEGRATION.md).
 */
export function HrUserLinkSection({ companyId, employee, onUnauthenticated, onChanged }: Props) {
  const { codes, status, retry } = useCompanyPermissions(companyId, onUnauthenticated);
  const canPickUsers = codes.includes('users.list');

  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [selectedUserId, setSelectedUserId] = useState<string | null>(null);
  const [confirmingUnlink, setConfirmingUnlink] = useState(false);

  useEffect(() => {
    setSelectedUserId(null);
    setError(null);
    setSuccess(null);
    setConfirmingUnlink(false);
  }, [employee.id]);

  const linked = employee.linkedUserId !== null && employee.userCompanyMembershipId !== null;

  const loadUserOptions = useMemo(
    () => async (search: string, signal: AbortSignal) => {
      const directory = await getCompanyUsers(
        companyId,
        { ...(search.trim() === '' ? {} : { search: search.trim() }), membershipActive: true, page: 1, pageSize: 25 },
        signal,
      );
      return directory.items.map((user) => ({
        key: user.userId,
        label: [user.firstName, user.lastName].filter(Boolean).join(' ') || user.username || 'کاربر بدون نام',
        detail: user.username ?? null,
      }));
    },
    [companyId],
  );

  const link = async () => {
    if (selectedUserId === null) return;
    setSubmitting(true);
    setError(null);
    try {
      await linkHrEmployeeUser(companyId, employee.id, selectedUserId);
      setSuccess('حساب کاربری به کارمند متصل شد.');
      setSelectedUserId(null);
      onChanged();
    } catch (cause: unknown) {
      if (onUnauthenticated(cause)) return;
      setError(cause instanceof ApiError ? cause.message : 'اتصال حساب انجام نشد. لطفاً دوباره تلاش کنید.');
    } finally {
      setSubmitting(false);
    }
  };

  const unlink = async () => {
    setSubmitting(true);
    setError(null);
    try {
      await unlinkHrEmployeeUser(companyId, employee.id);
      setSuccess('اتصال حساب کاربری جدا شد.');
      setConfirmingUnlink(false);
      onChanged();
    } catch (cause: unknown) {
      if (onUnauthenticated(cause)) return;
      setError(cause instanceof ApiError ? cause.message : 'جداسازی حساب انجام نشد. لطفاً دوباره تلاش کنید.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <FormSection
      title="حساب کاربری"
      description="اتصال کارمند به حساب کاربری اختیاری است؛ کارمند می‌تواند بدون حساب کاربری ثبت شود. اتصال فقط به عضو فعال همین شرکت ممکن است و هیچ مجوزی از این راه به کارمند داده نمی‌شود."
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

      <div className="sm:col-span-2 rounded-lg border border-line bg-cool/60 p-4">
        <div className="flex flex-wrap items-center gap-2">
          <h4 className="text-sm font-bold text-ink">وضعیت اتصال</h4>
          {linked ? (
            <Badge tone="success" icon={null}>
              متصل به حساب کاربری
            </Badge>
          ) : (
            <Badge tone="neutral" icon={null}>
              بدون حساب کاربری
            </Badge>
          )}
        </div>
        {linked ? (
          <p className="mt-2 text-sm text-sub">
            شناسه کاربر متصل:{' '}
            <code dir="ltr" className="text-xs">
              {employee.linkedUserId}
            </code>
          </p>
        ) : (
          <p className="mt-2 text-sm text-sub">این کارمند به هیچ حساب کاربری متصل نیست و بدون اتصال هم می‌تواند به کار خود ادامه دهد.</p>
        )}
      </div>

      {linked ? (
        <div className="sm:col-span-2 flex flex-wrap gap-2">
          <Button
            type="button"
            variant="outline"
            size="lg"
            disabled={submitting}
            className="border-danger/40 text-danger hover:bg-danger-soft"
            onClick={() => setConfirmingUnlink(true)}
          >
            جداسازی حساب کاربری
          </Button>
        </div>
      ) : status === 'error' ? (
        <div className="sm:col-span-2">
          <Alert
            tone="warning"
            action={
              <Button type="button" variant="outline" size="lg" onClick={retry}>
                تلاش دوباره
              </Button>
            }
          >
            دریافت مجوزهای شرکت برای نمایش فرم اتصال انجام نشد.
          </Alert>
        </div>
      ) : canPickUsers && status === 'ready' ? (
        <div className="sm:col-span-2 space-y-4 rounded-lg border border-line bg-white p-4">
          <h4 className="text-sm font-bold text-ink">اتصال به حساب کاربری موجود</h4>
          <p className="text-xs leading-6 text-sub">
            حساب جدید ساخته نمی‌شود؛ فقط یکی از کاربران عضو فعال همین شرکت انتخاب می‌شود. هر حساب در هر شرکت به حداکثر یک کارمند متصل می‌شود.
          </p>
          <HrSearchSelect
            searchLabel="انتخاب کاربر (جستجو)"
            value={selectedUserId}
            onValueChange={setSelectedUserId}
            load={loadUserOptions}
            placeholder="انتخاب کاربر"
            clearLabel="پاک‌کردن انتخاب کاربر"
            emptyLabel="کاربر فعالی با این جستجو پیدا نشد"
          />
          <Button type="button" size="lg" disabled={submitting || selectedUserId === null} onClick={() => void link()}>
            {submitting ? 'در حال اتصال…' : 'اتصال حساب'}
          </Button>
        </div>
      ) : (
        <div className="sm:col-span-2">
          <Alert tone="info">
            برای جستجوی کاربران جهت اتصال، مجوز «کاربران شرکت» (users.list) لازم است؛ Backend فهرست کاربران مخصوص HR ندارد و این صفحه به‌جای حدس، همین
            محدودیت را نشان می‌دهد.
          </Alert>
        </div>
      )}

      <ConfirmDialog
        open={confirmingUnlink}
        title="جداسازی حساب کاربری"
        description="کارمند بدون حساب کاربری همچنان در فهرست منابع انسانی می‌ماند؛ فقط اتصال به حساب جدا می‌شود."
        confirmLabel="جداسازی"
        tone="danger"
        busy={submitting}
        onCancel={() => setConfirmingUnlink(false)}
        onConfirm={() => unlink()}
      />
    </FormSection>
  );
}
