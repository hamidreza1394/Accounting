import { useEffect, useMemo, useState } from 'react';
import { usePageBreadcrumb } from '@/components/shell/PageBreadcrumb';
import { buttonVariants } from '@/components/ui/button';
import { ErrorState, LoadingState } from '@/components/Feedback';
import { Link, useDocumentTitle, useParams } from '@/lib/router';
import { isAbortError } from '@/lib/staleGuard';
import { getProduct, updateProduct } from '../api/tradeClient';
import type { ProductDetail } from '../api/tradeTypes';
import { ProductEditor } from '../components/ProductEditor';
import { TradeForbidden, TradeModuleUnavailable } from '../components/TradeStates';
import { useLeaveTo } from '../components/useLeaveTo';
import { setProductFlash } from '../lib/productFlash';
import { invalidateProductList } from '../lib/productListRefresh';
import { productFormFromDetail } from '../lib/productValidation';
import { isModuleBlocked, mapProductFailure, type TradeFailure } from '../lib/tradeErrors';
import type { ProductFieldKey } from '../lib/productValidation';

type Props = { onUnauthenticated: (error: unknown) => boolean };

/**
 * Editing a product (`PUT /trade/products/{id}`). The `rowVersion` of the last read travels with the save; a
 * concurrent change answers 409 `trade_concurrency_conflict` and the page offers to read the fresh data (the
 * form is then rebuilt from it).
 */
export function ProductEditPage({ onUnauthenticated }: Props) {
  const { id = '' } = useParams();
  const leaveTo = useLeaveTo();
  const [product, setProduct] = useState<ProductDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [failure, setFailure] = useState<TradeFailure<ProductFieldKey> | null>(null);
  const [attempt, setAttempt] = useState(0);

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

  const baseline = useMemo(() => (product ? productFormFromDetail(product) : null), [product]);
  useDocumentTitle(product ? `ویرایش ${product.name}` : null);
  usePageBreadcrumb([
    { label: 'بازرگانی خارجی' },
    { label: 'کالاها', to: '/trade/products' },
    product ? { label: product.name, to: `/trade/products/${product.id}` } : { label: 'کالا' },
    { label: 'ویرایش' },
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
  if (failure || product === null || baseline === null) {
    return <ErrorState message={failure?.message ?? 'دریافت کالا انجام نشد.'} onRetry={() => setAttempt((value) => value + 1)} />;
  }

  const detailPath = `/trade/products/${product.id}`;
  return (
    <div className="space-y-5">
      <header className="space-y-2">
        <h1 className="text-2xl font-extrabold tracking-tight text-title">ویرایش کالا</h1>
        <p className="text-sm text-sub">
          کد <bdi dir="ltr" className="font-mono">{product.code}</bdi> را در صورت نیاز می‌توانید تغییر دهید؛ تکراری بودن آن در همین شرکت بررسی می‌شود.
        </p>
      </header>
      <ProductEditor
        guardTitle={`ویرایش ${product.name}`}
        baseline={baseline}
        submitLabel="ذخیرهٔ تغییرات"
        savingLabel="در حال ذخیره…"
        save={(body) => updateProduct(product.id, body, product.rowVersion)}
        onUnauthenticated={onUnauthenticated}
        onRefresh={async () => {
          // A silent re-read: the page stays mounted (no loading state), only the stored row version/baseline move on.
          const fresh = await getProduct(product.id);
          setProduct(fresh);
          return fresh;
        }}
        onSaved={(saved) => {
          setProductFlash(saved.id, 'updated');
          invalidateProductList();
          leaveTo(detailPath);
        }}
        onExit={() => leaveTo(detailPath)}
      />
    </div>
  );
}
