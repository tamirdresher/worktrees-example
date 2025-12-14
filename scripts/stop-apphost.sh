#!/bin/bash

# Aspire AppHost Stopper (Bash)
# Stops a running Aspire AppHost process.

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
GRAY='\033[0;90m'
NC='\033[0m' # No Color

# Check if PID was provided
if [ -z "$1" ]; then
    echo -e "${RED}Error: No PID provided.${NC}"
    echo ""
    echo "Usage: $0 <PID>"
    echo ""
    echo "To find running AppHost instances:"
    echo "  ./scripts/list-apphosts.sh"
    echo ""
    exit 1
fi

PID=$1

echo -e "${YELLOW}Stopping Aspire AppHost (PID: $PID)...${NC}"
echo ""

# Check if process exists
if ! kill -0 $PID 2>/dev/null; then
    echo -e "${RED}Error: Process with PID $PID not found or not accessible.${NC}"
    echo ""
    echo -e "${GRAY}The process may have already stopped.${NC}"
    echo ""
    exit 1
fi

# Get process info
PROC_INFO=$(ps -p $PID -o pid,comm,args 2>/dev/null | tail -n 1)

echo -e "${GRAY}Process Details:${NC}"
echo -e "${GRAY}  $PROC_INFO${NC}"
echo ""

# Try graceful shutdown first (SIGTERM)
echo -e "${YELLOW}Sending SIGTERM (graceful shutdown)...${NC}"
if kill -TERM $PID 2>/dev/null; then
    # Wait up to 10 seconds for process to terminate
    for i in {1..10}; do
        if ! kill -0 $PID 2>/dev/null; then
            echo -e "${GREEN}Process terminated gracefully.${NC}"
            echo ""
            
            # Clean up PID files
            find .aspire/logs -name "apphost-*.pid" -type f 2>/dev/null | while read pidfile; do
                if [ "$(cat "$pidfile" 2>/dev/null)" == "$PID" ]; then
                    rm -f "$pidfile"
                    echo -e "${GRAY}Removed PID file: $pidfile${NC}"
                fi
            done
            
            echo ""
            echo -e "${GREEN}AppHost stopped successfully.${NC}"
            echo ""
            exit 0
        fi
        sleep 1
    done
    
    # If still running after 10 seconds, force kill
    echo -e "${YELLOW}Process did not terminate gracefully. Sending SIGKILL (force kill)...${NC}"
    if kill -KILL $PID 2>/dev/null; then
        echo -e "${GREEN}Process forcefully terminated.${NC}"
    else
        echo -e "${RED}Failed to kill process.${NC}"
        exit 1
    fi
else
    echo -e "${RED}Failed to send SIGTERM to process.${NC}"
    exit 1
fi

# Clean up PID files
find .aspire/logs -name "apphost-*.pid" -type f 2>/dev/null | while read pidfile; do
    if [ "$(cat "$pidfile" 2>/dev/null)" == "$PID" ]; then
        rm -f "$pidfile"
        echo -e "${GRAY}Removed PID file: $pidfile${NC}"
    fi
done

echo ""
echo -e "${GREEN}AppHost stopped successfully.${NC}"
echo ""