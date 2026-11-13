import { usePageBreadcrumb } from '@/components/shell/PageBreadcrumb';
import { useDocumentTitle } from '@/lib/router';
import { createParty } from '../api/tradeClient';
import { PartyEditor } from '../components/PartyEditor';
import { useLeaveTo } from '../components/useLeaveTo';
import { emptyPartyForm } from '../lib/partyValidation';
import { setPartyFlash } from '../lib/partyFlash';
import { invalidatePartyList } from '../lib/partyListRefresh';

type Props = { onUnauthenticated: (error: unknown) => boolean };

/** Creating a party (`POST /trade/parties`). After a successful save the details open with a success notice. */
export function PartyCreatePage({ onUnauthenticated }: Props) {
  const leaveTo = useLeaveTo();
  useDocumentTitle('طرف‌حساب جدید');
  usePageBreadcrumb([{ label: 'بازرگانی خارجی' }, { label: 'طرف‌حساب‌ها', to: '/trade/parties' }, { label: 'طرف‌حساب جدید' }]);

  return (
    <div className="space-y-5">
      <header className="space-y-2">
        <h1 className="text-2xl font-extrabold tracking-tight text-title">طرف‌حساب جدید</h1>
        <p className="text-sm text-sub">
          کد طرف‌حساب در این شرکت یکتاست. نقش‌ها مشخص می‌کنند طرف‌حساب در کدام بخش از فرایند بازرگانی انتخاب شود.
        </p>
      </header>
      <PartyEditor
        guardTitle="طرف‌حساب جدید"
        baseline={emptyPartyForm}
        submitLabel="ثبت طرف‌حساب"
        savingLabel="در حال ثبت…"
        save={createParty}
        onUnauthenticated={onUnauthenticated}
        onSaved={(party) => {
          setPartyFlash(party.id, 'created');
          invalidatePartyList();
          leaveTo(`/trade/parties/${party.id}`);
        }}
        onExit={() => leaveTo('/trade/parties')}
      />
    </div>
  );
}
