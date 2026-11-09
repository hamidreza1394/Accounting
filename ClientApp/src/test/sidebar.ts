import { screen, within } from '@testing-library/react';
import type { UserEvent } from '@testing-library/user-event';

/**
 * The sidebar starts with every group folded and only an explicit click opens one. A test that
 * needs an entry of «مدیریت سیستم» opens the group the way a user does; it never relies on the
 * active route to have opened it.
 */
export async function openSystemMenu(user: UserEvent, nav?: HTMLElement): Promise<HTMLElement> {
  const scope = nav ? within(nav) : screen;
  const group = await scope.findByRole('button', { name: /مدیریت سیستم/ });
  if (group.getAttribute('aria-expanded') !== 'true') await user.click(group);
  return group;
}
