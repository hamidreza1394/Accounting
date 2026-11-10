#!/usr/bin/env bash
set -euo pipefail

build_connection_string() {
  local database="$1"
  local password="${MSSQL_SA_PASSWORD:?ephemeral SQL Server password is unavailable}"
  local value

  value="Server=localhost,1433"
  value+=";Database=${database}"
  value+=";User Id=sa"
  value+=";Password=${password}"
  value+=";TrustServerCertificate=True"
  value+=";Encrypt=True"
  printf '%s' "$value"
}

mask_connection_string() {
  printf '::add-mask::%s\n' "$1"
}

case "${1:-}" in
  test)
    vendor_auth_db="$(build_connection_string FaranouVendorAuthTests)"
    vendor_tests_db="$(build_connection_string FaranouVendorTests)"
    customer_tests_db="$(build_connection_string FaranouTests)"

    # Mask each complete value before it enters the child process environment.
    mask_connection_string "$vendor_auth_db"
    mask_connection_string "$vendor_tests_db"
    mask_connection_string "$customer_tests_db"

    export FARANO_VENDOR_ConnectionStrings__VendorDb="$vendor_auth_db"
    export FARANO_VENDOR_TEST_SQLSERVER="$vendor_tests_db"
    export FARANOU_TEST_SQLSERVER="$customer_tests_db"
    dotnet test Vendor.sln --configuration Release
    ;;
  ef-snapshot)
    master_db="$(build_connection_string master)"
    mask_connection_string "$master_db"
    export FARANO_VENDOR_ConnectionStrings__VendorDb="$master_db"
    dotnet ef migrations has-pending-model-changes \
      --project Vendor/Vendor.Persistence/Vendor.Persistence.csproj \
      --startup-project Vendor/Vendor.Host/Vendor.Host.csproj \
      --context VendorDbContext
    ;;
  *)
    echo "Usage: run-vendor-ci-database-checks.sh {test|ef-snapshot}" >&2
    exit 2
    ;;
esac
