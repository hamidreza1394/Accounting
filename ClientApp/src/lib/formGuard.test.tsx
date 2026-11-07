import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { PaneTabKeyContext } from '@/components/shell/WorkspaceTabs';
import {
  dirtyForms,
  dirtySummary,
  hasAnyDirtyForm,
  isTabDirty,
  resetDirtyFormsForTests,
  useFormGuard,
} from './formGuard';

afterEach(() => resetDirtyFormsForTests());

function Form({ dirty, message }: { dirty: boolean; message?: string | undefined }) {
  const guard = useFormGuard(dirty, message);
  return (
    <button type="button" onClick={guard.markClean}>
      {dirty ? 'dirty' : 'clean'}-{guard.dirty ? 'y' : 'n'}
    </button>
  );
}

function renderForm(props: { dirty: boolean; tabKey?: string | null; message?: string }) {
  const form = <Form dirty={props.dirty} message={props.message} />;
  return render(props.tabKey !== undefined ? <PaneTabKeyContext.Provider value={props.tabKey}>{form}</PaneTabKeyContext.Provider> : form);
}

describe('formGuard (shared dirty-form registry)', () => {
  it('registers a dirty form under its workspace tab and reports it', () => {
    renderForm({ dirty: true, tabKey: '/admin/users/new' });
    expect(hasAnyDirtyForm()).toBe(true);
    expect(isTabDirty('/admin/users/new')).toBe(true);
    expect(isTabDirty('/hr/org/units')).toBe(false);
    expect(dirtyForms()).toHaveLength(1);
  });

  it('cleans up when the page unmounts (no stale warnings)', () => {
    const view = renderForm({ dirty: true, tabKey: '/admin/users/new' });
    expect(hasAnyDirtyForm()).toBe(true);
    view.unmount();
    expect(hasAnyDirtyForm()).toBe(false);
  });

  it('follows the form: clean again after reset, and markClean clears immediately', async () => {
    const user = userEvent.setup();
    const view = render(<Form dirty />);
    expect(hasAnyDirtyForm()).toBe(true);
    await user.click(screen.getByRole('button'));
    expect(hasAnyDirtyForm()).toBe(false);
    view.rerender(<Form dirty={false} />);
    expect(hasAnyDirtyForm()).toBe(false);
  });

  it('warns about leaving the site only while dirty', () => {
    let unloadListeners = 0;
    const realAdd = window.addEventListener.bind(window);
    const realRemove = window.removeEventListener.bind(window);
    const addSpy = vi.spyOn(window, 'addEventListener').mockImplementation(((type: string, listener: EventListenerOrEventListenerObject, options?: boolean | AddEventListenerOptions) => {
      if (type === 'beforeunload') unloadListeners += 1;
      return realAdd(type, listener as EventListener, options);
    }) as typeof window.addEventListener);
    const removeSpy = vi.spyOn(window, 'removeEventListener').mockImplementation(((type: string, listener: EventListenerOrEventListenerObject, options?: boolean | EventListenerOptions) => {
      if (type === 'beforeunload') unloadListeners = Math.max(0, unloadListeners - 1);
      return realRemove(type, listener as EventListener, options);
    }) as typeof window.removeEventListener);
    try {
      const view = renderForm({ dirty: true, tabKey: '/admin/users/new' });
      expect(unloadListeners).toBe(1);
      view.rerender(
        <PaneTabKeyContext.Provider value="/admin/users/new">
          <Form dirty={false} />
        </PaneTabKeyContext.Provider>,
      );
      expect(unloadListeners).toBe(0);
    } finally {
      addSpy.mockRestore();
      removeSpy.mockRestore();
    }
  });

  it('summarises one tab and several tabs in Persian', () => {
    expect(dirtySummary()).toBe('');
    renderForm({ dirty: true, tabKey: '/a', message: 'فرم کاربر تغییرات ذخیره‌نشده دارد.' });
    expect(dirtySummary()).toBe('فرم کاربر تغییرات ذخیره‌نشده دارد.');
    renderForm({ dirty: true, tabKey: '/b', message: 'فرم واحد سازمانی تغییرات ذخیره‌نشده دارد.' });
    expect(dirtySummary()).toContain('۲ فرم');
    expect(dirtySummary()).toContain('۲ تب');
  });

  it('registers under a fixed key outside the tab shell, so global guards still protect', () => {
    renderForm({ dirty: true });
    expect(isTabDirty('single-page')).toBe(true);
  });
});
