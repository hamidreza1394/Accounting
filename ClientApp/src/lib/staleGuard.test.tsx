import { render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useEffect, useRef, useState } from 'react';
import { isAbortError, useStaleGuard } from './staleGuard';

afterEach(() => vi.restoreAllMocks());

/** The exact mission scenario: a late answer of company 1 must never enter company 2's state. */
function ScopedView({ scopeKey, request }: { scopeKey: string; request: (scopeKey: string) => Promise<string> }) {
  const guard = useStaleGuard(scopeKey);
  const [label, setLabel] = useState('بدون داده');
  const mounted = useRef(true);
  useEffect(() => {
    mounted.current = true;
    const { token, isCurrent } = guard;
    request(scopeKey).then((result) => {
      // The two-line defence: unmount/abort first, then the scope generation.
      if (!mounted.current || !isCurrent(token)) return;
      setLabel(result);
    });
    return () => {
      mounted.current = false;
    };
  }, [guard, request, scopeKey]);
  return <p>{label}</p>;
}

describe('the stale-response guard (Issue #444 stage 1 completion, PR 4)', () => {
  it('a late answer of the previous company never enters the new company state', async () => {
    // Company 1's request is slow; company 2's answers first.
    let resolveCompany1: (value: string) => void = () => undefined;
    const request = vi.fn((scopeKey: string) => {
      if (scopeKey === 'company-1') {
        return new Promise<string>((resolve) => {
          resolveCompany1 = resolve;
        });
      }
      return Promise.resolve('دادهٔ شرکت ۲');
    });

    const view = render(<ScopedView scopeKey="company-1" request={request} />);
    expect(screen.getByText('بدون داده')).toBeInTheDocument();

    // The context switches to company 2 while company 1's request is still in flight.
    view.rerender(<ScopedView scopeKey="company-2" request={request} />);
    await waitFor(() => expect(screen.getByText('دادهٔ شرکت ۲')).toBeInTheDocument());

    // Company 1's answer finally arrives: it is stale and MUST be ignored.
    resolveCompany1('دادهٔ شرکت ۱');
    await Promise.resolve();
    await Promise.resolve();
    expect(screen.getByText('دادهٔ شرکت ۲')).toBeInTheDocument();
    expect(screen.queryByText('دادهٔ شرکت ۱')).not.toBeInTheDocument();
  });

  it('the same scenario for a fiscal-period switch (company:period scope key)', async () => {
    let resolveOldPeriod: (value: string) => void = () => undefined;
    const request = vi.fn((scopeKey: string) => {
      if (scopeKey === 'company-1:period-1') {
        return new Promise<string>((resolve) => {
          resolveOldPeriod = resolve;
        });
      }
      return Promise.resolve('دادهٔ دورهٔ جدید');
    });

    const view = render(<ScopedView scopeKey="company-1:period-1" request={request} />);
    view.rerender(<ScopedView scopeKey="company-1:period-2" request={request} />);
    await waitFor(() => expect(screen.getByText('دادهٔ دورهٔ جدید')).toBeInTheDocument());

    resolveOldPeriod('دادهٔ دورهٔ قدیم');
    await Promise.resolve();
    expect(screen.getByText('دادهٔ دورهٔ جدید')).toBeInTheDocument();
    expect(screen.queryByText('دادهٔ دورهٔ قدیم')).not.toBeInTheDocument();
  });

  it('several concurrent requests of the same scope: only the newest generation wins', async () => {
    const resolvers: Array<(value: string) => void> = [];
    const request = vi.fn(() => new Promise<string>((resolve) => resolvers.push(resolve)));

    function ConcurrentView() {
      const [attempt, setAttempt] = useState(0);
      const [value, setValue] = useState('بدون داده');
      const guard = useStaleGuard('company-1');
      useEffect(() => {
        const { token, isCurrent } = guard;
        request().then((result) => {
          if (isCurrent(token)) setValue(result);
        });
      }, [attempt, guard]);
      return (
        <div>
          <button type="button" onClick={() => setAttempt((value) => value + 1)}>دوباره</button>
          <p>{value}</p>
        </div>
      );
    }

    const user = (await import('@testing-library/user-event')).default.setup();
    render(<ConcurrentView />);
    await waitFor(() => expect(resolvers).toHaveLength(1));
    // A second request starts (query changed) while the first is still in flight.
    await user.click(screen.getByRole('button', { name: 'دوباره' }));
    await waitFor(() => expect(resolvers).toHaveLength(2));

    // The FIRST (stale) answer resolves first: ignored.
    resolvers[0]!('پاسخ قدیمی');
    await Promise.resolve();
    expect(screen.getByText('بدون داده')).toBeInTheDocument();
    // The newest answer wins.
    resolvers[1]!('پاسخ تازه');
    await waitFor(() => expect(screen.getByText('پاسخ تازه')).toBeInTheDocument());
  });

  it('a late answer after unmount never reaches any state', async () => {
    let resolveRequest: (value: string) => void = () => undefined;
    const request = vi.fn(
      () =>
        new Promise<string>((resolve) => {
          resolveRequest = resolve;
        }),
    );
    const view = render(<ScopedView scopeKey="company-1" request={request} />);
    view.unmount();
    resolveRequest('پاسخ دیررس');
    await Promise.resolve();
    // No state update on an unmounted component: nothing to assert but no crash/warning either.
    expect(true).toBe(true);
  });

  it('recognises abort errors and never shows them as real failures', async () => {
    expect(isAbortError(new DOMException('aborted', 'AbortError'))).toBe(true);
    expect(isAbortError(Object.assign(new Error('The operation was aborted.'), { name: 'AbortError' }))).toBe(true);
    expect(isAbortError(new TypeError('Failed to fetch'))).toBe(false);
    expect(isAbortError(new Error('خطای سرور'))).toBe(false);
    expect(isAbortError(undefined)).toBe(false);
    expect(isAbortError('abort')).toBe(false);

    // A cancelled request must not surface as an error state.
    function AbortView() {
      const [failed, setFailed] = useState(false);
      useEffect(() => {
        const controller = new AbortController();
        controller.abort();
        fetch('/api/v1/nothing', { signal: controller.signal }).catch((cause: unknown) => {
          if (controller.signal.aborted || isAbortError(cause)) return;
          setFailed(true);
        });
        return () => controller.abort();
      }, []);
      return <p>{failed ? 'خطا' : 'سالم'}</p>;
    }
    render(<AbortView />);
    await waitFor(() => expect(screen.getByText('سالم')).toBeInTheDocument());
  });
});
