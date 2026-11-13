import { PackageX, ShieldAlert } from 'lucide-react';
import { ErrorState, LoadingState } from '@/components/Feedback';
import { buttonVariants } from '@/components/ui/button';
import { Link } from '@/lib/router';
import { tradeTexts } from '../lib/tradeLabels';
import { useTradeAccess, type TradePermission } from '../access/TradeAccess';
import type { ReactNode } from 'react';

/** The friendly state for «the Trade module cannot be used here» (not licensed, or no valid work context). */
export function TradeModuleUnavailable({ reason = 'module' }: { reason?: 'module' | 'context' }) {
  return (
    <section role="status" className="mx-auto max-w-xl rounded-xl border border-line bg-white p-8 text-center">
      <PackageX aria-hidden="true" className="mx-auto text-sub" size={40} />
      <h1 className="mt-3 text-xl font-extrabold text-title">{tradeTexts.moduleUnavailableTitle}</h1>
      <p className="mt-2 text-sm leading-7 text-sub">
        {reason === 'context' ? tradeTexts.contextRequiredBody : tradeTexts.moduleUnavailableBody}
      </p>
      <Link to="/" className={buttonVariants({ variant: 'outline', size: 'lg', className: 'mt-5' })}>
        بازگشت به داشبورد
      </Link>
    </section>
  );
}

/** The panel for a missing permission. */
export function TradeForbidden() {
  return (
    <section role="alert" className="mx-auto max-w-xl rounded-xl border border-line bg-white p-8 text-center">
      <ShieldAlert aria-hidden="true" className="mx-auto text-sub" size={40} />
      <h1 className="mt-3 text-xl font-extrabold text-title">{tradeTexts.forbiddenTitle}</h1>
      <p className="mt-2 text-sm leading-7 text-sub">{tradeTexts.forbiddenBody}</p>
      <Link to="/" className={buttonVariants({ variant: 'outline', size: 'lg', className: 'mt-5' })}>
        بازگشت به داشبورد
      </Link>
    </section>
  );
}

/**
 * The route guard of every Trade page: fail-closed on the capabilities hint (loading, error,
 * module unavailable, missing permission). The server still authorizes every request.
 */
export function TradeRouteGuard({ permission, children }: { permission: TradePermission; children: ReactNode }) {
  const access = useTradeAccess();
  if (access.status === 'loading') return <LoadingState label={tradeTexts.loadingAccess} />;
  if (access.status === 'error') return <ErrorState message={tradeTexts.capabilitiesError} onRetry={access.retry} />;
  if (access.status === 'unavailable') return <TradeModuleUnavailable reason={access.unavailableReason ?? 'module'} />;
  if (!access.can(permission)) return <TradeForbidden />;
  return <>{children}</>;
}
