import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { Check, ChevronLeft, ChevronRight, KeyRound, Plus, ShieldCheck, Users, X } from 'lucide-react';
import { ApiError, createCompanyRole, getCompanyRoleTemplates, getCompanyRoles, getPermissionCatalog, updateCompanyRole } from '../api/client';
import type { CompanyRoleSummary, CompanyRoleTemplate, PermissionCatalogItem } from '../api/types';

type Props = { companyId: string; companyName: string; onUnauthenticated: (error: unknown) => boolean };

function subsystemLabel(code: string): string {
  if (code === 'trade') return 'بازرگانی خارجی';
  return code;
}

export function AccessGroupManagementPage({ companyId, companyName, onUnauthenticated }: Props) {
  const [roles, setRoles] = useState<CompanyRoleSummary[]>([]);
  const [catalog, setCatalog] = useState<PermissionCatalogItem[]>([]);
  const [templates, setTemplates] = useState<CompanyRoleTemplate[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [editorOpen, setEditorOpen] = useState(false);
  const [editingRole, setEditingRole] = useState<CompanyRoleSummary | null>(null);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError(null);
    Promise.all([
      getCompanyRoles(companyId, controller.signal),
      getPermissionCatalog(controller.signal),
      getCompanyRoleTemplates(controller.signal),
    ]).then(([nextRoles, nextCatalog, nextTemplates]) => {
      if (controller.signal.aborted) return;
      setRoles(nextRoles);
      setCatalog(nextCatalog);
      setTemplates(nextTemplates);
      setSelectedId((current) => current && nextRoles.some(role => role.id === current)
        ? current
        : nextRoles[0]?.id ?? null);
    }).catch((cause: unknown) => {
      if (controller.signal.aborted) return;
      if (!onUnauthenticated(cause)) setError(cause instanceof ApiError ? cause.message : 'دریافت گروه‌های دسترسی انجام نشد.');
    }).finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [companyId, onUnauthenticated]);

  const selectedRole = roles.find(role => role.id === selectedId) ?? null;
  const selectedPermissions = useMemo(
    () => new Set(selectedRole?.permissionCodes ?? []),
    [selectedRole],
  );
  const permissionNames = useMemo(
    () => new Map(catalog.map(permission => [permission.code, permission.name])),
    [catalog],
  );

  async function saveRole(values: {
    code: string; name: string; templateCode: string | null; permissionCodes: string[]; isActive: boolean;
  }) {
    setSaving(true);
    setSaveError(null);
    try {
      if (editingRole) {
        await updateCompanyRole(companyId, editingRole.id, {
          name: values.name,
          isActive: values.isActive,
          permissionCodes: values.permissionCodes,
        });
        setSelectedId(editingRole.id);
      } else {
        const id = await createCompanyRole(companyId, {
          code: values.code,
          name: values.name,
          templateCode: values.templateCode,
          permissionCodes: values.permissionCodes,
        });
        setSelectedId(id);
      }
      const refreshed = await getCompanyRoles(companyId);
      setRoles(refreshed);
      setEditorOpen(false);
    } catch (cause: unknown) {
      if (!onUnauthenticated(cause)) setSaveError(cause instanceof ApiError ? cause.message : 'ذخیره گروه دسترسی انجام نشد.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <div dir="rtl" className="space-y-5">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <p className="text-xs font-semibold text-sky-700">مدیریت سیستم / گروه‌های دسترسی</p>
          <h1 className="mt-1 text-2xl font-extrabold tracking-tight text-slate-900">گروه‌های دسترسی</h1>
          <p className="mt-2 text-sm text-slate-500">تعریف الگوهای مجوز و تخصیص آن‌ها در محدوده شرکت انتخاب‌شده</p>
        </div>
        <div className="flex items-center gap-2 rounded-xl border border-sky-100 bg-sky-50 px-3 py-2 text-xs font-semibold text-sky-800"><ShieldCheck size={16} /> {companyName}</div>
      </header>

      {error ? <p role="alert" className="rounded-xl bg-rose-50 p-4 text-sm text-rose-700">{error}</p> : null}
      <div className="grid min-w-0 gap-4 xl:grid-cols-[minmax(280px,0.85fr)_minmax(0,1.5fr)]">
        <section className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
          <header className="flex items-center justify-between border-b border-slate-100 p-4">
            <div className="flex items-center gap-2"><Users size={18} className="text-sky-700" /><h2 className="font-bold text-slate-900">گروه‌های این شرکت</h2></div>
            <button type="button" onClick={() => { setEditingRole(null); setSaveError(null); setEditorOpen(true); }} className="inline-flex items-center gap-1.5 rounded-lg bg-[#12365d] px-3 py-2 text-xs font-semibold text-white hover:bg-[#194a79]"><Plus size={15} /> گروه جدید</button>
          </header>
          <div className="min-h-64 space-y-2 p-2">
            {loading ? <p className="p-6 text-center text-sm text-slate-500">در حال دریافت گروه‌ها…</p> : null}
            {!loading && roles.length === 0 ? <p className="p-7 text-center text-sm text-slate-500">هنوز گروه دسترسی برای این شرکت تعریف نشده است.</p> : null}
            {roles.map(role => (
              <button key={role.id} type="button" onClick={() => setSelectedId(role.id)} aria-pressed={selectedId === role.id} className={'w-full rounded-xl border p-3 text-right transition ' + (selectedId === role.id ? 'border-sky-200 bg-sky-50 shadow-sm' : 'border-transparent hover:border-slate-200 hover:bg-slate-50')}>
                <div className="flex items-center justify-between gap-2"><span className="truncate text-sm font-bold text-slate-800">{role.name}</span><span className={'rounded-full px-2 py-0.5 text-[10px] font-semibold ' + (role.isActive ? 'bg-emerald-50 text-emerald-700' : 'bg-slate-100 text-slate-500')}>{role.isActive ? 'فعال' : 'غیرفعال'}</span></div>
                <p className="mt-1 text-[11px] text-slate-500" dir="ltr">{role.code}</p>
                <div className="mt-2 flex gap-3 text-[11px] text-slate-500"><span>{role.permissionCount} مجوز</span><span>{role.activeMemberCount} عضو فعال</span></div>
              </button>
            ))}
          </div>
        </section>

        <section className="min-w-0 rounded-2xl border border-slate-200 bg-white shadow-sm">
          {selectedRole ? (
            <>
              <header className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-100 bg-gradient-to-l from-slate-50 to-white p-5">
                <div className="min-w-0"><p className="text-xs font-semibold text-sky-700">جزئیات گروه دسترسی</p><h2 className="mt-1 truncate text-lg font-bold text-slate-900">{selectedRole.name}</h2><p className="mt-1 text-xs text-slate-500" dir="ltr">{selectedRole.code}</p></div>
                <button type="button" disabled={selectedRole.isSystemRole} onClick={() => { setEditingRole(selectedRole); setSaveError(null); setEditorOpen(true); }} className="rounded-xl border border-slate-200 bg-white px-3 py-2 text-xs font-semibold text-slate-700 hover:border-sky-300 disabled:opacity-40">ویرایش گروه</button>
              </header>
              <div className="grid gap-3 p-5 sm:grid-cols-3">
                <Metric label="تعداد مجوزها" value={selectedRole.permissionCount} />
                <Metric label="اعضای فعال" value={selectedRole.activeMemberCount} />
                <Metric label="وضعیت گروه" value={selectedRole.isActive ? 'فعال' : 'غیرفعال'} />
              </div>
              <div className="border-t border-slate-100 p-5">
                <div className="mb-3 flex items-center gap-2"><KeyRound size={17} className="text-sky-700" /><h3 className="font-bold text-slate-900">مجوزهای تخصیص‌یافته</h3></div>
                {selectedRole.permissionCodes.length === 0 ? <p className="rounded-xl bg-slate-50 p-4 text-sm text-slate-500">هیچ مجوزی به این گروه تخصیص داده نشده است.</p> : (
                  <div className="grid gap-2 sm:grid-cols-2">
                    {selectedRole.permissionCodes.map(code => <div key={code} className="flex min-w-0 items-start gap-2 rounded-xl border border-slate-100 p-3"><Check size={15} className="mt-0.5 shrink-0 text-emerald-600" /><div className="min-w-0"><p className="text-sm font-semibold text-slate-800">{permissionNames.get(code) ?? code}</p><p className="mt-1 break-all text-[10px] text-slate-400" dir="ltr">{code}</p></div></div>)}
                  </div>
                )}
              </div>
            </>
          ) : (
            <div className="flex min-h-72 flex-col items-center justify-center text-center"><KeyRound size={30} className="text-slate-300" /><p className="mt-3 font-semibold text-slate-600">گروهی انتخاب نشده است</p><p className="mt-1 text-sm text-slate-400">از فهرست، یک گروه را برای مشاهده مجوزها انتخاب کنید.</p></div>
          )}
        </section>
      </div>

      {editorOpen ? <RoleEditor key={editingRole?.id ?? 'new'} role={editingRole} catalog={catalog} templates={templates} saving={saving} error={saveError} onClose={() => setEditorOpen(false)} onSave={saveRole} /> : null}
      {!loading && catalog.length === 0 ? <p className="rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm leading-6 text-amber-800">کاتالوگ مجوز هنوز برای زیرسیستم‌های این شرکت ثبت نشده است. تا زمان ثبت و اتصال مجوزهای واقعی، الگوی فرضی برای حسابداری، خزانه‌داری یا انبار ساخته نمی‌شود.</p> : null}
    </div>
  );
}

function Metric({ label, value }: { label: string; value: string | number }) {
  return <div className="rounded-xl bg-slate-50 p-3"><p className="text-xs text-slate-500">{label}</p><p className="mt-1 text-lg font-bold text-slate-800">{value}</p></div>;
}

function RoleEditor({ role, catalog, templates, saving, error, onClose, onSave }: {
  role: CompanyRoleSummary | null;
  catalog: PermissionCatalogItem[];
  templates: CompanyRoleTemplate[];
  saving: boolean;
  error: string | null;
  onClose: () => void;
  onSave: (values: { code: string; name: string; templateCode: string | null; permissionCodes: string[]; isActive: boolean }) => Promise<void>;
}) {
  const [code, setCode] = useState(role?.code ?? '');
  const [name, setName] = useState(role?.name ?? '');
  const [templateCode, setTemplateCode] = useState('');
  const [selected, setSelected] = useState<string[]>(role?.permissionCodes ?? []);
  const [isActive, setIsActive] = useState(role?.isActive ?? true);
  const groups = useMemo(() => catalog.reduce<Record<string, PermissionCatalogItem[]>>((all, permission) => {
    (all[permission.subsystemCode] ??= []).push(permission);
    return all;
  }, {}), [catalog]);

  function chooseTemplate(value: string) {
    setTemplateCode(value);
    const template = templates.find(item => item.code === value);
    if (template) setSelected([...template.permissionCodes]);
  }

  function togglePermission(permissionCode: string, checked: boolean) {
    setSelected(current => checked
      ? [...new Set([...current, permissionCode])]
      : current.filter(codeValue => codeValue !== permissionCode));
  }

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    void onSave({ code, name, templateCode: role ? null : templateCode || null, permissionCodes: selected, isActive });
  }

  return (
    <div className="fixed inset-0 z-[75] flex items-center justify-center bg-slate-950/55 p-3 backdrop-blur-sm" onMouseDown={(event) => { if (event.target === event.currentTarget && !saving) onClose(); }}>
      <section role="dialog" aria-modal="true" aria-labelledby="role-editor-title" className="flex max-h-[94vh] w-full max-w-4xl flex-col overflow-hidden rounded-2xl bg-white shadow-2xl">
        <header className="flex items-center justify-between border-b border-slate-100 bg-gradient-to-l from-slate-50 to-white px-5 py-4">
          <div><p className="text-xs font-semibold text-sky-700">مدیریت دسترسی شرکت</p><h2 id="role-editor-title" className="mt-1 text-lg font-bold text-slate-900">{role ? 'ویرایش گروه دسترسی' : 'ساخت گروه دسترسی'}</h2></div>
          <button type="button" aria-label="بستن فرم" disabled={saving} onClick={onClose} className="rounded-lg p-2 text-slate-500 hover:bg-slate-100"><X size={18} /></button>
        </header>
        <form onSubmit={submit} className="min-h-0 space-y-4 overflow-y-auto p-5">
          <div className="grid gap-3 sm:grid-cols-2">
            {!role ? <label className="block"><span className="mb-1.5 block text-xs font-semibold text-slate-600">کد گروه</span><input required maxLength={80} dir="ltr" value={code} onChange={event => setCode(event.target.value.toUpperCase())} placeholder="ACCOUNTING_OPERATOR" className="h-11 w-full rounded-xl border border-slate-200 px-3 text-sm outline-none focus:border-sky-400" /></label> : null}
            <label className="block"><span className="mb-1.5 block text-xs font-semibold text-slate-600">عنوان گروه</span><input required maxLength={120} value={name} onChange={event => setName(event.target.value)} className="h-11 w-full rounded-xl border border-slate-200 px-3 text-sm outline-none focus:border-sky-400" /></label>
            {!role ? <label className="block"><span className="mb-1.5 block text-xs font-semibold text-slate-600">الگوی اولیه</span><select value={templateCode} onChange={event => chooseTemplate(event.target.value)} className="h-11 w-full rounded-xl border border-slate-200 bg-white px-3 text-sm"><option value="">بدون الگو</option>{templates.map(template => <option key={template.code} value={template.code}>{template.name}</option>)}</select></label> : null}
          </div>

          <div className="flex flex-wrap items-center justify-between gap-2 rounded-xl bg-slate-50 px-4 py-3">
            <div><p className="text-sm font-bold text-slate-800">جزئیات مجوزها</p><p className="mt-1 text-xs text-slate-500">{selected.length} مجوز انتخاب شده از {catalog.length} مجوز ثبت‌شده</p></div>
            <div className="flex gap-2"><button type="button" onClick={() => setSelected(catalog.map(item => item.code))} className="text-xs font-semibold text-sky-700">انتخاب همه</button><button type="button" onClick={() => setSelected([])} className="text-xs font-semibold text-slate-500">پاک‌کردن انتخاب</button></div>
          </div>

          {Object.entries(groups).map(([subsystem, permissions]) => (
            <section key={subsystem} className="overflow-hidden rounded-xl border border-slate-200">
              <header className="flex items-center justify-between bg-slate-50 px-4 py-3"><h3 className="text-sm font-bold text-slate-800">{subsystemLabel(subsystem)}</h3><span className="text-[11px] text-slate-500">{permissions.filter(item => selected.includes(item.code)).length} از {permissions.length}</span></header>
              <div className="grid gap-2 p-3 sm:grid-cols-2">
                {permissions.map(permission => <label key={permission.code} className="flex min-w-0 items-start gap-2 rounded-lg border border-slate-100 p-3 hover:bg-slate-50"><input type="checkbox" checked={selected.includes(permission.code)} onChange={event => togglePermission(permission.code, event.target.checked)} className="mt-1" /><span className="min-w-0"><span className="block text-sm font-semibold text-slate-700">{permission.name}</span><span className="mt-1 block break-all text-[10px] text-slate-400" dir="ltr">{permission.code}</span></span></label>)}
              </div>
            </section>
          ))}

          {role ? <label className="flex items-center gap-2 rounded-xl bg-slate-50 px-4 py-3 text-sm font-semibold text-slate-700"><input type="checkbox" checked={isActive} onChange={event => setIsActive(event.target.checked)} /> گروه فعال باشد</label> : null}
          {error ? <p role="alert" className="rounded-xl bg-rose-50 p-3 text-sm text-rose-700">{error}</p> : null}
          <footer className="flex justify-end gap-2 border-t border-slate-100 pt-4"><button type="button" disabled={saving} onClick={onClose} className="rounded-xl border border-slate-200 px-4 py-2 text-sm font-semibold text-slate-600">انصراف</button><button type="submit" disabled={saving} className="rounded-xl bg-[#12365d] px-5 py-2 text-sm font-semibold text-white disabled:opacity-60">{saving ? 'در حال ذخیره…' : role ? 'ذخیره تغییرات' : 'ایجاد گروه'}</button></footer>
        </form>
      </section>
    </div>
  );
}
