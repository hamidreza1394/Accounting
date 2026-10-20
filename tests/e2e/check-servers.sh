#!/usr/bin/env bash
# Starts the two server variants of the E2E run on their own and fails with the server log
# (as CI annotations) when one does not answer, so start-up problems are visible.
set -uo pipefail
publish="${FARANOU_E2E_PUBLISH:?}"
failed=0
# The first-run settings live in LocalApplicationData, which .NET only reports when it exists.
mkdir -p "${RUNNER_TEMP:-/tmp}/faranou-e2e-setup"

start() { # start <name> <port> <path> [env...]
  local name="$1" port="$2" path="$3"; shift 3
  local log; log="$(mktemp)"
  (cd "$publish" && env "$@" ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS="http://localhost:$port" AllowedHosts=localhost dotnet Server.dll) >"$log" 2>&1 &
  local pid=$!
  local status=000
  for _ in $(seq 1 40); do
    status="$(curl -s -o /dev/null -w '%{http_code}' "http://localhost:$port$path" || true)"
    [ "$status" = "200" ] && break
    kill -0 "$pid" 2>/dev/null || break
    sleep 1
  done
  kill "$pid" 2>/dev/null; wait "$pid" 2>/dev/null
  if [ "$status" = "200" ]; then
    echo "ok   $name answers $path"
  else
    echo "::error title=E2E server '$name' did not start (last status $status)::$(tail -n 60 "$log" | tr '\n' '~' | cut -c1-6000)"
    failed=1
  fi
}

start database 5090 /Account/Login "ConnectionStrings__ConnectionString=$FARANOU_E2E_SQL"
start setup 5091 /DatabaseSetup "LOCALAPPDATA=${RUNNER_TEMP:-/tmp}/faranou-e2e-setup" "XDG_DATA_HOME=${RUNNER_TEMP:-/tmp}/faranou-e2e-setup"
exit "$failed"
