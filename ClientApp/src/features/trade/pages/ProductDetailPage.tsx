import { useEffect, useState, type ReactNode } from 'react';
import { usePageBreadcrumb } from '@/components/shell/PageBreadcrumb';
import { Alert } from '@/components/ui/Alert';
import { buttonVariants } from '@/components/ui/button';
import { StatusBadge } from '@/components/ui/StatusBadge';
import { ErrorState, LoadingState } from '@/components/Feedback';
import { Link, useDocumentTitle, useParams } from '@/lib/router';
import { isAbortError } from '@/lib/staleGuard';
import { useTradeAccess } from '../access/TradeAccess';
import { getProduct } from '../api/tradeClient';
import type { ProductDetail } from '../api/tradeTypes';
import { TradeForbidden, TradeModuleUnavailable } from '../components/TradeStates';
import { useUnits } from '../components/useUnits';
import { clearProductFlash, peekProductFlash, productFlashText } from '../lib/productFlash';
import type { ProductFieldKey } from '../lib/productValidation';
import { isModuleBlocked, mapProductFailure, type TradeFailure } from '../lib/tradeErrors';
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
 * The product details (`GET /trade/products/{id}`). Read-only: there is no delete, activate or deactivate
 * action (D-083 is not decided); `isActive` is only shown.
 */
export function ProductDetailPage({ onUnauthenticated }: Props) {
  const { id = '' } = useParams();
  const access = useTradeAccess();
  const canUpdate = access.can('product.update');
  const units = useUnits(onUnauthenticated);

  const [product, setProduct] = useState<ProductDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [failure, setFailure] = useState<TradeFailure<ProductFieldKey> | null>(null);
  const [attempt, setAttempt] = useState(0);
  const [flash] = useState(() => peekProductFlash(id));
  useEffect(() => clearProductFlash, []);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setFailure(null);
    getProduct(id, controller.signal)
      .then((detail) => {
        if (controller.signal.aborted) return;
        setProduct(detail);
        setLoading(false);
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || isAbortError(cause) || onUnauthenticated(cause)) return;
        setProduct(null);
        setFailure(mapProductFailure(cause));
        setLoading(false);
      });
    return () => controller.abort();
  }, [id, attempt, onUnauthenticated]);

  useDocumentTitle(product ? product.name : null);
  usePageBreadcrumb([
    { label: 'بازرگانی خارجی' },
    { label: 'کالاها', to: '/trade/products' },
    { label: product ? product.name : 'جزئیات کالا' },
  ]);

  if (loading) return <LoadingState label="در حال بارگذاری کالا…" />;
  if (isModuleBlocked(failure)) return <TradeModuleUnavailable reason={failure?.kind === 'context-required' ? 'context' : 'module'} />;
  if (failure?.kind === 'forbidden') return <TradeForbidden />;
  if (failure?.kind === 'not-found') {
    return (
      <section role="alert" className="mx-auto max-w-xl rounded-xl border border-line bg-white p-8 text-center">
        <h1 className="text-xl font-extrabold text-title">کالا پیدا نشد</h1>
        <p className="mt-2 text-sm leading-7 text-sub">{failure.message}</p>
        <Link to="/trade/products" className={buttonVariants({ variant: 'outline', size: 'lg', className: 'mt-5' })}>
          بازگشت به فهرست
        </Link>
      </section>
    );
  }
  if (failure || product === null) {
    return <ErrorState message={failure?.message ?? 'دریافت کالا انجام نشد.'} onRetry={() => setAttempt((value) => value + 1)} />;
  }

  const unitName = units.units.find((unit) => unit.code === product.baseUnitCode)?.nameFa;

  return (
    <div className="space-y-5">
      <header className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap items-center gap-3">
          <h1 className="text-2xl font-extrabold tracking-tight text-title">{product.name}</h1>
          <bdi dir="ltr" className="whitespace-nowrap rounded-md bg-cool px-2 py-1 font-mono text-xs text-sub">
            {product.code}
          </bdi>
          <StatusBadge status={product.isActive ? 'active' : 'inactive'} />
        </div>
        <div className="flex flex-wrap gap-2">
          {canUpdate ? (
            <Link to={`/trade/products/${product.id}/edit`} className={buttonVariants({ size: 'lg' })}>
              ویرایش
            </Link>
          ) : null}
          <Link to="/trade/products" className={buttonVariants({ variant: 'outline', size: 'lg' })}>
            بازگشت به فهرست
          </Link>
        </div>
      </header>

      {flash ? <Alert tone="info">{productFlashText[flash]}</Alert> : null}

      <section aria-labelledby="product-identity" className="rounded-xl border border-line bg-white p-5">
        <h2 id="product-identity" className="text-base font-bold text-ink">
          مشخصات
        </h2>
        <dl className="mt-4 grid gap-4 sm:grid-cols-2">
          <Field label="کد">
            <bdi dir="ltr" className="font-mono">
              {product.code}
            </bdi>
          </Field>
          <Field label="نام">{product.name}</Field>
        </dl>
      </section>

      <section aria-labelledby="product-classification" className="rounded-xl border border-line bg-white p-5">
        <h2 id="product-classification" className="text-base font-bold text-ink">
          طبقه‌بندی
        </h2>
        <dl className="mt-4 grid gap-4 sm:grid-cols-2">
          <Field label="کد گروه کالا">
            {product.productGroupCode ? (
              <bdi dir="ltr" className="font-mono">
                {product.productGroupCode}
              </bdi>
            ) : (
              empty
            )}
          </Field>
          <Field label="کد تعرفه (HS)">
            {product.hsCode ? (
              <bdi dir="ltr" className="font-mono">
                {product.hsCode}
              </bdi>
            ) : (
              empty
            )}
          </Field>
        </dl>
      </section>

      <section aria-labelledby="product-unit" className="rounded-xl border border-line bg-white p-5">
        <h2 id="product-unit" className="text-base font-bold text-ink">
          واحد
        </h2>
        <dl className="mt-4">
          <Field label="واحد پایه">
            <span className="flex items-center gap-2">
              <bdi dir="ltr" className="font-mono text-xs text-sub">
                {product.baseUnitCode}
              </bdi>
              {unitName ?? null}
            </span>
          </Field>
        </dl>
      </section>

      <section aria-labelledby="product-audit" className="rounded-xl border border-line bg-white p-5">
        <h2 id="product-audit" className="text-base font-bold text-ink">
          اطلاعات ثبت
        </h2>
        <dl className="mt-4 grid gap-4 sm:grid-cols-2">
          <Field label="تاریخ ثبت">{formatTradeInstant(product.createdAtUtc)}</Field>
          <Field label="آخرین ویرایش">{product.updatedAtUtc ? formatTradeInstant(product.updatedAtUtc) : empty}</Field>
        </dl>
      </section>
    </div>
  );
}
