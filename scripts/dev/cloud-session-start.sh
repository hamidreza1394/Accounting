#!/usr/bin/env bash
# SessionStart hook (see .claude/settings.json). In a Claude Code cloud session only
# (CLAUDE_CODE_REMOTE=true) it makes sure `dotnet` exists; locally it does nothing.
#
# It never fails the session: a missing SDK is reported on stderr and the session goes on.
# Installing the SDK is idempotent and takes seconds once the environment cache (or an earlier
# run) has it. For a faster start, put the same two lines in the environment's *Setup script*
# (docs/DEVELOPMENT.md, "Cloud sessions"); this hook then finds the SDK already installed.
# SQL Server is deliberately NOT started here (2 GB image, needed only for the integration and
# E2E tests): start it on demand with scripts/dev/test-sqlserver.sh.
set -uo pipefail

[ "${CLAUDE_CODE_REMOTE:-}" = "true" ] || exit 0

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if ! bash "$here/install-dotnet.sh" >&2; then
  echo "cloud-session-start: could not install the .NET SDK; dotnet is unavailable in this session" >&2
  exit 0
fi

# Variables for every later command of the session (the file only exists in hook context).
if [ -n "${CLAUDE_ENV_FILE:-}" ]; then
  if [ -n "${DOTNET_INSTALL_DIR:-}" ]; then dotnet_dir="$DOTNET_INSTALL_DIR"
  elif [ "$(id -u)" = "0" ]; then dotnet_dir="/usr/share/dotnet"
  else dotnet_dir="$HOME/.dotnet"; fi
  {
    echo "export DOTNET_ROOT='$dotnet_dir'"
    echo "export PATH='$dotnet_dir':\"\$PATH\""
    echo 'export DOTNET_CLI_TELEMETRY_OPTOUT=1'
    echo 'export DOTNET_NOLOGO=1'
    echo 'export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1'
  } >>"$CLAUDE_ENV_FILE"
fi
exit 0
