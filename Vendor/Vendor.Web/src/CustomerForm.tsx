import { useState, type FormEvent } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';

export type CustomerDraft = {
  id?: string;
  customerNumber: string;
  legalName: string;
  displayName: string;
  rowVersion?: string;
};

type CustomerFormProps = {
  initial?: CustomerDraft;
  onCancel: () => void;
  onSaved: () => void;
};

type ApiError = { error?: string; message?: string };

function errorMessage(status: number, code: string): string {
  if (status === 401) return 'نشست شما معتبر نیست یا پایان یافته است؛ دوباره وارد شوید.';
  if (status === 403) return 'حساب شما مجوز مدیریت مشتریان را ندارد.';
  if (status === 409 && code === 'customer_number_exists') return 'این شماره مشتری قبلاً ثبت شده است.';
  if (status === 409 && code === 'stale_row_version') return 'اطلاعات مشتری تغییر کرده است؛ فهرست را تازه‌سازی و دوباره تلاش کنید.';
  if (status === 400 && code === 'invalid_customer') return 'اطلاعات مشتری معتبر نیست؛ فیلدها را بررسی کنید.';
  if (status === 400 && code === 'invalid_row_version') return 'نسخه اطلاعات برای ویرایش معتبر نیست؛ فهرست را تازه‌سازی کنید.';
  if (status === 429) return 'تعداد درخواست‌ها زیاد است؛ کمی بعد دوباره تلاش کنید.';
  if (status >= 500) return 'خطای داخلی سامانه رخ داد. جزئیات حساس نمایش داده نمی‌شود.';
  return 'ذخیره اطلاعات ناموفق بود؛ دوباره تلاش کنید.';
}

export function CustomerForm({ initial, onCancel, onSaved }: CustomerFormProps) {
  const editing = Boolean(initial?.id);
  const [customerNumber, setCustomerNumber] = useState(initial?.customerNumber ?? '');
  const [legalName, setLegalName] = useState(initial?.legalName ?? '');
  const [displayName, setDisplayName] = useState(initial?.displayName ?? '');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy) return;
    setBusy(true);
    setError('');
    let saved = false;
    try {
      const tokenResponse = await fetch('/api/auth/antiforgery', {
        credentials: 'same-origin',
        cache: 'no-store',
      });
      if (!tokenResponse.ok) throw new Error(errorMessage(tokenResponse.status, ''));
      const tokenBody = await tokenResponse.json() as { requestToken?: string };
      if (!tokenBody.requestToken) throw new Error('توکن امنیتی در پاسخ سرور وجود ندارد.');

      const payload = {
        customerNumber: customerNumber.trim(),
        legalName: legalName.trim(),
        displayName: displayName.trim(),
        ...(editing ? { rowVersion: initial?.rowVersion ?? '' } : {}),
      };
      const response = await fetch(editing ? `/api/vendor/customers/${initial!.id}` : '/api/vendor/customers/', {
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
      setError(reason instanceof Error ? reason.message : 'ذخیره اطلاعات ناموفق بود.');
    } finally {
      setBusy(false);
    }
    if (saved) onSaved();
  }

  return <Card aria-labelledby="customer-form-title" dir="rtl">
    <CardHeader className="border-b">
      <CardTitle id="customer-form-title">{editing ? 'ویرایش مشتری تجاری' : 'ثبت مشتری تجاری'}</CardTitle>
      <CardDescription>این فرم فقط اطلاعات تجاری سامانه مرکزی را ثبت می‌کند و در دیتابیس حسابداری مشتری تغییری ایجاد نمی‌کند.</CardDescription>
    </CardHeader>
    <CardContent className="pt-4">
      <form className="space-y-4" onSubmit={submit}>
        <div className="grid gap-4 md:grid-cols-2">
          <div className="space-y-2">
            <label htmlFor="customer-number" className="text-sm font-medium">شماره مشتری</label>
            <input id="customer-number" name="customerNumber" required maxLength={32} autoComplete="off" dir="ltr"
              className="h-10 w-full rounded-md border border-input bg-background px-3 text-left outline-none focus-visible:ring-2 focus-visible:ring-ring/50"
              value={customerNumber} onChange={event => setCustomerNumber(event.target.value)} disabled={busy} />
          </div>
          <div className="space-y-2">
            <label htmlFor="customer-legal-name" className="text-sm font-medium">نام حقوقی</label>
            <input id="customer-legal-name" name="legalName" required maxLength={200} autoComplete="organization"
              className="h-10 w-full rounded-md border border-input bg-background px-3 outline-none focus-visible:ring-2 focus-visible:ring-ring/50"
              value={legalName} onChange={event => setLegalName(event.target.value)} disabled={busy} />
          </div>
          <div className="space-y-2 md:col-span-2">
            <label htmlFor="customer-display-name" className="text-sm font-medium">نام نمایشی</label>
            <input id="customer-display-name" name="displayName" required maxLength={200} autoComplete="off"
              className="h-10 w-full rounded-md border border-input bg-background px-3 outline-none focus-visible:ring-2 focus-visible:ring-ring/50"
              value={displayName} onChange={event => setDisplayName(event.target.value)} disabled={busy} />
          </div>
        </div>
        {error && <p role="alert" className="text-sm leading-6 text-destructive">{error}</p>}
        <div className="flex flex-wrap gap-2">
          <Button type="submit" disabled={busy || !customerNumber.trim() || !legalName.trim() || !displayName.trim()}>
            {busy ? 'در حال ذخیره…' : editing ? 'ذخیره تغییرات' : 'ثبت مشتری'}
          </Button>
          <Button type="button" variant="outline" onClick={onCancel} disabled={busy}>انصراف</Button>
        </div>
      </form>
    </CardContent>
  </Card>;
}
