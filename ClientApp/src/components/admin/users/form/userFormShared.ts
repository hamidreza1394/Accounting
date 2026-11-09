import { ApiError } from '@/api/client';
import { listHrEmployees } from '@/features/hr/api/hrClient';
import type { HrEmployeeListItem } from '@/features/hr/api/hrTypes';
import type { CanHint } from '@/lib/permissions';

/**
 * What the user form needs to know about the signed-in shell. Every field is a UI hint only: the
 * server decides each request (D-013, D-064). The defaults grant nothing (fail-closed).
 */
export type UserFormContext = {
  /** The validated work-context company; `null` when none is selected. */
  activeCompanyId: string | null;
  activeCompanyName: string | null;
  can: CanHint;
};

export const noUserFormContext: UserFormContext = {
  activeCompanyId: null,
  activeCompanyName: null,
  can: () => false,
};

/** The standalone «مجوزهای دسترسی» page (company, user, role, subsystem and form permissions). */
export const accessManagementPath = '/admin/access';

/** Per-user in-memory cache shared by the lazy tabs of the form (no storage, gone with the page). */
export type FormTabCache = Map<string, unknown>;

export const employeeLinkKey = (userId: string, companyId: string): string => `${userId}:form-hr-link:${companyId}`;
export const employeeDetailKey = (userId: string, employeeId: string): string => `${userId}:form-hr-detail:${employeeId}`;

/**
 * The personnel record linked to this user in one company (the stage 3 `userId` filter), or `null`
 * when none is linked. The server decides who may read it (403/404 reach the caller as `ApiError`).
 */
export async function loadLinkedEmployee(companyId: string, userId: string, signal: AbortSignal): Promise<HrEmployeeListItem | null> {
  const page = await listHrEmployees(companyId, { userId, page: 1, pageSize: 5 }, signal);
  return page.items[0] ?? null;
}

export function isForbidden(cause: unknown): boolean {
  return cause instanceof ApiError && (cause.status === 403 || cause.status === 404);
}
