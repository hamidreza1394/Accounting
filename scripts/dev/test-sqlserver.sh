#!/usr/bin/env bash
# A disposable, isolated SQL Server 2022 (real SQL Server, not SQLite) for the integration and E2E
# tests, run in Docker. Mirrors the service container of .github/workflows/dotnet.yml.
#
# Usage:
#   scripts/dev/test-sqlserver.sh up      start (or reuse) the container, wait until it accepts
#                                         logins, print `export` lines for the test variables
#   scripts/dev/test-sqlserver.sh env     print the `export` lines of an already running container
#   scripts/dev/test-sqlserver.sh down    remove the container and its generated password file
#   out="$(scripts/dev/test-sqlserver.sh up)" && eval "$out"   then run dotnet test / the E2E suite
#   (do not write eval "$(...up)" directly: when `up` fails it prints nothing and eval succeeds)
#
# Isolation and secrets:
#   * the container listens on 127.0.0.1 only (port FARANOU_TEST_SQL_PORT, default 14333, so it
#     cannot collide with a SQL Server already on 1433);
#   * the `sa` password is random, generated on first `up`, kept in a mode-600 file outside the
#     repository ($XDG_CACHE_HOME or ~/.cache /faranou-test-sqlserver/sa-password). `up` and `env`
#     print it only inside the `export` lines meant for eval -- do not run them without capturing
#     the output (it would land in a terminal or an agent transcript). While the container runs the
#     password is visible to `docker inspect` and the process list of this machine; the server
#     only exists for the test run. Nothing is committed;
#   * the xUnit tests create and drop their own databases (FaranouTests_CG_*); the E2E schema
#     (FaranouE2E) is created by `Server.dll --migrate` and left in this throw-away server until
#     `down`. The script never talks to any other server;
#   * "isolated" means a separate disposable instance bound to localhost; it is not network-isolated
#     from other containers on the same Docker host. `down` does not stop dockerd;
#   * it needs a Docker daemon. In a Claude Code cloud session dockerd is installed but not
#     running; this script starts it when it is root and no daemon answers.
set -euo pipefail

name="${FARANOU_TEST_SQL_CONTAINER:-faranou-test-mssql}"
port="${FARANOU_TEST_SQL_PORT:-14333}"
image="${FARANOU_TEST_SQL_IMAGE:-mcr.microsoft.com/mssql/server:2022-latest}"
label="faranou.test-sqlserver=1"
[[ "$port" =~ ^[0-9]{1,5}$ ]] || { echo "test-sqlserver: FARANOU_TEST_SQL_PORT must be a number" >&2; exit 1; }
state_dir="${XDG_CACHE_HOME:-$HOME/.cache}/faranou-test-sqlserver"
password_file="$state_dir/sa-password"

die() { echo "test-sqlserver: $*" >&2; exit 1; }
log() { echo "test-sqlserver: $*" >&2; }

ensure_docker() {
  command -v docker >/dev/null 2>&1 || die "docker is not installed"
  if docker info >/dev/null 2>&1; then return 0; fi
  if [ "$(id -u)" = "0" ] && command -v dockerd >/dev/null 2>&1; then
    log "no Docker daemon answers; starting dockerd (log: $state_dir/dockerd.log)"
    mkdir -p "$state_dir"
    nohup dockerd >"$state_dir/dockerd.log" 2>&1 &
    for _ in $(seq 1 30); do
      docker info >/dev/null 2>&1 && return 0
      sleep 1
    done
  fi
  die "no Docker daemon is reachable (start Docker, or run the tests in CI where the SQL Server service container exists)"
}

password() {
  if [ ! -s "$password_file" ]; then
    install -d -m 700 "$state_dir"
    umask 077
    # Random, satisfies the SQL Server complexity rules (upper, lower, digit, symbol-free).
    printf 'Fa%sx1' "$(head -c 24 /dev/urandom | od -An -tx1 | tr -d ' \n')" >"$password_file"
  fi
  chmod 600 "$password_file"
  cat "$password_file"
}

print_env() {
  local pw; pw="$(password)"
  echo "export FARANOU_TEST_SQLSERVER='Server=127.0.0.1,$port;Database=FaranouTests;User Id=sa;Password=$pw;TrustServerCertificate=True;Encrypt=True'"
  echo "export FARANOU_REQUIRE_SQLSERVER=1"
  echo "export FARANOU_E2E_SQL='Server=127.0.0.1,$port;Database=FaranouE2E;User Id=sa;Password=$pw;TrustServerCertificate=True;Encrypt=True'"
}

wait_ready() {
  local pw; pw="$(password)"
  for _ in $(seq 1 60); do
    if SQLCMDPASSWORD="$pw" docker exec -e SQLCMDPASSWORD "$name" /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -Q 'SELECT 1' -b >/dev/null 2>&1; then
      return 0
    fi
    sleep 2
  done
  docker logs --tail 20 "$name" >&2 || true
  die "SQL Server did not become ready in 120 s"
}

is_running() { [ -n "$(docker ps -q -f "name=^/${name}$")" ]; }

# A running container is reused only if it publishes the wanted loopback port and accepts the
# password in the file; otherwise it is stale (other port/image/password) and is replaced.
reusable() {
  is_running || return 1
  docker port "$name" 1433/tcp 2>/dev/null | grep -q "^127\.0\.0\.1:$port\$" || return 1
  SQLCMDPASSWORD="$(password)" docker exec -e SQLCMDPASSWORD "$name" /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -Q 'SELECT 1' -b >/dev/null 2>&1
}

case "${1:-up}" in
  up)
    ensure_docker
    pw="$(password)"
    if reusable; then
      log "container $name is already running"
    else
      if [ -n "$(docker ps -aq -f "name=^/${name}$")" ]; then
        [ "$(docker inspect -f '{{index .Config.Labels "faranou.test-sqlserver"}}' "$name" 2>/dev/null)" = "1" ] \
          || die "a container named $name exists but was not created by this script; refusing to remove it"
        log "container $name does not match the configured port/password; replacing it"
        docker rm -f "$name" >/dev/null 2>&1 || die "could not remove the old container $name"
      fi
      docker image inspect "$image" >/dev/null 2>&1 || { log "pulling $image"; docker pull -q "$image" >/dev/null; }
      MSSQL_SA_PASSWORD="$pw" docker run -d --name "$name" --label "$label" -p "127.0.0.1:$port:1433" \
        -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD "$image" >/dev/null
    fi
    wait_ready
    log "ready on 127.0.0.1:$port"
    print_env
    ;;
  env)
    reusable || die "container $name is not running with the configured port/password (use: up)"
    print_env
    ;;
  down)
    if docker info >/dev/null 2>&1; then
      if [ -n "$(docker ps -aq -f "name=^/${name}$")" ]; then
        [ "$(docker inspect -f '{{index .Config.Labels "faranou.test-sqlserver"}}' "$name" 2>/dev/null)" = "1" ] \
          || die "container $name was not created by this script; refusing to remove it"
        docker rm -f "$name" >/dev/null 2>&1 || true
        [ -z "$(docker ps -aq -f "name=^/${name}$")" ] || die "could not remove container $name; the password file was kept"
      fi
    fi
    rm -f "$password_file"
    log "removed $name and its password file"
    ;;
  *) die "usage: $0 up|env|down" ;;
esac
