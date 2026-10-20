#!/usr/bin/env bash
# Tests of the output-directory guard of scripts/publish.sh (no build is run:
# FARANOU_PUBLISH_PREPARE_ONLY=1). Every refused case also proves that nothing was deleted.
# Usage: scripts/tests/publish-output.test.sh
set -uo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd -P)"
publish="$repo_root/scripts/publish.sh"
sandbox="$(mktemp -d)"
trap 'rm -rf -- "$sandbox"' EXIT
failures=0
passed=0

mkdir -p "$sandbox/home" "$sandbox/work" "$sandbox/outside"
echo keep > "$sandbox/home/sentinel"
echo keep > "$sandbox/work/sentinel"

run() { # run <cwd> <args...> -> exit code; HOME points into the sandbox
  local cwd="$1"; shift
  (cd "$cwd" && HOME="$sandbox/home" FARANOU_PUBLISH_PREPARE_ONLY=1 bash "$publish" "$@" >/dev/null 2>"$sandbox/stderr")
}

expect_refused() { # expect_refused <name> <cwd> <path> <file-that-must-survive>
  local name="$1" cwd="$2" path="$3" survivor="$4"
  if run "$cwd" "$path"; then
    echo "FAIL $name: accepted '$path'"; failures=$((failures + 1)); return
  fi
  if ! grep -q "refusing output directory" "$sandbox/stderr"; then
    echo "FAIL $name: no refusal message"; failures=$((failures + 1)); return
  fi
  if [ ! -e "$survivor" ]; then
    echo "FAIL $name: $survivor was deleted"; failures=$((failures + 1)); return
  fi
  echo "ok   $name"; passed=$((passed + 1))
}

expect_accepted() { # expect_accepted <name> <cwd> <path> <expected-canonical>
  local name="$1" cwd="$2" path="$3" expected="$4"
  if ! run "$cwd" "$path"; then
    echo "FAIL $name: refused '$path': $(cat "$sandbox/stderr")"; failures=$((failures + 1)); return
  fi
  if [ ! -f "$expected/.faranou-publish-output" ]; then
    echo "FAIL $name: marker missing in $expected"; failures=$((failures + 1)); return
  fi
  echo "ok   $name"; passed=$((passed + 1))
}

# --- refused paths ----------------------------------------------------------------
expect_refused "file-system root"            "$sandbox/work" "/"                         "$sandbox/work/sentinel"
expect_refused "empty path"                  "$sandbox/work" ""                          "$sandbox/work/sentinel"
expect_refused "repository root"             "$sandbox/work" "$repo_root"                "$repo_root/Template.sln"
expect_refused "repository root via .."      "$sandbox/work" "$repo_root/Server/.."      "$repo_root/Template.sln"
expect_refused "ancestor of the repository"  "$sandbox/work" "$(dirname "$repo_root")"   "$repo_root/Template.sln"
expect_refused "source folder in the repo"   "$sandbox/work" "$repo_root/Server"         "$repo_root/Server/Program.cs"
expect_refused "artifacts itself"            "$sandbox/work" "$repo_root/artifacts"      "$repo_root/Template.sln"
expect_refused "home directory"              "$sandbox/work" "$sandbox/home"             "$sandbox/home/sentinel"
expect_refused "home directory via ~ value"  "$sandbox/work" "$sandbox/home/."           "$sandbox/home/sentinel"
expect_refused "ancestor of home"            "$sandbox/work" "$sandbox"                  "$sandbox/home/sentinel"
expect_refused "current directory (.)"       "$sandbox/work" "."                         "$sandbox/work/sentinel"
expect_refused "current directory (abs)"     "$sandbox/work" "$sandbox/work"             "$sandbox/work/sentinel"

mkdir -p "$sandbox/outside/data"; echo keep > "$sandbox/outside/data/important.txt"
expect_refused "non-empty foreign directory" "$sandbox/work" "$sandbox/outside/data"     "$sandbox/outside/data/important.txt"

echo keep > "$sandbox/outside/a-file"
expect_refused "existing file"               "$sandbox/work" "$sandbox/outside/a-file"   "$sandbox/outside/a-file"

ln -s "$repo_root" "$sandbox/outside/link-to-repo"
expect_refused "symbolic link to the repo"   "$sandbox/work" "$sandbox/outside/link-to-repo" "$repo_root/Template.sln"
ln -s "$sandbox/outside/data" "$sandbox/outside/link-to-data"
expect_refused "symbolic link to a folder"   "$sandbox/work" "$sandbox/outside/link-to-data" "$sandbox/outside/data/important.txt"
expect_refused "path below a symbolic link"  "$sandbox/work" "$sandbox/outside/link-to-data/sub" "$sandbox/outside/data/important.txt"

# --- accepted paths -----------------------------------------------------------------
expect_accepted "new directory outside the repo" "$sandbox/work" "$sandbox/outside/publish-1" "$sandbox/outside/publish-1"
echo stale > "$sandbox/outside/publish-1/stale.txt"
expect_accepted "earlier publish output (marker) is replaced" "$sandbox/work" "$sandbox/outside/publish-1" "$sandbox/outside/publish-1"
if [ -e "$sandbox/outside/publish-1/stale.txt" ]; then echo "FAIL stale file survived"; failures=$((failures + 1)); fi
mkdir -p "$sandbox/outside/empty"
expect_accepted "existing empty directory" "$sandbox/work" "$sandbox/outside/empty" "$sandbox/outside/empty"
expect_accepted "relative path" "$sandbox/outside" "relative-out" "$sandbox/outside/relative-out"

# The default (artifacts/publish in the repository) must keep working. It is only touched when
# it does not exist yet or was created by a publish, and it is cleaned up again.
default_out="$repo_root/artifacts/publish"
if [ ! -e "$default_out" ] || [ -f "$default_out/.faranou-publish-output" ]; then
  had_default=0; [ -e "$default_out" ] && had_default=1
  if (cd "$sandbox/work" && HOME="$sandbox/home" FARANOU_PUBLISH_PREPARE_ONLY=1 bash "$publish" >/dev/null 2>"$sandbox/stderr") \
     && [ -f "$default_out/.faranou-publish-output" ]; then
    echo "ok   default output (artifacts/publish)"; passed=$((passed + 1))
  else
    echo "FAIL default output: $(cat "$sandbox/stderr")"; failures=$((failures + 1))
  fi
  if [ "$had_default" = 0 ]; then rm -rf -- "$default_out"; rmdir "$repo_root/artifacts" 2>/dev/null || true; fi
else
  echo "skip default output: $default_out exists without a publish marker"
fi

echo "$passed passed, $failures failed"
[ "$failures" -eq 0 ]
