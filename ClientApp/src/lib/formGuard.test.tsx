import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { StrictMode } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { PaneTabKeyContext } from '@/components/shell/WorkspaceTabs';
import {
  formStatusLabel,
  formEntries,
  hasAnyProblemForm,
  problemForms,
  problemSummary,
  resetFormsForTests,
  tabProblemForms,
  tabStatus,
  useFormGuard,
  useFormRegistryVersion,
  workspaceStatus,
  worstFormStatus,
  type FormStatus,
} from './formGuard';

afterEach(() => resetFormsForTests());

type FormProps = {
  dirty?: boolean;
  saving?: boolean;
  errorMessage?: string | null;
  title?: string;
  dirtyMessage?: string;
  tabKey?: string | null;
};

/** A tiny form wired to the shared guard exactly like the real pages (`tabKey` is scoped by `renderForm`). */
function Form({ dirty = false, saving = false, errorMessage = null, title, dirtyMessage }: Omit<FormProps, 'tabKey'>) {
  const guard = useFormGuard({
    dirty,
    saving,
    errorMessage,
    ...(title !== undefined ? { title } : {}),
    ...(dirtyMessage !== undefined ? { dirtyMessage } : {}),
  });
  return (
    <button type="button" onClick={guard.markClean}>
      {guard.status}
    </button>
  );
}

function renderForm(props: FormProps) {
  const { tabKey, ...guardProps } = props;
  const form = <Form {...guardProps} />;
  return render(tabKey !== undefined && tabKey !== null ? <PaneTabKeyContext.Provider value={tabKey}>{form}</PaneTabKeyContext.Provider> : form);
}

describe('four-state form lifecycle registry (Issue #444 stage 1; D-076)', () => {
  it('registers nothing for an initially clean form', () => {
    renderForm({ dirty: false, tabKey: '/admin/users/new' });
    expect(hasAnyProblemForm()).toBe(false);
    expect(tabStatus('/admin/users/new')).toBe('clean');
    expect(formEntries()).toHaveLength(0);
  });

  it('a real change makes the form Dirty', () => {
    const view = renderForm({ dirty: false, tabKey: '/admin/users/new' });
    view.rerender(
      <PaneTabKeyContext.Provider value="/admin/users/new">
        <Form dirty />
      </PaneTabKeyContext.Provider>,
    );
    expect(hasAnyProblemForm()).toBe(true);
    expect(tabStatus('/admin/users/new')).toBe('dirty');
    expect(formEntries()[0]?.status).toBe('dirty');
  });

  it('starting a save makes the form Saving (and the tab with it)', () => {
    const view = renderForm({ dirty: true, tabKey: '/a' });
    view.rerender(
      <PaneTabKeyContext.Provider value="/a">
        <Form dirty saving />
      </PaneTabKeyContext.Provider>,
    );
    expect(tabStatus('/a')).toBe('saving');
    expect(formEntries()[0]?.status).toBe('saving');
    expect(formEntries()[0]?.saveToken).not.toBeNull();
  });

  it('a confirmed successful save returns the form to Clean', () => {
    const view = renderForm({ dirty: true, saving: true, tabKey: '/a' });
    // The server confirmed: the form leaves Saving and reports clean (then unmounts/navigates).
    view.rerender(
      <PaneTabKeyContext.Provider value="/a">
        <Form dirty={false} saving={false} />
      </PaneTabKeyContext.Provider>,
    );
    expect(tabStatus('/a')).toBe('clean');
    expect(hasAnyProblemForm()).toBe(false);
  });

  it('a failed save turns the form Error and keeps it there (dirty is not silently cleared)', () => {
    const view = renderForm({ dirty: true, saving: true, tabKey: '/a' });
    view.rerender(
      <PaneTabKeyContext.Provider value="/a">
        <Form dirty errorMessage="ثبت کارمند انجام نشد." />
      </PaneTabKeyContext.Provider>,
    );
    expect(tabStatus('/a')).toBe('error');
    expect(formEntries()[0]?.message).toBe('ثبت کارمند انجام نشد.');
  });

  it('a successful retry clears the Error state', () => {
    const view = renderForm({ dirty: true, errorMessage: 'ذخیره انجام نشد.', tabKey: '/a' });
    expect(tabStatus('/a')).toBe('error');
    // Retry: Error -> Saving ...
    view.rerender(
      <PaneTabKeyContext.Provider value="/a">
        <Form dirty saving />
      </PaneTabKeyContext.Provider>,
    );
    expect(tabStatus('/a')).toBe('saving');
    // ... confirmed by the server: Saving -> Clean.
    view.rerender(
      <PaneTabKeyContext.Provider value="/a">
        <Form dirty={false} saving={false} />
      </PaneTabKeyContext.Provider>,
    );
    expect(tabStatus('/a')).toBe('clean');
    expect(hasAnyProblemForm()).toBe(false);
  });

  it('an unknown save outcome is never Clean on its own; only the explicit cancel clears it', async () => {
    const user = userEvent.setup();
    const view = renderForm({ dirty: true, saving: true, tabKey: '/a' });
    // The connection broke mid-save: the form reports the unknown-outcome failure message.
    view.rerender(
      <PaneTabKeyContext.Provider value="/a">
        <Form dirty errorMessage="نتیجهٔ ذخیره نامشخص است؛ پیش از ادامه، وضعیت رکورد را بررسی کنید." />
      </PaneTabKeyContext.Provider>,
    );
    expect(tabStatus('/a')).toBe('error');
    expect(hasAnyProblemForm()).toBe(true);
    // No automatic transition exists: without a user decision the status stays Error.
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(tabStatus('/a')).toBe('error');
    // The explicit user decision (cancel the changes) is the only path back to Clean.
    await user.click(screen.getByRole('button'));
    view.rerender(
      <PaneTabKeyContext.Provider value="/a">
        <Form dirty={false} errorMessage={null} />
      </PaneTabKeyContext.Provider>,
    );
    expect(tabStatus('/a')).toBe('clean');
  });

  it('several forms on one tab aggregate to the worst status (error > saving > dirty > clean)', () => {
    render(
      <PaneTabKeyContext.Provider value="/a">
        <Form dirty />
        <Form saving />
        <Form dirty={false} />
      </PaneTabKeyContext.Provider>,
    );
    expect(tabStatus('/a')).toBe('saving');
    expect(tabProblemForms('/a')).toHaveLength(2);
    // One clean form per tab never creates a problem entry.
    expect(problemForms()).toHaveLength(2);
  });

  it('Error outranks Saving and Dirty on the same tab', () => {
    render(
      <PaneTabKeyContext.Provider value="/a">
        <Form dirty />
        <Form errorMessage="خطا" />
        <Form saving />
      </PaneTabKeyContext.Provider>,
    );
    expect(tabStatus('/a')).toBe('error');
    expect(workspaceStatus()).toBe('error');
  });

  it('forms on two tabs stay independent', () => {
    render(
      <>
        <PaneTabKeyContext.Provider value="/a">
          <Form dirty />
        </PaneTabKeyContext.Provider>
        <PaneTabKeyContext.Provider value="/b">
          <Form dirty={false} />
        </PaneTabKeyContext.Provider>
      </>,
    );
    expect(tabStatus('/a')).toBe('dirty');
    expect(tabStatus('/b')).toBe('clean');
    expect(workspaceStatus()).toBe('dirty');
  });

  it('unmount removes exactly this form and never touches the other one', () => {
    const first = renderForm({ dirty: true, tabKey: '/a' });
    const second = render(
      <PaneTabKeyContext.Provider value="/a">
        <Form dirty />
      </PaneTabKeyContext.Provider>,
    );
    expect(tabProblemForms('/a')).toHaveLength(2);
    first.unmount();
    expect(tabStatus('/a')).toBe('dirty');
    expect(tabProblemForms('/a')).toHaveLength(1);
    second.unmount();
    expect(tabStatus('/a')).toBe('clean');
  });

  it('React Strict Mode never double-registers a form', () => {
    render(
      <StrictMode>
        <PaneTabKeyContext.Provider value="/a">
          <Form dirty title="فرم" />
        </PaneTabKeyContext.Provider>
      </StrictMode>,
    );
    expect(tabProblemForms('/a')).toHaveLength(1);
    expect(formEntries()[0]?.title).toBe('فرم');
  });

  it.each<FormStatus>(['dirty', 'saving', 'error'])('arms beforeunload while %s and disarms when clean', (status) => {
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
      const dirty = status === 'dirty';
      const saving = status === 'saving';
      const errorMessage = status === 'error' ? 'خطا' : null;
      const view = renderForm({ dirty, saving, errorMessage, tabKey: '/a' });
      expect(unloadListeners).toBe(1);
      view.rerender(
        <PaneTabKeyContext.Provider value="/a">
          <Form dirty={false} saving={false} errorMessage={null} />
        </PaneTabKeyContext.Provider>,
      );
      expect(unloadListeners).toBe(0);
    } finally {
      addSpy.mockRestore();
      removeSpy.mockRestore();
    }
  });

  it('never writes form data to localStorage or sessionStorage', () => {
    renderForm({ dirty: true, saving: false, errorMessage: 'خطای ذخیره', title: 'فرم کاربر', tabKey: '/a' });
    expect(window.localStorage.length).toBe(0);
    expect(window.sessionStorage.length).toBe(0);
    // And the registry entry itself carries no form values, only the status record.
    const entry = formEntries()[0]!;
    expect(Object.keys(entry).sort()).toEqual(['id', 'message', 'saveToken', 'status', 'tabKey', 'title']);
  });

  it('summarises one tab and several tabs in Persian for the dialogs', () => {
    expect(problemSummary()).toBe('');
    renderForm({ dirty: true, tabKey: '/a', dirtyMessage: 'فرم کاربر تغییرات ذخیره‌نشده دارد.' });
    expect(problemSummary()).toBe('فرم کاربر تغییرات ذخیره‌نشده دارد.');
    renderForm({ dirty: true, tabKey: '/b', dirtyMessage: 'فرم واحد سازمانی تغییرات ذخیره‌نشده دارد.' });
    expect(problemSummary()).toContain('۲ فرم');
    expect(problemSummary()).toContain('۲ تب');
    renderForm({ saving: true, tabKey: '/c' });
    expect(problemSummary()).toContain('۳ تب');
  });

  it('registers under a fixed key outside the tab shell, so global guards still protect', () => {
    renderForm({ dirty: true });
    expect(tabStatus('single-page')).toBe('dirty');
    expect(hasAnyProblemForm()).toBe(true);
  });

  it('reports Persian status labels for every state', () => {
    expect(formStatusLabel.clean).toBe('بدون تغییر ذخیره‌نشده');
    expect(formStatusLabel.dirty).toBe('دارای تغییر ذخیره‌نشده');
    expect(formStatusLabel.saving).toBe('در حال ذخیره');
    expect(formStatusLabel.error).toBe('خطا در ذخیره');
  });

  it('ranks statuses for the roll-up and lets subscribers observe changes', async () => {
    expect(worstFormStatus('clean', 'dirty')).toBe('dirty');
    expect(worstFormStatus('saving', 'dirty')).toBe('saving');
    expect(worstFormStatus('error', 'saving')).toBe('error');

    const versions: number[] = [];
    function Probe() {
      versions.push(useFormRegistryVersion());
      return null;
    }
    const view = render(<Probe />);
    const before = versions[versions.length - 1]!;
    view.rerender(
      <PaneTabKeyContext.Provider value="/a">
        <Form dirty />
        <Probe />
      </PaneTabKeyContext.Provider>,
    );
    await Promise.resolve();
    expect(versions[versions.length - 1]!).toBeGreaterThan(before);
  });

  it('keeps markClean working for the confirmed-save moment and explicit cancels', async () => {
    const user = userEvent.setup();
    renderForm({ dirty: true, tabKey: '/a' });
    expect(tabStatus('/a')).toBe('dirty');
    await user.click(screen.getByRole('button'));
    expect(tabStatus('/a')).toBe('clean');
    expect(hasAnyProblemForm()).toBe(false);
  });
});
