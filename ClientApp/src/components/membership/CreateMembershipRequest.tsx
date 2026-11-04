import { useEffect, useState } from 'react';
import { ApiError, createMembershipRequest, getMembershipDestinations } from '../../api/client';
import type { MembershipDestination, MembershipRequestPage } from '../../api/types';
import { Button } from '../ui/button';
import { FormField, Input } from '../ui/FormField';

type Props = { onUnauthenticated: (error: unknown) => boolean; onCreated: () => void };

/** Exact-name search only: the server limits discovery to the caller's customer boundary. */
export function CreateMembershipRequest({ onUnauthenticated, onCreated }: Props) {
  const [search, setSearch] = useState('');
  const [query, setQuery] = useState('');
  const [page, setPage] = useState(1);
  const [attempt, setAttempt] = useState(0);
  const [results, setResults] = useState<MembershipRequestPage<MembershipDestination> | null>(null);
  const [loading, setLoading] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');

  useEffect(() => {
    if (!query) return undefined;
    const controller = new AbortController();
    setLoading(true);
    setResults(null);
    getMembershipDestinations(query, page, controller.signal).then((result) => {
      if (!controller.signal.aborted) { setResults(result); setLoading(false); }
    }).catch((cause: unknown) => {
      if (controller.signal.aborted || onUnauthenticated(cause)) return;
      setError(cause instanceof ApiError ? cause.message : 'جستجوی شرکت انجام نشد.');
      setLoading(false);
    });
    return () => controller.abort();
  }, [query, page, attempt, onUnauthenticated]);

  async function submit(companyId: string) {
    setBusy(true); setError(''); setNotice('');
    try {
      await createMembershipRequest(companyId);
      setResults(null); setQuery(''); setNotice('درخواست عضویت ثبت شد و در انتظار تأیید است.');
      onCreated();
    } catch (cause) {
      if (!onUnauthenticated(cause)) setError(cause instanceof ApiError ? cause.message : 'ثبت درخواست انجام نشد.');
    } finally { setBusy(false); }
  }

  return (
    <section className="space-y-3 rounded-xl border border-line bg-white p-5" aria-labelledby="request-membership-title">
      <h2 id="request-membership-title" className="text-lg font-bold">ثبت درخواست عضویت</h2>
      <p className="text-sm text-sub">نام دقیق شرکت مقصد را وارد کنید؛ فقط شرکت‌های مجاز در محدوده مشتری نمایش داده می‌شوند.</p>
      <form className="space-y-3" onSubmit={(event) => {
        event.preventDefault(); setError(''); setNotice(''); setResults(null);
        const value = search.trim();
        if (value.length < 3 || value.length > 120) { setQuery(''); setError('نام دقیق شرکت باید بین ۳ و ۱۲۰ نویسه باشد.'); return; }
        setPage(1); setQuery(value); setAttempt((current) => current + 1);
      }}>
        <FormField label="نام دقیق شرکت مقصد"><Input value={search} maxLength={120} onChange={(event) => { setSearch(event.target.value); setQuery(''); setResults(null); setError(''); }} /></FormField>
        <Button type="submit" disabled={loading || busy}>جستجوی شرکت</Button>
      </form>
      {loading ? <p role="status">در حال جستجو…</p> : null}
      {error ? <p role="alert" className="text-danger">{error}</p> : null}
      {notice ? <p role="status">{notice}</p> : null}
      {results ? results.items.length === 0 ? <p>شرکت قابل درخواستی پیدا نشد.</p> : (
        <ul className="space-y-2">{results.items.map((company) => (
          <li key={company.companyId} className="flex flex-wrap items-center gap-3"><span>{company.name}</span><Button type="button" disabled={busy} onClick={() => void submit(company.companyId)}>درخواست عضویت در {company.name}</Button></li>
        ))}</ul>
      ) : null}
      {results && results.totalCount > results.pageSize ? <div className="flex gap-2">
        <Button type="button" variant="outline" disabled={page <= 1} onClick={() => setPage(page - 1)}>صفحه قبلی شرکت‌ها</Button>
        <Button type="button" variant="outline" disabled={page * results.pageSize >= results.totalCount} onClick={() => setPage(page + 1)}>صفحه بعدی شرکت‌ها</Button>
      </div> : null}
    </section>
  );
}
