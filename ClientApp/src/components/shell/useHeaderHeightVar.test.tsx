import { render } from '@testing-library/react';
import { useRef } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { headerHeightVar, useHeaderHeightVar } from './useHeaderHeightVar';

function Harness() {
  const root = useRef<HTMLDivElement>(null);
  const header = useRef<HTMLElement>(null);
  useHeaderHeightVar(header, root);
  return (
    <div ref={root} data-testid="root">
      <header ref={header}>سربرگ</header>
    </div>
  );
}

afterEach(() => vi.restoreAllMocks());

describe('useHeaderHeightVar', () => {
  it('publishes the measured header height on the workspace root', () => {
    vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockReturnValue({ height: 118.4 } as DOMRect);
    const { getByTestId } = render(<Harness />);
    expect(getByTestId('root').style.getPropertyValue(headerHeightVar)).toBe('118px');
  });

  it('leaves the CSS fallback in place when nothing can be measured (no layout)', () => {
    const { getByTestId } = render(<Harness />);
    expect(getByTestId('root').style.getPropertyValue(headerHeightVar)).toBe('');
  });

  it('follows the header when it grows (wrapped header, resize)', () => {
    const callbacks: Array<() => void> = [];
    vi.stubGlobal(
      'ResizeObserver',
      class {
        constructor(callback: () => void) {
          callbacks.push(callback);
        }
        observe() {}
        disconnect() {}
      },
    );
    const spy = vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockReturnValue({ height: 92 } as DOMRect);
    const { getByTestId } = render(<Harness />);
    expect(getByTestId('root').style.getPropertyValue(headerHeightVar)).toBe('92px');
    spy.mockReturnValue({ height: 150 } as DOMRect);
    callbacks.forEach((callback) => callback());
    expect(getByTestId('root').style.getPropertyValue(headerHeightVar)).toBe('150px');
    vi.unstubAllGlobals();
  });
});
