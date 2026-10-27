import { useEffect, useState } from 'react';
import { AlertCircle, ChevronLeft, ChevronRight, LoaderCircle, RefreshCw } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';

export type BusinessSection = 'customers' | 'installations' | 'contracts' | 'licenses';

type PageResponse<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
};
type Row = Record<string, unknown>;
type Column = { key: string; label: string; kind?: 'date' | 'status' | 'environment' };

const resources: Record<BusinessSection, { title: string; description: string; endpoint: string; columns: Column[] }> = {
  customers: {
    title: 'مشتریان تجاری',
    description: 'فهرست مشتریان ثبت‌شده در سامانه مرکزی فروشنده.',
    endpoint: '/api/vendor/customers/',
    columns: [
      { key: 'customerNumber', label: 'شماره مشتری' },
      { key: 'legalName', label: 'نام حقوقی' },
      { key: 'displayName', label: 'نام نمایشی' },
      { key: 'status', label: 'وضعیت', kind: 'status' },
      { key: 'createdAtUtc', label: 'تاریخ ثبت', kind: 'date' },
    ],
  },
  installations: {
    title: 'نصب‌ها',
    description: 'نصب‌ها و محیط‌های ثبت‌شده برای مشتریان تجاری.',
    endpoint: '/api/vendor/installations/',
    columns: [
      { key: 'installationCode', label: 'کد نصب' },
      { key: 'name', label: 'نام نصب' },
      { key: 'environment', label: 'محیط', kind: 'environment' },
      { key: 'status', label: 'وضعیت', kind: 'status' },
      { key: 'lastSeenAtUtc', label: 'آخرین مشاهده', kind: 'date' },
    ],
  },
  contracts: {
    title: 'قراردادها',
    description: 'قراردادهای تجاری و بازه اعتبار آن‌ها.',
    endpoint: '/api/vendor/contracts/',
    columns: [
      { key: 'contractNumber', label: 'شماره قرارداد' },
      { key: 'status', label: 'وضعیت', kind: 'status' },
      { key: 'startsAtUtc', label: 'شروع اعتبار', kind: 'date' },
      { key: 'endsAtUtc', label: 'پایان اعتبار', kind: 'date' },
      { key: 'currencyCode', label: 'ارز' },
    ],
  },
  licenses: {
    title: 'مجوزها و لایسنس‌ها',
    description: 'مجوزهای ثبت‌شده؛ وضعیت این فهرست به‌معنی صدور یا امضای رمزنگاری‌شده نیست.',
    endpoint: '/api/vendor/licenses/',
    columns: [
      { key: 'licenseNumber', label: 'شماره لایسنس' },
      { key: 'productCode', label: 'محصول' },
      { key: 'status', label: 'وضعیت', kind: 'status' },
      { key: 'validFromUtc', label: 'شروع اعتبار', kind: 'date' },
      { key: 'validUntilUtc', label: 'پایان اعتبار', kind: 'date' },
    ],
  },
};

const statusLabels: Record<string, string> = {
  Active: 'فعال',
  Draft: 'پیش‌نویس',
  Suspended: 'تعلیق‌شده',
  Expired: 'منقضی',
  Revoked: 'لغوشده',
};
const environmentLabels: Record<string, string> = {
  Production: 'عملیاتی',
  Staging: 'آزمایشی',
  Development: 'توسعه',
};

function formatValue(value: unknown, kind?: Column['kind']): string {
  if (value === null || value === undefined || value === '') return '—';
  const raw = String(value);
  if (kind === 'status') return statusLabels[raw] ?? raw;
  if (kind === 'environment') return environmentLabels[raw] ?? raw;
  if (kind === 'date') {
    const date = new Date(raw);
    if (Number.isNaN(date.getTime())) return '—';
    return new Intl.DateTimeFormat('fa-IR', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'UTC' }).format(date);
  }
  return raw;
}

function errorFor(status: number): string {
  if (status === 401) return 'نشست شما معتبر نیست یا پایان یافته است؛ دوباره وارد شوید.';
  if (status === 403) return 'حساب شما مجوز مشاهده این بخش را ندارد.';
  if (status === 429) return 'تعداد درخواست‌ها زیاد است؛ کمی بعد دوباره تلاش کنید.';
  if (status >= 500) return 'خطای داخلی سامانه رخ داد. شناسه یا جزئیات حساس نمایش داده نمی‌شود.';
  return 'دریافت اطلاعات ناموفق بود. وضعیت درخواست را بررسی و دوباره تلاش کنید.';
}

export function BusinessLists({ section }: { section: BusinessSection }) {
  const resource = resources[section];
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [data, setData] = useState<PageResponse<Row> | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    let active = true;
    setLoading(true);
    setError('');
    fetch(`${resource.endpoint}?page=${page}&pageSize=${pageSize}`, {
      credentials: 'same-origin',
      cache: 'no-store',
      headers: { Accept: 'application/json' },
    }).then(async response => {
      if (!response.ok) throw new Error(errorFor(response.status));
      const result = await response.json() as PageResponse<Row>;
      if (!Array.isArray(result.items) || typeof result.totalCount !== 'number' ||
          typeof result.page !== 'number' || typeof result.pageSize !== 'number' ||
          typeof result.totalPages !== 'number' || typeof result.hasNextPage !== 'boolean' ||
          typeof result.hasPreviousPage !== 'boolean') {
        throw new Error('پاسخ سرور با قرارداد صفحه‌بندی مورد انتظار سازگار نیست.');
      }
      if (active) setData(result);
    }).catch(reason => {
      if (active) {
        setData(null);
        setError(reason instanceof Error ? reason.message : 'دریافت اطلاعات ناموفق بود.');
      }
    }).finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [resource.endpoint, page, pageSize]);

  function changePageSize(value: string) {
    const next = Number(value);
    if ([25, 50, 100].includes(next)) {
      setPageSize(next);
      setPage(1);
    }
  }

  return <section className="space-y-5" aria-labelledby="business-page-title" dir="rtl">
    <div className="flex flex-wrap items-start justify-between gap-3">
      <div>
        <span className="eyebrow">مدیریت تجاری</span>
        <h1 id="business-page-title" className="mt-1 text-2xl font-bold text-[#193b57]">{resource.title}</h1>
        <p className="mt-2 text-sm leading-7 text-muted-foreground">{resource.description}</p>
      </div>
      <Button type="button" variant="outline" onClick={() => { setData(null); setPage(current => current); }} disabled={loading}>
        <RefreshCw size={16}/> تازه‌سازی
      </Button>
    </div>

    <Card>
      <CardHeader className="border-b">
        <CardTitle>{resource.title}</CardTitle>
        <CardDescription>اطلاعات از API سامانه خوانده می‌شود؛ دادهٔ نمونه یا ساختگی نمایش داده نمی‌شود.</CardDescription>
      </CardHeader>
      <CardContent className="pt-4">
        {loading ? <div className="flex min-h-36 items-center justify-center gap-3 text-sm text-muted-foreground" role="status"><LoaderCircle className="animate-spin" size={20}/> در حال دریافت اطلاعات…</div>
        : error ? <div className="flex min-h-36 flex-col items-center justify-center gap-3 text-center" role="alert"><AlertCircle className="text-destructive" size={26}/><p className="max-w-xl text-sm leading-7 text-destructive">{error}</p><Button type="button" variant="outline" onClick={() => { setError(''); setLoading(true); setPage(current => current); }}>تلاش دوباره</Button></div>
        : !data || data.items.length === 0 ? <div className="flex min-h-36 flex-col items-center justify-center gap-2 text-center"><span className="text-sm font-semibold">رکوردی برای نمایش وجود ندارد</span><p className="text-sm text-muted-foreground">پس از ثبت اطلاعات، موارد در این فهرست نمایش داده می‌شوند.</p></div>
        : <div className="overflow-x-auto">
          <table className="w-full min-w-[680px] border-separate border-spacing-0 text-right text-sm">
            <thead><tr>{resource.columns.map(column => <th key={column.key} scope="col" className="border-b border-border bg-muted/60 px-4 py-3 font-semibold text-muted-foreground">{column.label}</th>)}</tr></thead>
            <tbody>{data.items.map((row, index) => <tr key={String(row.id ?? row.customerNumber ?? row.installationCode ?? row.contractNumber ?? row.licenseNumber ?? index)} className="hover:bg-muted/30">
              {resource.columns.map(column => <td key={column.key} className="border-b border-border px-4 py-3 text-foreground">{formatValue(row[column.key], column.kind)}</td>)}
            </tr>)}</tbody>
          </table>
        </div>}
        {!loading && !error && data && <div className="mt-4 flex flex-wrap items-center justify-between gap-3 border-t border-border pt-4">
          <div className="flex flex-wrap items-center gap-3 text-xs text-muted-foreground">
            <span>تعداد کل: <b className="text-foreground">{new Intl.NumberFormat('fa-IR').format(data.totalCount)}</b></span>
            <label className="flex items-center gap-2">تعداد در صفحه
              <select aria-label="تعداد در صفحه" className="h-9 rounded-md border border-input bg-background px-2 text-foreground" value={pageSize} onChange={event => changePageSize(event.target.value)}>
                <option value={25}>۲۵</option><option value={50}>۵۰</option><option value={100}>۱۰۰</option>
              </select>
            </label>
            <span>صفحه {new Intl.NumberFormat('fa-IR').format(data.page)} از {new Intl.NumberFormat('fa-IR').format(data.totalPages)}</span>
          </div>
          <div className="flex items-center gap-2">
            <Button type="button" variant="outline" size="sm" disabled={!data.hasPreviousPage || loading} onClick={() => setPage(current => Math.max(1, current - 1))}><ChevronRight size={16}/> قبلی</Button>
            <Button type="button" variant="outline" size="sm" disabled={!data.hasNextPage || loading} onClick={() => setPage(current => current + 1)}>بعدی <ChevronLeft size={16}/></Button>
          </div>
        </div>}
      </CardContent>
    </Card>
  </section>;
}
