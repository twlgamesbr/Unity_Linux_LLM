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
# Query /v1/models for available models
AVAILABLE_MODELS="[]"
AVAILABLE_MODELS=$(curl -sf http://localhost:8080/v1/models 2>/dev/null | \
  python3 -c "
import json,sys
try:
    d=json.load(sys.stdin)
    print(json.dumps([m['id'] for m in d.get('data',[])]))
except: print(json.dumps([]))
" 2>/dev/null) || true

# Query /api/backend-traces for recent model activity
RECENT_TRACES="[]"
TRACES_JSON=$(curl -sf http://localhost:8080/api/backend-traces 2>/dev/null) || TRACES_JSON="[]"
RECENT_TRACES=$(echo "$TRACES_JSON" | python3 -c "
import json,sys
try:
    traces = json.load(sys.stdin)
    if isinstance(traces, list):
        summary = []
        for t in traces[-5:]:
            summary.append({
                'model': t.get('model_name', '?'),
                'type': t.get('type', '?'),
                'duration_ms': round(t.get('duration', 0) / 1e6, 1),
                'tokens_in': t.get('data', {}).get('token_usage', {}).get('prompt', 0),
                'tokens_out': t.get('data', {}).get('token_usage', {}).get('completion', 0),
                'error': t.get('error', '')
            })
        print(json.dumps(summary))
    else:
        print(json.dumps([]))
except:
    print(json.dumps([]))
" 2>/dev/null) || RECENT_TRACES="[]"

# Query /metrics for LocalAI API call stats
METRICS_SAMPLE="{}"
METRICS=$(curl -sf http://localhost:8080/metrics 2>/dev/null) || METRICS=""
if [ -n "$METRICS" ]; then
  METRICS_SAMPLE=$(echo "$METRICS" | python3 -c "
import json,sys
try:
    lines = sys.stdin.read().split('\n')
    api_calls = [l for l in lines if 'api_call_count' in l and 'method=' in l]
    result = {}
    for l in api_calls[-5:]:
        parts = l.split()
        if len(parts) >= 2:
            result[l.strip()] = int(parts[-1])
    print(json.dumps(result))
except:
    print(json.dumps({}))
" 2>/dev/null) || METRICS_SAMPLE="{}"
fi

# Update long-term.json
jq --argjson containers "$CONTAINERS" \
   --argjson available_models "$AVAILABLE_MODELS" \
   --argjson recent_traces "$RECENT_TRACES" \
   --argjson metrics "$METRICS_SAMPLE" \
   --arg ts "$TIMESTAMP" \
   '.infrastructure_snapshot = {
      timestamp: $ts,
      containers: $containers,
      localai: {
        available_models: $available_models,
        recent_traces: $recent_traces,
        api_metrics: $metrics
      }
    } | .last_infra_check = $ts' \
   "$LONG_TERM" > "${LONG_TERM}.tmp" && mv "${LONG_TERM}.tmp" "$LONG_TERM"

echo "Infrastructure snapshot saved at $TIMESTAMP"
echo "Containers:"; echo "$CONTAINERS" | jq '.'
echo "Available models:"; echo "$AVAILABLE_MODELS" | jq '.'
echo "Recent traces:"; echo "$RECENT_TRACES" | jq '.'
echo "API metrics:"; echo "$METRICS_SAMPLE" | jq '.'
