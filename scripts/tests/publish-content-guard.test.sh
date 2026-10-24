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
printf '-----BEGIN CERTIFICATE-----\nMIIB\n-----END CERTIFICATE-----\n' > "$clean/public-cert.pem"
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
printf '-----BEGIN RSA PRIVATE KEY-----\nTEST-FIXTURE-NOT-A-REAL-KEY\n-----END RSA PRIVATE KEY-----\n' > "$pem/leak.txt"
if faranou_assert_customer_publish_clean "$pem" 2>/dev/null; then
  fail "PEM private key content rejected" "guard accepted BEGIN RSA PRIVATE KEY fixture"
else
  ok "PEM private key content rejected"
fi

echo "$passed passed, $failures failed"
[ "$failures" -eq 0 ]
