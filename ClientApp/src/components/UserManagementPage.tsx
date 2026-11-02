import { useEffect, useState, type FormEvent } from 'react';
import { Building2, ChevronLeft, ChevronRight, Pencil, Search, ShieldCheck, UserPlus, UserRound, Users } from 'lucide-react';
import { ApiError, assignUserCompanyRole, createAdminUser, getAdminUser, getAdminUsers, getCompanyRoles, updateAdminUser } from '../api/client';
import type { AdminUserDetails, AdminUserListItem, AdminUserListResponse, CompanyRoleSummary, CreateAdminUserRequest, UpdateAdminUserRequest } from '../api/types';

type Props = { companyId: string; companyName: string; onUnauthenticated: (error: unknown) => boolean };

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

function UserDetails({ user, onEdit }: { user: AdminUserDetails; onEdit: () => void }) {
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
          <div className="flex items-center gap-2"><Status active={user.isActive} /><button type="button" onClick={onEdit} className="inline-flex items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 py-2 text-xs font-semibold text-slate-700 hover:border-sky-300 hover:text-sky-800"><Pencil size={14} /> ویرایش</button></div>
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

export function UserManagementPage({ companyId, companyName, onUnauthenticated }: Props) {
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
  const [editorOpen, setEditorOpen] = useState(false);
  const [editingUser, setEditingUser] = useState<AdminUserDetails | null>(null);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [refreshKey, setRefreshKey] = useState(0);
  const [companyRoles, setCompanyRoles] = useState<CompanyRoleSummary[]>([]);
  const [assignmentRoleId, setAssignmentRoleId] = useState('');
  const [assignmentSaving, setAssignmentSaving] = useState(false);
  const [assignmentError, setAssignmentError] = useState<string | null>(null);

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
  }, [search, status, page, refreshKey, onUnauthenticated]);

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

  useEffect(() => {
    const controller = new AbortController();
    getCompanyRoles(companyId, controller.signal)
      .then((roles) => { if (!controller.signal.aborted) setCompanyRoles(roles); })
      .catch((cause: unknown) => { if (!controller.signal.aborted && !onUnauthenticated(cause)) setAssignmentError(cause instanceof ApiError ? cause.message : 'دریافت نقش‌های شرکت انجام نشد.'); });
    return () => controller.abort();
  }, [companyId, onUnauthenticated]);

  useEffect(() => {
    if (!details) { setAssignmentRoleId(''); return; }
    const membership = details.memberships.find(item => item.companyId === companyId);
    const role = companyRoles.find(item => item.code === membership?.roleCode && item.isActive);
    setAssignmentRoleId(role?.id ?? '');
  }, [details, companyId, companyRoles]);

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
        <div className="flex flex-wrap items-center gap-2">
          <div className="flex items-center gap-2 rounded-xl border border-sky-100 bg-sky-50 px-3 py-2 text-xs font-semibold text-sky-800"><ShieldCheck size={16} /> دسترسی مدیر سراسری نصب</div>
          <button type="button" onClick={() => { setEditingUser(null); setSaveError(null); setEditorOpen(true); }} className="inline-flex items-center gap-2 rounded-xl bg-[#12365d] px-4 py-2.5 text-sm font-semibold text-white shadow-sm hover:bg-[#194a79]"><UserPlus size={17} /> کاربر جدید</button>
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
          {!detailsLoading && !detailsError && details ? <><UserDetails user={details} onEdit={() => { setEditingUser(details); setSaveError(null); setEditorOpen(true); }} /><CompanyMembershipEditor companyName={companyName} roles={companyRoles.filter(role => role.isActive)} selectedRoleId={assignmentRoleId} onRoleChange={setAssignmentRoleId} onAssign={async () => { if (!details || !assignmentRoleId) return; setAssignmentSaving(true); setAssignmentError(null); try { await assignUserCompanyRole(companyId, assignmentRoleId, details.id); setDetails(await getAdminUser(details.id)); setRefreshKey(value => value + 1); } catch (cause: unknown) { if (!onUnauthenticated(cause)) setAssignmentError(cause instanceof ApiError ? cause.message : 'ثبت عضویت انجام نشد.'); } finally { setAssignmentSaving(false); } }} saving={assignmentSaving} error={assignmentError} currentMembership={details.memberships.find(membership => membership.companyId === companyId) ?? null} /></> : null}
          {!detailsLoading && !detailsError && !details ? (
            <div className="flex min-h-72 flex-col items-center justify-center rounded-2xl border border-dashed border-slate-300 bg-white text-center">
              <UserRound size={30} className="text-slate-300" />
              <p className="mt-3 font-semibold text-slate-600">کاربری برای نمایش انتخاب نشده است</p>
              <p className="mt-1 text-sm text-slate-400">از فهرست سمت راست یک کاربر را انتخاب کنید.</p>
            </div>
          ) : null}
        </div>
      </div>
      {editorOpen ? (
        <UserEditor
          key={editingUser?.id ?? 'new-user'}
          initialUser={editingUser}
          saving={saving}
          error={saveError}
          onClose={() => setEditorOpen(false)}
          onSave={async (values) => {
            setSaving(true);
            setSaveError(null);
            try {
              if (editingUser) {
                const request: UpdateAdminUserRequest = {
                  username: values.username,
                  firstName: values.firstName,
                  lastName: values.lastName,
                  emailAddress: values.emailAddress,
                  cellPhoneNumber: values.cellPhoneNumber,
                  isActive: values.isActive,
                };
                await updateAdminUser(editingUser.id, request);
                setDetails(await getAdminUser(editingUser.id));
                setSelectedId(editingUser.id);
              } else {
                const request: CreateAdminUserRequest = values;
                const newId = await createAdminUser(request);
                setSearch('');
                setSearchInput('');
                setStatus('all');
                setPage(1);
                setSelectedId(newId);
                setDetails(await getAdminUser(newId));
              }
              setRefreshKey((value) => value + 1);
              setEditorOpen(false);
            } catch (cause: unknown) {
              if (!onUnauthenticated(cause)) setSaveError(cause instanceof ApiError ? cause.message : 'ذخیره اطلاعات کاربر انجام نشد.');
            } finally {
              setSaving(false);
            }
          }}
        />
      ) : null}
    </div>
  );
}

type EditorValues = CreateAdminUserRequest;
function UserEditor({ initialUser, saving, error, onClose, onSave }: {
  initialUser: AdminUserDetails | null;
  saving: boolean;
  error: string | null;
  onClose: () => void;
  onSave: (values: EditorValues) => Promise<void>;
}) {
  const [username, setUsername] = useState(initialUser?.username ?? '');
  const [firstName, setFirstName] = useState(initialUser?.firstName ?? '');
  const [lastName, setLastName] = useState(initialUser?.lastName ?? '');
  const [emailAddress, setEmailAddress] = useState(initialUser?.emailAddress ?? '');
  const [cellPhoneNumber, setCellPhoneNumber] = useState(initialUser?.cellPhoneNumber ?? '');
  const [isActive, setIsActive] = useState(initialUser?.isActive ?? true);
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    await onSave({ username, firstName, lastName, emailAddress, cellPhoneNumber, isActive, password, confirmPassword });
  }

  return (
    <div className="fixed inset-0 z-[70] flex items-center justify-center bg-slate-950/55 p-3 backdrop-blur-sm" onMouseDown={(event) => { if (event.target === event.currentTarget && !saving) onClose(); }}>
      <section role="dialog" aria-modal="true" aria-labelledby="user-editor-title" className="max-h-[92vh] w-full max-w-2xl overflow-y-auto rounded-2xl bg-white shadow-2xl">
        <header className="flex items-center justify-between border-b border-slate-100 bg-gradient-to-l from-slate-50 to-white px-5 py-4">
          <div><p className="text-xs font-semibold text-sky-700">مدیریت سیستم</p><h2 id="user-editor-title" className="mt-1 text-lg font-bold text-slate-900">{initialUser ? 'ویرایش کاربر' : 'ایجاد کاربر جدید'}</h2></div>
          <button type="button" aria-label="بستن فرم" disabled={saving} onClick={onClose} className="rounded-lg p-2 text-slate-500 hover:bg-slate-100"><X size={18} /></button>
        </header>
        <form onSubmit={submit} className="space-y-5 p-5">
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="نام" value={firstName} onChange={setFirstName} required />
            <Field label="نام خانوادگی" value={lastName} onChange={setLastName} required />
            <Field label="نام کاربری" value={username} onChange={setUsername} required dir="ltr" />
            <Field label="ایمیل" value={emailAddress} onChange={setEmailAddress} required type="email" dir="ltr" />
            <Field label="شماره همراه" value={cellPhoneNumber} onChange={setCellPhoneNumber} dir="ltr" type="tel" />
            {!initialUser ? <><Field label="رمز عبور اولیه" value={password} onChange={setPassword} required type="password" dir="ltr" /><Field label="تکرار رمز عبور" value={confirmPassword} onChange={setConfirmPassword} required type="password" dir="ltr" /></> : null}
          </div>
          <label className="flex items-center gap-2 rounded-xl bg-slate-50 px-4 py-3 text-sm font-semibold text-slate-700"><input type="checkbox" checked={isActive} onChange={(event) => setIsActive(event.target.checked)} /> حساب کاربری فعال باشد</label>
          {!initialUser ? <p className="rounded-xl border border-amber-100 bg-amber-50 px-4 py-3 text-xs leading-6 text-amber-800">رمز اولیه فقط هنگام ایجاد ارسال می‌شود و به‌صورت هش امن ذخیره خواهد شد. برای تغییر یا بازنشانی رمز از فرایند امنیتی مستقل استفاده می‌شود.</p> : null}
          {error ? <p role="alert" className="rounded-xl bg-rose-50 px-4 py-3 text-sm text-rose-700">{error}</p> : null}
          <footer className="flex justify-end gap-2 border-t border-slate-100 pt-4">
            <button type="button" disabled={saving} onClick={onClose} className="rounded-xl border border-slate-200 px-4 py-2.5 text-sm font-semibold text-slate-600 hover:bg-slate-50">انصراف</button>
            <button type="submit" disabled={saving} className="rounded-xl bg-[#12365d] px-5 py-2.5 text-sm font-semibold text-white hover:bg-[#194a79] disabled:opacity-60">{saving ? 'در حال ذخیره…' : initialUser ? 'ذخیره تغییرات' : 'ایجاد حساب کاربری'}</button>
          </footer>
        </form>
      </section>
    </div>
  );
}

function Field({ label, value, onChange, required = false, type = 'text', dir = 'rtl' }: {
  label: string; value: string; onChange: (value: string) => void; required?: boolean; type?: string; dir?: 'rtl' | 'ltr';
}) {
  return <label className="block min-w-0"><span className="mb-1.5 block text-xs font-semibold text-slate-600">{label}</span><input type={type} dir={dir} value={value} onChange={(event) => onChange(event.target.value)} required={required} maxLength={type === 'email' ? 254 : type === 'tel' ? 32 : type === 'password' ? 128 : 100} className="h-11 w-full rounded-xl border border-slate-200 bg-white px-3 text-sm text-slate-800 outline-none transition focus:border-sky-400 focus:ring-2 focus:ring-sky-100" /></label>;
}

function CompanyMembershipEditor({ companyName, roles, selectedRoleId, onRoleChange, onAssign, saving, error, currentMembership }: {
  companyName: string;
  roles: CompanyRoleSummary[];
  selectedRoleId: string;
  onRoleChange: (value: string) => void;
  onAssign: () => void;
  saving: boolean;
  error: string | null;
  currentMembership: AdminUserDetails['memberships'][number] | null;
}) {
  return (
    <section className="mt-4 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
      <div className="flex flex-wrap items-center justify-between gap-2"><div><h3 className="font-bold text-slate-900">عضویت و گروه دسترسی در {companyName}</h3><p className="mt-1 text-xs text-slate-500">نقش انتخاب‌شده فقط برای همین شرکت اعمال می‌شود.</p></div><span className={'rounded-full px-2.5 py-1 text-xs font-semibold ' + (currentMembership?.isActive ? 'bg-emerald-50 text-emerald-700' : 'bg-slate-100 text-slate-600')}>{currentMembership?.isActive ? 'عضویت فعال' : currentMembership ? 'عضویت غیرفعال' : 'بدون عضویت'}</span></div>
      <div className="mt-4 flex flex-wrap items-end gap-3">
        <label className="min-w-[220px] flex-1"><span className="mb-1.5 block text-xs font-semibold text-slate-600">گروه دسترسی شرکت</span><select value={selectedRoleId} onChange={event => onRoleChange(event.target.value)} className="h-11 w-full rounded-xl border border-slate-200 bg-white px-3 text-sm"><option value="">انتخاب گروه دسترسی…</option>{roles.map(role => <option key={role.id} value={role.id}>{role.name} ({role.permissionCount} مجوز)</option>)}</select></label>
        <button type="button" disabled={!selectedRoleId || saving} onClick={onAssign} className="h-11 rounded-xl bg-[#12365d] px-4 text-sm font-semibold text-white hover:bg-[#194a79] disabled:opacity-50">{saving ? 'در حال ثبت…' : currentMembership ? 'تغییر گروه / فعال‌سازی عضویت' : 'ایجاد عضویت و تخصیص گروه'}</button>
      </div>
      {error ? <p role="alert" className="mt-3 rounded-lg bg-rose-50 p-3 text-xs text-rose-700">{error}</p> : null}
      {roles.length === 0 ? <p className="mt-3 rounded-lg bg-amber-50 p-3 text-xs text-amber-800">برای این شرکت هنوز گروه فعال وجود ندارد. ابتدا از بخش گروه‌های دسترسی یک گروه بسازید.</p> : null}
    </section>
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
