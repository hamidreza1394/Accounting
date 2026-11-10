#!/usr/bin/env bash
set -euo pipefail

workflow=".github/workflows/vendor-ci.yml"
database_script=".github/scripts/run-vendor-ci-database-checks.sh"
[[ -f "$workflow" ]] || { echo "Vendor CI workflow not found: $workflow" >&2; exit 1; }
[[ -f "$database_script" ]] || { echo "Vendor CI database helper not found: $database_script" >&2; exit 1; }

fail() {
  echo "Vendor CI secret-hygiene regression failed: $1" >&2
  exit 1
}

assert_workflow_before() {
  local earlier
  local later
  earlier="$(grep -nF "$1" "$workflow" | head -n 1 | cut -d: -f1 || true)"
  later="$(grep -nF "$2" "$workflow" | head -n 1 | cut -d: -f1 || true)"
  [[ -n "$earlier" && -n "$later" ]] || fail "missing workflow masking/use contract: $1 -> $2"
  (( earlier < later )) || fail "a SoftHSM PIN is used before it is masked"
}

assert_before() {
  local earlier
  local later
  earlier="$(grep -nF "$1" "$database_script" | head -n 1 | cut -d: -f1 || true)"
  later="$(grep -nF "$2" "$database_script" | head -n 1 | cut -d: -f1 || true)"
  [[ -n "$earlier" && -n "$later" ]] || fail "missing masking/export contract: $1 -> $2"
  (( earlier < later )) || fail "a connection string is exported before it is masked"
}

if grep -nF "Ci1Faranou" "$workflow"; then
  fail "predictable SQL Server test password is committed to the workflow"
fi

if grep -nE -- '--so-pin[[:space:]]+[0-9]+|--pin[[:space:]]+[0-9]+|FARANO_TEST_PKCS11_PIN=[0-9]+' "$workflow"; then
  fail "a static SoftHSM test PIN is committed to the workflow"
fi

if grep -nE '^[[:space:]]*(MSSQL_SA_PASSWORD|FARANO_VENDOR_ConnectionStrings__VendorDb|FARANO_VENDOR_TEST_SQLSERVER|FARANOU_TEST_SQLSERVER):' "$workflow"; then
  fail "SQL password or connection string is declared as a YAML environment value"
fi

if grep -nE '^[[:space:]]*[A-Za-z0-9_]+:[[:space:]].*Password=' "$workflow"; then
  fail "connection string with a password is declared as a YAML mapping value"
fi

if grep -niE 'server[[:space:]]*=|data source[[:space:]]*=|;[[:space:]]*password[[:space:]]*=|initial catalog[[:space:]]*=' "$workflow"; then
  fail "connection-string fragments must not be embedded in the workflow source"
fi

password_mask_line="$(grep -nF '"$password"' "$workflow" | head -n 1 | cut -d: -f1 || true)"
docker_line="$(grep -nF 'docker run' "$workflow" | head -n 1 | cut -d: -f1 || true)"
[[ -n "$password_mask_line" && -n "$docker_line" ]] || fail "SQL password masking or ephemeral SQL Server startup is missing"
(( password_mask_line < docker_line )) || fail "the SQL password is not masked before docker run"

grep -Fq 'MSSQL_SA_PASSWORD=%s' "$workflow" || fail "ephemeral SQL password is not transferred through GITHUB_ENV"
grep -Fq '>> "$GITHUB_ENV"' "$workflow" || fail "runner environment handoff is missing"
grep -Fq 'docker rm --force' "$workflow" || fail "ephemeral SQL Server cleanup is missing"
grep -Fq 'if: always()' "$workflow" || fail "SQL Server cleanup is not unconditional"
grep -Fq 'bash .github/scripts/run-vendor-ci-database-checks.sh test' "$workflow" || fail "Vendor tests must run through the non-echoed helper"
grep -Fq 'bash .github/scripts/run-vendor-ci-database-checks.sh ef-snapshot' "$workflow" || fail "EF snapshot check must run through the non-echoed helper"

if grep -nE '^[[:space:]]*set[[:space:]]+-[^#]*x([[:space:]]|$)|printenv|(^|[[:space:]])env[[:space:]]*($|\|)' "$database_script"; then
  fail "database helper must not enable tracing or dump its environment"
fi

grep -Fq '::add-mask::%s' "$database_script" || fail "complete connection-string masking is missing"
grep -Fq 'dotnet test Vendor.sln' "$database_script" || fail "Vendor test invocation is missing"
grep -Fq 'dotnet ef migrations has-pending-model-changes' "$database_script" || fail "EF snapshot verification is missing"
assert_before 'mask_connection_string "$vendor_auth_db"' 'export FARANO_VENDOR_ConnectionStrings__VendorDb="$vendor_auth_db"'
assert_before 'mask_connection_string "$vendor_tests_db"' 'export FARANO_VENDOR_TEST_SQLSERVER="$vendor_tests_db"'
assert_before 'mask_connection_string "$customer_tests_db"' 'export FARANOU_TEST_SQLSERVER="$customer_tests_db"'
assert_before 'mask_connection_string "$master_db"' 'export FARANO_VENDOR_ConnectionStrings__VendorDb="$master_db"'

if grep -nE '(echo|printf).*(vendor_auth_db|vendor_tests_db|customer_tests_db|master_db|MSSQL_SA_PASSWORD)' "$database_script"; then
  fail "database credentials or connection strings must not be printed"
fi

grep -Fq 'so_pin="$(openssl rand -hex 16)"' "$workflow" || fail "SoftHSM SO PIN must be generated per run"
grep -Fq 'user_pin="$(openssl rand -hex 16)"' "$workflow" || fail "SoftHSM user PIN must be generated per run"
grep -Fq 'FARANO_TEST_PKCS11_PIN=%s' "$workflow" || fail "ephemeral SoftHSM user PIN handoff is missing"
assert_workflow_before '"$so_pin"' 'softhsm2-util --init-token'
assert_workflow_before '"$user_pin"' 'softhsm2-util --init-token'

echo "Vendor CI secret-hygiene regression passed."
