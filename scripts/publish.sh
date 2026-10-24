#!/usr/bin/env bash
# Production build of Faranou, in a fixed order:
#   1. npm ci (exact versions from ClientApp/package-lock.json)
#   2. type check and tests of the client
#   3. npm run build  -> Server/wwwroot/app
#   4. offline-resources check of the client build
#   5. dotnet publish of the server (net10.0, Release)
#   6. checks of the publish output: app/index.html, its JS/CSS, fonts and images
#   7. G2 content guard (D-041): no Vendor assemblies / private-key material
# Usage: scripts/publish.sh [output-directory]   (default: artifacts/publish)
# The output directory is validated before anything is built or deleted
# (scripts/lib/publish-output.sh). FARANOU_PUBLISH_PREPARE_ONLY=1 stops after that step
# (used by scripts/tests/publish-output.test.sh).
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P)"
# shellcheck source=lib/publish-output.sh
source "$repo_root/scripts/lib/publish-output.sh"

if [ "$#" -gt 0 ]; then requested="$1"; else requested="$repo_root/artifacts/publish"; fi
output="$(faranou_resolve_output "$requested" "$repo_root")"
faranou_prepare_output "$output"
echo "==> Output directory: $output"

if [ "${FARANOU_PUBLISH_PREPARE_ONLY:-}" = "1" ]; then
  exit 0
fi

echo "==> Client: npm ci"
cd "$repo_root/ClientApp"
npm ci --no-fund --no-audit

echo "==> Client: type check and tests"
npm run typecheck
npm test

echo "==> Client: production build (Server/wwwroot/app)"
npm run build
npm run check:offline

echo "==> Server: dotnet publish -> $output"
cd "$repo_root"
dotnet publish Server/Server.csproj --configuration Release --output "$output"

echo "==> Publish output: required client files"
test -f "$output/wwwroot/app/index.html" || { echo "missing wwwroot/app/index.html in the publish output" >&2; exit 1; }
ls "$output"/wwwroot/app/assets/*.js >/dev/null 2>&1 || { echo "no JavaScript bundle in wwwroot/app/assets" >&2; exit 1; }
ls "$output"/wwwroot/app/assets/*.css >/dev/null 2>&1 || { echo "no stylesheet in wwwroot/app/assets" >&2; exit 1; }

# The same static check as above, now against the publish output: every font, image and
# bundle that the client references must be present in the published wwwroot.
node "$repo_root/ClientApp/scripts/check-offline-build.mjs" --wwwroot "$output/wwwroot"

echo "==> Publish output: G2 content guard (no Vendor assemblies / private keys)"
faranou_assert_customer_publish_clean "$output"

echo "==> Published to $output"
