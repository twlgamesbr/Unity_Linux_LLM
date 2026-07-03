#!/usr/bin/env bash
# infra-snapshot.sh — save current Docker infrastructure state to memory
# Run this to update long-term.json with live container status.
# Output: prints the updated infrastructure block.

set -euo pipefail

MEMORY_DIR="$(dirname "$0")/../memory"
LONG_TERM="$MEMORY_DIR/long-term.json"
TIMESTAMP=$(date -u +"%Y-%m-%dT%H:%M:%SZ")

# Build container status JSON
CONTAINERS=$(docker ps --format '{{json .}}' | jq -s '
  map({
    (.Names): {
      image: .Image,
      status: (.Status | sub(" "; "_")),
      created: .CreatedAt,
      ports: .Ports,
      state: .State
    }
  }) | add
')

# Update long-term.json
jq --argjson containers "$CONTAINERS" \
   --arg ts "$TIMESTAMP" \
   '.infrastructure_snapshot = {
      timestamp: $ts,
      containers: $containers
    } | .last_infra_check = $ts' \
   "$LONG_TERM" > "${LONG_TERM}.tmp" && mv "${LONG_TERM}.tmp" "$LONG_TERM"

echo "Infrastructure snapshot saved at $TIMESTAMP"
echo "$CONTAINERS" | jq '.'
