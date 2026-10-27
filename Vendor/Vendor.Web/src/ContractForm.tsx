import { useEffect, useState, type FormEvent } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ManagedSelect, type SelectOption } from '@/components/forms/managed-select';

export type ContractDraft = {
  id?: string;
  customerId: string;
  contractNumber: string;
  startsAtUtc: string;
  endsAtUtc: string;
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
type PageResponse<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
};
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

function todayUtcDate() {
  return new Date().toISOString().slice(0, 10);
}

function asUtcDate(value: string) {
  return new Date(`${value}T00:00:00.000Z`).toISOString();
}

function errorMessage(status: number, code: string): string {
  if (status === 401) return 'نشست شما معتبر نیست یا پایان یافته است؛ دوباره وارد شوید.';
  if (status === 403) return 'حساب شما مجوز لازم برای این عملیات را ندارد.';
  if (status === 400 && code === 'invalid_contract') return 'شماره، تاریخ‌ها یا کد ارز معتبر نیستند.';
  if (status === 400 && code === 'customer_not_found') return 'مشتری انتخاب‌شده وجود ندارد یا در دسترس نیست.';
  if (status === 400 && code === 'invalid_row_version') return 'نسخه اطلاعات برای ویرایش معتبر نیست؛ فهرست را تازه‌سازی کنید.';
  if (status === 409 && code === 'contract_number_exists') return 'این شماره قرارداد برای مشتری انتخاب‌شده قبلاً ثبت شده است.';
  if (status === 409 && code === 'stale_row_version') return 'قرارداد تغییر کرده است؛ فهرست را تازه‌سازی و دوباره تلاش کنید.';
  if (status === 409 && code === 'contract_not_editable_after_draft') return 'قرارداد از وضعیت پیش‌نویس خارج شده و دیگر قابل ویرایش نیست.';
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
  const [startsAtUtc, setStartsAtUtc] = useState(initial?.startsAtUtc?.slice(0, 10) ?? todayUtcDate());
  const [endsAtUtc, setEndsAtUtc] = useState(initial?.endsAtUtc?.slice(0, 10) ?? '');
  const [currencyCode, setCurrencyCode] = useState(initial?.currencyCode ?? 'IRR');
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
    if (!editing || !initial?.customerId || !canReadCustomers) {
      if (editing && initial?.customerId) setCustomerLabel(initial.customerId);
      return;
    }
    let active = true;
    fetch(`/api/vendor/customers/${initial.customerId}`, {
      credentials: 'same-origin', cache: 'no-store', headers: { Accept: 'application/json' },
    }).then(async response => {
      if (!response.ok) throw new Error('نام مشتری مرتبط قابل دریافت نیست.');
      const customer = await response.json() as { customerNumber?: string; displayName?: string };
      if (active) setCustomerLabel([customer.customerNumber, customer.displayName].filter(Boolean).join(' — ') || initial.customerId);
    }).catch(() => { if (active) setCustomerLabel(initial.customerId); });
    return () => { active = false; };
  }, [canReadCustomers, editing, initial?.customerId]);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy) return;
    setBusy(true);
    setError('');
    let saved = false;
    try {
      const number = contractNumber.trim();
      const currency = currencyCode.trim().toUpperCase();
      if (!editing && (!canReadCustomers || !customerId)) throw new Error('برای انتخاب مشتری باید مجوز مشاهده مشتریان را داشته باشید.');
      if (!number || number.length > 64 || !startsAtUtc || !/^[A-Z]{3}$/.test(currency) ||
          (endsAtUtc && endsAtUtc < startsAtUtc)) {
        throw new Error('شماره قرارداد، تاریخ‌ها یا کد ارز معتبر نیستند.');
      }
      const tokenResponse = await fetch('/api/auth/antiforgery', { credentials: 'same-origin', cache: 'no-store' });
      if (!tokenResponse.ok) throw new Error(errorMessage(tokenResponse.status, ''));
      const tokenBody = await tokenResponse.json() as { requestToken?: string };
      if (!tokenBody.requestToken) throw new Error('توکن امنیتی در پاسخ سرور وجود ندارد.');

      const payload = {
        ...(!editing ? { customerId } : {}),
        contractNumber: number,
        startsAtUtc: asUtcDate(startsAtUtc),
        endsAtUtc: endsAtUtc ? asUtcDate(endsAtUtc) : null,
        currencyCode: currency,
        ...(editing ? { rowVersion: initial?.rowVersion ?? '' } : {}),
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
        try {
          const body = await response.json() as ApiError;
          code = body.error ?? body.message ?? '';
        } catch {
          // Keep the public error generic when the response is not JSON.
        }
        throw new Error(errorMessage(response.status, code));
      }
      saved = true;
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'ذخیره اطلاعات قرارداد ناموفق بود.');
    } finally {
      setBusy(false);
    }
    if (saved) onSaved();
  }

  return <Card aria-labelledby="contract-form-title" dir="rtl">
    <CardHeader className="border-b">
      <CardTitle id="contract-form-title">{editing ? 'ویرایش قرارداد پیش‌نویس' : 'ثبت قرارداد جدید'}</CardTitle>
      <CardDescription>قرارداد جدید در وضعیت پیش‌نویس ثبت می‌شود؛ فقط قراردادهای پیش‌نویس قابل ویرایش هستند. این فرم دیتابیس حسابداری مشتری را تغییر نمی‌دهد.</CardDescription>
    </CardHeader>
    <CardContent className="pt-4">
      <form className="space-y-4" onSubmit={submit} noValidate>
        {editing ? (
          <div className="grid gap-2">
            <span className="text-sm font-semibold">مشتری مرتبط</span>
            <p className="rounded-md border border-input bg-muted/40 px-3 py-2 text-sm" dir="auto">{customerLabel || initial?.customerId}</p>
          </div>
        ) : (
          <div className="space-y-2">
            <ManagedSelect name="contract-customer" label="مشتری تجاری" options={customerOptions} value={customerId}
              onValueChange={setCustomerId} required disabled={busy || customerLoading || !canReadCustomers}
              hint={customerLoading ? 'در حال دریافت مشتریان…' : customerPages.totalCount === 0 ? 'ابتدا مشتری تجاری ثبت کنید.' : `مجموع مشتریان: ${customerPages.totalCount}`}
              {...(!canReadCustomers ? { error: 'برای انتخاب مشتری، مجوز مشاهده مشتریان لازم است.' } : customerError ? { error: customerError } : {})} />
            {canReadCustomers && (customerPages.hasPreviousPage || customerPages.hasNextPage) && (
              <div className="flex flex-wrap items-center gap-2">
                <Button type="button" size="sm" variant="outline" disabled={busy || customerLoading || !customerPages.hasPreviousPage}
                  onClick={() => setCustomerPage(page => Math.max(1, page - 1))}>مشتریان قبلی</Button>
                <span className="text-xs text-muted-foreground">صفحه {customerPage}</span>
                <Button type="button" size="sm" variant="outline" disabled={busy || customerLoading || !customerPages.hasNextPage}
                  onClick={() => setCustomerPage(page => page + 1)}>مشتریان بعدی</Button>
              </div>
            )}
          </div>
        )}
        <div className="grid gap-4 md:grid-cols-2">
          <div className="space-y-2">
            <label htmlFor="contract-number" className="text-sm font-medium">شماره قرارداد</label>
            <input id="contract-number" required maxLength={64} autoComplete="off" dir="ltr"
              className="h-10 w-full rounded-md border border-input bg-background px-3 text-left outline-none focus-visible:ring-2 focus-visible:ring-ring/50"
              value={contractNumber} onChange={event => setContractNumber(event.target.value)} disabled={busy} />
          </div>
          <ManagedSelect label="ارز قرارداد" name="contract-currency"
            options={currencyOptionsFor(initial?.currencyCode)} value={currencyCode}
            onValueChange={setCurrencyCode} required disabled={busy} />
          <div className="space-y-2">
            <label htmlFor="contract-starts" className="text-sm font-medium">تاریخ شروع</label>
            <input id="contract-starts" type="date" required
              className="h-10 w-full rounded-md border border-input bg-background px-3 outline-none focus-visible:ring-2 focus-visible:ring-ring/50"
              value={startsAtUtc} onChange={event => setStartsAtUtc(event.target.value)} disabled={busy} />
          </div>
          <div className="space-y-2">
            <label htmlFor="contract-ends" className="text-sm font-medium">تاریخ پایان (اختیاری)</label>
            <input id="contract-ends" type="date" min={startsAtUtc || undefined}
              className="h-10 w-full rounded-md border border-input bg-background px-3 outline-none focus-visible:ring-2 focus-visible:ring-ring/50"
              value={endsAtUtc} onChange={event => setEndsAtUtc(event.target.value)} disabled={busy} />
          </div>
        </div>
        {error && <p role="alert" className="text-sm leading-6 text-destructive">{error}</p>}
        <div className="flex flex-wrap gap-2">
          <Button type="submit" disabled={busy || !contractNumber.trim() || !startsAtUtc || !/^[A-Za-z]{3}$/.test(currencyCode.trim()) ||
            (endsAtUtc && endsAtUtc < startsAtUtc) || (!editing && (!customerId || !canReadCustomers || customerLoading || Boolean(customerError)))}>
            {busy ? 'در حال ذخیره…' : editing ? 'ذخیره تغییرات' : 'ثبت قرارداد'}
          </Button>
          <Button type="button" variant="outline" onClick={onCancel} disabled={busy}>انصراف</Button>
        </div>
      </form>
    </CardContent>
  </Card>;
}
