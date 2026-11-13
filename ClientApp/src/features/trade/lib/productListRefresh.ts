import { createListVersion } from './listVersion';

const listVersion = createListVersion();

export const invalidateProductList = listVersion.invalidate;

/** Changes whenever a product was created or updated; put it in the list's load effect dependencies. */
export const useProductListVersion = listVersion.useVersion;
