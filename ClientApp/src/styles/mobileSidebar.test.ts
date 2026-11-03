import { describe, expect, it } from 'vitest';
// Vitest runs from the ClientApp folder. The project has no @types/node; the test only needs to read the stylesheet source.
// @ts-expect-error -- node:fs types are not installed
import { readFileSync } from 'node:fs';

const workingDirectory = (globalThis as unknown as { process: { cwd: () => string } }).process.cwd();
const css: string = (readFileSync as (path: string, encoding: string) => string)(`${workingDirectory}/src/styles/index.css`, 'utf8');

// Windows checkouts may use CRLF. Match CSS whitespace rather than requiring LF-only source text.
const closedDrawerRule = css.match(/\.faranou-sidebar\s*\{\s*position:\s*fixed;[\s\S]*?\}/)?.[0] ?? '';
const openDrawerRule = css.match(/\.faranou-sidebar\.is-open\s*\{[^}]*\}/)?.[0] ?? '';

describe('mobile sidebar stylesheet', () => {
  it('hides the closed drawer from focus and assistive technology after the slide-out', () => {
    expect(closedDrawerRule).toContain('visibility: hidden');
    expect(closedDrawerRule).toMatch(/transition:\s*transform 180ms ease, visibility 0s linear 180ms/);
  });

  it('shows the open drawer immediately', () => {
    expect(openDrawerRule).toContain('visibility: visible');
    expect(openDrawerRule).toContain('transition-delay: 0s');
  });
});
