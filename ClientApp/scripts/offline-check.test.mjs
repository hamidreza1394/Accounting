import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { afterEach, describe, expect, it } from 'vitest';
import { checkBuild } from './offline-check.mjs';

let roots = [];

function site(files) {
  const wwwroot = mkdtempSync(join(tmpdir(), 'faranou-offline-'));
  roots.push(wwwroot);
  for (const [path, content] of Object.entries(files)) {
    const file = join(wwwroot, ...path.split('/'));
    mkdirSync(dirname(file), { recursive: true });
    writeFileSync(file, content);
  }
  return checkBuild({ wwwroot, appRoot: join(wwwroot, 'app') });
}

const html = (body = '') =>
  `<!doctype html><html><head><link rel="icon" href="/images/icon.png"><script type="module" src="/app/assets/index.js"></script><link rel="stylesheet" href="/app/assets/index.css"></head><body>${body}</body></html>`;

afterEach(() => {
  for (const root of roots) {
    rmSync(root, { recursive: true, force: true });
  }
  roots = [];
});

describe('checkBuild', () => {
  it('accepts absolute and relative local paths that exist, resolving CSS urls against the CSS file', () => {
    const result = site({
      'app/index.html': html('<img src="assets/logo.svg" srcset="assets/logo.svg 1x, /images/icon.png 2x">'),
      'app/assets/index.js': 'import("./chunk.js");const u=new URL("./worker.js", import.meta.url);',
      'app/assets/chunk.js': '',
      'app/assets/worker.js': '',
      'app/assets/logo.svg': '<svg/>',
      'app/assets/index.css': "@font-face{src:url('../../fonts/a.woff2') format('woff2'),url(/fonts/a.woff)}.x{background:url(\"img/bg.png\")}",
      'app/assets/img/bg.png': 'png',
      'fonts/a.woff2': 'f',
      'fonts/a.woff': 'f',
      'images/icon.png': 'png',
    });

    expect(result.problems).toEqual([]);
    expect(result.resources).toContain('/fonts/a.woff2');
    expect(result.resources).toContain('/app/assets/img/bg.png');
    expect(result.resources).toContain('/app/assets/chunk.js');
  });

  it('reports a missing local file with the path where it was expected', () => {
    const result = site({
      'app/index.html': html(),
      'app/assets/index.js': '',
      'app/assets/index.css': "@font-face{src:url('/fonts/missing.woff2')}",
      'images/icon.png': 'png',
    });

    expect(result.problems).toHaveLength(1);
    expect(result.problems[0]).toContain('missing local resource /fonts/missing.woff2');
  });

  it('reports external resources in HTML, CSS and JS', () => {
    const result = site({
      'app/index.html': html('<script src="https://cdn.example.com/lib.js"></script><img src="//cdn.example.com/a.png">'),
      'app/assets/index.js': 'import("https://cdn.example.com/x.js");const f="https://fonts.example.com/f.woff2";',
      'app/assets/index.css': '@import "https://fonts.googleapis.com/css2";.x{background:url(https://cdn.example.com/bg.png)}',
      'images/icon.png': 'png',
    });

    const text = result.problems.join('\n');
    expect(text).toContain('external resource https://cdn.example.com/lib.js');
    expect(text).toContain('external resource //cdn.example.com/a.png');
    expect(text).toContain('external resource https://cdn.example.com/x.js');
    expect(text).toContain('external resource https://fonts.example.com/f.woff2');
    expect(text).toContain('external resource https://fonts.googleapis.com/css2');
    expect(text).toContain('external resource https://cdn.example.com/bg.png');
  });

  it('ignores query strings and fragments when looking a file up, but still finds a missing one', () => {
    const present = site({
      'app/index.html': html(),
      'app/assets/index.js': 'const icon="/images/icon.png?v=3";',
      'app/assets/index.css': "@font-face{src:url('/fonts/a.woff2?v=1#iefix')}",
      'fonts/a.woff2': 'f',
      'images/icon.png': 'png',
    });
    expect(present.problems).toEqual([]);

    const missing = site({
      'app/index.html': html(),
      'app/assets/index.js': '',
      'app/assets/index.css': "@font-face{src:url('/fonts/b.woff2?v=1')}",
      'images/icon.png': 'png',
    });
    expect(missing.problems.join('\n')).toContain('/fonts/b.woff2');
  });

  it('does not treat mentions of URLs without a resource extension as resources', () => {
    const result = site({
      'app/index.html': html(),
      'app/assets/index.js': 'throw Error("see https://react.dev/errors/418");',
      'app/assets/index.css': '',
      'images/icon.png': 'png',
    });

    expect(result.problems).toEqual([]);
  });

  it('rejects paths that leave the site root and a missing build', () => {
    const traversal = site({
      'app/index.html': html(),
      'app/assets/index.js': '',
      'app/assets/index.css': ".x{background:url('../../../secret.png')}.y{background:url('/%2e%2e/%2e%2e/secret.png')}",
      'images/icon.png': 'png',
    });
    // Like a browser, "../" never climbs above the site root: the reference becomes /secret.png.
    expect(traversal.problems.join('\n')).toContain('missing local resource /secret.png');
    // Encoded dot segments that would leave the site root on disk are rejected.
    expect(traversal.problems.join('\n')).toContain('points outside the site root');

    const wwwroot = mkdtempSync(join(tmpdir(), 'faranou-offline-'));
    roots.push(wwwroot);
    expect(checkBuild({ wwwroot, appRoot: join(wwwroot, 'app') }).problems[0]).toContain('npm run build');
  });
});
