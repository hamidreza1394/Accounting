import { useEffect, useState, type ReactNode } from 'react';
import { usePageBreadcrumb } from '@/components/shell/PageBreadcrumb';
import { Alert } from '@/components/ui/Alert';
import { buttonVariants } from '@/components/ui/button';
import { StatusBadge } from '@/components/ui/StatusBadge';
import { ErrorState, LoadingState } from '@/components/Feedback';
import { Link, useDocumentTitle, useParams } from '@/lib/router';
import { isAbortError } from '@/lib/staleGuard';
import { useTradeAccess } from '../access/TradeAccess';
import { getParty } from '../api/tradeClient';
import type { PartyDetail } from '../api/tradeTypes';
import { PartyRoleBadges } from '../components/PartyRoleBadges';
import { TradeForbidden, TradeModuleUnavailable } from '../components/TradeStates';
import { useCountries } from '../components/useCountries';
import { clearPartyFlash, partyFlashText, peekPartyFlash } from '../lib/partyFlash';
import { isModuleBlocked, mapTradeFailure, type TradeFailure } from '../lib/tradeErrors';
import { partyTitle } from '../lib/tradeLabels';
import { formatTradeInstant } from '../lib/tradeFormat';

type Props = { onUnauthenticated: (error: unknown) => boolean };

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex flex-col gap-1">
      <dt className="text-xs font-semibold text-sub">{label}</dt>
      <dd className="min-h-6 text-sm text-ink">{children}</dd>
    </div>
  );
}

const empty = <span className="text-sub">ثبت نشده</span>;

/**
 * The party details (`GET /trade/parties/{id}`). Read-only: there is no delete, activate or deactivate
 * action (D-083 is not decided); `isActive` is only shown. The legal identifier appears here and in the edit
 * form only — never in a URL, a log or an error text.
 */
export function PartyDetailPage({ onUnauthenticated }: Props) {
  const { id = '' } = useParams();
  const access = useTradeAccess();
  const canUpdate = access.can('party.update');
  const countries = useCountries(onUnauthenticated);

  const [party, setParty] = useState<PartyDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [failure, setFailure] = useState<TradeFailure | null>(null);
  const [attempt, setAttempt] = useState(0);
  const [flash] = useState(() => peekPartyFlash(id));
  useEffect(() => clearPartyFlash, []);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setFailure(null);
    getParty(id, controller.signal)
      .then((detail) => {
        if (controller.signal.aborted) return;
        setParty(detail);
        setLoading(false);
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || isAbortError(cause) || onUnauthenticated(cause)) return;
        setParty(null);
        setFailure(mapTradeFailure(cause));
        setLoading(false);
      });
    return () => controller.abort();
  }, [id, attempt, onUnauthenticated]);

  useDocumentTitle(party ? partyTitle(party) : null);
  usePageBreadcrumb([
    { label: 'بازرگانی خارجی' },
    { label: 'طرف‌حساب‌ها', to: '/trade/parties' },
    { label: party ? partyTitle(party) : 'جزئیات طرف‌حساب' },
  ]);

  if (loading) return <LoadingState label="در حال بارگذاری طرف‌حساب…" />;
  if (isModuleBlocked(failure)) return <TradeModuleUnavailable reason={failure?.kind === 'context-required' ? 'context' : 'module'} />;
  if (failure?.kind === 'forbidden') return <TradeForbidden />;
  if (failure?.kind === 'not-found') {
    return (
      <section role="alert" className="mx-auto max-w-xl rounded-xl border border-line bg-white p-8 text-center">
        <h1 className="text-xl font-extrabold text-title">طرف‌حساب پیدا نشد</h1>
        <p className="mt-2 text-sm leading-7 text-sub">{failure.message}</p>
        <Link to="/trade/parties" className={buttonVariants({ variant: 'outline', size: 'lg', className: 'mt-5' })}>
          بازگشت به فهرست
        </Link>
      </section>
    );
  }
  if (failure || party === null) {
    return <ErrorState message={failure?.message ?? 'دریافت طرف‌حساب انجام نشد.'} onRetry={() => setAttempt((value) => value + 1)} />;
  }

  const countryName = party.countryCode ? countries.countries.find((country) => country.code === party.countryCode)?.nameFa : undefined;

  return (
    <div className="space-y-5">
      <header className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap items-center gap-3">
          <h1 className="text-2xl font-extrabold tracking-tight text-title">{partyTitle(party)}</h1>
          <bdi dir="ltr" className="rounded-md bg-cool px-2 py-1 font-mono text-xs text-sub">
            {party.code}
          </bdi>
          <StatusBadge status={party.isActive ? 'active' : 'inactive'} />
        </div>
        <div className="flex flex-wrap gap-2">
          {canUpdate ? (
            <Link to={`/trade/parties/${party.id}/edit`} className={buttonVariants({ size: 'lg' })}>
              ویرایش
            </Link>
          ) : null}
          <Link to="/trade/parties" className={buttonVariants({ variant: 'outline', size: 'lg' })}>
            بازگشت به فهرست
          </Link>
        </div>
      </header>

      {flash ? <Alert tone="info">{partyFlashText[flash]}</Alert> : null}

      <section aria-labelledby="party-identity" className="rounded-xl border border-line bg-white p-5">
        <h2 id="party-identity" className="text-base font-bold text-ink">
          هویت
        </h2>
        <dl className="mt-4 grid gap-4 sm:grid-cols-2">
          <Field label="کد">
            <bdi dir="ltr" className="font-mono">
              {party.code}
            </bdi>
          </Field>
          <Field label="نام حقوقی">{party.legalName}</Field>
          <Field label="نام نمایشی">{party.displayName ?? empty}</Field>
          <Field label="شناسهٔ حقوقی">
            {party.legalIdentifier ? (
              <bdi dir="ltr" className="font-mono">
                {party.legalIdentifier}
              </bdi>
            ) : (
              empty
            )}
          </Field>
        </dl>
      </section>

      <section aria-labelledby="party-roles" className="rounded-xl border border-line bg-white p-5">
        <h2 id="party-roles" className="text-base font-bold text-ink">
          نقش‌ها
        </h2>
        <div className="mt-4">
          <PartyRoleBadges roles={party.roles} />
        </div>
      </section>

      <section aria-labelledby="party-country" className="rounded-xl border border-line bg-white p-5">
        <h2 id="party-country" className="text-base font-bold text-ink">
          کشور
        </h2>
        <dl className="mt-4">
          <Field label="کشور">
            {party.countryCode ? (
              <span className="flex items-center gap-2">
                <bdi dir="ltr" className="font-mono text-xs text-sub">
                  {party.countryCode}
                </bdi>
                {countryName ?? null}
              </span>
            ) : (
              empty
            )}
          </Field>
        </dl>
      </section>

      <section aria-labelledby="party-audit" className="rounded-xl border border-line bg-white p-5">
        <h2 id="party-audit" className="text-base font-bold text-ink">
          اطلاعات ثبت
        </h2>
        <dl className="mt-4 grid gap-4 sm:grid-cols-2">
          <Field label="تاریخ ثبت">{formatTradeInstant(party.createdAtUtc)}</Field>
          <Field label="آخرین ویرایش">{party.updatedAtUtc ? formatTradeInstant(party.updatedAtUtc) : empty}</Field>
        </dl>
      </section>
    </div>
  );
}
