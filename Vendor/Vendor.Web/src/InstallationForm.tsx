import { useEffect, useState, type FormEvent } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ManagedSelect, type SelectOption } from '@/components/forms/managed-select';

export type InstallationDraft = {
  id?: string;
  customerId: string;
  installationCode: string;
  name: string;
  environment: string;
  status?: string;
  rowVersion?: string;
};

type InstallationFormProps = {
  initial?: InstallationDraft;
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

const environments: SelectOption[] = [
  { value: 'Production', label: 'عملیاتی' },
  { value: 'Staging', label: 'آزمایشی' },
  { value: 'Development', label: 'توسعه' },
];

function errorMessage(status: number, code: string): string {
  if (status === 401) return 'نشست شما معتبر نیست یا پایان یافته است؛ دوباره وارد شوید.';
  if (status === 403) return 'حساب شما مجوز لازم برای این عملیات را ندارد.';
  if (status === 400 && code === 'invalid_installation') return 'اطلاعات نصب معتبر نیست؛ فیلدها را بررسی کنید.';
  if (status === 400 && code === 'customer_not_found') return 'مشتری انتخاب‌شده وجود ندارد یا در دسترس نیست.';
  if (status === 400 && code === 'invalid_row_version') return 'نسخه اطلاعات برای ویرایش معتبر نیست؛ فهرست را تازه‌سازی کنید.';
  if (status === 409 && code === 'installation_code_exists') return 'این کد نصب قبلاً ثبت شده است.';
  if (status === 409 && code === 'stale_row_version') return 'اطلاعات نصب تغییر کرده است؛ فهرست را تازه‌سازی و دوباره تلاش کنید.';
  if (status === 409 && code === 'invalid_status_transition') return 'این تغییر وضعیت مجاز نیست.';
  if (status === 429) return 'تعداد درخواست‌ها زیاد است؛ کمی بعد دوباره تلاش کنید.';
  if (status >= 500) return 'خطای داخلی سامانه رخ داد. جزئیات حساس نمایش داده نمی‌شود.';
  return 'ذخیره اطلاعات نصب ناموفق بود؛ دوباره تلاش کنید.';
}

export function InstallationForm({ initial, canReadCustomers, onCancel, onSaved }: InstallationFormProps) {
  const editing = Boolean(initial?.id);
  const [customerId, setCustomerId] = useState(initial?.customerId ?? '');
  const [customerOptions, setCustomerOptions] = useState<SelectOption[]>([]);
  const [customerPage, setCustomerPage] = useState(1);
  const [customerPages, setCustomerPages] = useState({ hasPreviousPage: false, hasNextPage: false, totalCount: 0 });
  const [customerLoading, setCustomerLoading] = useState(false);
  const [customerError, setCustomerError] = useState('');
  const [customerLabel, setCustomerLabel] = useState('');
  const [installationCode, setInstallationCode] = useState(initial?.installationCode ?? '');
  const [name, setName] = useState(initial?.name ?? '');
  const [environment, setEnvironment] = useState(initial?.environment ?? 'Production');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    if (editing || !canReadCustomers) return;
    let active = true;
    setCustomerLoading(true);
    setCustomerError('');
    fetch(`/api/vendor/customers/?page=${customerPage}&pageSize=100`, {
      credentials: 'same-origin',
      cache: 'no-store',
      headers: { Accept: 'application/json' },
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
      credentials: 'same-origin',
      cache: 'no-store',
      headers: { Accept: 'application/json' },
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
      if (!editing && (!canReadCustomers || !customerId)) {
        throw new Error('برای انتخاب مشتری باید مجوز مشاهده مشتریان را داشته باشید.');
      }
      const tokenResponse = await fetch('/api/auth/antiforgery', {
        credentials: 'same-origin',
        cache: 'no-store',
      });
      if (!tokenResponse.ok) throw new Error(errorMessage(tokenResponse.status, ''));
      const tokenBody = await tokenResponse.json() as { requestToken?: string };
      if (!tokenBody.requestToken) throw new Error('توکن امنیتی در پاسخ سرور وجود ندارد.');

      const payload = {
        ...(!editing ? { customerId } : {}),
        installationCode: installationCode.trim(),
        name: name.trim(),
        environment,
        ...(editing ? { rowVersion: initial?.rowVersion ?? '' } : {}),
      };
      const response = await fetch(editing ? `/api/vendor/installations/${initial!.id}` : '/api/vendor/installations/', {
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
      setError(reason instanceof Error ? reason.message : 'ذخیره اطلاعات نصب ناموفق بود.');
    } finally {
      setBusy(false);
    }
    if (saved) onSaved();
  }

  return <Card aria-labelledby="installation-form-title" dir="rtl">
    <CardHeader className="border-b">
      <CardTitle id="installation-form-title">{editing ? 'ویرایش نصب' : 'ثبت نصب جدید'}</CardTitle>
      <CardDescription>این فرم فقط فرادادهٔ نصب را در سامانه مرکزی ثبت می‌کند؛ سرور یا دیتابیس حسابداری مشتری را ایجاد یا تغییر نمی‌دهد.</CardDescription>
    </CardHeader>
    <CardContent className="pt-4">
      <form className="space-y-4" onSubmit={submit} noValidate>
        {editing ? (
          <div className="grid gap-2" dir="rtl">
            <span className="text-sm font-semibold">مشتری مرتبط</span>
            <p className="rounded-md border border-input bg-muted/40 px-3 py-2 text-sm" dir="auto">{customerLabel || initial?.customerId}</p>
          </div>
        ) : (
          <div className="space-y-2">
            <ManagedSelect
              name="installation-customer"
              label="مشتری تجاری"
              options={customerOptions}
              value={customerId}
              onValueChange={setCustomerId}
              required
              disabled={busy || customerLoading || !canReadCustomers}
              hint={customerLoading ? 'در حال دریافت مشتریان…' : `مجموع مشتریان: ${customerPages.totalCount}`}
              error={!canReadCustomers ? 'برای انتخاب مشتری، مجوز مشاهده مشتریان لازم است.' : customerError || undefined}
            />
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
            <label htmlFor="installation-code" className="text-sm font-medium">کد نصب</label>
            <input id="installation-code" required maxLength={64} autoComplete="off" dir="ltr"
              className="h-10 w-full rounded-md border border-input bg-background px-3 text-left outline-none focus-visible:ring-2 focus-visible:ring-ring/50"
              value={installationCode} onChange={event => setInstallationCode(event.target.value)} disabled={busy} />
          </div>
          <div className="space-y-2">
            <label htmlFor="installation-name" className="text-sm font-medium">نام نصب</label>
            <input id="installation-name" required maxLength={200} autoComplete="off"
              className="h-10 w-full rounded-md border border-input bg-background px-3 outline-none focus-visible:ring-2 focus-visible:ring-ring/50"
              value={name} onChange={event => setName(event.target.value)} disabled={busy} />
          </div>
          <ManagedSelect label="محیط نصب" name="installation-environment" options={environments}
            value={environment} onValueChange={setEnvironment} required disabled={busy} />
        </div>
        {error && <p role="alert" className="text-sm leading-6 text-destructive">{error}</p>}
        <div className="flex flex-wrap gap-2">
          <Button type="submit" disabled={busy || !installationCode.trim() || !name.trim() || (!editing && (!customerId || !canReadCustomers || customerLoading || Boolean(customerError)))}>
            {busy ? 'در حال ذخیره…' : editing ? 'ذخیره تغییرات' : 'ثبت نصب'}
          </Button>
          <Button type="button" variant="outline" onClick={onCancel} disabled={busy}>انصراف</Button>
        </div>
      </form>
    </CardContent>
  </Card>;
}
