import { useEffect, useId, useRef, useState, type FormEvent } from 'react';
import { ApiError, assignCompanyRoleMember, getCompanyUsers } from '../../../api/client';
import type { CompanyRole } from '../../../api/types';
import { Alert } from '../../ui/Alert';
import { Button } from '../../ui/button';
import { ConfirmDialog } from '../../ui/ConfirmDialog';
import { FormField, Input } from '../../ui/FormField';

type Candidate = { userId: string; username: string | null; name: string; currentRoleName: string | null };

type Props = {
  role: CompanyRole;
  companyId: string;
  onUnauthenticated: (error: unknown) => boolean;
  /** The server refused with 403: the caller re-reads the real permissions. */
  onForbidden?: (() => void) | undefined;
  /** Called after a successful assignment so the member list can reload. */
  onAssigned: () => void;
};

type Results = { kind: 'idle' } | { kind: 'loading' } | { kind: 'ready'; items: Candidate[]; more: boolean } | { kind: 'error'; message: string };

const searchLimit = 25;

function displayName(first: string | null, last: string | null, username: string | null): string {
  return `${first ?? ''} ${last ?? ''}`.trim() || username || 'کاربر بدون نام';
}

/**
 * Assigns an EXISTING active member of the company to this group (`POST .../roles/{roleId}/members/{userId}`).
 * Candidates come from the company directory, so the search needs the user-list permission; the server decides
 * everything else (delegation ceiling, system groups, self-assignment, inactive memberships). Nothing is
 * created here: a user without an active membership is not offered.
 */
export function RoleMemberAssign({ role, companyId, onUnauthenticated, onForbidden, onAssigned }: Props) {
  const searchId = useId();
  const [term, setTerm] = useState('');
  const [results, setResults] = useState<Results>({ kind: 'idle' });
  const [chosen, setChosen] = useState<Candidate | null>(null);
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const searchRef = useRef<AbortController | null>(null);
  const busyRef = useRef(false);
  const failureRef = useRef<HTMLDivElement>(null);
  const noticeRef = useRef<HTMLDivElement>(null);

  useEffect(() => () => searchRef.current?.abort(), []);
  useEffect(() => {
    if (failure) failureRef.current?.focus();
  }, [failure]);
  useEffect(() => {
    if (notice) noticeRef.current?.focus();
  }, [notice]);

  const search = async (event: FormEvent) => {
    event.preventDefault();
    searchRef.current?.abort();
    const controller = new AbortController();
    searchRef.current = controller;
    setNotice(null);
    setFailure(null);
    setResults({ kind: 'loading' });
    try {
      const page = await getCompanyUsers(companyId, { search: term, membershipActive: true, page: 1, pageSize: searchLimit }, controller.signal);
      if (controller.signal.aborted) return;
      const items = page.items
        .filter((user) => user.userIsActive && user.membershipIsActive && user.roleId !== role.id)
        .map((user) => ({
          userId: user.userId,
          username: user.username,
          name: displayName(user.firstName, user.lastName, user.username),
          currentRoleName: user.roleName,
        }));
      setResults({ kind: 'ready', items, more: page.totalCount > page.items.length });
    } catch (cause) {
      if (controller.signal.aborted || onUnauthenticated(cause)) return;
      setResults({
        kind: 'error',
        message:
          cause instanceof ApiError && cause.status === 403
            ? 'برای جستجوی کاربران به مجوز مشاهدهٔ فهرست کاربران این شرکت نیاز دارید.'
            : cause instanceof ApiError
              ? cause.message
              : 'جستجوی کاربران انجام نشد. لطفاً دوباره تلاش کنید.',
      });
    }
  };

  const confirm = async () => {
    if (!chosen || busyRef.current) return;
    busyRef.current = true;
    setBusy(true);
    setFailure(null);
    const user = chosen;
    try {
      await assignCompanyRoleMember(companyId, role.id, user.userId);
      setChosen(null);
      setNotice(`«${user.name}» به گروه «${role.name}» اختصاص یافت.`);
      setResults((current) => (current.kind === 'ready' ? { ...current, items: current.items.filter((item) => item.userId !== user.userId) } : current));
      onAssigned();
    } catch (cause) {
      if (onUnauthenticated(cause)) return;
      setChosen(null);
      if (cause instanceof ApiError) {
        setFailure(cause.message);
        if (cause.status === 403) onForbidden?.();
      } else {
        setFailure('نتیجهٔ اختصاص نامشخص است. فهرست اعضا را بررسی کنید و در صورت نیاز دوباره تلاش کنید.');
        onAssigned();
      }
    } finally {
      busyRef.current = false;
      setBusy(false);
    }
  };

  return (
    <div className="space-y-3 border-t border-line pt-4">
      <h3 className="text-base font-bold text-ink">افزودن عضو به این گروه</h3>
      <p className="text-sm text-sub">
        فقط کاربری که در این شرکت عضویت فعال دارد انتخاب می‌شود و گروه قبلی او با این گروه جایگزین می‌شود. سقف مجوزهای شما و محدودیت‌های گروه سیستمی سمت سرور بررسی می‌شود.
      </p>
      <form onSubmit={search} className="flex flex-wrap items-end gap-3" role="search">
        <div className="min-w-56 flex-1">
          <FormField label="جستجوی کاربر" hint="بخشی از نام یا نام کاربری.">
            <Input id={searchId} type="search" value={term} maxLength={120} onChange={(event) => setTerm(event.target.value)} />
          </FormField>
        </div>
        <Button type="submit" size="lg" disabled={results.kind === 'loading'}>
          جستجو
        </Button>
      </form>

      {notice ? <Alert ref={noticeRef} tone="info">{notice}</Alert> : null}
      {failure ? <Alert ref={failureRef} tone="error">{failure}</Alert> : null}
      {results.kind === 'error' ? <Alert tone="error">{results.message}</Alert> : null}
      {results.kind === 'loading' ? <p role="status" className="text-sm text-sub">در حال جستجو…</p> : null}
      {results.kind === 'ready' && results.more ? <p className="text-xs text-sub">نتایج بیشتری وجود دارد؛ برای دیدن بقیه عبارت جستجو را دقیق‌تر کنید.</p> : null}
      {results.kind === 'ready' && results.items.length === 0 ? <p className="text-sm text-sub">کاربر قابل اختصاصی پیدا نشد.</p> : null}
      {results.kind === 'ready' && results.items.length > 0 ? (
        <ul aria-label="نتایج جستجوی کاربر" className="divide-y divide-line rounded-lg border border-line">
          {results.items.map((item) => (
            <li key={item.userId} className="flex flex-wrap items-center justify-between gap-2 px-3 py-2">
              <div>
                <p className="font-bold text-ink">{item.name}</p>
                <p className="text-xs text-sub">
                  {item.username ? <code dir="ltr">{item.username}</code> : null}
                  {item.currentRoleName ? ` · گروه فعلی: ${item.currentRoleName}` : ''}
                </p>
              </div>
              <Button type="button" size="sm" variant="secondary" aria-label={`اختصاص ${item.name} به این گروه`} onClick={() => { setFailure(null); setNotice(null); setChosen(item); }}>
                اختصاص…
              </Button>
            </li>
          ))}
        </ul>
      ) : null}

      <ConfirmDialog
        open={chosen !== null}
        title="اختصاص عضو به گروه"
        confirmLabel="اختصاص به گروه"
        busy={busy}
        onConfirm={confirm}
        onCancel={() => {
          if (!busy) setChosen(null);
        }}
        description={
          <p>
            «{chosen?.name}» به گروه «{role.name}» اختصاص می‌یابد. دسترسی او با مجوزهای این گروه جایگزین می‌شود و در سوابق امنیتی ثبت می‌شود.
          </p>
        }
      />
    </div>
  );
}
