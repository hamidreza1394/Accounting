import { describe, expect, it } from 'vitest';
// Vitest runs from the ClientApp folder. The project has no @types/node; the test only needs to read the stylesheet source.
// @ts-expect-error -- node:fs types are not installed
import { readFileSync } from 'node:fs';

const workingDirectory = (globalThis as unknown as { process: { cwd: () => string } }).process.cwd();
const css: string = (readFileSync as (path: string, encoding: string) => string)(`${workingDirectory}/src/styles/index.css`, 'utf8');

// jsdom does not apply the stylesheet, so the closed-drawer rule is asserted on the source; the
// real-browser Tab order was checked separately (see the ISSUE-464 handoff).
describe('mobile sidebar stylesheet', () => {
  const mobileBlock = css.slice(css.indexOf('.faranou-sidebar {\n    position: fixed;'));

  it('hides the closed drawer from focus and assistive technology after the slide-out', () => {
    const closed = mobileBlock.slice(0, mobileBlock.indexOf('}'));
    expect(closed).toContain('visibility: hidden');
    expect(closed).toMatch(/transition: transform 180ms ease, visibility 0s linear 180ms/);
  });

  it('shows the open drawer immediately', () => {
    expect(mobileBlock).toMatch(/\.faranou-sidebar\.is-open \{[^}]*visibility: visible[^}]*transition-delay: 0s/);
  });
});
