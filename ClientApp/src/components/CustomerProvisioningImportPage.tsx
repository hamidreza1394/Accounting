import { useEffect, useState, type ChangeEvent, type FormEvent } from 'react';
import { AlertCircle, CheckCircle2, FileUp, KeyRound, ShieldCheck } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';

type AntiforgeryResponse = { headerName?: string; requestToken?: string };
type ProvisioningPackage = {
  formatVersion: number;
  keyId: string;
  algorithm: string;
  payloadJson: string;
  signatureBase64: string;
};
type ApplyResponse = {
  acknowledgement?: unknown;
  replayed?: boolean;
  enrollmentToken?: string | null;
  error?: string;
};
type ApiProblem = { detail?: string; title?: string; error?: string };

function getRequestId(packageText: string): string {
  const parsed = JSON.parse(packageText) as Partial<ProvisioningPackage>;
  if (!parsed.payloadJson || typeof parsed.payloadJson !== 'string') {
    throw new Error('ساختار بسته معتبر نیست یا بخش payloadJson وجود ندارد.');
  }
  const payload = JSON.parse(parsed.payloadJson) as { requestId?: unknown };
  if (typeof payload.requestId !== 'string' || !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(payload.requestId)) {
    throw new Error('شناسه درخواست در بسته معتبر نیست.');
  }
  return payload.requestId;
}

export function CustomerProvisioningImportPage() {
  const [packageText, setPackageText] = useState('');
  const [fileName, setFileName] = useState('');
  const [requestToken, setRequestToken] = useState('');
  const [headerName, setHeaderName] = useState('X-XSRF-TOKEN');
  const [requestId, setRequestId] = useState('');
  const [acknowledgement, setAcknowledgement] = useState('');
  const [enrollmentToken, setEnrollmentToken] = useState('');
  const [reasonCode, setReasonCode] = useState('delivery_lost');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [replayed, setReplayed] = useState(false);

  useEffect(() => {
    let cancelled = false;
    fetch('/api/v1/antiforgery/token', { credentials: 'same-origin', cache: 'no-store' })
      .then(async response => {
        if (!response.ok) throw new Error('دریافت توکن امنیتی ناموفق بود؛ دوباره تلاش کنید.');
        const body = await response.json() as AntiforgeryResponse;
        if (!cancelled) {
          setRequestToken(body.requestToken ?? '');
          setHeaderName(body.headerName ?? 'X-XSRF-TOKEN');
        }
      })
      .catch(reason => {
        if (!cancelled) setError(reason instanceof Error ? reason.message : 'خطای ارتباط با سرور.');
      });
    return () => { cancelled = true; };
  }, []);

  async function readFile(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (!file) return;
    setError('');
    try {
      if (file.size > 1024 * 1024) throw new Error('حجم فایل بسته نباید بیشتر از یک مگابایت باشد.');
      const text = await file.text();
      getRequestId(text);
      setPackageText(text);
      setFileName(file.name);
      setEnrollmentToken('');
      setAcknowledgement('');
    } catch (reason) {
      setPackageText('');
      setFileName('');
      setError(reason instanceof Error ? reason.message : 'خواندن فایل ناموفق بود.');
    }
  }

  async function applyPackage(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy || !requestToken) return;
    setBusy(true);
    setError('');
    setEnrollmentToken('');
    try {
      const id = getRequestId(packageText);
      const parsed = JSON.parse(packageText) as ProvisioningPackage;
      const response = await fetch('/api/v1/provisioning/apply', {
        method: 'POST',
        credentials: 'same-origin',
        cache: 'no-store',
        headers: { 'Content-Type': 'application/json', [headerName]: requestToken },
        body: JSON.stringify(parsed),
      });
      const body = await response.json().catch(() => ({})) as ApplyResponse & ApiProblem;
      if (!response.ok) {
        if (body.acknowledgement) setAcknowledgement(JSON.stringify(body.acknowledgement, null, 2));
        throw new Error(body.detail ?? body.error ?? body.title ?? 'اعمال بسته ناموفق بود.');
      }
      setRequestId(id);
      setReplayed(Boolean(body.replayed));
      setAcknowledgement(body.acknowledgement ? JSON.stringify(body.acknowledgement, null, 2) : '');
      setEnrollmentToken(body.enrollmentToken ?? '');
      if (body.replayed) {
        setError('این درخواست قبلاً اعمال شده است؛ توکن راه‌اندازی در پاسخ تکراری بازگردانده نمی‌شود.');
      }
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'اعمال بسته ناموفق بود.');
    } finally {
      setBusy(false);
    }
  }

  async function reissueToken() {
    if (busy || !requestId || !requestToken) return;
    setBusy(true);
    setError('');
    setEnrollmentToken('');
    try {
      const response = await fetch(`/api/v1/provisioning/${encodeURIComponent(requestId)}/enrollment/reissue`, {
        method: 'POST',
        credentials: 'same-origin',
        cache: 'no-store',
        headers: { 'Content-Type': 'application/json', [headerName]: requestToken },
        body: JSON.stringify({ reasonCode }),
      });
      const body = await response.json().catch(() => ({})) as { enrollmentToken?: string; error?: string; detail?: string };
      if (!response.ok || !body.enrollmentToken) throw new Error(body.detail ?? body.error ?? 'صدور مجدد توکن ناموفق بود.');
      setEnrollmentToken(body.enrollmentToken);
      setReplayed(false);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'صدور مجدد توکن ناموفق بود.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <main dir="rtl" className="min-h-screen bg-background px-4 py-8 text-foreground">
      <div className="mx-auto flex w-full max-w-3xl flex-col gap-6">
        <header className="space-y-2">
          <div className="flex items-center gap-2 text-primary"><ShieldCheck className="size-5" /><span className="text-sm font-semibold">راه‌اندازی امن مشتری</span></div>
          <h1 className="text-2xl font-bold tracking-tight">دریافت و اعمال بستهٔ تأمین مشتری</h1>
          <p className="text-sm leading-7 text-muted-foreground">این صفحه برای اپراتور مورد اعتماد نصب است. بستهٔ امضاشده را وارد کنید؛ ایجاد شرکت و مدیر اولیه فقط پس از اعتبارسنجی امضا و ثبت تراکنش انجام می‌شود.</p>
        </header>

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2"><FileUp className="size-5" /> وارد کردن بستهٔ امضاشده</CardTitle>
            <CardDescription>فایل JSON صادرشده از پنل Vendor را انتخاب کنید. فایل فقط در حافظهٔ همین صفحه خوانده می‌شود.</CardDescription>
          </CardHeader>
          <CardContent>
            <form onSubmit={applyPackage} className="space-y-4">
              <label htmlFor="provisioning-package" className="flex min-h-32 cursor-pointer flex-col items-center justify-center gap-2 rounded-lg border border-dashed border-border bg-muted/30 p-6 text-center hover:bg-muted/60">
                <FileUp className="size-7 text-muted-foreground" />
                <span className="font-medium">{fileName || 'انتخاب فایل بستهٔ JSON'}</span>
                <span className="text-xs text-muted-foreground">حداکثر حجم: ۱ مگابایت</span>
                <input id="provisioning-package" type="file" accept=".json,application/json" className="sr-only" onChange={readFile} />
              </label>
              <Button type="submit" className="w-full" disabled={!packageText || !requestToken || busy}>
                {busy ? 'در حال بررسی و اعمال…' : 'اعتبارسنجی و اعمال بسته'}
              </Button>
            </form>
          </CardContent>
        </Card>

        {error && <div role="alert" className="flex items-start gap-2 rounded-lg border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive"><AlertCircle className="mt-0.5 size-4 shrink-0" /><span>{error}</span></div>}

        {acknowledgement && (
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2"><CheckCircle2 className="size-5" /> پاسخ امضاشدهٔ مشتری</CardTitle>
              <CardDescription>شناسه درخواست: <span dir="ltr" className="font-mono">{requestId || 'در پاسخ'}</span>{replayed ? ' — پاسخ تکراری' : ''}</CardDescription>
            </CardHeader>
            <CardContent><pre dir="ltr" className="max-h-64 overflow-auto rounded-md bg-muted p-3 text-xs leading-6">{acknowledgement}</pre></CardContent>
          </Card>
        )}

        {enrollmentToken && (
          <Card className="border-primary/40">
            <CardHeader>
              <CardTitle className="flex items-center gap-2"><KeyRound className="size-5" /> توکن یک‌بارمصرف مدیر اولیه</CardTitle>
              <CardDescription>این توکن را فقط به مدیر مورد اعتماد مشتری تحویل دهید. پس از بستن یا تازه‌سازی صفحه، امکان مشاهدهٔ دوباره وجود ندارد.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              <code dir="ltr" className="block break-all rounded-md bg-muted p-3 text-center text-sm">{enrollmentToken}</code>
              <Button type="button" variant="outline" className="w-full" onClick={() => navigator.clipboard.writeText(enrollmentToken)}>کپی توکن</Button>
              <p className="text-xs leading-6 text-muted-foreground">توکن فقط در پاسخ موفق پس از commit نمایش داده شده و در پایگاه داده فقط hash آن نگهداری می‌شود.</p>
            </CardContent>
          </Card>
        )}

        {requestId && !enrollmentToken && (
          <Card>
            <CardHeader>
              <CardTitle>صدور مجدد توکن راه‌اندازی</CardTitle>
              <CardDescription>فقط در صورت تأیید اپراتور، توکن قبلی باطل و توکن جدید صادر می‌شود.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              <label htmlFor="reissue-reason" className="text-sm font-medium">علت صدور مجدد</label>
              <select id="reissue-reason" value={reasonCode} onChange={event => setReasonCode(event.target.value)} className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm">
                <option value="delivery_lost">توکن به گیرنده نرسیده است</option>
                <option value="operator_authorized">درخواست تأییدشدهٔ اپراتور</option>
              </select>
              <Button type="button" variant="outline" className="w-full" disabled={busy || !requestToken} onClick={reissueToken}>صدور مجدد و ابطال توکن قبلی</Button>
            </CardContent>
          </Card>
        )}
      </div>
    </main>
  );
}
