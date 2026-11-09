import { useEffect, useId, useRef, useState, type FormEvent } from 'react';
import { ApiError, getCompanyUsers } from '../../../api/client';
import type { CompanyDirectoryUser } from '../../../api/types';
import { Alert } from '../../ui/Alert';
import { Badge } from '../../ui/Badge';
import { FormField, Input } from '../../ui/FormField';
import { Button } from '../../ui/button';

export function userDisplayName(user: Pick<CompanyDirectoryUser, 'firstName' | 'lastName' | 'username'>): string {
  return `${user.firstName ?? ''} ${user.lastName ?? ''}`.trim() || user.username || 'کاربر بدون نام';
}

type Props = {
  companyId: string;
  onChoose: (user: CompanyDirectoryUser) => void;
  onUnauthenticated: (error: unknown) => boolean;
};

type Results =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'ready'; items: CompanyDirectoryUser[]; total: number }
  | { kind: 'error'; message: string; forbidden: boolean };

const searchLimit = 25;
const number = new Intl.NumberFormat('fa-IR');

/**
 * Searches the users of the chosen company (`GET /companies/{companyId}/users`, permission `users.list`). Every
 * membership is listed, active or not, so the page can state honestly that an inactive membership gives no
 * operational access. `companyId` only goes into the URL; the server decides whether it may be read.
 */
export function AccessUserPicker({ companyId, onChoose, onUnauthenticated }: Props) {
  const searchId = useId();
  const [term, setTerm] = useState('');
  const [results, setResults] = useState<Results>({ kind: 'idle' });
  const controllerRef = useRef<AbortController | null>(null);

  useEffect(() => () => controllerRef.current?.abort(), []);

  const search = async (event: FormEvent) => {
    event.preventDefault();
    controllerRef.current?.abort();
    const controller = new AbortController();
    controllerRef.current = controller;
    setResults({ kind: 'loading' });
    try {
      const page = await getCompanyUsers(companyId, { search: term, page: 1, pageSize: searchLimit }, controller.signal);
      if (!controller.signal.aborted) setResults({ kind: 'ready', items: page.items, total: page.totalCount });
    } catch (cause) {
      if (controller.signal.aborted || onUnauthenticated(cause)) return;
      const forbidden = cause instanceof ApiError && cause.status === 403;
      setResults({
        kind: 'error',
        forbidden,
        message: forbidden
          ? 'برای جستجوی کاربران شرکت به مجوز «مشاهده کاربران شرکت» نیاز است؛ بدون انتخاب کاربر هم می‌توانید مجوزهای نقش‌ها را مدیریت کنید.'
          : cause instanceof ApiError
            ? cause.message
            : 'جستجوی کاربران انجام نشد.',
      });
    }
  };

  return (
    <div className="space-y-3">
      <form onSubmit={search} role="search" aria-label="جستجوی کاربر شرکت" className="flex flex-wrap items-end gap-2">
        <FormField label="جستجوی کاربر" className="w-full sm:w-80" hint="نام، نام خانوادگی یا نام کاربری؛ خالی = ۲۵ کاربر اول.">
          <Input id={searchId} value={term} autoComplete="off" maxLength={120} onChange={(event) => setTerm(event.target.value)} />
        </FormField>
        <Button type="submit" size="lg" variant="outline" disabled={results.kind === 'loading'} aria-busy={results.kind === 'loading' || undefined}>
          جستجو
        </Button>
      </form>
      {results.kind === 'loading' ? <p role="status" className="text-sm text-sub">در حال جستجو…</p> : null}
      {results.kind === 'error' ? <Alert tone={results.forbidden ? 'info' : 'error'}>{results.message}</Alert> : null}
      {results.kind === 'ready' && results.items.length === 0 ? <p role="status" className="text-sm text-sub">کاربری با این مشخصات در شرکت پیدا نشد.</p> : null}
      {results.kind === 'ready' && results.items.length > 0 ? (
        <>
          <ul aria-label="نتایج جستجوی کاربران" className="divide-y divide-line rounded-xl border border-line bg-white">
            {results.items.map((user) => (
              <li key={user.userId} className="flex flex-wrap items-center gap-3 p-3">
                <div className="min-w-0 flex-1">
                  <p className="font-semibold text-ink">{userDisplayName(user)}</p>
                  <p className="text-xs text-sub">
                    {user.username ? <bdi dir="ltr">{user.username}</bdi> : null} · نقش فعلی: {user.roleName}
                  </p>
                </div>
                <Badge tone={user.membershipIsActive ? 'success' : 'neutral'} icon={null}>
                  {user.membershipIsActive ? 'عضویت فعال' : 'عضویت غیرفعال'}
                </Badge>
                <Button type="button" size="lg" variant="outline" aria-label={`انتخاب ${userDisplayName(user)}`} onClick={() => onChoose(user)}>
                  انتخاب
                </Button>
              </li>
            ))}
          </ul>
          {results.total > results.items.length ? (
            <p className="text-xs text-sub">فقط {number.format(results.items.length)} کاربر از {number.format(results.total)} نمایش داده شد؛ جستجو را دقیق‌تر کنید.</p>
          ) : null}
        </>
      ) : null}
    </div>
  );
}
