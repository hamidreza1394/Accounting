import { useCallback, useEffect, useRef, useState, type KeyboardEvent } from 'react';
import { History, ShieldCheck, UserRound, X } from 'lucide-react';
import { getMyProfile, UnauthenticatedError } from '../api/client';
import type { MyProfile } from '../api/types';
import { ActiveSessions } from './account/ActiveSessions';
import { HistoryTab } from './account/HistoryTab';
import { ProfileTab } from './account/ProfileTab';
import { SecurityTab } from './account/SecurityTab';
import { Tabs, type TabItem } from './ui/Tabs';

type AccountTab = 'profile' | 'security' | 'history';

type MyAccountPanelProps = {
  open: boolean;
  onClose: () => void;
  onPasswordChanged: () => void;
  onUnauthenticated: (error: unknown) => boolean;
};

const focusableSelector = 'button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [href], [tabindex]';

function TabLabel({ icon: Icon, children }: { icon: typeof UserRound; children: string }) {
  return (
    <span className="inline-flex items-center justify-center gap-1.5 sm:gap-2">
      <Icon size={16} className="shrink-0" aria-hidden="true" />
      {children}
    </span>
  );
}

export function MyAccountPanel({ open, onClose, onPasswordChanged, onUnauthenticated }: MyAccountPanelProps) {
  const [activeTab, setActiveTab] = useState<AccountTab>('profile');
  const [profile, setProfile] = useState<MyProfile | null>(null);
  const [profileLoading, setProfileLoading] = useState(false);
  const [profileError, setProfileError] = useState<string | null>(null);
  const [profileReload, setProfileReload] = useState(0);
  const [passwordSubmitting, setPasswordSubmitting] = useState(false);
  const closeButtonRef = useRef<HTMLButtonElement | null>(null);
  const panelRef = useRef<HTMLElement | null>(null);
  const retryProfile = useCallback(() => setProfileReload((key) => key + 1), []);

  useEffect(() => {
    if (!open) return;
    const controller = new AbortController();
    setProfileLoading(true);
    setProfileError(null);
    getMyProfile(controller.signal)
      .then((result) => {
        if (!controller.signal.aborted) setProfile(result);
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted) return;
        if (error instanceof UnauthenticatedError && onUnauthenticated(error)) return;
        setProfileError(error instanceof Error ? error.message : 'دریافت مشخصات حساب ناموفق بود.');
      })
      .finally(() => {
        if (!controller.signal.aborted) setProfileLoading(false);
      });
    return () => controller.abort();
  }, [open, onUnauthenticated, profileReload]);

  // Lock page scroll, move focus into the drawer, and give focus back to the opener on close.
  useEffect(() => {
    if (!open) return;
    const opener = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    closeButtonRef.current?.focus();
    return () => {
      document.body.style.overflow = previousOverflow;
      // Reset while the drawer renders nothing, so reopening starts on the profile without a flash.
      setActiveTab('profile');
      if (opener?.isConnected) opener.focus();
    };
  }, [open]);

  function handleDialogKeyDown(event: KeyboardEvent<HTMLElement>) {
    if (event.key === 'Escape') {
      event.preventDefault();
      onClose();
      return;
    }
    if (event.key !== 'Tab') return;
    const focusable = Array.from(panelRef.current?.querySelectorAll<HTMLElement>(focusableSelector) ?? []).filter(
      (element) => element.tabIndex >= 0 && !element.hasAttribute('disabled'),
    );
    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    if (!first || !last) return;
    if (!panelRef.current?.contains(document.activeElement)) {
      event.preventDefault();
      first.focus();
      return;
    }
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  }

  if (!open) return null;

  const items: TabItem[] = [
    {
      id: 'profile',
      disabled: passwordSubmitting,
      label: <TabLabel icon={UserRound}>مشخصات من</TabLabel>,
      content: <ProfileTab profile={profile} loading={profileLoading} error={profileError} onRetry={retryProfile} />,
    },
    {
      id: 'security',
      label: <TabLabel icon={ShieldCheck}>امنیت حساب</TabLabel>,
      content: (
        <>
          <SecurityTab submitting={passwordSubmitting} onSubmittingChange={setPasswordSubmitting} onPasswordChanged={onPasswordChanged} onUnauthenticated={onUnauthenticated} />
          {/* Ending this browser's own session leaves the same way a password change does: back to the login page. */}
          <ActiveSessions onUnauthenticated={onUnauthenticated} onSignedOut={onPasswordChanged} />
        </>
      ),
    },
    {
      id: 'history',
      disabled: passwordSubmitting,
      label: <TabLabel icon={History}>سوابق ورود</TabLabel>,
      content: <HistoryTab onUnauthenticated={onUnauthenticated} />,
    },
  ];

  return (
    <div className="fixed inset-0 z-[90]">
      <button
        type="button"
        tabIndex={-1}
        className="absolute inset-0 h-full w-full bg-night/55 backdrop-blur-[2px]"
        aria-label="بستن پنجره حساب کاربری"
        onClick={onClose}
      />
      <section
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="my-account-title"
        onKeyDown={handleDialogKeyDown}
        className="absolute inset-y-0 start-0 flex w-full max-w-2xl flex-col overflow-hidden border-e border-white/20 bg-sand shadow-2xl"
      >
        <header className="on-dark relative overflow-hidden bg-gradient-to-b from-navy-deep to-navy px-5 pb-5 pt-6 text-white sm:px-7">
          <div className="pointer-events-none absolute -start-12 -top-16 size-52 rounded-full border border-white/10" />
          <div className="pointer-events-none absolute -start-2 -top-6 size-36 rounded-full border border-gold/20" />
          <div className="relative flex items-start justify-between gap-4">
            <div className="flex min-w-0 items-center gap-3">
              <div className="flex size-12 shrink-0 items-center justify-center rounded-xl border border-gold/30 bg-white/10 text-gold">
                <UserRound size={24} aria-hidden="true" />
              </div>
              <div className="min-w-0">
                <p className="text-xs font-semibold text-white/75">مرکز حساب کاربری</p>
                <h2 id="my-account-title" className="mt-1 text-lg font-extrabold sm:text-xl">حساب کاربری من</h2>
                <p className="mt-1 text-xs text-white/75">مشخصات شخصی، امنیت و سوابق ورود</p>
              </div>
            </div>
            <button
              ref={closeButtonRef}
              type="button"
              onClick={onClose}
              aria-label="بستن"
              className="rounded-lg border border-white/15 bg-white/10 p-2 text-white outline-none transition hover:bg-white/20 focus-visible:ring-2 focus-visible:ring-gold"
            >
              <X size={19} aria-hidden="true" />
            </button>
          </div>
          <div className="relative mt-5 flex flex-wrap items-center gap-2 text-xs text-white/90">
            <span className="rounded-md border border-white/15 bg-white/10 px-3 py-1.5">{profile?.username ?? 'در حال دریافت نام کاربری…'}</span>
            <span className="rounded-md border border-gold/30 bg-gold/10 px-3 py-1.5 text-white">اطلاعات حساب جاری</span>
          </div>
        </header>

        <div className="min-h-0 flex-1 overflow-y-auto bg-white p-4 pb-8 sm:p-6 sm:pb-8">
          <Tabs label="بخش‌های حساب کاربری" items={items} value={activeTab} onValueChange={(id) => setActiveTab(id as AccountTab)} />
        </div>
        <footer className="flex shrink-0 flex-wrap items-center justify-between gap-x-4 gap-y-1 border-t border-line bg-white px-5 py-3 text-xs text-muted sm:px-7">
          <span>فرانو · مدیریت امن حساب</span>
          <span>اطلاعات محرمانه فقط برای صاحب حساب</span>
        </footer>
      </section>
    </div>
  );
}
