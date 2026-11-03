import { useEffect, useState, type FormEvent } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';

type AntiforgeryResponse = { headerName?: string; requestToken?: string };
type ApiError = { detail?: string; title?: string };

export function ProvisioningEnrollmentPage() {
  const [username, setUsername] = useState('');
  const [token, setToken] = useState('');
  const [password, setPassword] = useState('');
  const [confirmation, setConfirmation] = useState('');
  const [requestToken, setRequestToken] = useState('');
  const [headerName, setHeaderName] = useState('X-XSRF-TOKEN');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [complete, setComplete] = useState(false);

  useEffect(() => {
    let cancelled = false;
    fetch('/api/v1/provisioning/enrollment/antiforgery', {
      credentials: 'same-origin',
      cache: 'no-store',
    }).then(async response => {
      if (!response.ok) throw new Error('دریافت توکن امنیتی ناموفق بود؛ صفحه را تازه‌سازی کنید.');
      const body = await response.json() as AntiforgeryResponse;
      if (!cancelled) {
        setRequestToken(body.requestToken ?? '');
        setHeaderName(body.headerName ?? 'X-XSRF-TOKEN');
      }
    }).catch(reason => {
      if (!cancelled) setError(reason instanceof Error ? reason.message : 'خطای ارتباط با سرور.');
    });
    return () => { cancelled = true; };
  }, []);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy) return;
    if (password !== confirmation) {
      setError('تکرار گذرواژه با گذرواژه یکسان نیست.');
      return;
    }
    setBusy(true);
    setError('');
    try {
      const response = await fetch('/api/v1/provisioning/enrollment/complete', {
        method: 'POST',
        credentials: 'same-origin',
        cache: 'no-store',
        headers: { 'Content-Type': 'application/json', [headerName]: requestToken },
        body: JSON.stringify({ username: username.trim(), enrollmentToken: token.trim(), newPassword: password }),
      });
      if (!response.ok) {
        let detail = '';
        try {
          const body = await response.json() as ApiError;
          detail = body.detail ?? body.title ?? '';
        } catch { /* Keep the response generic. */ }
        if (response.status === 410) throw new Error('توکن راه‌اندازی منقضی شده است؛ با اپراتور نصب برای صدور مجدد تماس بگیرید.');
        if (response.status === 429) throw new Error('تعداد تلاش‌ها بیش از حد مجاز است؛ بعداً دوباره تلاش کنید.');
        throw new Error(detail || 'فعال‌سازی حساب انجام نشد؛ نام کاربری و توکن را بررسی کنید.');
      }
      setToken('');
      setPassword('');
      setConfirmation('');
      setComplete(true);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'فعال‌سازی حساب ناموفق بود.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <main dir="rtl" className="min-h-screen bg-background px-4 py-10 text-foreground">
      <div className="mx-auto flex min-h-[70vh] max-w-lg items-center">
        <Card className="w-full border-border/70 shadow-lg">
          <CardHeader className="space-y-2">
            <CardTitle className="text-2xl">فعال‌سازی مدیر اولیه فرانو</CardTitle>
            <CardDescription>توکن یک‌بارمصرف را از اپراتور مورد اعتماد نصب دریافت کنید و گذرواژهٔ اختصاصی خود را تعیین کنید.</CardDescription>
          </CardHeader>
          <CardContent>
            {complete ? (
              <div className="space-y-4" role="status">
                <p className="text-sm leading-7">حساب مدیر فعال شد. توکن مصرف شده و دیگر قابل استفاده نیست. اکنون می‌توانید وارد فرانو شوید.</p>
                <a className="inline-flex h-10 items-center rounded-md bg-primary px-4 text-sm font-medium text-primary-foreground" href="/Account/Login">رفتن به صفحه ورود</a>
              </div>
            ) : (
              <form onSubmit={submit} className="space-y-4">
                <div className="space-y-2">
                  <label htmlFor="enrollment-username" className="text-sm font-medium">نام کاربری مدیر</label>
                  <input id="enrollment-username" required maxLength={64} autoComplete="username" value={username}
                    onChange={event => setUsername(event.target.value)} className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" />
                </div>
                <div className="space-y-2">
                  <label htmlFor="enrollment-token" className="text-sm font-medium">توکن یک‌بارمصرف</label>
                  <input id="enrollment-token" required maxLength={64} autoComplete="off" spellCheck={false} dir="ltr" value={token}
                    onChange={event => setToken(event.target.value)} className="h-10 w-full rounded-md border border-input bg-background px-3 text-left text-sm" />
                </div>
                <div className="space-y-2">
                  <label htmlFor="enrollment-password" className="text-sm font-medium">گذرواژه جدید (حداقل ۱۲ نویسه)</label>
                  <input id="enrollment-password" type="password" required minLength={12} maxLength={128} autoComplete="new-password" value={password}
                    onChange={event => setPassword(event.target.value)} className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" />
                </div>
                <div className="space-y-2">
                  <label htmlFor="enrollment-confirmation" className="text-sm font-medium">تکرار گذرواژه</label>
                  <input id="enrollment-confirmation" type="password" required minLength={12} maxLength={128} autoComplete="new-password" value={confirmation}
                    onChange={event => setConfirmation(event.target.value)} className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" />
                </div>
                {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
                <Button type="submit" className="w-full" disabled={busy || !requestToken}>
                  {busy ? 'در حال فعال‌سازی…' : 'مصرف توکن و فعال‌سازی حساب'}
                </Button>
                <p className="text-xs leading-6 text-muted-foreground">توکن در مرورگر ذخیره نمی‌شود. پس از موفقیت، توکن فوراً مصرف خواهد شد.</p>
              </form>
            )}
          </CardContent>
        </Card>
      </div>
    </main>
  );
}
