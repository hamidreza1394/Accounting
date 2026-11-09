import { createAdminUser, ApiError } from '../../../api/client';
import { createHrEmployee, getNextEmployeeNumber } from '@/features/hr/api/hrClient';
import { normalizeText, toCreateRequest, type UserFormValues } from './userFormValidation';
import type { UserFlashKind } from './userFlash';

/** How many fresh suggestions are tried when the suggested number was taken in the meantime. */
const maxSuggestionRetries = 2;

export type CreateUserFlowInput = {
  values: UserFormValues;
  /** The work-context company the personnel record belongs to; `null` when none can be created. */
  companyId: string | null;
  /** The value the form pre-filled from `next-number`, to tell a suggestion from a typed number. */
  suggestedEmployeeNumber: string | null;
  /** Lets the app handle an expired session; returns true when it took over. */
  onUnauthenticated: (error: unknown) => boolean;
};

export type CreateUserFlowResult = { userId: string; flash: UserFlashKind; sessionExpired?: boolean };

/**
 * Creates the account and, when a personnel number was entered, the company's personnel record.
 * The two are separate resources (D-072): the record is created UNLINKED, because linking needs an
 * active membership of the company, which a new account does not have yet.
 *
 * Step 1 failures are thrown to the form (nothing was created). From the moment the account exists
 * this function never throws: a step 2 failure is reported through the returned flash kind, so the
 * created account is never hidden behind a form error.
 */
export async function createUserWithEmployee(input: CreateUserFlowInput): Promise<CreateUserFlowResult> {
  const { values, companyId, suggestedEmployeeNumber, onUnauthenticated } = input;
  const created = await createAdminUser(toCreateRequest(values));

  const typed = values.employeeNumber.trim();
  if (typed === '' || companyId === null) return { userId: created.id, flash: 'created' };

  const record = {
    firstName: normalizeText(values.firstName),
    lastName: normalizeText(values.lastName),
    hiredOnUtc: null,
  };
  let employeeNumber = typed;
  // A number the user typed is never replaced silently; only an untouched suggestion is renewed.
  let renewable = suggestedEmployeeNumber !== null && typed === suggestedEmployeeNumber;
  for (let attempt = 0; ; attempt += 1) {
    try {
      await createHrEmployee(companyId, { employeeNumber, ...record });
      return { userId: created.id, flash: 'createdWithEmployee' };
    } catch (cause) {
      if (cause instanceof ApiError && cause.status === 409) {
        if (!renewable || attempt >= maxSuggestionRetries) return { userId: created.id, flash: 'createdEmployeeDuplicate' };
        try {
          employeeNumber = (await getNextEmployeeNumber(companyId)).suggestedEmployeeNumber;
          renewable = true;
          continue;
        } catch (renewCause) {
          const expired = onUnauthenticated(renewCause);
          return { userId: created.id, flash: 'createdEmployeeFailed', sessionExpired: expired };
        }
      }
      const expired = onUnauthenticated(cause);
      return { userId: created.id, flash: 'createdEmployeeFailed', sessionExpired: expired };
    }
  }
}
