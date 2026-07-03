#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="${PROJECT_ROOT:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
AUTH_ROOT="$PROJECT_ROOT/Backend/PlayerAuthService"
COMPOSE_FILE="$AUTH_ROOT/docker-compose.yml"
SQL_FILE="$AUTH_ROOT/db/init.sql"
CONTAINER="${AUTH_DB_CONTAINER:-unity-linux-llm-auth-db}"

cd "$AUTH_ROOT"
docker compose -f "$COMPOSE_FILE" up -d postgres-auth

for _ in {1..30}; do
  if docker exec "$CONTAINER" pg_isready -U postgres -d unity_linux_llm_auth >/dev/null 2>&1; then
    break
  fi
  sleep 1
done

docker exec "$CONTAINER" pg_isready -U postgres -d unity_linux_llm_auth >/dev/null

docker exec -i "$CONTAINER" psql -v ON_ERROR_STOP=1 -U postgres -d unity_linux_llm_auth < "$SQL_FILE" >/dev/null
docker exec -i "$CONTAINER" psql -v ON_ERROR_STOP=1 -U postgres -d unity_linux_llm_auth < "$SQL_FILE" >/dev/null

echo "PASS: auth init.sql is idempotent"
