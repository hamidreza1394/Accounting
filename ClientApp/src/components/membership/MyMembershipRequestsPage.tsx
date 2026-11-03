import { useEffect, useMemo, useState } from 'react';
import { ApiError, getMyMembershipRequests } from '../../api/client';
import type { MembershipRequestPage, MyMembershipRequest } from '../../api/types';
import { formatDateTime } from '../../lib/format';
import { Alert } from '../ui/Alert';
import { Breadcrumb } from '../ui/Breadcrumb';
import { DataTable, type DataTableColumn } from '../ui/DataTable';
import { MembershipRequestStatusBadge } from './membershipStatus';

type Props = { onUnauthenticated: (error: unknown) => boolean };

const pageSizes = [10, 20, 50] as const;

function describe(cause: unknown): string {
  return cause instanceof ApiError ? cause.message : 'دریافت درخواست‌های عضویت انجام نشد. لطفاً دوباره تلاش کنید.';
}

/**
 * The signed-in user's own cross-company membership requests with their outcome. A request never
 * grants access by itself: only the destination company's approval creates a membership (D-065).
 */
export function MyMembershipRequestsPage({ onUnauthenticated }: Props) {
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState<number>(20);
  const [data, setData] = useState<MembershipRequestPage<MyMembershipRequest> | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError(null);
    getMyMembershipRequests(page, pageSize, controller.signal)
      .then((result) => {
        if (controller.signal.aborted) return;
        if (result.items.length === 0 && page > 1) {
          const lastPage = Math.max(1, Math.ceil(result.totalCount / pageSize));
          if (lastPage < page) {
            setPage(lastPage);
            return;
          }
        }
        setData(result);
        setLoading(false);
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || onUnauthenticated(cause)) return;
        setError(describe(cause));
        setLoading(false);
      });
    return () => controller.abort();
  }, [page, pageSize, attempt, onUnauthenticated]);

  const columns = useMemo<readonly DataTableColumn<MyMembershipRequest>[]>(
    () => [
      { key: 'company', header: 'شرکت مقصد', cell: (request) => request.destinationCompanyName },
      { key: 'status', header: 'وضعیت', cell: (request) => <MembershipRequestStatusBadge status={request.status} /> },
      { key: 'requested', header: 'تاریخ درخواست', cell: (request) => formatDateTime(request.requestedAtUtc) },
      { key: 'decided', header: 'تاریخ تصمیم', cell: (request) => formatDateTime(request.decidedAtUtc, '—') },
      { key: 'role', header: 'گروه دسترسی تأییدشده', cell: (request) => request.approvedRoleName ?? '—' },
      { key: 'reason', header: 'توضیح تصمیم', cell: (request) => request.decisionReason ?? '—' },
    ],
    [],
  );

  return (
    <div className="space-y-5">
      <header className="space-y-2">
        <Breadcrumb items={[{ label: 'حساب کاربری' }, { label: 'درخواست‌های عضویت من' }]} />
        <h1 className="text-2xl font-extrabold tracking-tight text-title">درخواست‌های عضویت من</h1>
        <p className="text-sm text-sub">
          وضعیت درخواست‌هایی که برای عضویت در شرکت‌های دیگر ثبت کرده‌اید.
        </p>
      </header>

      <Alert tone="info">تا زمانی که مدیر مجاز شرکت مقصد درخواست را تأیید نکند، هیچ دسترسی‌ای به داده‌های آن شرکت ایجاد نمی‌شود.</Alert>

      <DataTable
        caption="فهرست درخواست‌های عضویت من"
        columns={columns}
        rows={data?.items ?? []}
        getRowKey={(request) => request.id}
        loading={loading}
        error={error}
        onRetry={() => setAttempt((value) => value + 1)}
        emptyTitle="درخواست عضویتی ثبت نشده است"
        emptyDescription="هنوز برای عضویت در شرکت دیگری درخواستی ثبت نکرده‌اید."
        pagination={{
          page,
          pageSize,
          total: data?.totalCount ?? 0,
          pageSizeOptions: pageSizes,
          onPageChange: setPage,
          onPageSizeChange: (size) => {
            setPageSize(size);
            setPage(1);
          },
        }}
      />
    </div>
  );
}
