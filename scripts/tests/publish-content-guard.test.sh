#!/usr/bin/env bash
# G2 (D-041) tests for faranou_assert_customer_publish_clean.
# Uses only synthetic fixtures under a temp directory — no real private keys, no real Vendor build.
# Usage: scripts/tests/publish-content-guard.test.sh
set -uo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd -P)"
# shellcheck source=../lib/publish-output.sh
source "$repo_root/scripts/lib/publish-output.sh"

sandbox="$(mktemp -d)"
trap 'rm -rf -- "$sandbox"' EXIT
failures=0
passed=0

ok() { echo "ok   $1"; passed=$((passed + 1)); }
fail() { echo "FAIL $1: $2"; failures=$((failures + 1)); }

# --- positive: clean customer-like tree -------------------------------------------------
clean="$sandbox/clean"
mkdir -p "$clean/wwwroot/app/assets"
printf 'marker\n' > "$clean/.faranou-publish-output"
printf 'public class Server {}\n' > "$clean/Server.dll"
printf 'ok\n' > "$clean/wwwroot/app/index.html"
printf 'js\n' > "$clean/wwwroot/app/assets/main.js"
printf 'css\n' > "$clean/wwwroot/app/assets/main.css"
printf -- '-----BEGIN CERTIFICATE-----\nMIIB\n-----END CERTIFICATE-----\n' > "$clean/public-cert.pem"
# A fixture that silently came out empty would make the checks below meaningless.
[ -s "$clean/public-cert.pem" ] || fail "public certificate fixture created" "fixture file is empty"
if faranou_assert_customer_publish_clean "$clean"; then
  ok "clean publish output accepted"
else
  fail "clean publish output accepted" "guard rejected a valid tree"
fi

# --- negative: Vendor assembly ----------------------------------------------------------
vendor="$sandbox/vendor"
mkdir -p "$vendor"
printf 'x' > "$vendor/Server.dll"
printf 'x' > "$vendor/Vendor.Licensing.Host.dll"
if faranou_assert_customer_publish_clean "$vendor" 2>/dev/null; then
  fail "Vendor assembly rejected" "guard accepted Vendor.Licensing.Host.dll"
else
  ok "Vendor assembly rejected"
fi

# --- negative: .pfx ---------------------------------------------------------------------
pfx="$sandbox/pfx"
mkdir -p "$pfx"
printf 'x' > "$pfx/Server.dll"
printf 'not-a-real-key' > "$pfx/signing.pfx"
if faranou_assert_customer_publish_clean "$pfx" 2>/dev/null; then
  fail ".pfx rejected" "guard accepted signing.pfx"
else
  ok ".pfx rejected"
fi

# --- negative: .p12 and .key ------------------------------------------------------------
p12="$sandbox/p12"
mkdir -p "$p12"
printf 'x' > "$p12/Server.dll"
printf 'x' > "$p12/pair.p12"
if faranou_assert_customer_publish_clean "$p12" 2>/dev/null; then
  fail ".p12 rejected" "guard accepted pair.p12"
else
  ok ".p12 rejected"
fi

keyf="$sandbox/keyf"
mkdir -p "$keyf"
printf 'x' > "$keyf/Server.dll"
printf 'x' > "$keyf/device.key"
if faranou_assert_customer_publish_clean "$keyf" 2>/dev/null; then
  fail ".key rejected" "guard accepted device.key"
else
  ok ".key rejected"
fi

# --- negative: PEM private key content (fixture only; not a real key) -------------------
pem="$sandbox/pem"
mkdir -p "$pem"
printf 'x' > "$pem/Server.dll"
# Synthetic marker text only — not a cryptographic key.
printf -- '-----BEGIN RSA PRIVATE KEY-----\nTEST-FIXTURE-NOT-A-REAL-KEY\n-----END RSA PRIVATE KEY-----\n' > "$pem/leak.txt"
[ -s "$pem/leak.txt" ] || fail "PEM private key fixture created" "fixture file is empty"
if faranou_assert_customer_publish_clean "$pem" 2>/dev/null; then
  fail "PEM private key content rejected" "guard accepted BEGIN RSA PRIVATE KEY fixture"
else
  ok "PEM private key content rejected"
fi

# --- positive: tree matching the real publish output (allow-list subset) ----------------
allow_ok="$sandbox/allow-ok"
mkdir -p "$allow_ok/wwwroot/app/assets" "$allow_ok/fa-IR" "$allow_ok/de" "$allow_ok/runtimes/linux-x64/native" "$allow_ok/runtimes/win-x64/native" "$allow_ok/runtimes/win/lib/net10.0"
printf 'marker\n' > "$allow_ok/.faranou-publish-output"
printf 'x' > "$allow_ok/Server.dll"
printf 'x' > "$allow_ok/Server.pdb"
printf 'x' > "$allow_ok/Server"
printf 'x' > "$allow_ok/Application.dll"
printf 'x' > "$allow_ok/Faranou.Licensing.Contracts.dll"
printf 'x' > "$allow_ok/Microsoft.Data.SqlClient.dll"
printf 'x' > "$allow_ok/Microsoft.Extensions.Hosting.WindowsServices.dll"
printf 'x' > "$allow_ok/System.ServiceProcess.ServiceController.dll"
printf 'x' > "$allow_ok/runtimes/win/lib/net10.0/System.ServiceProcess.ServiceController.dll"
printf 'x' > "$allow_ok/fa-IR/Resources.resources.dll"
printf 'x' > "$allow_ok/de/Microsoft.Data.SqlClient.resources.dll"
printf 'x' > "$allow_ok/runtimes/linux-x64/native/libmsalruntime.so"
printf 'x' > "$allow_ok/runtimes/win-x64/native/Microsoft.Data.SqlClient.SNI.dll"
printf 'ok\n' > "$allow_ok/wwwroot/app/index.html"
printf 'config\n' > "$allow_ok/web.config"
printf 'deps\n' > "$allow_ok/Server.deps.json"
if faranou_assert_customer_publish_clean "$allow_ok"; then
  ok "allow-listed publish output accepted"
else
  fail "allow-listed publish output accepted" "guard rejected a probe-derived tree"
fi

# --- negative: assembly not in the allow-list --------------------------------------------
unlisted="$sandbox/allow-unlisted"
mkdir -p "$unlisted"
printf 'x' > "$unlisted/Server.dll"
printf 'x' > "$unlisted/NotListed.Library.dll"
if faranou_assert_customer_publish_clean "$unlisted" 2>/dev/null; then
  fail "unlisted assembly rejected" "guard accepted NotListed.Library.dll"
else
  ok "unlisted assembly rejected"
fi

# --- negative: satellite assembly in the wrong place -------------------------------------
sat_root="$sandbox/allow-sat-root"
mkdir -p "$sat_root"
printf 'x' > "$sat_root/Server.dll"
printf 'x' > "$sat_root/Microsoft.Data.SqlClient.resources.dll"
if faranou_assert_customer_publish_clean "$sat_root" 2>/dev/null; then
  fail "misplaced satellite rejected" "guard accepted a root-level resources.dll"
else
  ok "misplaced satellite rejected"
fi

# --- negative: unknown satellite culture ---------------------------------------------------
sat_cult="$sandbox/allow-sat-culture"
mkdir -p "$sat_cult/ar"
printf 'x' > "$sat_cult/Server.dll"
printf 'x' > "$sat_cult/ar/Microsoft.Data.SqlClient.resources.dll"
if faranou_assert_customer_publish_clean "$sat_cult" 2>/dev/null; then
  fail "unknown satellite culture rejected" "guard accepted ar/Microsoft.Data.SqlClient.resources.dll"
else
  ok "unknown satellite culture rejected"
fi

# --- negative: unknown satellite base name -------------------------------------------------
sat_base="$sandbox/allow-sat-base"
mkdir -p "$sat_base/fa-IR"
printf 'x' > "$sat_base/Server.dll"
printf 'x' > "$sat_base/fa-IR/Foo.resources.dll"
if faranou_assert_customer_publish_clean "$sat_base" 2>/dev/null; then
  fail "unknown satellite base rejected" "guard accepted fa-IR/Foo.resources.dll"
else
  ok "unknown satellite base rejected"
fi

# --- negative: unexpected path under runtimes/ ----------------------------------------------
runtime_evil="$sandbox/allow-runtime-evil"
mkdir -p "$runtime_evil/runtimes/linux-x64/native"
printf 'x' > "$runtime_evil/Server.dll"
printf 'x' > "$runtime_evil/runtimes/linux-x64/native/evil.so"
if faranou_assert_customer_publish_clean "$runtime_evil" 2>/dev/null; then
  fail "unexpected runtimes file rejected" "guard accepted runtimes/linux-x64/native/evil.so"
else
  ok "unexpected runtimes file rejected"
fi

# --- negative: binary in an unknown nested directory ----------------------------------------
nested_evil="$sandbox/allow-nested-evil"
mkdir -p "$nested_evil/plugins"
printf 'x' > "$nested_evil/Server.dll"
printf 'x' > "$nested_evil/plugins/Evil.Plugin.dll"
if faranou_assert_customer_publish_clean "$nested_evil" 2>/dev/null; then
  fail "nested unknown binary rejected" "guard accepted plugins/Evil.Plugin.dll"
else
  ok "nested unknown binary rejected"
fi

# --- negative: unlisted extension-less root file ---------------------------------------------
stray="$sandbox/allow-stray"
mkdir -p "$stray"
printf 'x' > "$stray/Server.dll"
printf 'x' > "$stray/stray-binary"
if faranou_assert_customer_publish_clean "$stray" 2>/dev/null; then
  fail "unlisted extension-less root file rejected" "guard accepted stray-binary"
else
  ok "unlisted extension-less root file rejected"
fi


# --- negative: dot-prefixed unlisted binary must not bypass the allow-list ----------------
dot_evil="$sandbox/allow-dot-evil"
mkdir -p "$dot_evil"
printf 'x' > "$dot_evil/Server.dll"
printf 'x' > "$dot_evil/.NotListed.Library.dll"
if faranou_assert_customer_publish_clean "$dot_evil" 2>/dev/null; then
  fail "dot-prefixed unlisted binary rejected" "guard accepted .NotListed.Library.dll"
else
  ok "dot-prefixed unlisted binary rejected"
fi


# --- negative: unlisted PDB must not bypass the root allow-list ----------------------------
pdb_evil="$sandbox/allow-pdb-evil"
mkdir -p "$pdb_evil"
printf 'x' > "$pdb_evil/Server.dll"
printf 'x' > "$pdb_evil/NotListed.Library.pdb"
if faranou_assert_customer_publish_clean "$pdb_evil" 2>/dev/null; then
  fail "unlisted PDB rejected" "guard accepted NotListed.Library.pdb"
else
  ok "unlisted PDB rejected"
fi

echo "$passed passed, $failures failed"
[ "$failures" -eq 0 ]
