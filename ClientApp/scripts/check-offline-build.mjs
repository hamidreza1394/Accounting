// CLI of offline-check.mjs.
//   node scripts/check-offline-build.mjs                      -> checks ../Server/wwwroot/app
//   node scripts/check-offline-build.mjs --wwwroot <dir>      -> checks <dir>/app (e.g. a publish output)
//   node scripts/check-offline-build.mjs --wwwroot <dir> --app <dir>
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { checkBuild } from './offline-check.mjs';

const here = dirname(fileURLToPath(import.meta.url));

function argument(name) {
  const index = process.argv.indexOf(name);
  return index >= 0 ? process.argv[index + 1] : undefined;
}

const wwwroot = resolve(argument('--wwwroot') ?? join(here, '..', '..', 'Server', 'wwwroot'));
const appRoot = resolve(argument('--app') ?? join(wwwroot, 'app'));

const { problems, resources } = checkBuild({ appRoot, wwwroot });

if (problems.length > 0) {
  console.error(`Offline check FAILED for ${appRoot}:`);
  console.error(problems.map((problem) => `  - ${problem}`).join('\n'));
  process.exit(1);
}

console.log(
  `Offline check passed for ${appRoot}: ${resources.length} local resources referenced and present, no external resource found by the static scan.`,
);
