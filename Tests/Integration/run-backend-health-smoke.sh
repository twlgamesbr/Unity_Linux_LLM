#!/usr/bin/env bash
set -euo pipefail

check() {
  local name="$1" url="$2"
  if curl -fsS --max-time 3 "$url" >/dev/null; then
    echo "$name PASS"
  else
    echo "$name FAIL ($url)" >&2
    return 1
  fi
}

check AUTH_HTTP "${AUTH_BASE_URL:-http://localhost:5100}/health"
check LOCALAI_MODELS "${LOCALAI_BASE_URL:-http://localhost:8080}/v1/models"
check QDRANT "${QDRANT_BASE_URL:-http://localhost:6333}/collections"

if [[ "${CHECK_COGNEE:-0}" == "1" ]]; then
  check COGNEE "${COGNEE_BASE_URL:-http://localhost:8000}/health"
else
  echo "COGNEE SKIPPED"
fi
