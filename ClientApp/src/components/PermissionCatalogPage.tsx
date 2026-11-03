import { useEffect, useMemo, useState } from 'react';
import { KeyRound, Search, ShieldCheck } from 'lucide-react';
import { ApiError, getPermissionCatalog } from '../api/client';
import type { PermissionCatalogItem } from '../api/types';

type Props = { onUnauthenticated: (error: unknown) => boolean };

function subsystemLabel(code: string): string {
  if (code === 'trade') return 'بازرگانی خارجی';
  return code;
}

export function PermissionCatalogPage({ onUnauthenticated }: Props) {
  const [permissions, setPermissions] = useState<PermissionCatalogItem[]>([]);
  const [search, setSearch] = useState('');
  const [subsystem, setSubsystem] = useState('all');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    getPermissionCatalog(controller.signal)
      .then(value => { if (!controller.signal.aborted) setPermissions(value); })
      .catch((cause: unknown) => {
        if (controller.signal.aborted) return;
        if (!onUnauthenticated(cause)) setError(cause instanceof ApiError ? cause.message : 'دریافت کاتالوگ مجوزها انجام نشد.');
      })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [onUnauthenticated]);

  const subsystems = useMemo(() => [...new Set(permissions.map(item => item.subsystemCode))].sort(), [permissions]);
  const filtered = useMemo(() => permissions.filter(item => {
    const matchesSubsystem = subsystem === 'all' || item.subsystemCode === subsystem;
    const needle = search.trim().toLocaleLowerCase();
    const matchesSearch = !needle || item.name.toLocaleLowerCase().includes(needle) ||
      item.code.toLocaleLowerCase().includes(needle) || (item.description ?? '').toLocaleLowerCase().includes(needle);
    return matchesSubsystem && matchesSearch;
  }), [permissions, search, subsystem]);
  const grouped = useMemo(() => filtered.reduce<Record<string, PermissionCatalogItem[]>>((result, item) => {
    (result[item.subsystemCode] ??= []).push(item);
    return result;
  }, {}), [filtered]);

  return (
    <div dir="rtl" className="space-y-5">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div><p className="text-xs font-semibold text-sky-700">مدیریت سیستم / کاتالوگ مجوزها</p><h1 className="mt-1 text-2xl font-extrabold tracking-tight text-slate-900">کاتالوگ و جزئیات مجوزها</h1><p className="mt-2 text-sm text-slate-500">فهرست مجوزهای ثبت‌شده و قابل اعمال در APIهای فرانو</p></div>
        <div className="flex items-center gap-2 rounded-xl border border-sky-100 bg-sky-50 px-3 py-2 text-xs font-semibold text-sky-800"><ShieldCheck size={16} /> کاتالوگ تحت کنترل نسخه</div>
      </header>

      <section className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
        <div className="flex flex-wrap gap-3">
          <label className="relative min-w-[220px] flex-1"><span className="sr-only">جستجوی مجوز</span><Search size={16} className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400" /><input value={search} onChange={event => setSearch(event.target.value)} placeholder="جستجو بر اساس عنوان، کد یا توضیحات…" className="h-11 w-full rounded-xl border border-slate-200 bg-slate-50 pe-9 ps-3 text-sm outline-none focus:border-sky-400 focus:bg-white" /></label>
          <select value={subsystem} onChange={event => setSubsystem(event.target.value)} aria-label="فیلتر زیرسیستم" className="h-11 min-w-48 rounded-xl border border-slate-200 bg-white px-3 text-sm"><option value="all">همه زیرسیستم‌ها</option>{subsystems.map(code => <option key={code} value={code}>{subsystemLabel(code)}</option>)}</select>
        </div>
        <div className="mt-3 flex flex-wrap items-center justify-between gap-2 text-xs text-slate-500"><span>{filtered.length} مجوز از {permissions.length} مجوز ثبت‌شده</span><span>مجوز جدید فقط از طریق تغییر کد و بازبینی فنی اضافه می‌شود.</span></div>
      </section>

      {loading ? <p className="rounded-xl border border-slate-200 bg-white p-8 text-center text-sm text-slate-500">در حال بارگذاری کاتالوگ…</p> : null}
      {error ? <p role="alert" className="rounded-xl bg-rose-50 p-4 text-sm text-rose-700">{error}</p> : null}
      {!loading && !error && permissions.length === 0 ? <div className="rounded-2xl border border-dashed border-slate-300 bg-white p-10 text-center"><KeyRound size={30} className="mx-auto text-slate-300" /><p className="mt-3 font-semibold text-slate-700">هنوز مجوزی در کاتالوگ ثبت نشده است</p><p className="mt-1 text-sm text-slate-500">ابتدا باید مجوزهای واقعی در ماژول مربوط تعریف و در API enforce شوند.</p></div> : null}
      {!loading && !error && filtered.length === 0 && permissions.length > 0 ? <p className="rounded-xl bg-white p-8 text-center text-sm text-slate-500">مجوزی با این فیلتر پیدا نشد.</p> : null}

      <div className="space-y-4">
        {Object.entries(grouped).map(([code, items]) => (
          <section key={code} className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
            <header className="flex items-center justify-between border-b border-slate-100 bg-gradient-to-l from-slate-50 to-white px-5 py-4"><div className="flex items-center gap-2"><span className="flex h-9 w-9 items-center justify-center rounded-xl bg-sky-50 text-sky-700"><KeyRound size={17} /></span><div><h2 className="font-bold text-slate-900">{subsystemLabel(code)}</h2><p className="mt-0.5 text-[11px] text-slate-500" dir="ltr">{code}</p></div></div><span className="rounded-full bg-slate-100 px-2.5 py-1 text-xs font-semibold text-slate-600">{items.length} مجوز</span></header>
            <div className="grid gap-2 p-4 md:grid-cols-2">
              {items.map(item => <article key={item.id} className="min-w-0 rounded-xl border border-slate-100 p-3"><div className="flex items-start justify-between gap-2"><h3 className="text-sm font-bold text-slate-800">{item.name}</h3><span className="shrink-0 rounded-md bg-slate-100 px-2 py-0.5 text-[10px] text-slate-500">{item.code.split('.').at(-1)}</span></div><p className="mt-1 break-all text-[11px] text-slate-500" dir="ltr">{item.code}</p>{item.description ? <p className="mt-2 text-xs leading-5 text-slate-500">{item.description}</p> : null}</article>)}
            </div>
          </section>
        ))}
      </div>
    </div>
  );
}
