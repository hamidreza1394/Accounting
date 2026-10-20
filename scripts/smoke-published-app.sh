#!/usr/bin/env bash
# Starts a published Faranou server and checks the client routes over HTTP:
#   /app/ and a deep client route return index.html with the strict CSP, every bundle that
#   index.html references is served, the local font and image are served, the API answers
#   401 (not the login page) and the Razor sign-in page still renders its form.
# Usage: scripts/smoke-published-app.sh <publish-directory>
# The server needs a non-empty connection string to leave first-run setup mode; the checked
# routes do not use the database, so an unreachable server name is enough here.
set -euo pipefail

publish="${1:?usage: smoke-published-app.sh <publish-directory>}"
port="${SMOKE_PORT:-5080}"
base="http://127.0.0.1:$port"
log="$(mktemp)"

export ASPNETCORE_URLS="$base"
export ASPNETCORE_ENVIRONMENT="Production"
export ConnectionStrings__ConnectionString="${SMOKE_CONNECTION_STRING:-Server=127.0.0.1,1;Database=FaranouSmoke;User Id=smoke;Password=unused;Connect Timeout=2;TrustServerCertificate=True}"

(cd "$publish" && exec dotnet Server.dll) >"$log" 2>&1 &
server=$!
trap 'kill "$server" 2>/dev/null || true; wait "$server" 2>/dev/null || true' EXIT

for _ in $(seq 1 60); do
  if curl -s -o /dev/null "$base/api/v1/session"; then break; fi
  if ! kill -0 "$server" 2>/dev/null; then echo "server exited:" >&2; cat "$log" >&2; exit 1; fi
  sleep 1
done

failures=0
check() { # check <description> <expected> <actual>
  if [ "$2" = "$3" ]; then echo "  ok   $1"; else echo "  FAIL $1 (expected '$2', got '$3')"; echo "::error title=Smoke test::$1 (expected '$2', got '$3')"; failures=$((failures + 1)); fi
}
status() { curl -s -o /dev/null -w '%{http_code}' "$base$1"; }
header() { curl -s -D - -o /dev/null "$base$1" | tr -d '\r' | awk -v name="$(echo "$2" | tr 'A-Z' 'a-z')" 'BEGIN{FS=": "} tolower($1)==name {sub(/^[^:]*: /,""); print; exit}'; }

echo "==> Smoke test of $publish at $base"
index="$(curl -s "$base/app/")"
check "GET /app/ -> 200" 200 "$(status /app/)"
check "GET /app/ is the client index.html" yes "$(grep -q 'id="root"' <<<"$index" && echo yes || echo no)"
check "GET /app/companies/x (client route) -> 200" 200 "$(status /app/companies/x)"
check "client route returns the same index.html" yes "$( [ "$(curl -s "$base/app/companies/x")" = "$index" ] && echo yes || echo no)"
check "/app/ index.html is revalidated" no-cache "$(header /app/ Cache-Control)"
check "/app/ has the strict CSP" yes "$(header /app/ Content-Security-Policy | grep -q "script-src 'self'; style-src 'self'" && echo yes || echo no)"

assets="$(grep -oE '(src|href)="/app/assets/[^"]+"' <<<"$index" | sed -E 's/^(src|href)="//; s/"$//')"
check "index.html references bundles" yes "$( [ -n "$assets" ] && echo yes || echo no)"
for asset in $assets; do
  check "GET $asset -> 200" 200 "$(status "$asset")"
done

check "GET /fonts/woff2/IRANSansX-Regular.woff2 -> 200" 200 "$(status /fonts/woff2/IRANSansX-Regular.woff2)"
check "GET /images/faranou-mark.svg -> 200" 200 "$(status /images/faranou-mark.svg)"
check "GET /api/v1/session (anonymous) -> 401" 401 "$(status /api/v1/session)"
check "GET /Account/Login -> 200" 200 "$(status /Account/Login)"
check "login page renders the anti-forgery token" yes "$(curl -s "$base/Account/Login" | grep -q '__RequestVerificationToken' && echo yes || echo no)"

if [ "$failures" -ne 0 ]; then
  echo "==> $failures smoke check(s) failed. Server log:" >&2
  tail -n 80 "$log" >&2
  exit 1
fi

echo "==> Smoke test passed"
