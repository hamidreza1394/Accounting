import type { AdminUserListItem } from '../../../api/types';

export const noName = 'بدون نام ثبت‌شده';

/** The display name of a user: first and last name, or a neutral label when both are empty. */
export function fullName(user: Pick<AdminUserListItem, 'firstName' | 'lastName'>): string {
  return [user.firstName, user.lastName].filter(Boolean).join(' ') || noName;
}
