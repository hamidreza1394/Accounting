import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react';
import { ChevronLeft, ChevronRight, Plus, ReceiptText, RefreshCw } from 'lucide-react';
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

type OrderLine = {
  id: string;
  productCode: string;
  description: string;
  quantity: number;
  unitAmount: number;
  lineAmount: number;
  licenseId: string | null;
};

type PaymentRow = {
  id: string;
  orderId: string;
  amount: number;
  currencyCode: string;
  method: string;
  externalReference: string | null;
  claimedPaidAtUtc: string | null;
  status: 'Recorded' | 'Verified' | 'Rejected';
  verifiedByUserId: string | null;
  verifiedAtUtc: string | null;
  rejectionReason: string | null;
  rowVersion: string;
};

type OrderRow = {
  id: string;
  customerId: string;
  customerNumber: string;
  orderNumber: string;
  totalAmount: number;
  currencyCode: string;
  dueAtUtc: string | null;
  createdAtUtc: string;
  paymentStatus: string;
  overdue: boolean;
  verifiedPaidAmount: number;
  remainingAmount: number;
  lines: OrderLine[];
  payments: PaymentRow[];
  rowVersion: string;
};

type ReceivableRow = {
  id: string;
  customerId: string;
  customerNumber: string;
  orderNumber: string;
  totalAmount: number;
  currencyCode: string;
  dueAtUtc: string | null;
  paymentStatus: string;
  overdue: boolean;
  verifiedPaidAmount: number;
  remainingAmount: number;
};

type CustomerOption = { id: string; customerNumber: string; legalName: string; displayName: string | null };
type ContractOption = { id: string; contractNumber: string };

type AmendmentRequestRow = {
  id: string;
  orderId: string;
  orderLineId: string;
  licenseId: string;
  kind: string;
  requestedMaxCompanies: number | null;
  requestedSupportUntilUtc: string | null;
  requestedUpdatesUntilUtc: string | null;
  status: string;
  failureCode: string | null;
  rowVersion: string;
};

type AmendmentPreviewSummary = {
  sequence: number;
  maxCompanies: number | null;
  supportUntilUtc: string | null;
  updatesUntilUtc: string | null;
};

type LineDraft = { productCode: string; description: string; quantity: string; unitAmount: string };

const paymentStatusLabels: Record<string, string> = {
  Unpaid: 'پرداخت‌نشده',
  PartiallyPaid: 'نیمه‌پرداخت‌شده',
  Paid: 'پرداخت‌شده',
  Recorded: 'ثبت‌شده (در انتظار بررسی)',
  Verified: 'تأییدشده',
  Rejected: 'ردشده',
};

const methodLabels: Record<string, string> = {
  bank_transfer: 'انتقال بانکی',
  cheque: 'چک',
  cash: 'نقدی',
  card: 'کارت',
};

const amendmentKindLabels: Record<string, string> = {
  maxCompanies: 'افزایش سقف شرکت‌ها',
  supportUpdates: 'تمدید پشتیبانی/به‌روزرسانی',
};

const amendmentStatusLabels: Record<string, string> = {
  Requested: 'ثبت‌شده (در انتظار ارسال برای تأیید)',
  PendingApproval: 'در انتظار تصمیم مدیر',
  Issued: 'صادر و امضا شد',
  Rejected: 'ردشده',
  Failed: 'ناموفق',
};

// Client-side mirror of the server rules; the server remains authoritative (fail-closed).
function amendmentKindsForProductCode(productCode: string): string[] {
  if (productCode.startsWith('addon.company')) return ['maxCompanies'];
  if (productCode.startsWith('renewal.support')) return ['supportUpdates'];
  return [];
}

const amendmentErrorMessages: Record<string, string> = {
  invalid_amendment_kind: 'نوع Amendment معتبر نیست.',
  amendment_kind_product_mismatch: 'نوع Amendment با محصول ردیف خرید سازگار نیست.',
  order_line_license_mismatch: 'لایسنس انتخاب‌شده با مرجع ردیف سفارش هم‌خوانی ندارد.',
  license_not_amendable: 'این لایسنس صادرشده و قابل Amendment نیست.',
  current_company_limit_missing: 'سقف فعلی شرکت‌های این لایسنس تعریف نشده است.',
  company_limit_not_increased: 'سقف جدید باید بزرگ‌تر از سقف فعلی باشد.',
  amendment_dates_missing: 'حداقل یکی از تاریخ‌های پشتیبانی یا به‌روزرسانی لازم است.',
  amendment_dates_in_past: 'تاریخ‌های تمدید باید در آینده باشند.',
  invalid_amendment_request: 'درخواست Amendment معتبر نیست؛ مقادیر را بررسی کنید.',
  order_line_not_found: 'ردیف سفارش یافت نشد؛ صفحه را تازه‌سازی کنید.',
  license_not_found: 'لایسنس یافت نشد؛ صفحه را تازه‌سازی کنید.',
  license_order_line_mismatch: 'لایسنس به مشتری این سفارش تعلق ندارد.',
  amendment_request_already_active: 'برای این ردیف از قبل یک Amendment فعال وجود دارد.',
  invalid_amendment_transition: 'وضعیت درخواست Amendment اجازه این عملیات را نمی‌دهد.',
  order_payment_not_verified: 'هیچ پرداخت تأییدشده‌ای برای این سفارش ثبت نشده است؛ ابتدا پرداخت را تأیید کنید.',
  amendment_envelope_missing: 'پاکت امضاشده لایسنس یافت نشد؛ صدور جدید لازم است.',
  amendment_envelope_invalid: 'پاکت ذخیره‌شده معتبر نیست؛ با پشتیبانی فنی تماس بگیرید.',
  amendment_signature_or_payload_invalid: 'اعتبارسنجی امضای لایسنس فعلی ناموفق بود؛ عملیات متوقف شد.',
  amendment_envelope_binding_invalid: 'پاکت امضاشده با این لایسنس هم‌خوانی ندارد.',
};

function formatMoney(value: number, currency: string): string {
  return new Intl.NumberFormat('fa-IR', { style: 'currency', currency, maximumFractionDigits: 4 }).format(value);
}

function formatDate(value: string | null): string {
  return value ? new Date(value).toLocaleString('fa-IR') : '—';
}

function apiMessage(status: number, code = ''): string {
  if (code && amendmentErrorMessages[code]) return amendmentErrorMessages[code];
  if (status === 401) return 'نشست شما منقضی شده است؛ دوباره وارد شوید.';
  if (status === 403) return 'برای این عملیات مجوز کافی ندارید.';
  if (status === 404) return 'رکورد موردنظر یافت نشد؛ فهرست را تازه‌سازی کنید.';
  if (code === 'invalid_antiforgery_token') return 'توکن امنیتی نامعتبر است؛ دوباره تلاش کنید.';
  if (code === 'invalid_sales_order') return 'اطلاعات سفارش معتبر نیست؛ مبلغ، ارز و شماره سفارش را بررسی کنید.';
  if (code === 'invalid_sales_order_line') return 'ردیف سفارش معتبر نیست؛ مبلغ هر ردیف باید حاصل‌ضرب تعداد در مبلغ واحد باشد.';
  if (code === 'sales_order_totals_mismatch') return 'جمع ردیف‌ها با مبلغ کل سفارش برابر نیست.';
  if (code === 'sales_order_number_conflict') return 'این شماره سفارش قبلاً ثبت شده است؛ شماره دیگری انتخاب کنید.';
  if (code === 'contract_customer_mismatch') return 'قرارداد انتخاب‌شده به این مشتری تعلق ندارد.';
  if (code === 'license_customer_mismatch') return 'لایسنس انتخاب‌شده به این مشتری تعلق ندارد.';
  if (code === 'invalid_payment') return 'اطلاعات پرداخت معتبر نیست؛ مبلغ، ارز و روش پرداخت را بررسی کنید.';
  if (code === 'payment_currency_mismatch') return 'ارز پرداخت با ارز سفارش یکسان نیست.';
  if (code === 'payment_already_decided') return 'این پرداخت قبلاً تصمیم‌گیری شده است و دیگر قابل بررسی نیست.';
  if (code === 'stale_row_version') return 'پرداخت هم‌زمان تغییر کرده است؛ جزئیات سفارش را تازه‌سازی کنید.';
  if (code === 'approval_already_pending') return 'برای این پرداخت از قبل درخواست بررسی در انتظار است.';
  if (status === 409) return 'این عملیات با وضعیت فعلی سازگار نیست.';
  if (status === 400) return 'درخواست معتبر نیست؛ اطلاعات را بررسی و دوباره تلاش کنید.';
  return 'عملیات انجام نشد؛ دوباره تلاش کنید.';
}

async function fetchPage<T>(endpoint: string): Promise<PageResponse<T>> {
  const response = await fetch(endpoint, { credentials: 'same-origin', cache: 'no-store', headers: { Accept: 'application/json' } });
  if (!response.ok) throw new Error(apiMessage(response.status));
  return await response.json() as PageResponse<T>;
}

async function readError(response: Response): Promise<string> {
  let code = '';
  try { code = ((await response.json()) as { error?: string }).error ?? ''; } catch { /* fall back to status */ }
  return apiMessage(response.status, code);
}

async function mutationToken(): Promise<string> {
  const tokenResponse = await fetch('/api/auth/antiforgery', { credentials: 'same-origin', cache: 'no-store' });
  if (!tokenResponse.ok) throw new Error(apiMessage(tokenResponse.status));
  const data = await tokenResponse.json() as { requestToken?: string };
  if (!data.requestToken) throw new Error('توکن امنیتی سرور دریافت نشد.');
  return data.requestToken;
}

function Pagination({ page, totalPages, disabled, onMove }: { page: number; totalPages: number; disabled: boolean; onMove: (page: number) => void }) {
  if (totalPages <= 1) return null;
  const formatter = new Intl.NumberFormat('fa-IR');
  return <div className="mt-4 flex flex-wrap items-center justify-between gap-3 border-t pt-4">
    <span className="text-sm text-muted-foreground">صفحه {formatter.format(page)} از {formatter.format(totalPages)}</span>
    <div className="flex items-center gap-2">
      <Button type="button" variant="outline" size="sm" disabled={page <= 1 || disabled} onClick={() => onMove(Math.max(1, page - 1))}>
        <ChevronRight size={14} /> قبلی
      </Button>
      <Button type="button" variant="outline" size="sm" disabled={page >= totalPages || disabled} onClick={() => onMove(Math.min(totalPages, page + 1))}>
        بعدی <ChevronLeft size={14} />
      </Button>
    </div>
  </div>;
}

function CreateOrderForm({ onDone, onError }: { onDone: () => void; onError: (message: string) => void }) {
  const [customers, setCustomers] = useState<CustomerOption[]>([]);
  const [contracts, setContracts] = useState<ContractOption[]>([]);
  const [customerId, setCustomerId] = useState('');
  const [contractId, setContractId] = useState('');
  const [orderNumber, setOrderNumber] = useState('');
  const [currencyCode, setCurrencyCode] = useState('IRR');
  const [dueAtUtc, setDueAtUtc] = useState('');
  const [lines, setLines] = useState<LineDraft[]>([{ productCode: '', description: '', quantity: '1', unitAmount: '' }]);
  const [busy, setBusy] = useState(false);
  const [loadingCustomers, setLoadingCustomers] = useState(true);

  useEffect(() => {
    let active = true;
    fetchPage<CustomerOption>('/api/vendor/customers/?page=1&pageSize=100')
      .then(result => { if (active) setCustomers(result.items.filter(item => typeof item.id === 'string')); })
      .catch(reason => onError(reason instanceof Error ? reason.message : 'دریافت مشتریان ناموفق بود.'))
      .finally(() => { if (active) setLoadingCustomers(false); });
    return () => { active = false; };
  }, [onError]);

  useEffect(() => {
    setContracts([]);
    setContractId('');
    if (!customerId) return;
    let active = true;
    fetchPage<ContractOption>(`/api/vendor/contracts/?customerId=${customerId}&page=1&pageSize=100`)
      .then(result => { if (active) setContracts(result.items.filter(item => typeof item.id === 'string')); })
      .catch(() => { /* contract dropdown stays optional */ });
    return () => { active = false; };
  }, [customerId]);

  function lineAmount(line: LineDraft): number {
    const quantity = Number(line.quantity);
    const unitAmount = Number(line.unitAmount);
    if (!Number.isFinite(quantity) || !Number.isFinite(unitAmount)) return NaN;
    return Math.round(quantity * unitAmount * 10000) / 10000;
  }

  const total = lines.map(lineAmount).reduce((sum, value) => (Number.isFinite(value) ? sum + value : NaN), 0);

  function updateLine(index: number, patch: Partial<LineDraft>) {
    setLines(current => current.map((line, position) => (position === index ? { ...line, ...patch } : line)));
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy) return;
    setBusy(true);
    try {
      const token = await mutationToken();
      const payload = {
        customerId,
        contractId: contractId || null,
        orderNumber: orderNumber.trim(),
        totalAmount: Number(total.toFixed(4)),
        currencyCode: currencyCode.trim().toUpperCase(),
        dueAtUtc: dueAtUtc ? new Date(dueAtUtc).toISOString() : null,
        lines: lines.map(line => ({
          productCode: line.productCode.trim(),
          description: line.description.trim(),
          quantity: Number(line.quantity),
          unitAmount: Number(line.unitAmount),
          lineAmount: Number(lineAmount(line).toFixed(4)),
        })),
      };
      const response = await fetch('/api/vendor/sales-orders', {
        method: 'POST', credentials: 'same-origin', cache: 'no-store',
        headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': token },
        body: JSON.stringify(payload),
      });
      if (!response.ok) throw new Error(await readError(response));
      onDone();
    } catch (reason) {
      onError(reason instanceof Error ? reason.message : 'ثبت سفارش ناموفق بود.');
    } finally { setBusy(false); }
  }

  const totalsValid = Number.isFinite(total) && total >= 0;

  return <Card><CardHeader><CardTitle>ثبت سفارش تجاری جدید</CardTitle>
    <CardDescription>سفارش شامل ردیف‌های خرید است؛ جمع ردیف‌ها باید با مبلغ کل برابر باشد و رکورد پس از ثبت در گزارش ممیزی ثبت می‌شود.</CardDescription></CardHeader>
    <CardContent>
      <form className="grid gap-4 md:grid-cols-2" onSubmit={submit}>
        <label className="grid gap-1 text-sm">مشتری
          <select required className="h-9 rounded-md border border-input bg-background px-2" value={customerId}
            onChange={event => setCustomerId(event.target.value)} disabled={loadingCustomers || busy} aria-label="انتخاب مشتری">
            <option value="">{loadingCustomers ? 'در حال دریافت…' : 'انتخاب کنید'}</option>
            {customers.map(item => <option key={item.id} value={item.id}>{item.customerNumber} — {item.displayName || item.legalName}</option>)}
          </select>
        </label>
        <label className="grid gap-1 text-sm">قرارداد (اختیاری)
          <select className="h-9 rounded-md border border-input bg-background px-2" value={contractId}
            onChange={event => setContractId(event.target.value)} disabled={!customerId || busy} aria-label="انتخاب قرارداد">
            <option value="">بدون قرارداد</option>
            {contracts.map(item => <option key={item.id} value={item.id}>{item.contractNumber}</option>)}
          </select>
        </label>
        <label className="grid gap-1 text-sm">شماره سفارش
          <input required className="h-9 rounded-md border border-input bg-background px-2" dir="ltr" maxLength={64}
            value={orderNumber} onChange={event => setOrderNumber(event.target.value)} disabled={busy} aria-label="شماره سفارش" />
        </label>
        <div className="grid gap-1 text-sm">
          <span>ارز و سررسید</span>
          <div className="flex gap-2">
            <input required className="h-9 w-24 rounded-md border border-input bg-background px-2 text-center" dir="ltr" maxLength={3}
              value={currencyCode} onChange={event => setCurrencyCode(event.target.value)} disabled={busy} aria-label="کد ارز" />
            <input type="date" className="h-9 rounded-md border border-input bg-background px-2" value={dueAtUtc}
              onChange={event => setDueAtUtc(event.target.value)} disabled={busy} aria-label="تاریخ سررسید" />
          </div>
        </div>
        <div className="md:col-span-2">
          <div className="mb-2 flex items-center justify-between">
            <span className="text-sm font-medium">ردیف‌های سفارش</span>
            <Button type="button" variant="outline" size="sm" disabled={busy} onClick={() => setLines(current => [...current, { productCode: '', description: '', quantity: '1', unitAmount: '' }])}>
              <Plus size={14} /> افزودن ردیف
            </Button>
          </div>
          {lines.map((line, index) => <div key={index} className="mb-2 grid grid-cols-2 gap-2 rounded-md border border-border p-2 md:grid-cols-5">
            <input required placeholder="کد محصول" className="h-9 rounded-md border border-input bg-background px-2" dir="ltr" maxLength={64}
              value={line.productCode} onChange={event => updateLine(index, { productCode: event.target.value })} disabled={busy} aria-label={`کد محصول ردیف ${index + 1}`} />
            <input required placeholder="توضیح" className="h-9 rounded-md border border-input bg-background px-2 md:col-span-2" maxLength={500}
              value={line.description} onChange={event => updateLine(index, { description: event.target.value })} disabled={busy} aria-label={`توضیح ردیف ${index + 1}`} />
            <input required type="number" min={1} step={1} placeholder="تعداد" className="h-9 rounded-md border border-input bg-background px-2" dir="ltr"
              value={line.quantity} onChange={event => updateLine(index, { quantity: event.target.value })} disabled={busy} aria-label={`تعداد ردیف ${index + 1}`} />
            <input required type="number" min={0} step="0.0001" placeholder="مبلغ واحد" className="h-9 rounded-md border border-input bg-background px-2" dir="ltr"
              value={line.unitAmount} onChange={event => updateLine(index, { unitAmount: event.target.value })} disabled={busy} aria-label={`مبلغ واحد ردیف ${index + 1}`} />
            <span className="text-xs text-muted-foreground md:col-span-5" dir="rtl">
              مبلغ ردیف: {Number.isFinite(lineAmount(line)) ? formatMoney(lineAmount(line), currencyCode) : '—'}
              {lines.length > 1 && <button type="button" className="mr-2 text-destructive underline" onClick={() => setLines(current => current.filter((_, position) => position !== index))} disabled={busy}>حذف ردیف</button>}
            </span>
          </div>)}
          <p className="text-sm" aria-live="polite">جمع ردیف‌ها: <b>{totalsValid ? formatMoney(total, currencyCode) : 'نامعتبر'}</b></p>
        </div>
        <div className="md:col-span-2">
          <Button type="submit" disabled={busy || !customerId || !totalsValid}>{busy ? 'در حال ثبت…' : 'ثبت سفارش'}</Button>
        </div>
      </form>
    </CardContent></Card>;
}

function RecordPaymentForm({ order, onRecorded, onError }: { order: OrderRow; onRecorded: () => void; onError: (message: string) => void }) {
  const [amount, setAmount] = useState('');
  const [method, setMethod] = useState('bank_transfer');
  const [externalReference, setExternalReference] = useState('');
  const [claimedPaidAtUtc, setClaimedPaidAtUtc] = useState('');
  const [busy, setBusy] = useState(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy) return;
    setBusy(true);
    try {
      const token = await mutationToken();
      const response = await fetch(`/api/vendor/sales-orders/${order.id}/payments`, {
        method: 'POST', credentials: 'same-origin', cache: 'no-store',
        headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': token },
        body: JSON.stringify({
          amount: Number(amount),
          currencyCode: order.currencyCode,
          method,
          externalReference: externalReference.trim() || null,
          claimedPaidAtUtc: claimedPaidAtUtc ? new Date(claimedPaidAtUtc).toISOString() : null,
        }),
      });
      if (!response.ok) throw new Error(await readError(response));
      setAmount(''); setExternalReference(''); setClaimedPaidAtUtc('');
      onRecorded();
    } catch (reason) {
      onError(reason instanceof Error ? reason.message : 'ثبت پرداخت ناموفق بود.');
    } finally { setBusy(false); }
  }

  return <Card><CardHeader><CardTitle>ثبت پرداخت جدید</CardTitle>
    <CardDescription>پرداخت به‌صورت دستی با مرجع رسید ثبت می‌شود و تا تأیید مستقل، بدهی را کاهش نمی‌دهد.</CardDescription></CardHeader>
    <CardContent>
      <form className="grid gap-4 md:grid-cols-2" onSubmit={submit}>
        <label className="grid gap-1 text-sm">مبلغ ({order.currencyCode})
          <input required type="number" min={0} step="0.0001" className="h-9 rounded-md border border-input bg-background px-2" dir="ltr"
            value={amount} onChange={event => setAmount(event.target.value)} disabled={busy} aria-label="مبلغ پرداخت" />
        </label>
        <label className="grid gap-1 text-sm">روش پرداخت
          <select required className="h-9 rounded-md border border-input bg-background px-2" value={method}
            onChange={event => setMethod(event.target.value)} disabled={busy} aria-label="روش پرداخت">
            {Object.entries(methodLabels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
          </select>
        </label>
        <label className="grid gap-1 text-sm">مرجع رسید (اختیاری)
          <input className="h-9 rounded-md border border-input bg-background px-2" dir="ltr" maxLength={200}
            value={externalReference} onChange={event => setExternalReference(event.target.value)} disabled={busy} aria-label="مرجع رسید" />
        </label>
        <label className="grid gap-1 text-sm">تاریخ ادعای پرداخت
          <input type="date" className="h-9 rounded-md border border-input bg-background px-2" value={claimedPaidAtUtc}
            onChange={event => setClaimedPaidAtUtc(event.target.value)} disabled={busy} aria-label="تاریخ ادعای پرداخت" />
        </label>
        <div className="md:col-span-2"><Button type="submit" disabled={busy}>{busy ? 'در حال ثبت…' : 'ثبت پرداخت'}</Button></div>
      </form>
    </CardContent></Card>;
}

export function VendorSales({ permissions }: { permissions: string[] }) {
  const canRead = permissions.includes('vendor.payments.read');
  const canManage = permissions.includes('vendor.payments.manage');
  const canAmendManage = permissions.includes('vendor.licenses.manage');
  const canAmendIssue = permissions.includes('vendor.licenses.issue');
  const [view, setView] = useState<'orders' | 'receivables'>('orders');
  const [orders, setOrders] = useState<PageResponse<OrderRow> | null>(null);
  const [orderPage, setOrderPage] = useState(1);
  const [receivables, setReceivables] = useState<PageResponse<ReceivableRow> | null>(null);
  const [receivablePage, setReceivablePage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [busyOrder, setBusyOrder] = useState<string | null>(null);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [showCreate, setShowCreate] = useState(false);
  const [openOrder, setOpenOrder] = useState<OrderRow | null>(null);
  const [orderAmendments, setOrderAmendments] = useState<AmendmentRequestRow[]>([]);
  const [amendmentForm, setAmendmentForm] = useState<{ line: OrderLine } | null>(null);
  const [amendmentKind, setAmendmentKind] = useState('');
  const [amendmentMaxCompanies, setAmendmentMaxCompanies] = useState('');
  const [amendmentSupportUntil, setAmendmentSupportUntil] = useState('');
  const [amendmentUpdatesUntil, setAmendmentUpdatesUntil] = useState('');
  const [amendmentPreview, setAmendmentPreview] = useState<AmendmentPreviewSummary | null>(null);
  const [busyAmendment, setBusyAmendment] = useState(false);
  // Latest order the operator asked to open; stale responses from an older order are discarded.
  const openedOrderRef = useRef<string | null>(null);

  const handleError = useCallback((message: string) => setError(message), []);

  const load = useCallback(async () => {
    if (!canRead) { setError('مجوز مشاهده فروش و پرداخت را ندارید.'); setLoading(false); return; }
    setLoading(true);
    setError('');
    try {
      const [orderPageData, receivablePageData] = await Promise.all([
        fetchPage<OrderRow>(`/api/vendor/sales-orders/?page=${orderPage}&pageSize=25`),
        fetchPage<ReceivableRow>(`/api/vendor/receivables/?page=${receivablePage}&pageSize=25`),
      ]);
      setOrders(orderPageData);
      setReceivables(receivablePageData);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'دریافت اطلاعات فروش ناموفق بود.');
    } finally { setLoading(false); }
  }, [canRead, orderPage, receivablePage]);

  useEffect(() => { void load(); }, [load]);

  async function openOrderDetail(orderId: string) {
    // Messages are intentionally preserved: callers like the payment flows set a notice
    // right before refreshing the order and the operator must still see it afterwards.
    // The amendment form belongs to a specific order's line, so switching orders resets it.
    openedOrderRef.current = orderId;
    setBusyOrder(orderId);
    setOrderAmendments([]);
    setAmendmentForm(null);
    setAmendmentKind('');
    setAmendmentMaxCompanies('');
    setAmendmentSupportUntil('');
    setAmendmentUpdatesUntil('');
    setAmendmentPreview(null);
    try {
      const response = await fetch(`/api/vendor/sales-orders/${orderId}`, { credentials: 'same-origin', cache: 'no-store', headers: { Accept: 'application/json' } });
      if (!response.ok) throw new Error(await readError(response));
      const order = await response.json() as OrderRow;
      const amendments: AmendmentRequestRow[] = [];
      // One paged call per page: the amendment panel has no paging controls of its own, so
      // every page for this order is loaded (bounded by the request loop, not a magic limit).
      for (let page = 1; ; page++) {
        const amendmentsResponse = await fetch(`/api/vendor/license-amendment-requests/?orderId=${orderId}&page=${page}&pageSize=50`, { credentials: 'same-origin', cache: 'no-store', headers: { Accept: 'application/json' } });
        if (!amendmentsResponse.ok) break;
        const pageData = (await amendmentsResponse.json()) as PageResponse<AmendmentRequestRow>;
        amendments.push(...pageData.items);
        if (!pageData.hasNextPage || pageData.totalPages <= page) break;
      }
      // Only apply if this order is still the one the operator wants to see.
      if (openedOrderRef.current !== orderId) return;
      setOpenOrder(order);
      setOrderAmendments(amendments);
    } catch (reason) {
      if (openedOrderRef.current === orderId) {
        setError(reason instanceof Error ? reason.message : 'دریافت جزئیات سفارش ناموفق بود.');
      }
    } finally { setBusyOrder(null); }
  }

  function amendmentPayloadFor(line: OrderLine): Record<string, unknown> | null {
    const kinds = amendmentKindsForProductCode(line.productCode);
    const kind = kinds.includes(amendmentKind) ? amendmentKind : kinds[0];
    if (!kind) return null;
    if (kind === 'maxCompanies') {
      const newMax = Number(amendmentMaxCompanies);
      return Number.isFinite(newMax) && newMax > 0
        ? { orderLineId: line.id, licenseId: line.licenseId, kind, requestedMaxCompanies: newMax }
        : null;
    }
    return {
      orderLineId: line.id,
      licenseId: line.licenseId,
      kind,
      requestedSupportUntilUtc: amendmentSupportUntil ? new Date(amendmentSupportUntil).toISOString() : null,
      requestedUpdatesUntilUtc: amendmentUpdatesUntil ? new Date(amendmentUpdatesUntil).toISOString() : null,
    };
  }

  function resetAmendmentForm() {
    setAmendmentForm(null); setAmendmentKind(''); setAmendmentMaxCompanies('');
    setAmendmentSupportUntil(''); setAmendmentUpdatesUntil(''); setAmendmentPreview(null);
  }

  async function postAmendmentJson(path: string, payload: Record<string, unknown>) {
    const token = await mutationToken();
    const response = await fetch(path, {
      method: 'POST', credentials: 'same-origin', cache: 'no-store',
      headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': token },
      body: JSON.stringify(payload),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response;
  }

  async function previewAmendment(line: OrderLine) {
    if (busyAmendment) return;
    setError(''); setNotice(''); setAmendmentPreview(null);
    const payload = amendmentPayloadFor(line);
    if (!payload) { setError('برای این ردیف، مقادیر Amendment را کامل کنید.'); return; }
    setBusyAmendment(true);
    try {
      const response = await postAmendmentJson('/api/vendor/license-amendment-requests/preview', payload);
      setAmendmentPreview(await response.json() as AmendmentPreviewSummary);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'پیش‌نمایش Amendment ناموفق بود.');
    } finally { setBusyAmendment(false); }
  }

  async function createAmendment(line: OrderLine) {
    if (busyAmendment) return;
    setError(''); setNotice(''); setAmendmentPreview(null);
    const payload = amendmentPayloadFor(line);
    if (!payload) { setError('برای این ردیف، مقادیر Amendment را کامل کنید.'); return; }
    setBusyAmendment(true);
    try {
      await postAmendmentJson('/api/vendor/license-amendment-requests', payload);
      setNotice('درخواست Amendment ثبت شد؛ پس از بازبینی، آن را برای تأیید مدیر ارسال کنید.');
      resetAmendmentForm();
      await openOrderDetail(openOrder!.id);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'ثبت درخواست Amendment ناموفق بود.');
    } finally { setBusyAmendment(false); }
  }

  async function submitAmendmentForApproval(row: AmendmentRequestRow) {
    if (busyAmendment || !canAmendIssue) return;
    setError(''); setNotice('');
    setBusyAmendment(true);
    try {
      await postAmendmentJson(
        `/api/vendor/license-amendment-requests/${row.id}/approval-request`,
        { rowVersion: row.rowVersion });
      setNotice('درخواست Amendment برای تأیید ارسال شد؛ تصمیم‌گیری در صفحه درخواست‌های تأیید انجام می‌شود.');
      await openOrderDetail(row.orderId);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'ارسال برای تأیید ناموفق بود.');
    } finally { setBusyAmendment(false); }
  }

  async function requestPaymentDecision(payment: PaymentRow, action: 'verify' | 'reject') {
    if (!canManage) return;
    setError(''); setNotice('');
    setBusyOrder(payment.id);
    try {
      const token = await mutationToken();
      const response = await fetch(`/api/vendor/payments/${payment.id}/approval-request`, {
        method: 'POST', credentials: 'same-origin', cache: 'no-store',
        headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': token },
        body: JSON.stringify({ action, rowVersion: payment.rowVersion }),
      });
      if (!response.ok) throw new Error(await readError(response));
      setNotice(action === 'verify'
        ? 'درخواست تأیید پرداخت ثبت شد؛ تصمیم‌گیری در صفحه درخواست‌های تأیید انجام می‌شود.'
        : 'درخواست رد پرداخت ثبت شد؛ تصمیم‌گیری در صفحه درخواست‌های تأیید انجام می‌شود.');
      await openOrderDetail(payment.orderId);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'ثبت درخواست بررسی ناموفق بود.');
    } finally { setBusyOrder(null); }
  }

  if (!canRead) {
    return <section className="space-y-5" dir="rtl" aria-labelledby="vendor-sales-title">
      <h1 id="vendor-sales-title" className="text-xl font-semibold">فروش و پرداخت</h1>
      <p role="alert" className="rounded-md border border-destructive/40 p-3 text-sm">مجوز مشاهده فروش و پرداخت برای حساب شما فعال نیست.</p>
    </section>;
  }

  const currentOrderPage = orders;
  const currentReceivables = receivables;

  return <section className="space-y-5" dir="rtl" aria-labelledby="vendor-sales-title">
    <div className="flex flex-wrap items-center justify-between gap-3">
      <div className="flex items-center gap-2"><ReceiptText size={20} />
        <h1 id="vendor-sales-title" className="text-xl font-semibold">فروش و پرداخت</h1></div>
      <div className="flex gap-2">
        <Button type="button" variant={view === 'orders' ? 'default' : 'outline'} size="sm" onClick={() => setView('orders')}>سفارش‌ها</Button>
        <Button type="button" variant={view === 'receivables' ? 'default' : 'outline'} size="sm" onClick={() => setView('receivables')}>مطالبات باز</Button>
        <Button type="button" variant="outline" size="sm" onClick={() => void load()} disabled={loading}><RefreshCw size={14} /> تازه‌سازی</Button>
        {canManage && view === 'orders' && <Button type="button" size="sm" onClick={() => setShowCreate(current => !current)}><Plus size={14} /> سفارش جدید</Button>}
      </div>
    </div>
    {error && <p role="alert" className="rounded-md border border-destructive/40 p-3 text-sm">{error}</p>}
    {notice && <p role="status" className="rounded-md border border-border p-3 text-sm">{notice}</p>}

    {view === 'orders' && <>
      {showCreate && canManage && <CreateOrderForm
        onDone={() => { setShowCreate(false); setNotice('سفارش با موفقیت ثبت شد.'); void load(); }}
        onError={handleError} />}
      <Card><CardHeader><CardTitle>سفارش‌های تجاری</CardTitle>
        <CardDescription>وضعیت پرداخت هر سفارش فقط از پرداخت‌های تأییدشده محاسبه می‌شود؛ پرداخت ثبت‌شده تا تأیید، بدهی را کاهش نمی‌دهد.</CardDescription></CardHeader>
        <CardContent>
          {loading ? <p role="status" className="py-6 text-sm text-muted-foreground">در حال دریافت سفارش‌ها…</p>
            : !currentOrderPage || currentOrderPage.items.length === 0 ? <p className="py-6 text-sm text-muted-foreground">سفارشی ثبت نشده است.</p>
            : <div className="overflow-x-auto"><table className="w-full min-w-[720px] text-sm">
              <thead><tr className="border-b text-right">
                <th scope="col" className="px-3 py-3 font-medium">شماره سفارش</th>
                <th scope="col" className="px-3 py-3 font-medium">مشتری</th>
                <th scope="col" className="px-3 py-3 font-medium">مبلغ کل</th>
                <th scope="col" className="px-3 py-3 font-medium">تأییدشده</th>
                <th scope="col" className="px-3 py-3 font-medium">مانده</th>
                <th scope="col" className="px-3 py-3 font-medium">وضعیت پرداخت</th>
                <th scope="col" className="px-3 py-3 font-medium">سررسید</th>
                <th scope="col" className="px-3 py-3 font-medium">عملیات</th>
              </tr></thead><tbody>{currentOrderPage.items.map(order => <tr key={order.id} className={'border-b last:border-0' + (order.overdue ? ' text-destructive' : '')}>
                <td className="px-3 py-3 font-mono text-xs" dir="ltr" title={order.orderNumber}>{order.orderNumber}</td>
                <td className="px-3 py-3">{order.customerNumber}</td>
                <td className="px-3 py-3">{formatMoney(order.totalAmount, order.currencyCode)}</td>
                <td className="px-3 py-3">{formatMoney(order.verifiedPaidAmount, order.currencyCode)}</td>
                <td className="px-3 py-3">{formatMoney(order.remainingAmount, order.currencyCode)}</td>
                <td className="px-3 py-3">{order.overdue && <span className="text-destructive">معوق — </span>}{paymentStatusLabels[order.paymentStatus] ?? order.paymentStatus}</td>
                <td className="px-3 py-3">{formatDate(order.dueAtUtc)}</td>
                <td className="px-3 py-3">
                  <Button type="button" size="sm" variant="outline" disabled={busyOrder === order.id}
                    onClick={() => (openOrder?.id === order.id ? setOpenOrder(null) : void openOrderDetail(order.id))}>
                    {busyOrder === order.id ? '…' : openOrder?.id === order.id ? 'بستن' : 'جزئیات'}
                  </Button>
                </td>
              </tr>)}</tbody></table></div>}
          <Pagination page={orderPage} totalPages={currentOrderPage?.totalPages ?? 0} disabled={loading} onMove={setOrderPage} />
        </CardContent></Card>
      {openOrder && <Card><CardHeader><CardTitle>سفارش {openOrder.orderNumber}</CardTitle>
        <CardDescription>ردیف‌ها، پرداخت‌ها و گردش بررسی پرداخت این سفارش.</CardDescription></CardHeader>
        <CardContent className="space-y-5">
          <div><h3 className="mb-2 text-sm font-medium">ردیف‌های سفارش</h3>
            {openOrder.lines.length === 0 ? <p className="text-sm text-muted-foreground">این سفارش ردیف ثبت‌شده ندارد.</p>
              : <div className="overflow-x-auto"><table className="w-full min-w-[560px] text-sm">
                <thead><tr className="border-b text-right">
                  <th scope="col" className="px-3 py-2 font-medium">کد محصول</th><th scope="col" className="px-3 py-2 font-medium">توضیح</th>
                  <th scope="col" className="px-3 py-2 font-medium">تعداد</th><th scope="col" className="px-3 py-2 font-medium">مبلغ واحد</th>
                  <th scope="col" className="px-3 py-2 font-medium">مبلغ ردیف</th>
                </tr></thead><tbody>{openOrder.lines.map(line => <tr key={line.id} className="border-b last:border-0">
                  <td className="px-3 py-2 font-mono text-xs" dir="ltr">{line.productCode}</td>
                  <td className="px-3 py-2">{line.description}</td>
                  <td className="px-3 py-2">{new Intl.NumberFormat('fa-IR').format(line.quantity)}</td>
                  <td className="px-3 py-2">{formatMoney(line.unitAmount, openOrder.currencyCode)}</td>
                  <td className="px-3 py-2">{formatMoney(line.lineAmount, openOrder.currencyCode)}</td>
                </tr>)}</tbody></table></div>}
          </div>
          <div><h3 className="mb-2 text-sm font-medium">پرداخت‌های ثبت‌شده</h3>
            {openOrder.payments.length === 0 ? <p className="text-sm text-muted-foreground">پرداختی برای این سفارش ثبت نشده است.</p>
              : <div className="overflow-x-auto"><table className="w-full min-w-[640px] text-sm">
                <thead><tr className="border-b text-right">
                  <th scope="col" className="px-3 py-2 font-medium">مبلغ</th><th scope="col" className="px-3 py-2 font-medium">روش</th>
                  <th scope="col" className="px-3 py-2 font-medium">مرجع رسید</th><th scope="col" className="px-3 py-2 font-medium">وضعیت</th>
                  <th scope="col" className="px-3 py-2 font-medium">زمان تصمیم</th><th scope="col" className="px-3 py-2 font-medium">درخواست بررسی</th>
                </tr></thead><tbody>{openOrder.payments.map(payment => <tr key={payment.id} className="border-b last:border-0">
                  <td className="px-3 py-2">{formatMoney(payment.amount, payment.currencyCode)}</td>
                  <td className="px-3 py-2">{methodLabels[payment.method] ?? payment.method}</td>
                  <td className="px-3 py-2 font-mono text-xs" dir="ltr">{payment.externalReference ?? '—'}</td>
                  <td className="px-3 py-2">{paymentStatusLabels[payment.status] ?? payment.status}</td>
                  <td className="px-3 py-2">{formatDate(payment.verifiedAtUtc)}</td>
                  <td className="px-3 py-2">{canManage && payment.status === 'Recorded' ? <div className="flex gap-2">
                    <Button type="button" size="sm" disabled={busyOrder === payment.id} onClick={() => void requestPaymentDecision(payment, 'verify')}>درخواست تأیید</Button>
                    <Button type="button" size="sm" variant="outline" disabled={busyOrder === payment.id} onClick={() => void requestPaymentDecision(payment, 'reject')}>درخواست رد</Button>
                  </div> : <span className="text-xs text-muted-foreground">{payment.status === 'Recorded' ? 'مجوز مدیریت پرداخت لازم است' : '—'}</span>}</td>
                </tr>)}</tbody></table></div>}
            <p className="mt-2 text-xs text-muted-foreground">تصمیم نهایی تأیید یا رد، فقط در صفحه «درخواست‌های تأیید» با مجوز بررسی پرداخت ثبت می‌شود.</p>
          </div>
          {canManage && <RecordPaymentForm order={openOrder} onRecorded={() => { setNotice('پرداخت ثبت شد؛ تا تأیید مستقل، مانده بدهی تغییری نمی‌کند.'); void openOrderDetail(openOrder.id); }} onError={handleError} />}
          <div>
            <h3 className="mb-1 text-sm font-medium">Amendment لایسنس (خرید تأییدشده)</h3>
            <p className="mb-2 text-xs text-muted-foreground">
              فقط ردیف‌های دارای مرجع لایسنس قابل Amendment هستند؛ نوع تغییر باید با محصول خرید هم‌خوان باشد و صدور، فقط با تأیید مدیر و امضای رسمی انجام می‌شود. موارد وابسته به CompanyReference (#392) فعال نیستند.
            </p>
            {openOrder.lines.filter(line => line.licenseId).length === 0
              ? <p className="text-sm text-muted-foreground">این سفارش ردیف متصل به لایسنس ندارد.</p>
              : <div className="overflow-x-auto"><table className="w-full min-w-[560px] text-sm">
                <thead><tr className="border-b text-right">
                  <th scope="col" className="px-3 py-2 font-medium">کد محصول</th>
                  <th scope="col" className="px-3 py-2 font-medium">وضعیت Amendment</th>
                  <th scope="col" className="px-3 py-2 font-medium">عملیات</th>
                </tr></thead><tbody>{openOrder.lines.filter(line => line.licenseId).map(line => {
                  const existing = orderAmendments.find(amendment => amendment.orderLineId === line.id);
                  const kinds = amendmentKindsForProductCode(line.productCode);
                  const active = existing && ['Requested', 'PendingApproval', 'Issued'].includes(existing.status);
                  return <tr key={line.id} className="border-b last:border-0">
                    <td className="px-3 py-2 font-mono text-xs" dir="ltr">{line.productCode}</td>
                    <td className="px-3 py-2">{existing
                      ? <span className={existing.status === 'Issued' ? 'text-green-700' : existing.status === 'Rejected' || existing.status === 'Failed' ? 'text-destructive' : ''}>
                          {amendmentKindLabels[existing.kind] ?? existing.kind} — {amendmentStatusLabels[existing.status] ?? existing.status}
                        </span>
                      : <span className="text-muted-foreground">بدون درخواست</span>}</td>
                    <td className="px-3 py-2">{canAmendManage && kinds.length > 0 && !active
                      ? <Button type="button" size="sm" variant="outline" disabled={busyAmendment}
                          onClick={() => { setAmendmentForm({ line }); setAmendmentKind(kinds[0] ?? ''); setAmendmentMaxCompanies(''); setAmendmentSupportUntil(''); setAmendmentUpdatesUntil(''); setAmendmentPreview(null); }}>
                          درخواست Amendment
                        </Button>
                      : <span className="text-xs text-muted-foreground">{kinds.length === 0 ? 'نوع پشتیبانی‌شده ندارد' : active ? 'درخواست فعال موجود است' : 'مجوز مدیریت لایسنس لازم است'}</span>}</td>
                  </tr>;
                })}</tbody></table></div>}
            {amendmentForm && <div className="mt-3 rounded-md border border-border p-3">
              <p className="mb-2 text-sm font-medium">Amendment برای ردیف <span dir="ltr" className="font-mono text-xs">{amendmentForm.line.productCode}</span></p>
              <div className="grid gap-3 md:grid-cols-2">
                {amendmentKindsForProductCode(amendmentForm.line.productCode).map(kind => <label key={kind} className="grid gap-1 text-sm">
                  نوع تغییر
                  <select className="h-9 rounded-md border border-input bg-background px-2" value={amendmentKind || kind}
                    onChange={event => { setAmendmentKind(event.target.value); setAmendmentPreview(null); }} disabled={busyAmendment} aria-label="نوع Amendment">
                    {amendmentKindsForProductCode(amendmentForm.line.productCode).map(value => <option key={value} value={value}>{amendmentKindLabels[value]}</option>)}
                  </select>
                </label>)}
                {(amendmentKind || amendmentKindsForProductCode(amendmentForm.line.productCode).at(0)) === 'maxCompanies'
                  ? <label className="grid gap-1 text-sm">سقف جدید شرکت‌ها
                    <input required type="number" min={1} step={1} className="h-9 rounded-md border border-input bg-background px-2" dir="ltr"
                      value={amendmentMaxCompanies} onChange={event => { setAmendmentMaxCompanies(event.target.value); setAmendmentPreview(null); }} disabled={busyAmendment} aria-label="سقف جدید شرکت‌ها" />
                  </label>
                  : <>
                    <label className="grid gap-1 text-sm">تا تاریخ پشتیبانی
                      <input type="date" className="h-9 rounded-md border border-input bg-background px-2" value={amendmentSupportUntil}
                        onChange={event => { setAmendmentSupportUntil(event.target.value); setAmendmentPreview(null); }} disabled={busyAmendment} aria-label="تا تاریخ پشتیبانی" />
                    </label>
                    <label className="grid gap-1 text-sm">تا تاریخ به‌روزرسانی
                      <input type="date" className="h-9 rounded-md border border-input bg-background px-2" value={amendmentUpdatesUntil}
                        onChange={event => { setAmendmentUpdatesUntil(event.target.value); setAmendmentPreview(null); }} disabled={busyAmendment} aria-label="تا تاریخ به‌روزرسانی" />
                    </label>
                  </>}
              </div>
              {amendmentPreview && <p className="mt-2 text-xs" aria-live="polite">
                پیش‌نمایش کاندید: نسخه {new Intl.NumberFormat('fa-IR').format(amendmentPreview.sequence)}
                {amendmentPreview.maxCompanies !== null && ` — سقف شرکت‌ها: ${new Intl.NumberFormat('fa-IR').format(amendmentPreview.maxCompanies)}`}
                {amendmentPreview.supportUntilUtc !== null && ` — پشتیبانی تا: ${formatDate(amendmentPreview.supportUntilUtc)}`}
                {amendmentPreview.updatesUntilUtc !== null && ` — به‌روزرسانی تا: ${formatDate(amendmentPreview.updatesUntilUtc)}`}
              </p>}
              <div className="mt-3 flex gap-2">
                <Button type="button" size="sm" variant="outline" disabled={busyAmendment} onClick={() => void previewAmendment(amendmentForm.line)}>پیش‌نمایش</Button>
                <Button type="button" size="sm" disabled={busyAmendment} onClick={() => void createAmendment(amendmentForm.line)}>ثبت درخواست</Button>
                <Button type="button" size="sm" variant="ghost" disabled={busyAmendment} onClick={resetAmendmentForm}>انصراف</Button>
              </div>
            </div>}
            {orderAmendments.filter(amendment => amendment.status === 'Requested').map(amendment => <div key={amendment.id} className="mt-2 flex flex-wrap items-center gap-2 rounded-md border border-border p-2 text-sm">
              <span>{amendmentKindLabels[amendment.kind] ?? amendment.kind} — {amendmentStatusLabels[amendment.status]}</span>
              {canAmendIssue && <Button type="button" size="sm" disabled={busyAmendment} onClick={() => void submitAmendmentForApproval(amendment)}>ارسال برای تأیید</Button>}
            </div>)}
            <p className="mt-2 text-xs text-muted-foreground">صدور Amendment همیشه از مسیر امضای رسمی لایسنس انجام می‌شود و تأیید پرداخت به‌تنهایی هیچ چیزی صادر نمی‌کند.</p>
          </div>
        </CardContent></Card>}
    </>}

    {view === 'receivables' && <Card><CardHeader><CardTitle>مطالبات باز</CardTitle>
      <CardDescription>گزارش زمان‌خوان مانده بدهی بر اساس پرداخت‌های تأییدشده؛ سفارش‌های پرداخت‌شده در این فهرست نمی‌آیند.</CardDescription></CardHeader>
      <CardContent>
        {loading ? <p role="status" className="py-6 text-sm text-muted-foreground">در حال دریافت مطالبات…</p>
          : !currentReceivables || currentReceivables.items.length === 0 ? <p className="py-6 text-sm text-muted-foreground">مطلب باز وجود ندارد.</p>
          : <div className="overflow-x-auto"><table className="w-full min-w-[720px] text-sm">
            <thead><tr className="border-b text-right">
              <th scope="col" className="px-3 py-3 font-medium">شماره سفارش</th>
              <th scope="col" className="px-3 py-3 font-medium">مشتری</th>
              <th scope="col" className="px-3 py-3 font-medium">مبلغ کل</th>
              <th scope="col" className="px-3 py-3 font-medium">تأییدشده</th>
              <th scope="col" className="px-3 py-3 font-medium">مانده</th>
              <th scope="col" className="px-3 py-3 font-medium">وضعیت</th>
              <th scope="col" className="px-3 py-3 font-medium">سررسید</th>
            </tr></thead><tbody>{currentReceivables.items.map(item => <tr key={item.id} className={'border-b last:border-0' + (item.overdue ? ' text-destructive' : '')}>
              <td className="px-3 py-3 font-mono text-xs" dir="ltr">{item.orderNumber}</td>
              <td className="px-3 py-3">{item.customerNumber}</td>
              <td className="px-3 py-3">{formatMoney(item.totalAmount, item.currencyCode)}</td>
              <td className="px-3 py-3">{formatMoney(item.verifiedPaidAmount, item.currencyCode)}</td>
              <td className="px-3 py-3">{formatMoney(item.remainingAmount, item.currencyCode)}</td>
              <td className="px-3 py-3">{item.overdue && <span className="text-destructive">معوق — </span>}{paymentStatusLabels[item.paymentStatus] ?? item.paymentStatus}</td>
              <td className="px-3 py-3">{formatDate(item.dueAtUtc)}</td>
            </tr>)}</tbody></table></div>}
        <Pagination page={receivablePage} totalPages={currentReceivables?.totalPages ?? 0} disabled={loading} onMove={setReceivablePage} />
      </CardContent></Card>}
  </section>;
}
