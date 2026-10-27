import { useEffect, useState, type FormEvent } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ManagedSelect, type SelectOption } from '@/components/forms/managed-select';

export type ContractDraft = {
  id?: string;
  customerId: string;
  contractNumber: string;
  startsAtUtc: string;
  endsAtUtc: string | null;
  currencyCode: string;
  status?: string;
  rowVersion?: string;
};

type ContractFormProps = {
  initial?: ContractDraft;
  canReadCustomers: boolean;
  onCancel: () => void;
  onSaved: () => void;
};

type Customer = { id: string; customerNumber: string; displayName: string };
type PageResponse<T> = { items: T[]; hasNextPage: boolean; hasPreviousPage: boolean; totalCount: number };
type ApiError = { error?: string; message?: string };

const currencies: SelectOption[] = [
  { value: 'USD', label: 'دلار آمریکا (USD)' },
  { value: 'EUR', label: 'یورو (EUR)' },
  { value: 'IRR', label: 'ریال ایران (IRR)' },
  { value: 'AED', label: 'درهم امارات (AED)' },
];

function currencyOptionsFor(currentCode?: string): SelectOption[] {
  if (!currentCode || currencies.some(option => option.value === currentCode)) return currencies;
  return [{ value: currentCode, label: `ارز فعلی ثبت‌شده (${currentCode})` }, ...currencies];
}

function dateInput(value?: string | null): string {
  if (!value) return '';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '' : date.toISOString().slice(0, 10);
}

function utcDate(value: string): string {
  return new Date(`${value}T00:00:00.000Z`).toISOString();
}

function errorMessage(status: number, code: string): string {
  if (status === 401) return 'نشست شما معتبر نیست یا پایان یافته است؛ دوباره وارد شوید.';
  if (status === 403) return 'حساب شما مجوز لازم برای مدیریت قراردادها را ندارد.';
  if (status === 400 && code === 'invalid_contract') return 'اطلاعات قرارداد معتبر نیست؛ شماره، ارز و بازه اعتبار را بررسی کنید.';
  if (status === 400 && code === 'customer_not_found') return 'مشتری انتخاب‌شده وجود ندارد یا در دسترس نیست.';
  if (status === 400 && code === 'invalid_row_version') return 'نسخه اطلاعات معتبر نیست؛ فهرست را تازه‌سازی کنید.';
  if (status === 409 && code === 'contract_number_exists') return 'این شماره قرارداد برای مشتری انتخاب‌شده قبلاً ثبت شده است.';
  if (status === 409 && code === 'stale_row_version') return 'اطلاعات قرارداد تغییر کرده است؛ فهرست را تازه‌سازی و دوباره تلاش کنید.';
  if (status === 409 && code === 'contract_not_editable_after_draft') return 'قرارداد پس از خروج از پیش‌نویس قابل ویرایش نیست.';
  if (status === 429) return 'تعداد درخواست‌ها زیاد است؛ کمی بعد دوباره تلاش کنید.';
  if (status >= 500) return 'خطای داخلی سامانه رخ داد. جزئیات حساس نمایش داده نمی‌شود.';
  return 'ذخیره اطلاعات قرارداد ناموفق بود؛ دوباره تلاش کنید.';
}

export function ContractForm({ initial, canReadCustomers, onCancel, onSaved }: ContractFormProps) {
  const editing = Boolean(initial?.id);
  const [customerId, setCustomerId] = useState(initial?.customerId ?? '');
  const [customerOptions, setCustomerOptions] = useState<SelectOption[]>([]);
  const [customerPage, setCustomerPage] = useState(1);
  const [customerPages, setCustomerPages] = useState({ hasPreviousPage: false, hasNextPage: false, totalCount: 0 });
  const [customerLoading, setCustomerLoading] = useState(false);
  const [customerError, setCustomerError] = useState('');
  const [customerLabel, setCustomerLabel] = useState('');
  const [contractNumber, setContractNumber] = useState(initial?.contractNumber ?? '');
  const [startsAt, setStartsAt] = useState(dateInput(initial?.startsAtUtc) || new Date().toISOString().slice(0, 10));
  const [endsAt, setEndsAt] = useState(dateInput(initial?.endsAtUtc));
  const [currencyCode, setCurrencyCode] = useState(initial?.currencyCode ?? 'USD');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    if (editing || !canReadCustomers) return;
    let active = true;
    setCustomerLoading(true);
    setCustomerError('');
    fetch(`/api/vendor/customers/?page=${customerPage}&pageSize=100`, {
      credentials: 'same-origin', cache: 'no-store', headers: { Accept: 'application/json' },
    }).then(async response => {
      if (!response.ok) throw new Error(errorMessage(response.status, ''));
      const page = await response.json() as PageResponse<Customer>;
      if (!Array.isArray(page.items) || typeof page.hasNextPage !== 'boolean' ||
          typeof page.hasPreviousPage !== 'boolean' || typeof page.totalCount !== 'number') {
        throw new Error('پاسخ فهرست مشتریان با قرارداد صفحه‌بندی سازگار نیست.');
      }
      if (!active) return;
      const options = page.items.filter(item => typeof item.id === 'string')
        .map(item => ({ value: item.id, label: `${item.customerNumber} — ${item.displayName}` }));
      setCustomerOptions(options);
      setCustomerPages({ hasPreviousPage: page.hasPreviousPage, hasNextPage: page.hasNextPage, totalCount: page.totalCount });
      setCustomerId(current => options.some(option => option.value === current) ? current : (options[0]?.value ?? ''));
    }).catch(reason => {
      if (active) {
        setCustomerOptions([]);
        setCustomerError(reason instanceof Error ? reason.message : 'دریافت فهرست مشتریان ناموفق بود.');
      }
    }).finally(() => { if (active) setCustomerLoading(false); });
    return () => { active = false; };
  }, [canReadCustomers, customerPage, editing]);

  useEffect(() => {
    if (!editing || !initial?.customerId) return;
    if (!canReadCustomers) {
      setCustomerLabel(initial.customerId);
      return;
    }
    let active = true;
    fetch(`/api/vendor/customers/${initial.customerId}`, {
      credentials: 'same-origin', cache: 'no-store', headers: { Accept: 'application/json' },
    }).then(async response => {
      if (!response.ok) throw new Error(errorMessage(response.status, ''));
      const customer = await response.json() as Customer;
      if (active) setCustomerLabel(`${customer.customerNumber} — ${customer.displayName}`);
    }).catch(() => { if (active) setCustomerLabel(initial.customerId); });
    return () => { active = false; };
  }, [canReadCustomers, editing, initial?.customerId]);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy || (!editing && (!canReadCustomers || !customerId))) return;
    setBusy(true);
    setError('');
    try {
      const tokenResponse = await fetch('/api/auth/antiforgery', { credentials: 'same-origin', cache: 'no-store' });
      if (!tokenResponse.ok) throw new Error(errorMessage(tokenResponse.status, ''));
      const tokenBody = await tokenResponse.json() as { requestToken?: string };
      if (!tokenBody.requestToken) throw new Error('توکن امنیتی در پاسخ سرور وجود ندارد.');
      const payload = {
        ...(editing ? {} : { customerId }),
        contractNumber: contractNumber.trim(),
        startsAtUtc: utcDate(startsAt),
        endsAtUtc: endsAt ? utcDate(endsAt) : null,
        currencyCode,
        ...(editing ? { rowVersion: initial?.rowVersion } : {}),
      };
      const response = await fetch(editing ? `/api/vendor/contracts/${initial!.id}` : '/api/vendor/contracts/', {
        method: editing ? 'PUT' : 'POST',
        credentials: 'same-origin',
        cache: 'no-store',
        headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': tokenBody.requestToken },
        body: JSON.stringify(payload),
      });
      if (!response.ok) {
        let code = '';
        try { const body = await response.json() as ApiError; code = body.error ?? body.message ?? ''; } catch { /* generic response */ }
        throw new Error(errorMessage(response.status, code));
      }
      onSaved();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'ذخیره اطلاعات قرارداد ناموفق بود.');
    } finally {
      setBusy(false);
    }
  }

  return <Card dir="rtl" aria-labelledby="contract-form-title">
    <CardHeader>
      <CardTitle id="contract-form-title">{editing ? 'ویرایش قرارداد پیش‌نویس' : 'ثبت قرارداد جدید'}</CardTitle>
      <CardDescription>تاریخ‌ها در ابتدای روز بر مبنای UTC ذخیره می‌شوند. قرارداد جدید با وضعیت پیش‌نویس ثبت می‌شود.</CardDescription>
    </CardHeader>
    <CardContent>
      <form className="grid gap-4 sm:grid-cols-2" onSubmit={submit}>
        {error && <p role="alert" className="sm:col-span-2 rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">{error}</p>}
        {editing
          ? <div className="grid gap-2 text-start"><label className="text-sm font-semibold" htmlFor="contract-customer-readonly">مشتری تجاری</label><input id="contract-customer-readonly" readOnly value={customerLabel || initial?.customerId || ''} className="min-h-11 rounded-md border bg-muted px-3 text-sm" /></div>
          : <div className="grid gap-2 text-start">
              <ManagedSelect label="مشتری تجاری" name="contract-customer" options={customerOptions} value={customerId} onValueChange={setCustomerId} required disabled={!canReadCustomers || customerLoading || !customerOptions.length} hint={customerLoading ? 'در حال دریافت مشتریان…' : `مجموع مشتریان: ${customerPages.totalCount}`} {...(!canReadCustomers ? { error: 'برای انتخاب مشتری، مجوز مشاهده مشتریان لازم است.' } : customerError ? { error: customerError } : {})} />
              <div className="flex items-center justify-between text-xs text-muted-foreground"><Button type="button" variant="outline" size="sm" disabled={customerLoading || !customerPages.hasPreviousPage} onClick={() => setCustomerPage(current => Math.max(1, current - 1))}>مشتری‌های قبلی</Button><span>صفحه {customerPage}</span><Button type="button" variant="outline" size="sm" disabled={customerLoading || !customerPages.hasNextPage} onClick={() => setCustomerPage(current => current + 1)}>مشتری‌های بعدی</Button></div>
            </div>}
        <div className="grid gap-2 text-start"><label htmlFor="contract-number" className="text-sm font-semibold">شماره قرارداد *</label><input id="contract-number" required maxLength={64} value={contractNumber} onChange={event => setContractNumber(event.currentTarget.value)} autoComplete="off" className="min-h-11 rounded-md border border-input bg-background px-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring" /></div>
        <ManagedSelect label="ارز قرارداد" name="contract-currency" options={currencyOptionsFor(initial?.currencyCode)} value={currencyCode} onValueChange={setCurrencyCode} required />
        <div className="grid gap-2 text-start"><label htmlFor="contract-starts" className="text-sm font-semibold">شروع اعتبار *</label><input id="contract-starts" type="date" required value={startsAt} onChange={event => setStartsAt(event.currentTarget.value)} className="min-h-11 rounded-md border border-input bg-background px-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring" /></div>
        <div className="grid gap-2 text-start"><label htmlFor="contract-ends" className="text-sm font-semibold">پایان اعتبار (اختیاری)</label><input id="contract-ends" type="date" min={startsAt} value={endsAt} onChange={event => setEndsAt(event.currentTarget.value)} className="min-h-11 rounded-md border border-input bg-background px-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring" /></div>
        <div className="flex flex-wrap justify-end gap-2 sm:col-span-2"><Button type="button" variant="outline" onClick={onCancel} disabled={busy}>انصراف</Button><Button type="submit" disabled={busy || (!editing && (!canReadCustomers || !customerId)) || !contractNumber.trim() || !startsAt || Boolean(endsAt && endsAt < startsAt)}>{busy ? 'در حال ذخیره…' : editing ? 'ذخیره تغییرات' : 'ثبت قرارداد'}</Button></div>
      </form>
    </CardContent>
  </Card>;
}
