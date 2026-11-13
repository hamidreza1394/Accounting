import { usePageBreadcrumb } from '@/components/shell/PageBreadcrumb';
import { useDocumentTitle } from '@/lib/router';
import { createProduct } from '../api/tradeClient';
import { ProductEditor } from '../components/ProductEditor';
import { useLeaveTo } from '../components/useLeaveTo';
import { setProductFlash } from '../lib/productFlash';
import { invalidateProductList } from '../lib/productListRefresh';
import { emptyProductForm } from '../lib/productValidation';

type Props = { onUnauthenticated: (error: unknown) => boolean };

/** Creating a product (`POST /trade/products`). After a successful save the details open with a success notice. */
export function ProductCreatePage({ onUnauthenticated }: Props) {
  const leaveTo = useLeaveTo();
  useDocumentTitle('کالای جدید');
  usePageBreadcrumb([{ label: 'بازرگانی خارجی' }, { label: 'کالاها', to: '/trade/products' }, { label: 'کالای جدید' }]);

  return (
    <div className="space-y-5">
      <header className="space-y-2">
        <h1 className="text-2xl font-extrabold tracking-tight text-title">کالای جدید</h1>
        <p className="text-sm text-sub">کد کالا در این شرکت یکتاست. واحد پایه از فهرست مرجع واحدها انتخاب می‌شود.</p>
      </header>
      <ProductEditor
        guardTitle="کالای جدید"
        baseline={emptyProductForm}
        submitLabel="ثبت کالا"
        savingLabel="در حال ثبت…"
        save={createProduct}
        onUnauthenticated={onUnauthenticated}
        onSaved={(product) => {
          setProductFlash(product.id, 'created');
          invalidateProductList();
          leaveTo(`/trade/products/${product.id}`);
        }}
        onExit={() => leaveTo('/trade/products')}
      />
    </div>
  );
}
