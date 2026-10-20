#!/usr/bin/env bash
# Production build of Faranou, in a fixed order:
#   1. npm ci (exact versions from ClientApp/package-lock.json)
#   2. type check and tests of the client
#   3. npm run build  -> Server/wwwroot/app
#   4. offline-resources check of the client build
#   5. dotnet publish of the server (net10.0, Release)
#   6. checks of the publish output: app/index.html, its JS/CSS, fonts and images
# Usage: scripts/publish.sh [output-directory]   (default: artifacts/publish)
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
output="${1:-$repo_root/artifacts/publish}"
case "$output" in /*) ;; *) output="$(pwd)/$output" ;; esac

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
rm -rf "$output"
dotnet publish Server/Server.csproj --configuration Release --output "$output"

echo "==> Publish output: required client files"
test -f "$output/wwwroot/app/index.html" || { echo "missing wwwroot/app/index.html in the publish output" >&2; exit 1; }
ls "$output"/wwwroot/app/assets/*.js >/dev/null 2>&1 || { echo "no JavaScript bundle in wwwroot/app/assets" >&2; exit 1; }
ls "$output"/wwwroot/app/assets/*.css >/dev/null 2>&1 || { echo "no stylesheet in wwwroot/app/assets" >&2; exit 1; }

# The same static check as above, now against the publish output: every font, image and
# bundle that the client references must be present in the published wwwroot.
node "$repo_root/ClientApp/scripts/check-offline-build.mjs" --wwwroot "$output/wwwroot"

echo "==> Published to $output"
