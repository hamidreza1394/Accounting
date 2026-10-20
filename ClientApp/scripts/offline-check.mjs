// Static check that a built client needs no internet resource at run time and that every
// local resource it references exists.
//
// What is checked (in the build output, not in the sources):
//   * HTML: src, href and srcset attributes;
//   * CSS: url(...) and @import, resolved relative to the CSS file itself;
//   * JS: import("...") with a literal specifier, new URL("...", import.meta.url), Vite's
//     preload list ("assets/..."), and string literals that look like a resource path or an
//     external resource URL (by file extension).
// Query strings and fragments are ignored when a file is looked up. Paths are resolved like a
// browser resolves them: "/x" against the site root (wwwroot), "x" and "../x" against the URL
// of the referencing file.
//
// Limits (documented in docs/TESTING.md): a URL that is assembled at run time (string
// concatenation, values from the API, user content) can not be found by a static scan, and
// text that merely mentions a URL (e.g. React's error-documentation link) is deliberately not
// treated as a resource. A passing check is evidence, not a guarantee, of offline operation.

import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative, resolve, sep } from 'node:path';
import { posix } from 'node:path';

const resourceExtensions =
  'js|mjs|css|woff2?|ttf|otf|eot|png|jpe?g|gif|svg|webp|avif|ico|bmp|json|webmanifest|wasm|mp3|mp4|webm';

const ignoredSchemes = /^(data:|blob:|about:|mailto:|tel:|#)/i;
const externalUrl = /^([a-z][a-z0-9+.-]*:)?\/\//i;

function listFiles(directory) {
  const files = [];
  for (const entry of readdirSync(directory)) {
    const path = join(directory, entry);
    if (statSync(path).isDirectory()) {
      files.push(...listFiles(path));
    } else {
      files.push(path);
    }
  }
  return files;
}

/**
 * @param {{ appRoot: string, wwwroot: string }} options
 *   appRoot: the built client (e.g. Server/wwwroot/app); wwwroot: the site root it is served from.
 * @returns {{ problems: string[], resources: string[] }}
 */
export function checkBuild({ appRoot, wwwroot }) {
  const problems = [];
  const resources = new Set();
  const root = resolve(wwwroot);
  const app = resolve(appRoot);

  if (!existsSync(join(app, 'index.html'))) {
    return { problems: [`${join(app, 'index.html')} is missing. Run "npm run build" first.`], resources: [] };
  }

  const appUrl = '/' + relative(root, app).split(sep).join('/') + '/';
  if (appUrl.startsWith('/..')) {
    return { problems: [`The client (${app}) is not inside the site root (${root}).`], resources: [] };
  }

  /** URL path of a file of the build, e.g. /app/assets/index.css */
  const urlOf = (file) => '/' + relative(root, file).split(sep).join('/');

  function inspect(reference, file, base = urlOf(file)) {
    const value = reference.trim();
    const source = relative(app, file).split(sep).join('/');

    if (value === '' || ignoredSchemes.test(value)) {
      return;
    }
    if (/^javascript:/i.test(value)) {
      problems.push(`${source}: javascript: URL "${value}"`);
      return;
    }
    if (externalUrl.test(value)) {
      problems.push(`${source}: external resource ${value}`);
      return;
    }
    if (/^[a-z][a-z0-9+.-]*:/i.test(value)) {
      problems.push(`${source}: unsupported URL scheme "${value}"`);
      return;
    }

    const path = value.split(/[?#]/)[0];
    if (path === '') {
      return;
    }

    const url = path.startsWith('/') ? posix.normalize(path) : posix.normalize(posix.join(posix.dirname(base), path));
    const onDisk = resolve(root, ...url.split('/').filter(Boolean).map(decodeURIComponent));

    if (onDisk !== root && !onDisk.startsWith(root + sep)) {
      problems.push(`${source}: "${value}" points outside the site root`);
      return;
    }

    resources.add(url);

    if (!existsSync(onDisk) || statSync(onDisk).isDirectory()) {
      problems.push(`${source}: missing local resource ${url} (referenced as "${value}", expected at ${relative(root, onDisk) || '.'} in the site root)`);
    }
  }

  for (const file of listFiles(app)) {
    const lower = file.toLowerCase();

    if (lower.endsWith('.html')) {
      const html = readFileSync(file, 'utf8');
      for (const match of html.matchAll(/\b(?:src|href)\s*=\s*(["'])(.*?)\1/gi)) {
        inspect(match[2], file);
      }
      for (const match of html.matchAll(/\bsrcset\s*=\s*(["'])(.*?)\1/gi)) {
        for (const candidate of match[2].split(',')) {
          inspect(candidate.trim().split(/\s+/)[0] ?? '', file);
        }
      }
    } else if (lower.endsWith('.css')) {
      const css = readFileSync(file, 'utf8');
      for (const match of css.matchAll(/url\(\s*(["']?)([^"')]+)\1\s*\)/gi)) {
        inspect(match[2], file);
      }
      for (const match of css.matchAll(/@import\s+(["'])([^"']+)\1/gi)) {
        inspect(match[2], file);
      }
    } else if (lower.endsWith('.js') || lower.endsWith('.mjs')) {
      const js = readFileSync(file, 'utf8');
      for (const match of js.matchAll(/\bimport\(\s*(["'`])([^"'`$]+)\1\s*\)/g)) {
        inspect(match[2], file);
      }
      for (const match of js.matchAll(/new\s+URL\(\s*(["'`])([^"'`$]+)\1\s*,\s*import\.meta\.url\s*\)/g)) {
        inspect(match[2], file);
      }
      // Vite's preload list holds paths relative to the base of the client.
      for (const match of js.matchAll(/(["'`])((?:\.\/)?assets\/[\w.-]+\.(?:js|css))\1/g)) {
        inspect(match[2], file, appUrl);
      }
      // Literal resource paths and literal external resource URLs, recognised by extension.
      const literal = new RegExp(String.raw`(["'\`])((?:https?:)?\/[^"'\`\s]*?\.(?:${resourceExtensions})(?:[?#][^"'\`\s]*)?)\1`, 'gi');
      for (const match of js.matchAll(literal)) {
        inspect(match[2], file);
      }
    }
  }

  return { problems, resources: [...resources].sort() };
}
