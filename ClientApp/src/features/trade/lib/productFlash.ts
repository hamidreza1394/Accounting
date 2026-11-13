import { createFlashStore } from './tradeFlash';

// The one-shot message from a product form page to the detail page (see `tradeFlash.ts`).

export type ProductFlashKind = 'created' | 'updated';

const store = createFlashStore<ProductFlashKind>();

export const setProductFlash = store.set;
export const peekProductFlash = store.peek;
export const clearProductFlash = store.clear;

export const productFlashText: Record<ProductFlashKind, string> = {
  created: 'کالا با موفقیت ثبت شد.',
  updated: 'تغییرات کالا ذخیره شد.',
};
