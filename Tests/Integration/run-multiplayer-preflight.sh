#!/usr/bin/env bash
set -u

PROJECT_ROOT="${PROJECT_ROOT:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
FAILED=0

run_stage() {
  local name="$1" script="$2"
  echo "==== $name ===="
  if "$script"; then
    echo "$name PASS"
  else
    echo "$name FAIL" >&2
    FAILED=1
  fi
}

run_stage CONFLICT_SCAN "$PROJECT_ROOT/Tests/Integration/run-network-conflict-scan.sh"
run_stage AUTH_DB_INIT "$PROJECT_ROOT/Tests/Integration/run-auth-db-init-idempotency.sh"
run_stage AUTH_HTTP "$PROJECT_ROOT/Tests/Integration/run-auth-http-smoke.sh"
run_stage BACKEND_HEALTH "$PROJECT_ROOT/Tests/Integration/run-backend-health-smoke.sh"
run_stage DEDICATED_SERVER "$PROJECT_ROOT/Tests/Integration/run-dedicated-server-smoke.sh"

if [[ "${RUN_UNITY_TESTS:-0}" == "1" ]]; then
  run_stage UNITY_EDITMODE "$PROJECT_ROOT/Tests/Unity/run-editmode-tests.sh"
else
  echo "UNITY_EDITMODE SKIPPED (set RUN_UNITY_TESTS=1 to enable)"
fi

exit "$FAILED"
