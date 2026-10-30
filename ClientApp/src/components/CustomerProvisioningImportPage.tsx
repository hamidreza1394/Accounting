import { useState, type ChangeEvent, type FormEvent } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';

type PackageEnvelope = { formatVersion: number; keyId: string; algorithm: string; payloadJson: string; signatureBase64: string };
type ApplyResponse = { acknowledgement?: { acknowledgementJson?: string }; enrollmentToken?: string | null; replayed?: boolean };
type Antiforgery = { headerName?: string; requestToken?: string };

async function getAntiforgery(): Promise<Antiforgery> {
  const response = await fetch('/api/v1/antiforgery/token', { credentials: 'same-origin', cache: 'no-store' });
  if (!response.ok) throw new Error('دریافت توکن امنیتی ناموفق بود.');
  return response.json() as Promise<Antiforgery>;
}

export function CustomerProvisioningImportPage() {
  const [fileText, setFileText] = useState('');
  const [requestId, setRequestId] = useState('');
  const [reasonCode, setReasonCode] = useState('delivery_lost');
  const [enrollmentToken, setEnrollmentToken] = useState('');
  const [acknowledgement, setAcknowledgement] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');

  async function onFileChanged(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    setEnrollmentToken('');
    setAcknowledgement('');
    setNotice('');
    setError('');
    if (!file) {
      setFileText('');
      setRequestId('');
      return;
    }
    try {
      const text = await file.text();
      const parsed = JSON.parse(text) as PackageEnvelope;
      const payload = JSON.parse(parsed.payloadJson) as { requestId?: string };
      if (!parsed.signatureBase64 || !parsed.payloadJson || !payload.requestId) {
        throw new Error('ساختار بستهٔ امضاشده معتبر نیست.');
      }
      setFileText(text);
      setRequestId(payload.requestId);
    } catch (reason) {
      setFileText('');
      setRequestId('');
      setError(reason instanceof Error ? reason.message : 'خواندن بسته ناموفق بود.');
    }
  }

  async function apply(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy || !fileText) return;
    setBusy(true);
    setError('');
    setNotice('');
    setEnrollmentToken('');
    setAcknowledgement('');
    try {
      const csrf = await getAntiforgery();
      const response = await fetch('/api/v1/provisioning/apply', {
        method: 'POST',
        credentials: 'same-origin',
        cache: 'no-store',
        headers: { 'Content-Type': 'application/json', [csrf.headerName ?? 'X-XSRF-TOKEN']: csrf.requestToken ?? '' },
        body: fileText,
      });
      const body = await response.json() as ApplyResponse & { detail?: string; error?: string };
      if (!response.ok) {
        if (body.acknowledgement?.acknowledgementJson) setAcknowledgement(body.acknowledgement.acknowledgementJson);
        throw new Error(body.error ?? body.detail ?? 'اعمال بسته ناموفق بود.');
      }
      setAcknowledgement(body.acknowledgement?.acknowledgementJson ?? '');
      if (body.enrollmentToken) {
        setEnrollmentToken(body.enrollmentToken);
        setNotice('بسته اعمال شد. این توکن فقط همین یک بار نمایش داده می‌شود؛ آن را فوراً به مدیر اولیه تحویل دهید.');
      } else {
        setNotice('این بسته قبلاً اعمال شده است. فقط acknowledgement بازگردانده شد و توکن دوباره نمایش داده نمی‌شود.');
      }
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'اعمال بسته ناموفق بود.');
    } finally {
      setBusy(false);
    }
  }

  async function reissue(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy || !requestId) return;
    setBusy(true);
    setError('');
    setNotice('');
    setEnrollmentToken('');
    try {
      const csrf = await getAntiforgery();
      const response = await fetch(`/api/v1/provisioning/${encodeURIComponent(requestId)}/enrollment/reissue`, {
        method: 'POST',
        credentials: 'same-origin',
        cache: 'no-store',
        headers: { 'Content-Type': 'application/json', [csrf.headerName ?? 'X-XSRF-TOKEN']: csrf.requestToken ?? '' },
        body: JSON.stringify({ reasonCode }),
      });
      const body = await response.json() as { enrollmentToken?: string; error?: string };
      if (!response.ok || !body.enrollmentToken) throw new Error(body.error ?? 'صدور مجدد توکن مجاز نیست.');
      setEnrollmentToken(body.enrollmentToken);
      setNotice('توکن قبلی باطل و توکن جدید صادر شد. این مقدار را فقط یک بار مشاهده می‌کنید.');
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'صدور مجدد توکن ناموفق بود.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <main dir="rtl" className="min-h-screen bg-background px-4 py-10 text-foreground">
      <div className="mx-auto grid max-w-3xl gap-5">
        <Card>
          <CardHeader>
            <CardTitle className="text-2xl">اعمال بستهٔ راه‌اندازی مشتری</CardTitle>
            <CardDescription>این صفحه فقط برای اپراتور زیرساختِ احراز هویت‌شده است. بسته ابتدا در نصب مشتری و با کلید عمومی مورد اعتماد بررسی می‌شود.</CardDescription>
          </CardHeader>
          <CardContent>
            <form onSubmit={apply} className="space-y-4">
              <div className="space-y-2">
                <label htmlFor="provisioning-package" className="text-sm font-medium">فایل JSON بستهٔ امضاشده</label>
                <input id="provisioning-package" type="file" accept="application/json,.json" onChange={onFileChanged}
                  className="block w-full rounded-md border border-input p-3 text-sm" />
              </div>
              {requestId && <p className="text-xs text-muted-foreground" dir="ltr">RequestId: {requestId}</p>}
              <Button type="submit" disabled={busy || !fileText}>{busy ? 'در حال بررسی و اعمال…' : 'بررسی و اعمال اتمیک بسته'}</Button>
            </form>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>بازصدور توکن Enrollment</CardTitle>
            <CardDescription>فقط وقتی پاسخ اولیه از دست رفته یا توکن منقضی/در معرض افشا بوده است. توکن قبلی در تراکنش باطل می‌شود.</CardDescription>
          </CardHeader>
          <CardContent>
            <form onSubmit={reissue} className="space-y-3">
              <div className="space-y-2">
                <label htmlFor="reissue-request-id" className="text-sm font-medium">RequestId بسته</label>
                <input id="reissue-request-id" required dir="ltr" value={requestId} onChange={event => setRequestId(event.target.value)}
                  className="h-10 w-full rounded-md border border-input bg-background px-3 text-left text-sm" />
              </div>
              <div className="space-y-2">
                <label htmlFor="reissue-reason" className="text-sm font-medium">علت بازصدور</label>
                <select id="reissue-reason" value={reasonCode} onChange={event => setReasonCode(event.target.value)}
                  className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm">
                  <option value="delivery_lost">پاسخ اولیه به اپراتور نرسید</option>
                  <option value="expired">توکن منقضی شد</option>
                  <option value="suspected_exposure">احتمال افشای توکن</option>
                </select>
              </div>
              <Button type="submit" variant="outline" disabled={busy || !requestId}>ابطال توکن قبلی و صدور مجدد</Button>
            </form>
          </CardContent>
        </Card>

        {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
        {notice && <p role="status" className="rounded-md border border-border p-3 text-sm leading-7">{notice}</p>}
        {enrollmentToken && <Card>
          <CardHeader><CardTitle>توکن یک‌بارمصرف مدیر اولیه</CardTitle></CardHeader>
          <CardContent className="space-y-2">
            <code dir="ltr" className="block break-all rounded-md bg-muted p-4 text-left text-base">{enrollmentToken}</code>
            <p className="text-xs leading-6 text-muted-foreground">این مقدار در دیتابیس، audit، log، acknowledgement یا URL ذخیره نمی‌شود. پس از بستن یا تازه‌سازی صفحه قابل بازیابی نیست؛ از مسیر بازصدور استفاده کنید.</p>
          </CardContent>
        </Card>}
        {acknowledgement && <Card>
          <CardHeader><CardTitle>Signed acknowledgement</CardTitle></CardHeader>
          <CardContent><pre dir="ltr" className="overflow-auto whitespace-pre-wrap break-all text-xs">{acknowledgement}</pre></CardContent>
        </Card>}
      </div>
    </main>
  );
}
