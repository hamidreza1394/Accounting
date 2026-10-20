// Verifies that the built client needs nothing from the internet at run time:
//   1. index.html loads scripts, styles and icons only from the same origin;
//   2. the CSS loads fonts/images only from the same origin;
//   3. every same-origin resource the build references exists in Server/wwwroot.
// Text such as the React error-documentation URL inside the JavaScript is not a resource
// and is not reported; dynamic imports from another origin are.
import { readFileSync, readdirSync, existsSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const wwwroot = join(here, '..', '..', 'Server', 'wwwroot');
const appRoot = join(wwwroot, 'app');

if (!existsSync(join(appRoot, 'index.html'))) {
  console.error('Server/wwwroot/app/index.html is missing. Run "npm run build" first.');
  process.exit(1);
}

const problems = [];
const referenced = new Set();

function inspect(reference, source) {
  if (/^(data:|#|mailto:)/i.test(reference)) {
    return;
  }
  if (/^(https?:)?\/\//i.test(reference)) {
    problems.push(`${source}: external resource ${reference}`);
    return;
  }
  if (reference.startsWith('/')) {
    referenced.add(reference.split(/[?#]/)[0]);
  }
}

const html = readFileSync(join(appRoot, 'index.html'), 'utf8');
for (const match of html.matchAll(/\b(?:src|href)\s*=\s*"([^"]+)"/gi)) {
  inspect(match[1], 'index.html');
}

const assets = join(appRoot, 'assets');
for (const file of readdirSync(assets)) {
  const content = readFileSync(join(assets, file), 'utf8');
  if (file.endsWith('.css')) {
    for (const match of content.matchAll(/url\(\s*['"]?([^'")]+)['"]?\s*\)/gi)) {
      inspect(match[1], `assets/${file}`);
    }
    for (const match of content.matchAll(/@import\s+(?:url\()?['"]?([^'");]+)/gi)) {
      inspect(match[1], `assets/${file}`);
    }
  }
  if (file.endsWith('.js')) {
    for (const match of content.matchAll(/\bimport\(\s*['"](https?:\/\/[^'"]+)['"]/gi)) {
      problems.push(`assets/${file}: external dynamic import ${match[1]}`);
    }
  }
}

for (const path of referenced) {
  if (!existsSync(join(wwwroot, ...path.split('/').filter(Boolean)))) {
    problems.push(`missing local resource ${path} (expected in Server/wwwroot)`);
  }
}

if (problems.length > 0) {
  console.error(problems.join('\n'));
  process.exit(1);
}

console.log(`Offline check passed: ${referenced.size} same-origin resources referenced, all present; no external resource.`);
