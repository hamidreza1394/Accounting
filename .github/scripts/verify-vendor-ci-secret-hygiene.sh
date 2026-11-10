#!/usr/bin/env bash
set -euo pipefail

workflow=".github/workflows/vendor-ci.yml"
[[ -f "$workflow" ]] || { echo "Vendor CI workflow not found: $workflow" >&2; exit 1; }

fail() {
  echo "Vendor CI secret-hygiene regression failed: $1" >&2
  exit 1
}

if grep -nF "Ci1Faranou" "$workflow"; then
  fail "predictable SQL Server test password is committed to the workflow"
fi

if grep -nE '^[[:space:]]*(MSSQL_SA_PASSWORD|FARANO_VENDOR_ConnectionStrings__VendorDb|FARANO_VENDOR_TEST_SQLSERVER|FARANOU_TEST_SQLSERVER):' "$workflow"; then
  fail "SQL password or connection string is declared as a YAML environment value"
fi

if grep -nE '^[[:space:]]*[A-Za-z0-9_]+:[[:space:]].*Password=' "$workflow"; then
  fail "connection string with a password is declared as a YAML mapping value"
fi

mask_line="$(grep -nF '::add-mask::' "$workflow" | head -n 1 | cut -d: -f1 || true)"
docker_line="$(grep -nF 'docker run' "$workflow" | head -n 1 | cut -d: -f1 || true)"
[[ -n "$mask_line" && -n "$docker_line" ]] || fail "masking or ephemeral SQL Server startup is missing"
(( mask_line < docker_line )) || fail "the SQL password is not masked before docker run"

grep -Fq 'MSSQL_SA_PASSWORD=%s' "$workflow" || fail "ephemeral SQL password is not transferred through GITHUB_ENV"
grep -Fq '>> "$GITHUB_ENV"' "$workflow" || fail "runner environment handoff is missing"
grep -Fq 'docker rm --force' "$workflow" || fail "ephemeral SQL Server cleanup is missing"
grep -Fq 'if: always()' "$workflow" || fail "SQL Server cleanup is not unconditional"

echo "Vendor CI secret-hygiene regression passed."
