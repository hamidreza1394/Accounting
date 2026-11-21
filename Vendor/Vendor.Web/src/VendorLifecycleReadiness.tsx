import { useCallback, useEffect, useRef, useState } from 'react';
import { RefreshCw, Search } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';

type PageResponse<T> = {
  items: T[];
  page: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
};

type CustomerOption = { id: string; customerNumber: string; legalName: string; displayName: string | null };

type LifecycleStage = {
  key: string;
  status: 'Ready' | 'Pending' | 'Blocked' | 'Failed' | 'NotApplicable';
  reason: string | null;
  referenceId: string | null;
  changedAtUtc: string | null;
  correctiveAction: string | null;
};

type ReadinessResponse = {
  customerId: string;
  customerNumber: string;
  displayName: string;
  generatedAtUtc: string;
  stages: LifecycleStage[];
};

const stageTitles: Record<string, string> = {
  customerRegistration: 'ثبت مشتری',
  commercialOrder: 'سفارش تجاری',
  payment: 'پرداخت',
  issuanceApproval: 'درخواست تأیید صدور',
  entitlement: 'Entitlement (لایسنس)',
  amendment: 'Amendment',
  signing: 'امضا',
  signatureVerification: 'اعتبارسنجی امضا',
  delivery: 'تحویل',
  ack: 'پاسخ امضاشده مشتری (ACK)',
  installation: 'نصب',
  bootstrap: 'راه‌اندازی اولیه مشتری (Bootstrap)',
  provisioning: 'Provisioning',
  runtimeGate: 'گیت اجرایی (Runtime Gate)',
};

const statusLabels: Record<LifecycleStage['status'], string> = {
  Ready: 'آماده',
  Pending: 'در جریان',
  Blocked: 'مسدود',
  Failed: 'ناموفق',
  NotApplicable: 'نامعتبر برای این مرحله',
};

const statusClasses: Record<LifecycleStage['status'], string> = {
  Ready: 'text-green-700',
  Pending: 'text-amber-700',
  Blocked: 'text-destructive',
  Failed: 'text-destructive',
  NotApplicable: 'text-muted-foreground',
};

function formatDate(value: string | null): string {
  return value ? new Date(value).toLocaleString('fa-IR') : '—';
}

function apiMessage(status: number): string {
  if (status === 401) return 'نشست شما منقضی شده است؛ دوباره وارد شوید.';
  if (status === 403) return 'برای مشاهده وضعیت این مشتری مجوز کافی ندارید.';
  if (status === 404) return 'مشتری موردنظر یافت نشد؛ فهرست را تازه‌سازی کنید.';
  if (status === 400) return 'درخواست معتبر نیست؛ شناسه مشتری را بررسی کنید.';
  return 'دریافت وضعیت چرخه ناموفق بود؛ دوباره تلاش کنید.';
}

export function VendorLifecycleReadiness({ permissions }: { permissions: string[] }) {
  const canRead = permissions.includes('vendor.customers.read');
  const [customers, setCustomers] = useState<CustomerOption[]>([]);
  const [search, setSearch] = useState('');
  const [selected, setSelected] = useState<CustomerOption | null>(null);
  const [readiness, setReadiness] = useState<ReadinessResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const listRequestId = useRef(0);

  const loadCustomers = useCallback(async () => {
    if (!canRead) { setError('مجوز مشاهده مشتریان را ندارید.'); setLoading(false); return; }
    const requestId = ++listRequestId.current;
    setLoading(true);
    setError('');
    try {
      const response = await fetch('/api/vendor/customers/?page=1&pageSize=100', { credentials: 'same-origin', cache: 'no-store', headers: { Accept: 'application/json' } });
      if (!response.ok) throw new Error(apiMessage(response.status));
      const data = (await response.json()) as PageResponse<CustomerOption>;
      if (listRequestId.current !== requestId) return;
      setCustomers(data.items);
    } catch (reason) {
      if (listRequestId.current === requestId) {
        setError(reason instanceof Error ? reason.message : 'دریافت فهرست مشتریان ناموفق بود.');
      }
    } finally {
      if (listRequestId.current === requestId) setLoading(false);
    }
  }, [canRead]);

  useEffect(() => { void loadCustomers(); }, [loadCustomers]);

  const loadReadiness = useCallback(async (customer: CustomerOption) => {
    if (busy) return;
    setBusy(true);
    setError('');
    setReadiness(null);
    try {
      const response = await fetch(`/api/vendor/customers/${customer.id}/readiness`, { credentials: 'same-origin', cache: 'no-store', headers: { Accept: 'application/json' } });
      if (!response.ok) throw new Error(apiMessage(response.status));
      setReadiness(await response.json() as ReadinessResponse);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'دریافت وضعیت چرخه ناموفق بود.');
    } finally { setBusy(false); }
  }, [busy]);

  const filtered = search.trim()
    ? customers.filter(customer =>
        customer.customerNumber.toLowerCase().includes(search.trim().toLowerCase()) ||
        customer.legalName.includes(search.trim()) ||
        (customer.displayName ?? '').includes(search.trim()))
    : customers;

  const summary = readiness ? {
    Ready: readiness.stages.filter(stage => stage.status === 'Ready').length,
    Pending: readiness.stages.filter(stage => stage.status === 'Pending').length,
    Blocked: readiness.stages.filter(stage => stage.status === 'Blocked').length,
    Failed: readiness.stages.filter(stage => stage.status === 'Failed').length,
  } : null;

  return <section aria-labelledby="vendor-readiness-title" dir="rtl" className="grid gap-4">
    <div className="page-heading"><div>
      <span className="eyebrow">تشخیص آمادگی</span>
      <h1 id="vendor-readiness-title">وضعیت چرخه مشتری</h1>
      <p className="text-sm text-muted-foreground">
        وضعیت هر مرحله از داده واقعی سامانه خوانده می‌شود؛ این صفحه فقط-خواندنی است و هیچ وضعیتی را تغییر نمی‌دهد.
      </p>
    </div></div>
    {error && <div className="message error" role="alert">{error}</div>}
    {!canRead && <Card><CardContent><p className="text-sm text-muted-foreground">مجوز مشاهده مشتریان برای این صفحه لازم است.</p></CardContent></Card>}
    {canRead && <>
      <Card>
        <CardHeader>
          <CardTitle>انتخاب مشتری</CardTitle>
          <CardDescription>مشتری را جست‌وجو و انتخاب کنید تا وضعیت واقعی چرخه او نمایش داده شود.</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="flex flex-wrap items-center gap-2">
            <div className="relative flex-1 min-w-[220px]">
              <Search size={16} className="absolute start-2 top-1/2 -translate-y-1/2 text-muted-foreground" aria-hidden="true"/>
              <input aria-label="جست‌وجوی مشتری" placeholder="شماره مشتری یا نام…" dir="auto"
                className="h-9 w-full rounded-md border border-input bg-background ps-8 pe-2"
                value={search} onChange={event => setSearch(event.target.value)} />
            </div>
            <Button type="button" variant="outline" onClick={() => void loadCustomers()} disabled={loading || busy}>
              <RefreshCw size={15}/> تازه‌سازی فهرست
            </Button>
          </div>
          {loading
            ? <p className="mt-3 text-sm text-muted-foreground" role="status">در حال بارگذاری مشتریان…</p>
            : filtered.length === 0
              ? <p className="mt-3 text-sm text-muted-foreground">مشتری‌ای یافت نشد.</p>
              : <ul className="mt-3 grid gap-1 max-h-64 overflow-y-auto">
                {filtered.map(customer => <li key={customer.id}>
                  <button type="button" className={'w-full rounded-md border border-border px-3 py-2 text-start text-sm hover:bg-accent ' + (selected?.id === customer.id ? 'bg-accent' : '')}
                    onClick={() => { setSelected(customer); void loadReadiness(customer); }}>
                    <b>{customer.displayName ?? customer.legalName}</b>
                    <span className="ms-2 font-mono text-xs text-muted-foreground" dir="ltr">{customer.customerNumber}</span>
                  </button>
                </li>)}
              </ul>}
        </CardContent>
      </Card>
      {busy && <p className="text-sm text-muted-foreground" role="status">در حال محاسبه وضعیت…</p>}
      {readiness && summary && <Card>
        <CardHeader>
          <CardTitle>چرخه {readiness.displayName}</CardTitle>
          <CardDescription>
            مشتری <span dir="ltr" className="font-mono text-xs">{readiness.customerNumber}</span> — محاسبه‌شده در {formatDate(readiness.generatedAtUtc)} · آماده {summary.Ready}، در جریان {summary.Pending}، مسدود {summary.Blocked}، ناموفق {summary.Failed}
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="overflow-x-auto">
            <table className="w-full min-w-[640px] text-sm">
              <thead><tr className="border-b text-right">
                <th scope="col" className="px-3 py-2 font-medium">مرحله</th>
                <th scope="col" className="px-3 py-2 font-medium">وضعیت</th>
                <th scope="col" className="px-3 py-2 font-medium">علت / توضیح</th>
                <th scope="col" className="px-3 py-2 font-medium">اقدام اصلاحی</th>
                <th scope="col" className="px-3 py-2 font-medium">مرجع و زمان</th>
              </tr></thead>
              <tbody>
                {readiness.stages.map(stage => <tr key={stage.key} className="border-b last:border-0 align-top">
                  <td className="px-3 py-2 font-medium">{stageTitles[stage.key] ?? stage.key}</td>
                  <td className={'px-3 py-2 font-bold ' + statusClasses[stage.status]}>{statusLabels[stage.status]}</td>
                  <td className="px-3 py-2 text-muted-foreground">{stage.reason ?? '—'}</td>
                  <td className="px-3 py-2">{stage.correctiveAction ?? '—'}</td>
                  <td className="px-3 py-2 text-xs text-muted-foreground">
                    {stage.referenceId && <span className="font-mono" dir="ltr">{stage.referenceId}</span>}
                    {stage.referenceId && ' · '}
                    {formatDate(stage.changedAtUtc)}
                  </td>
                </tr>)}
              </tbody>
            </table>
          </div>
          <p className="mt-3 text-xs text-muted-foreground">
            وضعیت‌ها فقط از منابع واقعی خوانده می‌شوند؛ مرحله ناموفق هرگز با موفقیت مرحله قبلی پنهان نمی‌شود و اطلاعات محرمانه (کلید امضا، Secret) در این نما نمایش داده نمی‌شود.
          </p>
        </CardContent>
      </Card>}
    </>}
  </section>;
}

export default VendorLifecycleReadiness;
