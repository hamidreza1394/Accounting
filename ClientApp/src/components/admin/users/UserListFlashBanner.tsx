import { useSyncExternalStore } from 'react';
import { Alert } from '../../ui/Alert';
import { Button } from '../../ui/button';
import { clearUserListFlash, readUserListFlash, subscribeUserListFlash, userFlashText, userFlashWarnings } from './userFlash';

/**
 * The outcome of a create, shown on the users list the form returned to. A half-finished creation
 * (personnel record or photo missing) is a warning, never plain success. Memory only; the user
 * can dismiss it and it expires on its own.
 */
export function UserListFlashBanner() {
  const flash = useSyncExternalStore(subscribeUserListFlash, readUserListFlash, () => null);
  if (!flash) return null;
  return (
    <div data-testid="user-list-flash">
      <Alert
        tone={userFlashWarnings.includes(flash.kind) ? 'warning' : 'info'}
        action={
          <Button type="button" variant="outline" size="sm" onClick={clearUserListFlash}>
            بستن پیام
          </Button>
        }
      >
        {userFlashText[flash.kind]}
      </Alert>
    </div>
  );
}
