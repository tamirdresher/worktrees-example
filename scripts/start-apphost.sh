#!/bin/bash

# Aspire AppHost Starter (Bash front-end)
# This script simply calls `dotnet run start-apphost.cs`

set -e

# Default values
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEFAULT_PROJECT_PATH="$SCRIPT_DIR/../src/NoteTaker.AppHost"
PROJECT_PATH="${1:-$DEFAULT_PROJECT_PATH}"

# Colors for output
CYAN='\033[0;36m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
GRAY='\033[0;90m'
NC='\033[0m' # No Color

echo -e "${CYAN}========================================${NC}"
echo -e "${CYAN}Aspire AppHost Starter (Bash front-end)${NC}"
echo -e "${CYAN}========================================${NC}"
echo ""

# Resolve project path
if [ ! -d "$PROJECT_PATH" ]; then
    echo -e "${RED}Error: Project path not found: $PROJECT_PATH${NC}"
    exit 1
fi

# Get absolute path
PROJECT_PATH=$(cd "$PROJECT_PATH" && pwd)
echo -e "${GRAY}Project: $PROJECT_PATH${NC}"
echo ""

# Ensure app.cs exists next to this script
APP_CS_PATH="$SCRIPT_DIR/start-apphost.cs"
if [ ! -f "$APP_CS_PATH" ]; then
    echo -e "${RED}Error: start-apphost.cs not found at: $APP_CS_PATH${NC}"
    echo -e "${YELLOW}Please place start-apphost.cs in the same folder as this script.${NC}"
    exit 1
fi

echo -e "${YELLOW}Running C# wrapper: start-apphost.cs${NC}"
echo ""

# Change to script directory to run the C# file
cd "$SCRIPT_DIR"

# Call dotnet run start-apphost.cs
dotnet run start-apphost.cs -- "$PROJECT_PATH"