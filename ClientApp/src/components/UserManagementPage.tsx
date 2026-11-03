import { useEffect, useState, type FormEvent } from 'react';
import { Building2, ChevronLeft, ChevronRight, Search, ShieldCheck, UserRound, Users } from 'lucide-react';
import { ApiError, getAdminUser, getAdminUsers } from '../api/client';
import type { AdminUserDetails, AdminUserListItem, AdminUserListResponse } from '../api/types';

type Props = { onUnauthenticated: (error: unknown) => boolean };

const pageSize = 20;

function formatDate(value: string | null): string {
  if (!value) return 'ثبت نشده';
  const date = new Date(value);
  if (!Number.isFinite(date.getTime())) return 'ثبت نشده';
  return new Intl.DateTimeFormat('fa-IR', { dateStyle: 'medium', timeStyle: 'short' }).format(date);
}

function fullName(user: Pick<AdminUserListItem, 'firstName' | 'lastName'>): string {
  return [user.firstName, user.lastName].filter(Boolean).join(' ') || 'بدون نام ثبت‌شده';
}

function Status({ active }: { active: boolean }) {
  return <span className={'inline-flex items-center rounded-full px-2.5 py-1 text-xs font-semibold ' + (active ? 'bg-emerald-50 text-emerald-700' : 'bg-slate-100 text-slate-600')}>
    <span className={'me-1.5 h-1.5 w-1.5 rounded-full ' + (active ? 'bg-emerald-500' : 'bg-slate-400')} />
    {active ? 'فعال' : 'غیرفعال'}
  </span>;
}

function UserDetails({ user }: { user: AdminUserDetails }) {
  return (
    <section className="min-w-0 rounded-2xl border border-slate-200 bg-white shadow-sm">
      <div className="border-b border-slate-100 bg-gradient-to-l from-slate-50 to-white p-5 sm:p-6">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="flex min-w-0 items-center gap-4">
            <div className="flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-[#102d52] to-[#1e5688] text-xl font-bold text-white shadow-md">
              {fullName(user).slice(0, 1)}
            </div>
            <div className="min-w-0">
              <h2 className="truncate text-lg font-bold text-slate-900">{fullName(user)}</h2>
              <p className="mt-1 text-sm text-slate-500" dir="ltr">@{user.username ?? '—'}</p>
            </div>
          </div>
          <Status active={user.isActive} />
        </div>
      </div>

      <div className="grid gap-4 p-5 sm:grid-cols-2 sm:p-6">
        <Info label="ایمیل" value={user.emailAddress} />
        <Info label="شماره همراه" value={user.cellPhoneNumber ?? 'ثبت نشده'} />
        <Info label="نقش قدیمی حساب" value={user.legacyRoleName ?? 'تعریف نشده'} />
        <Info label="آخرین ورود موفق" value={formatDate(user.lastLoginDateTime)} />
        <Info label="تاریخ ایجاد حساب" value={formatDate(user.insertDateTime)} />
        <Info label="آخرین ویرایش" value={formatDate(user.updateDateTime)} />
        <Info label="تأیید ایمیل" value={user.isEmailAddressVerified ? 'تأیید شده' : 'تأیید نشده'} />
        <Info label="تأیید موبایل" value={user.isCellPhoneNumberVerified ? 'تأیید شده' : 'تأیید نشده'} />
      </div>

      <div className="border-t border-slate-100 p-5 sm:p-6">
        <div className="mb-4 flex items-center gap-2">
          <Building2 size={18} className="text-sky-700" />
          <h3 className="font-bold text-slate-900">شرکت‌های دارای عضویت</h3>
          <span className="rounded-full bg-sky-50 px-2 py-0.5 text-xs font-semibold text-sky-700">{user.memberships.length}</span>
        </div>
        {user.memberships.length === 0 ? (
          <p className="rounded-xl bg-slate-50 px-4 py-5 text-sm text-slate-500">این حساب هنوز عضویت شرکتی ثبت‌شده‌ای ندارد.</p>
        ) : (
          <div className="space-y-2">
            {user.memberships.map((membership) => (
              <div key={membership.companyId} className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-slate-100 p-3">
                <div className="min-w-0">
                  <p className="font-semibold text-slate-800">{membership.companyName}</p>
                  <p className="mt-1 text-xs text-slate-500">{membership.roleName} · {membership.roleCode}</p>
                </div>
                <Status active={membership.isActive} />
              </div>
            ))}
          </div>
        )}
      </div>
    </section>
  );
}

function Info({ label, value }: { label: string; value: string }) {
  return <div className="min-w-0 rounded-xl bg-slate-50 px-3.5 py-3">
    <p className="text-xs font-medium text-slate-500">{label}</p>
    <p className="mt-1 break-words text-sm font-semibold text-slate-800">{value}</p>
  </div>;
}

export function UserManagementPage({ onUnauthenticated }: Props) {
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<'all' | 'active' | 'inactive'>('all');
  const [page, setPage] = useState(1);
  const [list, setList] = useState<AdminUserListResponse | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [details, setDetails] = useState<AdminUserDetails | null>(null);
  const [loading, setLoading] = useState(true);
  const [detailsLoading, setDetailsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [detailsError, setDetailsError] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError(null);
    getAdminUsers({ search, status, page, pageSize, sort: 'name', descending: false }, controller.signal)
      .then((result) => {
        if (controller.signal.aborted) return;
        setList(result);
        setSelectedId((current) => current && result.items.some((item) => item.id === current)
          ? current
          : result.items[0]?.id ?? null);
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted) return;
        if (!onUnauthenticated(cause)) setError(cause instanceof ApiError ? cause.message : 'دریافت فهرست کاربران انجام نشد.');
      })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [search, status, page, onUnauthenticated]);

  useEffect(() => {
    if (!selectedId) {
      setDetails(null);
      return;
    }
    const controller = new AbortController();
    setDetailsLoading(true);
    setDetailsError(null);
    getAdminUser(selectedId, controller.signal)
      .then((result) => { if (!controller.signal.aborted) setDetails(result); })
      .catch((cause: unknown) => {
        if (controller.signal.aborted) return;
        if (!onUnauthenticated(cause)) setDetailsError(cause instanceof ApiError ? cause.message : 'دریافت جزئیات کاربر انجام نشد.');
      })
      .finally(() => { if (!controller.signal.aborted) setDetailsLoading(false); });
    return () => controller.abort();
  }, [selectedId, onUnauthenticated]);

  function submitSearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setPage(1);
    setSearch(searchInput.trim());
  }

  const totalPages = list ? Math.max(1, Math.ceil(list.totalCount / list.pageSize)) : 1;

  return (
    <div className="space-y-5" dir="rtl">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <p className="text-xs font-semibold text-sky-700">مدیریت سیستم / کاربران</p>
          <h1 className="mt-1 text-2xl font-extrabold tracking-tight text-slate-900">مدیریت کاربران</h1>
          <p className="mt-2 text-sm text-slate-500">فهرست حساب‌های این نصب و جزئیات عضویت هر کاربر</p>
        </div>
        <div className="flex items-center gap-2 rounded-xl border border-sky-100 bg-sky-50 px-3 py-2 text-xs font-semibold text-sky-800">
          <ShieldCheck size={16} /> دسترسی مدیر سراسری نصب
        </div>
      </header>

      <div className="grid min-w-0 gap-4 xl:grid-cols-[minmax(300px,0.85fr)_minmax(0,1.5fr)]">
        <section className="min-w-0 overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
          <div className="border-b border-slate-100 p-4">
            <div className="mb-3 flex items-center justify-between gap-2">
              <div className="flex items-center gap-2">
                <Users size={18} className="text-sky-700" />
                <h2 className="font-bold text-slate-900">فهرست کاربران</h2>
              </div>
              <span className="rounded-full bg-slate-100 px-2.5 py-1 text-xs font-semibold tabular-nums text-slate-600">{list?.totalCount ?? '—'}</span>
            </div>
            <form onSubmit={submitSearch} className="flex gap-2">
              <label className="relative min-w-0 flex-1">
                <span className="sr-only">جستجوی کاربران</span>
                <Search size={16} className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400" />
                <input value={searchInput} onChange={(event) => setSearchInput(event.target.value)} placeholder="نام، نام کاربری، ایمیل یا موبایل…" className="h-10 w-full rounded-xl border border-slate-200 bg-slate-50 pe-9 ps-3 text-sm outline-none transition focus:border-sky-400 focus:bg-white focus:ring-2 focus:ring-sky-100" />
              </label>
              <button type="submit" className="rounded-xl bg-[#12365d] px-3 text-sm font-semibold text-white hover:bg-[#194a79]">جستجو</button>
            </form>
            <div className="mt-3 flex flex-wrap gap-2">
              {(['all', 'active', 'inactive'] as const).map((value) => (
                <button key={value} type="button" onClick={() => { setStatus(value); setPage(1); }} className={'rounded-lg px-3 py-1.5 text-xs font-semibold ' + (status === value ? 'bg-sky-100 text-sky-800' : 'bg-slate-100 text-slate-600 hover:bg-slate-200')}>
                  {value === 'all' ? 'همه' : value === 'active' ? 'فعال' : 'غیرفعال'}
                </button>
              ))}
            </div>
          </div>

          <div className="max-h-[620px] min-h-[220px] overflow-y-auto p-2">
            {loading ? <p className="p-6 text-center text-sm text-slate-500">در حال دریافت فهرست…</p> : null}
            {error ? <p role="alert" className="m-2 rounded-xl bg-rose-50 p-4 text-sm text-rose-700">{error}</p> : null}
            {!loading && !error && list?.items.length === 0 ? <p className="p-8 text-center text-sm text-slate-500">کاربری با این مشخصات پیدا نشد.</p> : null}
            {!loading && !error ? list?.items.map((user) => (
              <UserRow key={user.id} user={user} selected={selectedId === user.id} onSelect={() => setSelectedId(user.id)} />
            )) : null}
          </div>
          <footer className="flex items-center justify-between border-t border-slate-100 px-4 py-3 text-xs text-slate-500">
            <span>صفحه {list?.page ?? page} از {totalPages}</span>
            <div className="flex gap-1">
              <button type="button" aria-label="صفحه قبل" disabled={page <= 1 || loading} onClick={() => setPage((value) => Math.max(1, value - 1))} className="rounded-lg border border-slate-200 p-2 disabled:opacity-35"><ChevronRight size={16} /></button>
              <button type="button" aria-label="صفحه بعد" disabled={page >= totalPages || loading} onClick={() => setPage((value) => value + 1)} className="rounded-lg border border-slate-200 p-2 disabled:opacity-35"><ChevronLeft size={16} /></button>
            </div>
          </footer>
        </section>

        <div className="min-w-0">
          {detailsLoading ? <div className="rounded-2xl border border-slate-200 bg-white p-10 text-center text-sm text-slate-500">در حال دریافت جزئیات کاربر…</div> : null}
          {detailsError ? <p role="alert" className="rounded-xl bg-rose-50 p-4 text-sm text-rose-700">{detailsError}</p> : null}
          {!detailsLoading && !detailsError && details ? <UserDetails user={details} /> : null}
          {!detailsLoading && !detailsError && !details ? (
            <div className="flex min-h-72 flex-col items-center justify-center rounded-2xl border border-dashed border-slate-300 bg-white text-center">
              <UserRound size={30} className="text-slate-300" />
              <p className="mt-3 font-semibold text-slate-600">کاربری برای نمایش انتخاب نشده است</p>
              <p className="mt-1 text-sm text-slate-400">از فهرست سمت راست یک کاربر را انتخاب کنید.</p>
            </div>
          ) : null}
        </div>
      </div>
    </div>
  );
}

function UserRow({ user, selected, onSelect }: { user: AdminUserListItem; selected: boolean; onSelect: () => void }) {
  return (
    <button type="button" onClick={onSelect} aria-pressed={selected} className={'mb-1 flex w-full items-center gap-3 rounded-xl border p-3 text-right transition ' + (selected ? 'border-sky-200 bg-sky-50 shadow-sm' : 'border-transparent hover:border-slate-200 hover:bg-slate-50')}>
      <span className={'flex h-10 w-10 shrink-0 items-center justify-center rounded-xl text-sm font-bold ' + (selected ? 'bg-sky-700 text-white' : 'bg-slate-100 text-slate-600')}>{fullName(user).slice(0, 1)}</span>
      <span className="min-w-0 flex-1">
        <span className="block truncate text-sm font-bold text-slate-800">{fullName(user)}</span>
        <span className="mt-1 block truncate text-xs text-slate-500" dir="ltr">@{user.username ?? '—'}</span>
        <span className="mt-1 block text-[11px] text-slate-400">{user.activeCompanyCount} شرکت فعال · آخرین ورود: {formatDate(user.lastLoginDateTime)}</span>
      </span>
      <Status active={user.isActive} />
    </button>
  );
}
