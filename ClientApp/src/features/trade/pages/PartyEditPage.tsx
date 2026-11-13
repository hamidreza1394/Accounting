import { useEffect, useMemo, useState } from 'react';
import { usePageBreadcrumb } from '@/components/shell/PageBreadcrumb';
import { buttonVariants } from '@/components/ui/button';
import { ErrorState, LoadingState } from '@/components/Feedback';
import { Link, useDocumentTitle, useParams } from '@/lib/router';
import { isAbortError } from '@/lib/staleGuard';
import { getParty, updateParty } from '../api/tradeClient';
import type { PartyDetail } from '../api/tradeTypes';
import { PartyEditor } from '../components/PartyEditor';
import { TradeForbidden, TradeModuleUnavailable } from '../components/TradeStates';
import { useLeaveTo } from '../components/useLeaveTo';
import { formFromDetail } from '../lib/partyValidation';
import { setPartyFlash } from '../lib/partyFlash';
import { invalidatePartyList } from '../lib/partyListRefresh';
import { isModuleBlocked, mapTradeFailure, type TradeFailure } from '../lib/tradeErrors';
import { partyTitle } from '../lib/tradeLabels';

type Props = { onUnauthenticated: (error: unknown) => boolean };

/**
 * Editing a party (`PUT /trade/parties/{id}`). The `rowVersion` of the last read travels with the save; a
 * concurrent change answers 409 `trade_concurrency_conflict` and the page offers to read the fresh data (the
 * form is then rebuilt from it). The roles are a full replacement of the set.
 */
export function PartyEditPage({ onUnauthenticated }: Props) {
  const { id = '' } = useParams();
  const leaveTo = useLeaveTo();
  const [party, setParty] = useState<PartyDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [failure, setFailure] = useState<TradeFailure | null>(null);
  const [attempt, setAttempt] = useState(0);

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

  const baseline = useMemo(() => (party ? formFromDetail(party) : null), [party]);
  useDocumentTitle(party ? `ویرایش ${partyTitle(party)}` : null);
  usePageBreadcrumb([
    { label: 'بازرگانی خارجی' },
    { label: 'طرف‌حساب‌ها', to: '/trade/parties' },
    party ? { label: partyTitle(party), to: `/trade/parties/${party.id}` } : { label: 'طرف‌حساب' },
    { label: 'ویرایش' },
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
  if (failure || party === null || baseline === null) {
    return <ErrorState message={failure?.message ?? 'دریافت طرف‌حساب انجام نشد.'} onRetry={() => setAttempt((value) => value + 1)} />;
  }

  const detailPath = `/trade/parties/${party.id}`;
  return (
    <div className="space-y-5">
      <header className="space-y-2">
        <h1 className="text-2xl font-extrabold tracking-tight text-title">ویرایش طرف‌حساب</h1>
        <p className="text-sm text-sub">
          کد <bdi dir="ltr" className="font-mono">{party.code}</bdi> را در صورت نیاز می‌توانید تغییر دهید؛ تکراری بودن آن در همین شرکت بررسی می‌شود.
        </p>
      </header>
      <PartyEditor
        guardTitle={`ویرایش ${partyTitle(party)}`}
        baseline={baseline}
        submitLabel="ذخیرهٔ تغییرات"
        savingLabel="در حال ذخیره…"
        save={(body) => updateParty(party.id, body, party.rowVersion)}
        onUnauthenticated={onUnauthenticated}
        onRefresh={async () => {
          // A silent re-read: the page stays mounted (no loading state), only the stored row version/baseline move on.
          const fresh = await getParty(party.id);
          setParty(fresh);
          return fresh;
        }}
        onSaved={(saved) => {
          setPartyFlash(saved.id, 'updated');
          invalidatePartyList();
          leaveTo(detailPath);
        }}
        onExit={() => leaveTo(detailPath)}
      />
    </div>
  );
}
