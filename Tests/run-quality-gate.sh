#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="${PROJECT_ROOT:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)}"

"$PROJECT_ROOT/Tests/Integration/run-network-conflict-scan.sh"

if [[ "${RUN_EXTERNAL_SMOKE:-0}" == "1" ]]; then
  "$PROJECT_ROOT/Tests/Integration/run-auth-db-init-idempotency.sh"
  "$PROJECT_ROOT/Tests/Integration/run-auth-http-smoke.sh"
  "$PROJECT_ROOT/Tests/Integration/run-backend-health-smoke.sh"
  "$PROJECT_ROOT/Tests/Integration/run-dedicated-server-smoke.sh"
else
  echo "External smoke tests skipped (set RUN_EXTERNAL_SMOKE=1 to require Docker/Auth/LocalAI/Qdrant/server)."
fi

if [[ "${RUN_UNITY_TESTS:-0}" == "1" ]]; then
  "$PROJECT_ROOT/Tests/Unity/run-editmode-tests.sh"
else
  echo "Unity EditMode tests skipped (set RUN_UNITY_TESTS=1 to require Unity batchmode)."
fi
