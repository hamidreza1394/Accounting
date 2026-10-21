import type { SelectedContext, Session } from '../api/types';
import { CompanyOverviewPanel } from './CompanyOverviewPanel';

type DashboardProps = {
  session: Session;
  context: SelectedContext;
  onUnauthenticated: (error: unknown) => boolean;
};

/**
 * The first screen of the working environment. It works in the company and fiscal period the
 * user chose when entering; changing them is done from the context bar, never from the address.
 */
export function Dashboard({ session, context, onUnauthenticated }: DashboardProps) {
  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-bold text-navy">داشبورد</h1>
        <p className="text-muted">خوش آمدید، {session.fullName}.</p>
      </div>

      <CompanyOverviewPanel
        companyId={context.company.id}
        currentPeriodId={context.fiscalPeriod.id}
        onUnauthenticated={onUnauthenticated}
      />
    </div>
  );
}
