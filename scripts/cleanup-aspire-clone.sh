#!/bin/bash
# Cleanup all Docker containers and volumes for the current Aspire clone
#
# This script removes all Docker containers and volumes created by Aspire
# for the current clone (based on git folder name). This is useful for:
# - Complete cleanup when switching branches
# - Resolving container/volume conflicts
# - Fresh start after testing
#
# Usage:
#   ./scripts/cleanup-aspire-clone.sh          # Interactive cleanup
#   ./scripts/cleanup-aspire-clone.sh --force  # Force cleanup without prompts
#   ./scripts/cleanup-aspire-clone.sh --dry-run # Preview what would be removed

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
WHITE='\033[1;37m'
GRAY='\033[0;37m'
NC='\033[0m' # No Color

# Parse arguments
FORCE=false
DRY_RUN=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --force|-f)
            FORCE=true
            shift
            ;;
        --dry-run|-d)
            DRY_RUN=true
            shift
            ;;
        --help|-h)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --force, -f     Skip confirmation prompts"
            echo "  --dry-run, -d   Show what would be removed without removing"
            echo "  --help, -h      Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

# Determine clone name from git folder
get_clone_name() {
    local git_dir
    git_dir=$(git rev-parse --git-dir 2>/dev/null || echo "")
    
    if [[ -n "$git_dir" ]]; then
        local git_common_dir
        git_common_dir=$(git rev-parse --git-common-dir 2>/dev/null || echo "")
        
        if [[ -n "$git_common_dir" ]] && [[ "$git_common_dir" != "$git_dir" ]]; then
            # This is a worktree
            local worktree_path
            worktree_path=$(dirname "$git_dir")
            basename "$worktree_path"
        else
            # Regular clone or main worktree
            local repo_root
            repo_root=$(git rev-parse --show-toplevel 2>/dev/null || echo "")
            if [[ -n "$repo_root" ]]; then
                basename "$repo_root"
            else
                echo "default"
            fi
        fi
    else
        echo "default"
    fi
}

CLONE_NAME=$(get_clone_name)
echo -e "${CYAN}Clone Name: $CLONE_NAME${NC}"
echo ""

# Find all containers for this clone
echo -e "${YELLOW}Searching for Aspire containers...${NC}"
CONTAINER_PATTERNS=(
    "Onboarding-$CLONE_NAME"
    "*-$CLONE_NAME"
    "aspire-*-$CLONE_NAME"
)

CONTAINERS=()
for pattern in "${CONTAINER_PATTERNS[@]}"; do
    while IFS= read -r line; do
        [[ -n "$line" ]] && CONTAINERS+=("$line")
    done < <(docker ps -a --filter "name=$pattern" --format "{{.ID}}|{{.Names}}" 2>/dev/null || true)
done

# Find all volumes for this clone
echo -e "${YELLOW}Searching for Aspire volumes...${NC}"
VOLUME_PATTERNS=(
    "cosmosdata-$CLONE_NAME"
    "storage-$CLONE_NAME"
    "*-$CLONE_NAME"
)

VOLUMES=()
for pattern in "${VOLUME_PATTERNS[@]}"; do
    while IFS= read -r line; do
        [[ -n "$line" ]] && VOLUMES+=("$line")
    done < <(docker volume ls --filter "name=$pattern" --format "{{.Name}}" 2>/dev/null || true)
done

# Display what will be removed
echo ""
echo -e "${CYAN}=== Resources to Remove ===${NC}"
echo ""

if [[ ${#CONTAINERS[@]} -eq 0 ]]; then
    echo -e "${GRAY}  No containers found${NC}"
else
    echo -e "${YELLOW}Containers (${#CONTAINERS[@]}):${NC}"
    for container in "${CONTAINERS[@]}"; do
        IFS='|' read -r id name <<< "$container"
        echo -e "${WHITE}  - $name ($id)${NC}"
    done
fi

echo ""

if [[ ${#VOLUMES[@]} -eq 0 ]]; then
    echo -e "${GRAY}  No volumes found${NC}"
else
    echo -e "${YELLOW}Volumes (${#VOLUMES[@]}):${NC}"
    for volume in "${VOLUMES[@]}"; do
        echo -e "${WHITE}  - $volume${NC}"
    done
fi

echo ""

# Exit if dry run
if [[ "$DRY_RUN" == true ]]; then
    echo -e "${GREEN}[DRY RUN] No changes made${NC}"
    exit 0
fi

# Exit if nothing to remove
if [[ ${#CONTAINERS[@]} -eq 0 ]] && [[ ${#VOLUMES[@]} -eq 0 ]]; then
    echo -e "${GREEN}Nothing to remove${NC}"
    exit 0
fi

# Confirm removal
if [[ "$FORCE" != true ]]; then
    echo -e "${RED}WARNING: This will permanently remove all listed resources!${NC}"
    read -r -p "Continue? (y/N) " -n 1 REPLY
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo -e "${YELLOW}Cancelled${NC}"
        exit 0
    fi
fi

# Remove containers
if [[ ${#CONTAINERS[@]} -gt 0 ]]; then
    echo ""
    echo -e "${YELLOW}Removing containers...${NC}"
    for container in "${CONTAINERS[@]}"; do
        IFS='|' read -r id name <<< "$container"
        
        echo -n "  Removing $name..."
        if docker rm -f "$id" >/dev/null 2>&1; then
            echo -e " ${GREEN}✓${NC}"
        else
            echo -e " ${RED}✗ Failed${NC}"
        fi
    done
fi

# Remove volumes
if [[ ${#VOLUMES[@]} -gt 0 ]]; then
    echo ""
    echo -e "${YELLOW}Removing volumes...${NC}"
    for volume in "${VOLUMES[@]}"; do
        echo -n "  Removing $volume..."
        if docker volume rm "$volume" >/dev/null 2>&1; then
            echo -e " ${GREEN}✓${NC}"
        else
            echo -e " ${RED}✗ Failed${NC}"
        fi
    done
fi

echo ""
echo -e "${GREEN}Cleanup complete!${NC}"
echo ""
echo -e "${CYAN}Note: You may need to restart the AppHost to recreate containers.${NC}"