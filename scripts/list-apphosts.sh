#!/usr/bin/env sh
# list-apphosts.sh
#
# Lists all running Aspire AppHost processes by invoking list-apphosts.cs.
#
# Usage:
#   ./list-apphosts.sh
#
# Notes:
# - Requires: dotnet SDK available on PATH
# - Must be run from anywhere; it will execute from the script directory.

set -eu

# Resolve the directory of this script (works for sh/bash/zsh when executed normally)
SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
LIST_APP_CS="$SCRIPT_DIR/list-apphosts.cs"

if [ ! -f "$LIST_APP_CS" ]; then
  printf '%s\n' "ERROR: list-apphosts.cs not found at $LIST_APP_CS" >&2
  exit 1
fi

# Run from the script directory (matching the PowerShell behavior)
(
  cd "$SCRIPT_DIR"
  dotnet run "$LIST_APP_CS"
)
