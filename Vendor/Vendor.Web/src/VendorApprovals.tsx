import { useCallback, useEffect, useState } from 'react';
import { Check, ChevronLeft, ChevronRight, RefreshCw, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';

type Approval = {
  id: string;
  resourceType: 'customer' | 'installation';
  resourceId: string;
  targetStatus: 'Active' | 'Suspended';
  requestedByUserId: string;
  requestedAtUtc: string;
  status: 'Pending' | 'Approved' | 'Rejected' | 'Stale';
  rowVersion: string;
};
type PageResponse<T> = { items: T[]; page: number; pageSize: number; totalCount: number; totalPages: number; hasPreviousPage: boolean; hasNextPage: boolean };
type Props = { permissions: string[] };

function errorMessage(status: number, code: string) {
  if (status === 401) return 'نشست شما منقضی شده است؛ دوباره وارد شوید.';
  if (status === 403) return 'حساب شما مجوز بررسی درخواست را ندارد یا درخواست خودتان قابل تأیید نیست.';
  if (status === 409 && code === 'stale_approval_version') return 'درخواست هم‌زمان تغییر کرده است؛ فهرست را تازه‌سازی کنید.';
  if (status === 409 && code === 'approval_resource_stale') return 'رکورد اصلی پس از ثبت درخواست تغییر کرده است؛ درخواست به وضعیت منقضی منتقل شد.';
  if (status === 409 && code === 'approval_already_decided') return 'این درخواست قبلاً بررسی شده است.';
  return 'عملیات انجام نشد. وضعیت درخواست را تازه‌سازی کنید.';
}

export function VendorApprovals({ permissions }: Props) {
  const [items, setItems] = useState<Approval[]>([]);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [totalPages, setTotalPages] = useState(0);
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const canRead = permissions.includes('vendor.approvals.read');
  const canReview = permissions.includes('vendor.approvals.review');

  const load = useCallback(async () => {
    if (!canRead) {
      setLoading(false);
      setError('حساب شما مجوز مشاهده درخواست‌های تأیید را ندارد.');
      return;
    }
    setLoading(true);
    setError('');
    try {
      const response = await fetch(`/api/vendor/approvals/?status=Pending&page=${page}&pageSize=${pageSize}`, {
        credentials: 'same-origin',
        cache: 'no-store',
        headers: { Accept: 'application/json' },
      });
      if (!response.ok) throw new Error(errorMessage(response.status, ''));
      const page = await response.json() as PageResponse<Approval>;
      if (!Array.isArray(page.items) || typeof page.totalPages !== 'number' || typeof page.hasNextPage !== 'boolean' || typeof page.hasPreviousPage !== 'boolean') throw new Error('پاسخ فهرست درخواست‌ها معتبر نیست.');
      setItems(page.items);
      setTotalPages(page.totalPages);
      if (page.items.length === 0 && page.page > 1 && page.page > page.totalPages) setPage(Math.max(1, page.totalPages));
    } catch (reason) {
      setItems([]);
      setError(reason instanceof Error ? reason.message : 'دریافت درخواست‌ها ناموفق بود.');
    } finally {
      setLoading(false);
    }
  }, [canRead, page, pageSize]);

  useEffect(() => { void load(); }, [load]);

  async function decide(item: Approval, decision: 'Approved' | 'Rejected') {
    if (!canReview || busyId) return;
    setBusyId(item.id);
    setError('');
    setNotice('');
    try {
      const tokenResponse = await fetch('/api/auth/antiforgery', {
        credentials: 'same-origin',
        cache: 'no-store',
      });
      if (!tokenResponse.ok) throw new Error(errorMessage(tokenResponse.status, ''));
      const token = await tokenResponse.json() as { requestToken?: string };
      if (!token.requestToken) throw new Error('توکن امنیتی در پاسخ سرور وجود ندارد.');
      const response = await fetch(`/api/vendor/approvals/${item.id}/decision`, {
        method: 'POST',
        credentials: 'same-origin',
        cache: 'no-store',
        headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': token.requestToken },
        body: JSON.stringify({ decision, rowVersion: item.rowVersion, comment: null }),
      });
      if (!response.ok) {
        let code = '';
        try { code = ((await response.json()) as { error?: string }).error ?? ''; } catch { /* use generic message */ }
        if (response.status === 409) await load();
        throw new Error(errorMessage(response.status, code));
      }
      setNotice(decision === 'Approved' ? 'درخواست با موفقیت تأیید و اعمال شد.' : 'درخواست رد شد.');
      await load();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'بررسی درخواست ناموفق بود.');
    } finally {
      setBusyId(null);
    }
  }

  return <section className="space-y-5" dir="rtl" aria-labelledby="vendor-approvals-title">
    <div className="flex flex-wrap items-center justify-between gap-3">
      <div>
        <h1 id="vendor-approvals-title" className="text-xl font-semibold">درخواست‌های تأیید</h1>
        <p className="mt-1 text-sm text-muted-foreground">تغییر وضعیت مشتری یا نصب فقط با تأیید مدیر دیگری اعمال می‌شود.</p>
      </div>
      <Button type="button" variant="outline" onClick={() => void load()} disabled={loading}>
        <RefreshCw size={16} aria-hidden="true"/> تازه‌سازی
      </Button>
    </div>
    {error && <p role="alert" className="rounded-md border border-destructive/40 p-3 text-sm">{error}</p>}
    {notice && <p role="status" className="rounded-md border border-border p-3 text-sm">{notice}</p>}
    <Card>
      <CardHeader>
        <CardTitle>در انتظار بررسی</CardTitle>
        <CardDescription>درخواست‌کننده اجازه تأیید درخواست خودش را ندارد.</CardDescription>
      </CardHeader>
      <CardContent>
        {loading ? <p role="status" className="py-6 text-sm text-muted-foreground">در حال دریافت درخواست‌ها…</p>
          : items.length === 0 ? <p className="py-6 text-sm text-muted-foreground">درخواست در انتظاری وجود ندارد.</p>
          : <div className="overflow-x-auto">
            <table className="w-full min-w-[680px] text-sm">
              <thead><tr className="border-b text-right">
                <th scope="col" className="px-3 py-3 font-medium">نوع منبع</th>
                <th scope="col" className="px-3 py-3 font-medium">شناسه منبع</th>
                <th scope="col" className="px-3 py-3 font-medium">وضعیت پیشنهادی</th>
                <th scope="col" className="px-3 py-3 font-medium">درخواست‌کننده</th>
                <th scope="col" className="px-3 py-3 font-medium">زمان ثبت</th>
                <th scope="col" className="px-3 py-3 font-medium">عملیات</th>
              </tr></thead>
              <tbody>{items.map(item => <tr key={item.id} className="border-b last:border-0">
                <td className="px-3 py-3">{item.resourceType === 'customer' ? 'مشتری' : 'نصب'}</td>
                <td className="px-3 py-3 font-mono text-xs" dir="ltr">{item.resourceId}</td>
                <td className="px-3 py-3">{item.targetStatus === 'Suspended' ? 'تعلیق' : 'فعال‌سازی'}</td>
                <td className="px-3 py-3 font-mono text-xs" dir="ltr">{item.requestedByUserId}</td>
                <td className="px-3 py-3" dir="ltr">{new Intl.DateTimeFormat('fa-IR', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(item.requestedAtUtc))}</td>
                <td className="px-3 py-3"><div className="flex gap-2">
                  <Button type="button" size="sm" disabled={!canReview || Boolean(busyId)} onClick={() => void decide(item, 'Approved')} aria-label={`تأیید درخواست ${item.id}`}>
                    <Check size={15} aria-hidden="true"/> تأیید
                  </Button>
                  <Button type="button" size="sm" variant="outline" disabled={!canReview || Boolean(busyId)} onClick={() => void decide(item, 'Rejected')} aria-label={`رد درخواست ${item.id}`}>
                    <X size={15} aria-hidden="true"/> رد
                  </Button>
                </div></td>
              </tr>)}</tbody>
            </table>
          </div>}
        {totalPages > 1 && <div className="mt-4 flex flex-wrap items-center justify-between gap-3 border-t pt-4">
          <label className="flex items-center gap-2 text-sm text-muted-foreground">
            تعداد در صفحه
            <select className="h-9 rounded-md border border-input bg-background px-2 text-foreground" value={pageSize} onChange={event => { setPageSize(Number(event.target.value)); setPage(1); }} disabled={loading || Boolean(busyId)} aria-label="تعداد درخواست در صفحه">
              <option value={25}>۲۵</option><option value={50}>۵۰</option><option value={100}>۱۰۰</option>
            </select>
          </label>
          <div className="flex items-center gap-2">
            <span className="text-sm text-muted-foreground">صفحه {new Intl.NumberFormat('fa-IR').format(page)} از {new Intl.NumberFormat('fa-IR').format(totalPages)}</span>
            <Button type="button" variant="outline" size="sm" disabled={page <= 1 || loading || Boolean(busyId)} onClick={() => setPage(current => Math.max(1, current - 1))}><ChevronRight size={15} aria-hidden="true"/> قبلی</Button>
            <Button type="button" variant="outline" size="sm" disabled={page >= totalPages || loading || Boolean(busyId)} onClick={() => setPage(current => Math.min(totalPages, current + 1))}>بعدی <ChevronLeft size={15} aria-hidden="true"/></Button>
          </div>
        </div>}
        {!canReview && canRead && <p className="mt-3 text-sm text-muted-foreground">مجوز بررسی درخواست‌ها برای حساب شما فعال نیست.</p>}
      </CardContent>
    </Card>
  </section>;
}
