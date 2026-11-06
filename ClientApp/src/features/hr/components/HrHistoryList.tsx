import { useEffect, useState } from 'react';
import { ApiError } from '@/api/client';
import { DataTable, type DataTableColumn } from '@/components/ui/DataTable';
import { formatHrInstant } from '../lib/hrFormat';
import { hrEventTypeTitle } from '../lib/hrLabels';
import { getHrEmployeeHistory } from '../api/hrClient';
import type { HrEmployeeHistoryItem } from '../api/hrTypes';

type Props = {
  companyId: string;
  employeeId: string;
  onUnauthenticated: (error: unknown) => boolean;
};

const pageSizes = [10, 25, 50] as const;

/**
 * The employee's audit history (`GET /org/employees/{id}/history`). The backend exposes exactly
 * the six allowlisted fields — no payload, no IP, no details JSON — so this list is read-only by
 * construction and labels unknown event types with their raw code instead of hiding them.
 */
export function HrHistoryList({ companyId, employeeId, onUnauthenticated }: Props) {
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState<number>(25);
  const [items, setItems] = useState<readonly HrEmployeeHistoryItem[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [failure, setFailure] = useState<string | null>(null);
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setFailure(null);
    getHrEmployeeHistory(companyId, employeeId, page, pageSize, controller.signal)
      .then((result) => {
        if (controller.signal.aborted) return;
        setItems(result.items);
        setTotal(result.totalCount);
        setLoading(false);
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || onUnauthenticated(cause)) return;
        setFailure(cause instanceof ApiError ? cause.message : 'دریافت تاریخچه انجام نشد. لطفاً دوباره تلاش کنید.');
        setLoading(false);
      });
    return () => controller.abort();
  }, [companyId, employeeId, page, pageSize, attempt, onUnauthenticated]);

  useEffect(() => setPage(1), [employeeId]);

  const columns = [
    {
      key: 'event',
      header: 'رویداد',
      cell: (item: HrEmployeeHistoryItem) => <span className="font-medium">{hrEventTypeTitle(item.eventType)}</span>,
    },
    {
      key: 'entity',
      header: 'موجودیت',
      cell: (item: HrEmployeeHistoryItem) => (
        <span className="flex flex-col">
          <span>{item.entityType}</span>
          <code dir="ltr" className="text-[11px] text-sub">
            {item.entityId}
          </code>
        </span>
      ),
    },
    { key: 'time', header: 'زمان', cell: (item: HrEmployeeHistoryItem) => formatHrInstant(item.occurredAtUtc) },
    {
      key: 'actor',
      header: 'کاربر عامل',
      cell: (item: HrEmployeeHistoryItem) =>
        item.actorUserId ? (
          <code dir="ltr" className="text-xs">
            {item.actorUserId}
          </code>
        ) : (
          '—'
        ),
    },
    {
      key: 'correlation',
      header: 'شناسه همبستگی',
      cell: (item: HrEmployeeHistoryItem) =>
        item.correlationId ? (
          <code dir="ltr" className="text-xs text-sub">
            {item.correlationId}
          </code>
        ) : (
          '—'
        ),
    },
  ] as const satisfies readonly DataTableColumn<HrEmployeeHistoryItem>[];

  return (
    <DataTable
      caption="تاریخچه رخدادهای کارمند"
      columns={columns}
      rows={items}
      getRowKey={(item) => `${item.entityType}:${item.entityId}:${item.occurredAtUtc}:${item.eventType}`}
      loading={loading}
      error={failure}
      onRetry={() => setAttempt((value) => value + 1)}
      emptyTitle="رخدادی ثبت نشده است"
      emptyDescription="به‌محض ثبت کارمند یا تغییر انتساب او، رویدادها اینجا نمایش داده می‌شوند."
      pagination={{
        page,
        pageSize,
        total,
        pageSizeOptions: pageSizes,
        onPageChange: setPage,
        onPageSizeChange: (size) => {
          setPageSize(size);
          setPage(1);
        },
      }}
    />
  );
}
