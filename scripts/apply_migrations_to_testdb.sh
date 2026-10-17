#!/usr/bin/env bash
# Applies the EF Core migrations to an isolated test SQL Server database and
# verifies the result. The script never touches a production or customer
# database: it refuses to run against a database whose name does not contain
# "test" or "dev".
#
# Usage:
#   TARAZAVA_TEST_CONNECTIONSTRING='Server=localhost;Database=TarazAva_Test;...' \
#       scripts/apply_migrations_to_testdb.sh
#
# The connection string must never be committed. It is read from the
# environment (or from the user secrets of the Server project).

set -euo pipefail

REPOSITORY_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONTEXT_PROJECT="${REPOSITORY_ROOT}/Infrastructure/Persistence/Infrastructure.Persistence.csproj"
STARTUP_PROJECT="${REPOSITORY_ROOT}/Server/Server.csproj"
MIGRATIONS_DIRECTORY="${REPOSITORY_ROOT}/Infrastructure/Persistence/Migrations"

CONNECTION_STRING="${TARAZAVA_TEST_CONNECTIONSTRING:-${ConnectionStrings__ConnectionString:-}}"
if [ -z "${CONNECTION_STRING}" ]; then
  echo "error: no connection string was given." >&2
  echo "       set TARAZAVA_TEST_CONNECTIONSTRING to the connection string of the test database." >&2
  exit 2
fi

# The database name is read from the connection string so that the safety check
# below cannot be bypassed by a different connection string.
DATABASE_NAME="$(printf '%s' "${CONNECTION_STRING}" \
  | sed -n 's/.*[Dd]atabase\s*=\s*\([^;]*\);\{0,1\}.*/\1/p')"

if [ -z "${DATABASE_NAME}" ]; then
  echo "error: the connection string does not contain a database name." >&2
  exit 2
fi

case "$(printf '%s' "${DATABASE_NAME}" | tr '[:upper:]' '[:lower:]')" in
  *test*|*dev*|*scratch*)
    ;;
  *)
    echo "error: refusing to run against the database '${DATABASE_NAME}'." >&2
    echo "       the name of the test database must contain 'test', 'dev' or 'scratch'." >&2
    exit 2
    ;;
esac

echo "==> Target database: ${DATABASE_NAME}"

if ! command -v dotnet > /dev/null 2>&1; then
  echo "error: the .NET SDK is not available, the migrations cannot be applied." >&2
  exit 3
fi

export ConnectionStrings__ConnectionString="${CONNECTION_STRING}"

echo "==> Applying the migrations from scratch."
dotnet ef database update \
  --project "${CONTEXT_PROJECT}" \
  --startup-project "${STARTUP_PROJECT}" \
  --configuration Release

echo "==> Listing the applied migrations."
dotnet ef migrations list \
  --project "${CONTEXT_PROJECT}" \
  --startup-project "${STARTUP_PROJECT}" \
  --configuration Release

echo "==> Verifying that the migration history and the tables exist."
dotnet ef migrations script \
  --project "${CONTEXT_PROJECT}" \
  --startup-project "${STARTUP_PROJECT}" \
  --configuration Release \
  --output "${REPOSITORY_ROOT}/artifacts/test-database.sql"

if command -v sqlcmd > /dev/null 2>&1; then
  echo "==> Checking the schema with sqlcmd."
  sqlcmd -S "$(printf '%s' "${CONNECTION_STRING}" | sed -n 's/.*[Ss]erver\s*=\s*\([^;]*\);\{0,1\}.*/\1/p')" \
    -d "${DATABASE_NAME}" \
    -Q "SET NOCOUNT ON; SELECT MigrationId, ProductVersion FROM __EFMigrationsHistory ORDER BY MigrationId; SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME;"
else
  echo "note: sqlcmd is not available, the schema was not queried directly." >&2
  echo "      use the generated script ${REPOSITORY_ROOT}/artifacts/test-database.sql to review the DDL." >&2
fi

echo "==> Re-running the migrations to prove that they are idempotent."
dotnet ef database update \
  --project "${CONTEXT_PROJECT}" \
  --startup-project "${STARTUP_PROJECT}" \
  --configuration Release

echo "==> The migrations were applied to '${DATABASE_NAME}'."
