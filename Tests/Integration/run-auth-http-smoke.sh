#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="${PROJECT_ROOT:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
AUTH_ROOT="$PROJECT_ROOT/Backend/PlayerAuthService"
BASE_URL="${AUTH_BASE_URL:-http://localhost:5100}"
STARTED_PID=""

cleanup() {
  if [[ -n "$STARTED_PID" ]]; then
    kill "$STARTED_PID" >/dev/null 2>&1 || true
    wait "$STARTED_PID" 2>/dev/null || true
  fi
}
trap cleanup EXIT

if ! curl -fsS "$BASE_URL/health" >/dev/null 2>&1; then
  echo "Auth API not healthy at $BASE_URL; starting dotnet service..."
  (cd "$AUTH_ROOT" && dotnet run --project PlayerAuthService.csproj > "$PROJECT_ROOT/TestResults/auth-http-smoke.log" 2>&1) &
  STARTED_PID="$!"
fi

for _ in {1..45}; do
  if curl -fsS "$BASE_URL/health" >/dev/null 2>&1; then
    echo "PASS: /health OK"
    break
  fi
  sleep 1
done
curl -fsS "$BASE_URL/health" >/dev/null

suffix="$(date +%s%N | tail -c 8)"
username="smoke_${suffix}"
password="Smoke_${suffix}_pw"
register_payload="{\"username\":\"$username\",\"password\":\"$password\"}"
login_payload="{\"username\":\"$username\",\"password\":\"$password\",\"rememberMe\":false}"

register_status=$(curl -sS -o /tmp/npc-auth-register.json -w '%{http_code}' -H 'Content-Type: application/json' -d "$register_payload" "$BASE_URL/api/auth/register")
[[ "$register_status" == "201" ]] || { cat /tmp/npc-auth-register.json >&2; echo "FAIL: register returned HTTP $register_status" >&2; exit 1; }
echo "PASS: register returned 201"

login_status=$(curl -sS -o /tmp/npc-auth-login.json -w '%{http_code}' -H 'Content-Type: application/json' -d "$login_payload" "$BASE_URL/api/auth/login")
[[ "$login_status" == "200" ]] || { cat /tmp/npc-auth-login.json >&2; echo "FAIL: login returned HTTP $login_status" >&2; exit 1; }
echo "PASS: login returned 200"
