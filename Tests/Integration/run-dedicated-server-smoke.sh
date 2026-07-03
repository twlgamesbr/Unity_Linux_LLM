#!/usr/bin/env bash
set -euo pipefail

CONTAINER="${NPC_SERVER_CONTAINER:-npc-dedicated-server}"
PORT="${NPC_SERVER_PORT:-11474}"

docker ps --format '{{.Names}}' | grep -Fx "$CONTAINER" >/dev/null || { echo "FAIL: container $CONTAINER is not running" >&2; exit 1; }
echo "PASS: container $CONTAINER is running"

if ss -lunp | grep -q ":$PORT\b"; then
  echo "PASS: UDP port $PORT is bound"
else
  echo "FAIL: UDP port $PORT is not bound" >&2
  ss -lunp >&2 || true
  exit 1
fi

logs="$(docker logs "$CONTAINER" --since 10m 2>&1 || true)"
if grep -E 'Server started|NetworkHost.*Success|Starting configured network mode|Transport configured' <<<"$logs" >/dev/null; then
  echo "PASS: server logs contain network startup signal"
else
  echo "FAIL: server logs do not contain expected network startup signal" >&2
  tail -n 120 <<<"$logs" >&2
  exit 1
fi
