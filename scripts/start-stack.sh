#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════════
# Unity Linux LLM — Docker Stack Orchestrator
# ═══════════════════════════════════════════════════════════════════
# Starts/stops the full development stack with health-check gating
# and a status summary table.
#
# Usage:
#   ./scripts/start-stack.sh                       # start core services
#   ./scripts/start-stack.sh --profile all          # start everything
#   ./scripts/start-stack.sh --profile game         # + dedicated server + webgl
#   ./scripts/start-stack.sh --stop                 # tear down everything
#   ./scripts/start-stack.sh --status               # print health table
#   ./scripts/start-stack.sh --restart              # stop + start core
#   ./scripts/start-stack.sh --health               # just check endpoints
#
# Profiles:
#   core       — Qdrant, LocalAI proxy, Supabase (auth, rest, db, realtime...)
#   monitoring — Datadog Agent
#   game       — Dedicated server, WebGL client
#   devtools   — Codebase watchdog
# ═══════════════════════════════════════════════════════════════════

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
COMPOSE_FILE="$PROJECT_ROOT/docker-compose.yml"
ENV_FILE="$PROJECT_ROOT/.env"
ENV_TEMPLATE="$PROJECT_ROOT/.env.template"
BACKEND_DIR="$PROJECT_ROOT/Backend"

# ── Composed service definitions ───────────────────────────────────
declare -A COMPOSE_GROUPS
COMPOSE_GROUPS["core"]="Backend/qdrant/docker-compose.yml Backend/localai-proxy/docker-compose.yml Backend/supabase-stack/docker-compose.yml"
COMPOSE_GROUPS["monitoring"]="Backend/datadog-host/docker-compose.yml"
COMPOSE_GROUPS["game"]="Backend/unity-dedicated-server/docker-compose.yml Backend/webgl-client/docker-compose.yml"
COMPOSE_GROUPS["devtools"]="Backend/codebase-watchdog/docker-compose.yml"

# ── Defaults ──────────────────────────────────────────────────────
PROFILE="core"
ACTION="up"

# ── Parse args ────────────────────────────────────────────────────
while [[ $# -gt 0 ]]; do
  case "$1" in
    --profile|-p)    PROFILE="$2"; shift 2 ;;
    --stop)          ACTION="stop"   ; shift ;;
    --restart)       ACTION="restart"; shift ;;
    --status|-s)     ACTION="status" ; shift ;;
    --health|-c)     ACTION="health" ; shift ;;
    --help|-h)       echo "Usage: $0 [--profile <name>] [--stop|--restart|--status|--health]"; exit 0 ;;
    *)               echo "Unknown: $1"; exit 1 ;;
  esac
done

# ── Support functions ─────────────────────────────────────────────

color() { local code="$1"; shift; echo -e "\033[${code}m$*\033[0m"; }
green()  { color 32 "$@"; }
yellow() { color 33 "$@"; }
red()    { color 31 "$@"; }
bold()   { color 1 "$@"; }

check_env() {
  if [[ ! -f "$ENV_FILE" ]]; then
    yellow "WARNING: No .env file — creating from .env.template with dev defaults..."
    cp "$ENV_TEMPLATE" "$ENV_FILE"
  fi
  set +e
  source "$ENV_FILE" 2>/dev/null
  set -e
  if [[ -z "${DD_API_KEY:-}" ]]; then
    yellow "WARNING: DD_API_KEY is unset. Datadog monitoring will be degraded."
  fi
}

# Execute docker compose on each file in the current profile group
compose_on_group() {
  local action="$1"  # up, down, ps, etc.
  local extra_args="${2:-}"
  local group_key="$PROFILE"

  if [[ "$PROFILE" == "all" ]]; then
    for key in core monitoring game devtools; do
      _compose_on_files "$action" "$extra_args" "${COMPOSE_GROUPS[$key]}"
    done
  else
    local files="${COMPOSE_GROUPS[$group_key]:-}"
    if [[ -z "$files" ]]; then
      red "Unknown profile: $group_key"
      echo "Available: core, monitoring, game, devtools, all"
      exit 1
    fi
    _compose_on_files "$action" "$extra_args" "$files"
  fi
}

_compose_on_files() {
  local action="$1"
  local extra_args="$2"
  shift 2
  local files_list="$*"

  for file_rel in $files_list; do
    local file="$PROJECT_ROOT/$file_rel"
    if [[ ! -f "$file" ]]; then
      yellow "  Skipping (not found): $file"
      continue
    fi
    local dir="$(dirname "$file")"
    local name="$(basename "$dir")"
    echo "  [$name] docker compose $action..."
    cd "$dir"
    docker compose $action $extra_args 2>&1 \
      | grep -v 'Network.*\(Created\|Removed\)' \
      | grep -v 'network external\|Network.*external' \
      | sed "s/^/    /"
  done
  cd "$PROJECT_ROOT"
}

do_up() {
  bold "Starting Unity Linux LLM — profile: $PROFILE"
  echo ""
  compose_on_group "up" "-d"
  echo ""
  sleep 3
  do_health
  echo ""
  do_status
}

do_stop() {
  bold "Stopping all Unity Linux LLM containers..."
  compose_on_group "down" "--remove-orphans"
  echo ""
  bold "Also stopping any remaining named containers..."
  for container in \
    qdrant supabase-stack-db supabase-stack-auth supabase-stack-rest \
    supabase-stack-realtime supabase-stack-imgproxy supabase-stack-storage \
    supabase-stack-meta supabase-stack-functions supabase-stack-studio \
    supabase-stack-pg-exporter localai-proxy dd-agent npc-webgl-client \
    ddproxy npc-dedicated-server codebase-watchdog; do
    docker stop "$container" 2>/dev/null && echo "  Stopped: $container" || true
  done
  echo "  Done."
}

do_status() {
  bold "Unity Linux LLM — Container Status"
  echo ""
  printf "  %-32s %-12s %-10s %s\n" "CONTAINER" "STATUS" "PORTS" "HEALTH"
  printf -- "  %-32s %-12s %-10s %s\n" "$(printf '═%.0s' {1..32})" "$(printf '═%.0s' {1..12})" "$(printf '═%.0s' {1..10})" "$(printf '═%.0s' {1..20})"

  local containers=(
    "qdrant:6333"
    "supabase-stack-db:55432"
    "supabase-stack-auth:8091"
    "supabase-stack-rest:8092"
    "realtime-dev.supabase-realtime:8093"
    "supabase-stack-imgproxy:8095"
    "supabase-stack-storage:8094"
    "supabase-stack-meta:8096"
    "supabase-stack-functions:8098"
    "supabase-stack-studio:8097"
    "supabase-stack-pg-exporter:9187"
    "localai-proxy:8090"
    "dd-agent:-"
    "npc-webgl-client:8085"
    "ddproxy:9090"
    "npc-dedicated-server:11474"
    "codebase-watchdog:-"
  )

  for entry in "${containers[@]}"; do
    local name="${entry%%:*}"
    local port="${entry##*:}"
    local status_text="$(docker inspect --format='{{.State.Status}}' "$name" 2>/dev/null || echo "missing")"
    local health_text="$(docker inspect --format='{{if .State.Health}}{{.State.Health.Status}}{{else}}—{{end}}' "$name" 2>/dev/null || echo "—")"

    case "$status_text" in
      running)  status_fmt="$(green "running")" ;;
      missing)  status_fmt="$(yellow "missing")" ;;
      *)        status_fmt="$(red "$status_text")" ;;
    esac

    case "$health_text" in
      healthy)  health_fmt="$(green "healthy")" ;;
      —)        health_fmt="—" ;;
      *)        health_fmt="$(yellow "$health_text")" ;;
    esac

    local ports_display="$port"
    [[ "$port" == "-" ]] && ports_display="—"

    printf "  %-32s %-12b %-10s %b\n" "$name" "$status_fmt" "$ports_display" "$health_fmt"
  done

  echo ""
  yellow "Note: LocalAI runs as a systemd service (localai-orchestrator.service), not Docker."
  echo "  systemctl --user status localai-orchestrator.service"
}

do_health() {
  bold "Health Checks"
  echo ""

  local failed=0
  local endpoints=(
    "Qdrant:http://localhost:6333/healthz"
    "LocalAI Proxy:http://localhost:8090/health"
    "Supabase Auth:http://localhost:8091/health"
  )

  for entry in "${endpoints[@]}"; do
    local name="${entry%%:*}"
    local url="${entry#*:}"
    printf "  %-25s " "$name"
    if wget -qO- --timeout=5 "$url" >/dev/null 2>&1; then
      green "OK"
    else
      red "FAIL"
      failed=1
    fi
  done

  echo ""
  if [[ "$failed" -eq 0 ]]; then
    green "  ✓ All core services healthy."
  else
    yellow "  ⚠ Some services unreachable (may still be starting up)."
    yellow "  Run '$0 --status' to check container states."
    return 1
  fi
}

# ── Main ──────────────────────────────────────────────────────────

cd "$PROJECT_ROOT"

case "$ACTION" in
  up)
    check_env
    do_up
    ;;

  stop)
    do_stop
    ;;

  restart)
    bold "Restarting stack..."
    do_stop
    echo ""
    sleep 2
    check_env
    do_up
    ;;

  status)
    do_status
    ;;

  health)
    do_health
    ;;
esac
