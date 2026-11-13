import { createListVersion } from './listVersion';

const listVersion = createListVersion();

export const invalidatePartyList = listVersion.invalidate;

/** Changes whenever a party was created or updated; put it in the list's load effect dependencies. */
export const usePartyListVersion = listVersion.useVersion;
