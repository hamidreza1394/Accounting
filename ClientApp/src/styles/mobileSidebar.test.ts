import { describe, expect, it } from 'vitest';
// Vitest runs from the ClientApp folder. The project has no @types/node; the test only needs to read the stylesheet source.
// @ts-expect-error -- node:fs types are not installed
import { readFileSync } from 'node:fs';

const workingDirectory = (globalThis as unknown as { process: { cwd: () => string } }).process.cwd();
const read = (name: string): string => (readFileSync as (path: string, encoding: string) => string)(`${workingDirectory}/src/styles/${name}`, 'utf8');
const css: string = read('index.css');
const premiumCss: string = read('premium-dashboard.css');

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

  it('has no collapse button or icon-rail styles left', () => {
    for (const source of [css, premiumCss]) {
      expect(source).not.toContain('faranou-sidebar-fold');
      expect(source).not.toContain('faranou-sidebar--collapsed');
      expect(source).not.toContain('nav-group-dot--rail');
    }
  });

  it('frames the whole sidebar with one subtle border and a desktop width of 280-320px', () => {
    const frame = premiumCss.match(/\.faranou-sidebar\.faranou-sidebar--premium\s*\{[\s\S]*?\n\}/)?.[0] ?? '';
    expect(frame).toMatch(/border:\s*1px solid/);
    const width = Number(frame.match(/\bwidth:\s*(\d+)px/)?.[1]);
    expect(width).toBeGreaterThanOrEqual(280);
    expect(width).toBeLessThanOrEqual(320);
    expect(frame).toContain('overflow: hidden');
  });

  it('scrolls only inside the menu, never the frame, and never sideways', () => {
    const nav = premiumCss.match(/\.faranou-sidebar-nav\s*\{[^}]*\}/)?.[0] ?? '';
    expect(nav).toContain('overflow-y: auto');
    expect(nav).toContain('overflow-x: hidden');
    expect(nav).toContain('min-height: 0');
    const footer = premiumCss.match(/\.faranou-sidebar--premium \.faranou-sidebar-footer\s*\{[^}]*\}/)?.[0] ?? '';
    expect(footer).toContain('flex: 0 0 auto');
  });

  it('gives the active item a start-edge bar, focus-visible outlines and only logical properties in the sidebar block', () => {
    expect(premiumCss).toMatch(/\.faranou-nav-active::before\s*\{[^}]*inset-inline-start:\s*0/);
    expect(premiumCss).toMatch(/\.faranou-sidebar-nav :is\(a, button\):focus-visible/);
    const menuBlock = premiumCss.slice(premiumCss.indexOf('---- Sidebar menu'), premiumCss.indexOf('/* Larger, clearer active workspace tabs */'));
    expect(menuBlock.length).toBeGreaterThan(500);
    expect(menuBlock).not.toMatch(/(?:margin|padding|border)-(?:left|right)|(?:^|[\s;{])(?:left|right):/m);
  });

  it('keeps the drawer narrower than the viewport on phones (premium width is overridden at <=1023px)', () => {
    expect(premiumCss).toMatch(/@media \(max-width: 1023px\)\s*\{\s*\.faranou-sidebar\.faranou-sidebar--premium\s*\{[^}]*width:\s*min\(300px, 86vw\)/);
  });
});
