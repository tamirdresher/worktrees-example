#!/usr/bin/env sh
# kill-apphost.sh
#
# Equivalent to kill-apphost.ps1:
#   - Kill single PID
#   - Kill PID tree
#   - Kill all AppHosts in current git worktree
#
# Usage:
#   ./kill-apphost.sh --pid 12345
#   ./kill-apphost.sh --pid 12345 --tree
#   ./kill-apphost.sh --all
#
# Notes:
# - Requires: dotnet SDK on PATH
# - Runs kill-apphost.cs from the script directory, like the PS1

set -eu

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
KILL_APP_CS="$SCRIPT_DIR/kill-apphost.cs"

if [ ! -f "$KILL_APP_CS" ]; then
  printf '%s\n' "ERROR: kill-apphost.cs not found at $KILL_APP_CS" >&2
  exit 1
fi

PID=""
TREE="0"
ALL="0"

usage() {
  cat >&2 <<EOF
Usage:
  $(basename "$0") --pid <PID>
  $(basename "$0") --pid <PID> --tree
  $(basename "$0") --all
EOF
  exit 1
}

while [ $# -gt 0 ]; do
  case "$1" in
    --pid|-p)
      [ $# -ge 2 ] || usage
      PID="$2"
      shift 2
      ;;
    --tree|-t)
      TREE="1"
      shift 1
      ;;
    --all|-a)
      ALL="1"
      shift 1
      ;;
    -h|--help)
      usage
      ;;
    *)
      printf '%s\n' "ERROR: Unknown argument: $1" >&2
      usage
      ;;
  esac
done

if [ "$ALL" = "1" ] && [ -n "$PID" ]; then
  printf '%s\n' "ERROR: --all cannot be combined with --pid" >&2
  exit 1
fi

if [ "$ALL" != "1" ] && [ -z "$PID" ]; then
  printf '%s\n' "ERROR: You must provide --pid unless you specify --all" >&2
  usage
fi

# Run from the script directory (matching PS1 behavior)
cd "$SCRIPT_DIR"

if [ "$ALL" = "1" ]; then
  dotnet run "$KILL_APP_CS" -- -All
elif [ "$TREE" = "1" ]; then
  dotnet run "$KILL_APP_CS" -- -Tree "$PID"
else
  dotnet run "$KILL_APP_CS" -- "$PID"
fi
