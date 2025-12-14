#!/bin/bash
# Cleanup script - Stops all Cosmos DB containers and removes clone state

echo -e "\033[1;31mClone Cleanup\033[0m"
echo -e "\033[1;31m======================================\033[0m"
echo ""

# Stop all running Cosmos DB containers
echo -e "\033[1;33mStopping Cosmos DB containers...\033[0m"
containers=$(docker ps -a --filter "ancestor=mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator" --format "{{.ID}}")

if [ -n "$containers" ]; then
    echo "$containers" | while read -r container; do
        echo -e "\033[0;37m  Removing container: $container\033[0m"
        docker rm -f "$container" > /dev/null 2>&1
    done
    echo -e "\033[1;32mAll Cosmos DB containers stopped.\033[0m"
else
    echo -e "\033[0;37mNo Cosmos DB containers found.\033[0m"
fi

echo ""

# Remove state file
state_file=".aspire/state/cosmos-clones.json"
if [ -f "$state_file" ]; then
    echo -e "\033[1;33mRemoving clone state file...\033[0m"
    rm -f "$state_file"
    echo -e "\033[1;32mClone state file removed.\033[0m"
else
    echo -e "\033[0;37mNo clone state file found.\033[0m"
fi

echo ""
echo -e "\033[1;32mCleanup complete!\033[0m"
echo ""
echo -e "\033[1;36mNote: Docker volumes are preserved. To remove them, run:\033[0m"
echo -e "\033[0;37m  docker volume ls | grep cosmosdata | awk '{print \$2}' | xargs -r docker volume rm\033[0m"