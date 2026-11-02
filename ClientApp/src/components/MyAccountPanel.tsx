import { useEffect, useRef, useState, type FormEvent, type KeyboardEvent } from 'react';
import {
  CalendarClock,
  CheckCircle2,
  Eye,
  EyeOff,
  History,
  KeyRound,
  LogIn,
  LogOut,
  Mail,
  Phone,
  ShieldCheck,
  UserRound,
  X,
} from 'lucide-react';
import { changeMyPassword, getMyLoginHistory, getMyProfile, UnauthenticatedError } from '../api/client';
import type { LoginHistoryItem, MyProfile } from '../api/types';

type AccountTab = 'profile' | 'security' | 'history';

type MyAccountPanelProps = {
  open: boolean;
  onClose: () => void;
  onPasswordChanged: () => void;
  onUnauthenticated: (error: unknown) => boolean;
};

const tabs: { id: AccountTab; label: string; icon: typeof UserRound }[] = [
  { id: 'profile', label: 'مشخصات من', icon: UserRound },
  { id: 'security', label: 'امنیت حساب', icon: ShieldCheck },
  { id: 'history', label: 'سوابق ورود', icon: History },
];

function formatDate(value: string | null): string {
  if (!value) return 'ثبت نشده';
  const date = new Date(value);
  if (!Number.isFinite(date.getTime())) return 'ثبت نشده';
  return new Intl.DateTimeFormat('fa-IR', { dateStyle: 'medium', timeStyle: 'short' }).format(date);
}

function eventLabel(event: LoginHistoryItem['eventType']): { label: string; tone: string; icon: typeof LogIn } {
  switch (event) {
    case 'LoginSucceeded':
      return { label: 'ورود موفق', tone: 'border-emerald-200 bg-emerald-50 text-emerald-800', icon: LogIn };
    case 'LoginFailed':
      return { label: 'ورود ناموفق', tone: 'border-rose-200 bg-rose-50 text-rose-800', icon: ShieldCheck };
    case 'LoginDeniedInactive':
      return { label: 'حساب غیرفعال', tone: 'border-amber-200 bg-amber-50 text-amber-800', icon: ShieldCheck };
    case 'LoginDeniedEnrollmentPending':
      return { label: 'فعال‌سازی تکمیل نشده', tone: 'border-amber-200 bg-amber-50 text-amber-800', icon: ShieldCheck };
    case 'LogoutSucceeded':
      return { label: 'خروج از حساب', tone: 'border-slate-200 bg-slate-50 text-slate-700', icon: LogOut };
  }
}

function InfoCard({ icon: Icon, label, value, verified }: {
  icon: typeof UserRound;
  label: string;
  value: string | null;
  verified?: boolean;
}) {
  return (
    <div className="min-w-0 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
      <div className="mb-2 flex items-center gap-2 text-xs font-semibold text-slate-500">
        <Icon size={15} aria-hidden="true" />
        <span>{label}</span>
      </div>
      <div className="flex min-w-0 items-center justify-between gap-2">
        <strong className="min-w-0 break-words text-sm text-slate-800">{value || 'ثبت نشده'}</strong>
        {verified === undefined ? null : (
          <span className={'shrink-0 rounded-full px-2 py-1 text-[10px] font-semibold ' + (verified ? 'bg-emerald-50 text-emerald-700' : 'bg-amber-50 text-amber-700')}>
            {verified ? 'تأیید شده' : 'تأیید نشده'}
          </span>
        )}
      </div>
    </div>
  );
}

export function MyAccountPanel({ open, onClose, onPasswordChanged, onUnauthenticated }: MyAccountPanelProps) {
  const [activeTab, setActiveTab] = useState<AccountTab>('profile');
  const [profile, setProfile] = useState<MyProfile | null>(null);
  const [history, setHistory] = useState<LoginHistoryItem[]>([]);
  const [profileLoading, setProfileLoading] = useState(false);
  const [historyLoading, setHistoryLoading] = useState(false);
  const [profileError, setProfileError] = useState<string | null>(null);
  const [historyError, setHistoryError] = useState<string | null>(null);
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmNewPassword, setConfirmNewPassword] = useState('');
  const [showPasswords, setShowPasswords] = useState(false);
  const [passwordLoading, setPasswordLoading] = useState(false);
  const [passwordError, setPasswordError] = useState<string | null>(null);
  const closeButtonRef = useRef<HTMLButtonElement>(null);
  const panelRef = useRef<HTMLElement>(null);

  useEffect(() => {
    if (!open) return;
    const controller = new AbortController();
    setProfileLoading(true);
    setProfileError(null);
    getMyProfile(controller.signal)
      .then(setProfile)
      .catch((error: unknown) => {
        if (controller.signal.aborted) return;
        if (error instanceof UnauthenticatedError && onUnauthenticated(error)) return;
        setProfileError(error instanceof Error ? error.message : 'دریافت مشخصات حساب ناموفق بود.');
      })
      .finally(() => {
        if (!controller.signal.aborted) setProfileLoading(false);
      });
    return () => controller.abort();
  }, [open, onUnauthenticated]);

  useEffect(() => {
    if (!open || activeTab !== 'history') return;
    const controller = new AbortController();
    setHistoryLoading(true);
    setHistoryError(null);
    getMyLoginHistory(controller.signal)
      .then(setHistory)
      .catch((error: unknown) => {
        if (controller.signal.aborted) return;
        if (error instanceof UnauthenticatedError && onUnauthenticated(error)) return;
        setHistoryError(error instanceof Error ? error.message : 'دریافت سوابق ورود ناموفق بود.');
      })
      .finally(() => {
        if (!controller.signal.aborted) setHistoryLoading(false);
      });
    return () => controller.abort();
  }, [open, activeTab, onUnauthenticated]);

  useEffect(() => {
    if (!open) return;
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    closeButtonRef.current?.focus();
    return () => {
      document.body.style.overflow = previousOverflow;
    };
  }, [open]);

  function handleDialogKeyDown(event: KeyboardEvent<HTMLElement>) {
    if (event.key === 'Escape') {
      event.preventDefault();
      onClose();
      return;
    }
    if (event.key !== 'Tab') return;
    const focusable = panelRef.current?.querySelectorAll<HTMLElement>(
      'button:not([disabled]), input:not([disabled]), [tabindex]:not([tabindex="-1"])',
    );
    if (!focusable?.length) return;
    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  }

  async function submitPassword(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setPasswordError(null);
    setPasswordLoading(true);
    try {
      await changeMyPassword({ currentPassword, newPassword, confirmNewPassword });
      onPasswordChanged();
    } catch (error: unknown) {
      if (error instanceof UnauthenticatedError && onUnauthenticated(error)) return;
      setPasswordError(error instanceof Error ? error.message : 'تغییر رمز با خطا مواجه شد.');
      setPasswordLoading(false);
    }
  }

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-[90]">
      <button
        type="button"
        className="absolute inset-0 h-full w-full bg-slate-950/55 backdrop-blur-[2px]"
        aria-label="بستن پنجره حساب کاربری"
        onClick={onClose}
      />
      <section
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="my-account-title"
        onKeyDown={handleDialogKeyDown}
        className="absolute inset-y-0 right-0 flex w-full max-w-2xl flex-col overflow-hidden border-l border-white/20 bg-slate-50 shadow-2xl"
      >
        <header className="relative overflow-hidden bg-gradient-to-l from-[#0b2342] via-[#12365d] to-[#174a70] px-5 pb-5 pt-6 text-white sm:px-7">
          <div className="pointer-events-none absolute -left-12 -top-16 h-52 w-52 rounded-full border border-white/10" />
          <div className="pointer-events-none absolute -left-2 -top-6 h-36 w-36 rounded-full border border-amber-300/15" />
          <div className="relative flex items-start justify-between gap-4">
            <div className="flex min-w-0 items-center gap-3">
              <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl border border-amber-200/30 bg-white/10 text-amber-200 shadow-inner">
                <UserRound size={24} />
              </div>
              <div className="min-w-0">
                <p className="text-[11px] font-semibold tracking-wide text-sky-100/75">مرکز حساب کاربری</p>
                <h2 id="my-account-title" className="mt-1 text-lg font-extrabold sm:text-xl">حساب کاربری من</h2>
                <p className="mt-1 text-xs text-blue-100/75">مشخصات شخصی، امنیت و سوابق ورود</p>
              </div>
            </div>
            <button
              ref={closeButtonRef}
              type="button"
              onClick={onClose}
              aria-label="بستن"
              className="rounded-xl border border-white/15 bg-white/10 p-2 text-white transition hover:bg-white/20 focus:outline-none focus:ring-2 focus:ring-amber-300"
            >
              <X size={19} />
            </button>
          </div>
          <div className="relative mt-5 flex flex-wrap items-center gap-2 text-xs text-sky-50/90">
            <span className="rounded-lg border border-white/15 bg-white/10 px-3 py-1.5">{profile?.username ?? 'در حال دریافت نام کاربری…'}</span>
            <span className="rounded-lg border border-amber-200/25 bg-amber-300/10 px-3 py-1.5 text-amber-100">اطلاعات حساب جاری</span>
          </div>
        </header>

        <div className="border-b border-slate-200 bg-white px-3 sm:px-6">
          <div role="tablist" aria-label="بخش‌های حساب کاربری" className="flex gap-1 overflow-x-auto">
            {tabs.map(({ id, label, icon: Icon }) => (
              <button
                key={id}
                type="button"
                role="tab"
                id={`my-account-tab-${id}`}
                aria-selected={activeTab === id}
                aria-controls={`my-account-panel-${id}`}
                onClick={() => setActiveTab(id)}
                className={'inline-flex min-h-12 shrink-0 items-center gap-2 border-b-2 px-3 text-xs font-bold transition sm:px-4 ' + (activeTab === id ? 'border-amber-500 text-[#12365d]' : 'border-transparent text-slate-500 hover:text-slate-800')}
              >
                <Icon size={16} />
                {label}
              </button>
            ))}
          </div>
        </div>

        <div className="min-h-0 flex-1 overflow-y-auto p-4 sm:p-6">
          {activeTab === 'profile' ? (
            <div id="my-account-panel-profile" role="tabpanel" aria-labelledby="my-account-tab-profile" className="space-y-4">
              <div className="flex items-center justify-between gap-3">
                <div>
                  <h3 className="font-extrabold text-slate-800">مشخصات حساب</h3>
                  <p className="mt-1 text-xs text-slate-500">این اطلاعات فقط برای حساب واردشده نمایش داده می‌شود.</p>
                </div>
                <span className="rounded-xl bg-emerald-50 px-3 py-2 text-[11px] font-bold text-emerald-700">فقط اطلاعات شخصی</span>
              </div>
              {profileLoading ? <p className="rounded-xl bg-white p-5 text-sm text-slate-500">در حال دریافت اطلاعات…</p> : null}
              {profileError ? <p role="alert" className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-700">{profileError}</p> : null}
              {profile ? (
                <div className="grid gap-3 sm:grid-cols-2">
                  <InfoCard icon={UserRound} label="نام و نام خانوادگی" value={[profile.firstName, profile.lastName].filter(Boolean).join(' ') || null} />
                  <InfoCard icon={KeyRound} label="نام کاربری" value={profile.username} />
                  <InfoCard icon={Mail} label="ایمیل" value={profile.emailAddress} verified={profile.isEmailAddressVerified} />
                  <InfoCard icon={Phone} label="شماره همراه" value={profile.cellPhoneNumber} verified={profile.isCellPhoneNumberVerified} />
                  <InfoCard icon={CalendarClock} label="آخرین ورود ثبت‌شده" value={formatDate(profile.lastLoginDateTime)} />
                  <InfoCard icon={CalendarClock} label="تاریخ ایجاد حساب" value={formatDate(profile.insertDateTime)} />
                </div>
              ) : null}
            </div>
          ) : null}

          {activeTab === 'security' ? (
            <div id="my-account-panel-security" role="tabpanel" aria-labelledby="my-account-tab-security" className="space-y-4">
              <div>
                <h3 className="font-extrabold text-slate-800">تغییر رمز عبور</h3>
                <p className="mt-1 text-xs leading-6 text-slate-500">برای تغییر رمز، رمز فعلی را وارد کنید. پس از موفقیت از همه نشست‌های قبلی خارج می‌شوید و باید دوباره وارد شوید.</p>
              </div>
              <form onSubmit={(event) => void submitPassword(event)} className="space-y-4 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-5">
                {[
                  { label: 'رمز فعلی', value: currentPassword, set: setCurrentPassword, autoComplete: 'current-password' },
                  { label: 'رمز جدید', value: newPassword, set: setNewPassword, autoComplete: 'new-password' },
                  { label: 'تکرار رمز جدید', value: confirmNewPassword, set: setConfirmNewPassword, autoComplete: 'new-password' },
                ].map((field, index) => (
                  <label key={field.label} className="block space-y-2">
                    <span className="text-xs font-bold text-slate-700">{field.label}</span>
                    <input
                      type={showPasswords ? 'text' : 'password'}
                      value={field.value}
                      onChange={(event) => field.set(event.target.value)}
                      autoComplete={field.autoComplete}
                      required
                      minLength={index === 0 ? undefined : 8}
                      maxLength={128}
                      className="h-12 w-full rounded-xl border border-slate-300 bg-slate-50 px-4 text-sm text-slate-900 outline-none transition placeholder:text-slate-400 focus:border-sky-600 focus:bg-white focus:ring-4 focus:ring-sky-100"
                      dir="ltr"
                    />
                  </label>
                ))}
                <button type="button" onClick={() => setShowPasswords((visible) => !visible)} className="inline-flex items-center gap-2 text-xs font-semibold text-sky-800 hover:text-sky-950">
                  {showPasswords ? <EyeOff size={15} /> : <Eye size={15} />}
                  {showPasswords ? 'پنهان‌کردن رمزها' : 'نمایش رمزها'}
                </button>
                <p className="text-[11px] leading-5 text-slate-500">رمز جدید باید حداقل ۸ و حداکثر ۱۲۸ نویسه داشته باشد و در فهرست رمزهای رایج نباشد.</p>
                {passwordError ? <p role="alert" className="rounded-xl border border-rose-200 bg-rose-50 p-3 text-xs text-rose-700">{passwordError}</p> : null}
                <div className="flex flex-wrap items-center justify-end gap-2 border-t border-slate-100 pt-4">
                  <button type="button" onClick={() => { setCurrentPassword(''); setNewPassword(''); setConfirmNewPassword(''); setPasswordError(null); }} className="rounded-xl border border-slate-300 px-4 py-2.5 text-xs font-bold text-slate-600 hover:bg-slate-50">
                    پاک‌کردن
                  </button>
                  <button type="submit" disabled={passwordLoading} className="inline-flex items-center gap-2 rounded-xl bg-[#12365d] px-5 py-2.5 text-xs font-bold text-white shadow-md shadow-blue-950/15 transition hover:bg-[#0b2747] disabled:cursor-wait disabled:opacity-60">
                    <KeyRound size={15} />
                    {passwordLoading ? 'در حال ثبت…' : 'تغییر رمز و خروج'}
                  </button>
                </div>
              </form>
            </div>
          ) : null}

          {activeTab === 'history' ? (
            <div id="my-account-panel-history" role="tabpanel" aria-labelledby="my-account-tab-history" className="space-y-4">
              <div>
                <h3 className="font-extrabold text-slate-800">سوابق ورود و خروج من</h3>
                <p className="mt-1 text-xs leading-6 text-slate-500">فقط رخدادهای قابل انتساب به همین حساب نمایش داده می‌شوند. پایان ناشناخته نشست، خروج موفق تلقی نمی‌شود.</p>
              </div>
              {historyLoading ? <p className="rounded-xl bg-white p-5 text-sm text-slate-500">در حال دریافت سوابق…</p> : null}
              {historyError ? <p role="alert" className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-700">{historyError}</p> : null}
              {!historyLoading && !historyError && history.length === 0 ? (
                <div className="rounded-2xl border border-dashed border-slate-300 bg-white p-8 text-center">
                  <History className="mx-auto mb-3 text-slate-400" size={30} />
                  <p className="text-sm font-bold text-slate-700">سابقه‌ای ثبت نشده است</p>
                  <p className="mt-1 text-xs text-slate-500">رویدادهای ورود موفق، ورود ناموفق و خروج صریح در این بخش دیده می‌شوند.</p>
                </div>
              ) : null}
              <div className="space-y-2">
                {history.map((item, index) => {
                  const event = eventLabel(item.eventType);
                  const Icon = event.icon;
                  return (
                    <article key={`${item.eventType}-${item.occurredAtUtc}-${index}`} className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
                      <div className="flex min-w-0 items-center gap-3">
                        <span className={'flex h-10 w-10 shrink-0 items-center justify-center rounded-xl border ' + event.tone}>
                          <Icon size={17} />
                        </span>
                        <div className="min-w-0">
                          <strong className="block text-sm text-slate-800">{event.label}</strong>
                          <span className="mt-1 block text-xs text-slate-500">{formatDate(item.occurredAtUtc)}</span>
                        </div>
                      </div>
                      <span className="rounded-lg bg-slate-100 px-3 py-2 text-[11px] font-medium text-slate-600" dir="ltr">
                        {item.ipAddress || 'IP ثبت نشده'}
                      </span>
                    </article>
                  );
                })}
              </div>
            </div>
          ) : null}
        </div>
        <footer className="flex items-center justify-between border-t border-slate-200 bg-white px-5 py-3 text-[10px] text-slate-400 sm:px-7">
          <span>فرانو · مدیریت امن حساب</span>
          <span>اطلاعات محرمانه فقط برای صاحب حساب</span>
        </footer>
      </section>
    </div>
  );
}
