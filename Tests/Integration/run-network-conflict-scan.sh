#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="${PROJECT_ROOT:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
SCENE="$PROJECT_ROOT/Assets/Scenes/NPCDialoguePrototype1.unity"
RUNTIME="$PROJECT_ROOT/Assets/Scripts/Runtime"
AUTH_SQL="$PROJECT_ROOT/Backend/PlayerAuthService/db/init.sql"

fail() { echo "FAIL: $*" >&2; exit 1; }
pass() { echo "PASS: $*"; }

if grep -R --include='*.cs' -nE '\.(StartHost|StartClient|StartServer)\s*\(' "$RUNTIME" \
  | grep -v 'NPCNetworkBootstrap.cs' \
  | grep -v '^[^:]*:[0-9]*:\s*///' \
  | grep -v '^[^:]*:[0-9]*:\s*//' >/tmp/npc-direct-start-offenders.txt; then
  cat /tmp/npc-direct-start-offenders.txt >&2
  fail "direct NetworkManager StartHost/StartClient/StartServer calls outside NPCNetworkBootstrap"
fi
pass "network start authority is centralized"

[[ -f "$SCENE" ]] || fail "scene not found: $SCENE"

if grep -n "autoDetectStartupMode: 1" "$SCENE" >/tmp/npc-autodetect.txt; then
  cat /tmp/npc-autodetect.txt >&2
  fail "main scene has AuthNetworkBridge autoDetectStartupMode enabled"
fi
pass "main scene does not enable auth startup auto-detect"

if grep -n "startAsHost: 1" "$SCENE" >/tmp/npc-start-host.txt; then
  cat /tmp/npc-start-host.txt >&2
  fail "main scene has AuthNetworkBridge startAsHost enabled"
fi
pass "main scene auth bridge is not configured as host"

if grep -n "startNetworkingAfterInitialization: 1" "$SCENE" >/tmp/npc-scene-start.txt; then
  cat /tmp/npc-scene-start.txt >&2
  fail "main scene initialization is configured to start networking"
fi
pass "scene initialization does not own network startup"

[[ -f "$AUTH_SQL" ]] || fail "auth SQL not found: $AUTH_SQL"
python3 - "$AUTH_SQL" <<'PY'
import re, sys
path = sys.argv[1]
text = open(path, encoding='utf-8').read()
# CREATE ROLE inside a DO $$ ... $$ block can be guarded with IF NOT EXISTS.
without_do_blocks = re.sub(r'DO\s+\$\$.*?\$\$\s*;', '', text, flags=re.IGNORECASE | re.DOTALL)
for line_no, line in enumerate(without_do_blocks.splitlines(), 1):
    if re.search(r'^\s*CREATE\s+ROLE\s+(web_anon|authenticator)\b', line, re.IGNORECASE):
        print(f'{path}:{line_no}: {line}', file=sys.stderr)
        sys.exit(1)
PY
pass "auth SQL role creation is guarded/idempotent"

for trigger in tr_v_players_insert tr_v_players_update tr_v_players_delete tr_v_player_sessions_insert tr_v_player_sessions_update tr_v_player_sessions_delete; do
  grep -q "DROP TRIGGER IF EXISTS $trigger" "$AUTH_SQL" || fail "missing DROP TRIGGER IF EXISTS for $trigger"
done
pass "auth SQL trigger creation is idempotent"
