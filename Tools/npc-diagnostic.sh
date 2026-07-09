#!/usr/bin/env bash
#
# NPC Dialogue Diagnostic — automated model sanity checker
# Tests every LocalAI model with a simple prompt, reports garbage output.
# Also validates model scene configuration.
#
set -euo pipefail

LOCALAI="${LOCALAI_URL:-http://localhost:8080}"
UNITY_SCENE="${UNITY_SCENE:-Assets/Scenes/NPCDialoguePrototype1.unity}"
PROJECT_ROOT="${PROJECT_ROOT:-/mnt/data/Projects_SSD/Unity_Projects/Unity_Linux_LLM}"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
BOLD='\033[1m'
NC='\033[0m'

PASS=0
FAIL=0
WARN=0

pass()  { PASS=$((PASS+1)); echo -e "  ${GREEN}✓ PASS${NC}: $1"; }
fail()  { FAIL=$((FAIL+1)); echo -e "  ${RED}✗ FAIL${NC}: $1"; }
warn()  { WARN=$((WARN+1)); echo -e "  ${YELLOW}⚠ WARN${NC}: $1"; }

echo -e "${BOLD}${CYAN}╔══════════════════════════════════════════════════════╗${NC}"
echo -e "${BOLD}${CYAN}║    NPC Dialogue Diagnostic — Model Sanity Checker    ║${NC}"
echo -e "${BOLD}${CYAN}╚══════════════════════════════════════════════════════╝${NC}"
echo ""

# ── Step 1: Check LocalAI is alive ──────────────────────────────────
echo -e "${BOLD}❶  LocalAI Health${NC}"
LOCALAI_OK=false
if curl -sf "$LOCALAI/health" > /dev/null 2>&1; then
    pass "LocalAI is healthy at $LOCALAI"
    LOCALAI_OK=true
elif curl -sf "$LOCALAI/v1/models" > /dev/null 2>&1; then
    warn "LocalAI /health failed but /v1/models responds"
    LOCALAI_OK=true
else
    fail "LocalAI is not reachable at $LOCALAI"
    echo ""
    echo -e "${BOLD}❷  Models (skipped — LocalAI unreachable)${NC}"
    echo ""
    echo -e "${BOLD}❸  GGUF File Audit (skipped)${NC}"
    echo ""
    echo -e "${BOLD}❹  Scene Config Check (skipped)${NC}"
    echo ""
    summary
    exit 1
fi
echo ""

# ── Step 2: Test every model ────────────────────────────────────────
echo -e "${BOLD}❷  Model Inference Tests${NC}"

# Get model list from LocalAI
MODELS=$(curl -sf "$LOCALAI/v1/models" | python3 -c "
import json,sys
data = json.load(sys.stdin)
for m in data.get('data', []):
    print(m['id'])
" 2>/dev/null)

if [ -z "$MODELS" ]; then
    fail "No models returned by LocalAI"
else
    echo -e "  Models found: $(echo "$MODELS" | wc -l)"
fi

echo ""

COHERENT_THRESHOLD=${COHERENCE_THRESHOLD:-0.3}
# Python coherence checker — single script to avoid heredoc issues
infer_model() {
    local model="$1"
    local result
    result=$(curl -sf -X POST "$LOCALAI/v1/chat/completions" \
        -H "Content-Type: application/json" \
        -d '{
            "model":"'"$model"'",
            "messages":[{"role":"user","content":"Repeat exactly: HELLO123"}],
            "max_tokens":15,
            "temperature":0.01,
            "top_p":0.1,
            "seed":42
        }' 2>&1) || true

    echo "$result"
}

for model in $MODELS; do
    echo -ne "  Testing: ${CYAN}$model${NC} ... "

    # Skip embedding models
    case "$model" in
        *embedding*|*nomic*|*mmproj*) echo -e "${YELLOW}SKIP (embedding/vision)${NC}"; continue;;
        *router*|*intelligent*) echo -e "${YELLOW}SKIP (router)${NC}"; continue;;
    esac

    TIMEOUT_FLAG=""
    timeout 120 bash -c "
        result=\$(curl -sf -X POST $LOCALAI/v1/chat/completions \
            -H 'Content-Type: application/json' \
            -d '{\"model\":\"$model\",\"messages\":[{\"role\":\"user\",\"content\":\"Repeat exactly: HELLO123\"}],\"max_tokens\":15,\"temperature\":0.01,\"top_p\":0.1,\"seed\":42}' 2>&1)
        echo \"\$result\" > /tmp/npc_diag_${model//\//_}.json
    " 2>/dev/null && TIMEOUT_FLAG="ok" || TIMEOUT_FLAG="timeout"

    if [ "$TIMEOUT_FLAG" = "timeout" ]; then
        echo -e "${RED}TIMEOUT (>120s)${NC}"
        fail "$model — inference timeout (>120s)"
        continue
    fi

    # Parse response
    content=$(python3 -c "
import json,sys
try:
    with open('/tmp/npc_diag_${model//\//_}.json') as f:
        d = json.load(f)
    c = d.get('choices', [{}])[0].get('message', {}).get('content', '')
    print(repr(c))
except: print('PARSE_ERROR')
" 2>/dev/null)

    if [ "$content" = "PARSE_ERROR" ] || [ -z "$content" ]; then
        echo -e "${RED}PARSE ERROR${NC}"
        fail "$model — response parse failure"
        continue
    fi

    # Check for garbage using heuristics
    python3 -c "
import json, sys, math
content = $content
text = content.strip()

# Heuristic 1: Repetition ratio
if len(text) == 0:
    print('EMPTY')
    sys.exit(0)

# Count unique words vs total words for repetition detection
words = text.split()
if len(words) > 1:
    unique_ratio = len(set(words)) / len(words)
else:
    unique_ratio = 1.0

# Heuristic 2: Garbage character ratio (escape sequences, LaTeX fragments, math symbols)
garbage_patterns = ['\\\\l', '\\\\q', '\\\\t', '\\\\th', '\\\\text', '\\\\cdot', '\\\\approx',
                   '\\\\theta', '\\\\sum', '\\\\tight', '\\\\qrr', '\\\\ld', '\\\\quad',
                   '\\times', '\\q', '\\ld', '\\th']
garbage_count = sum(1 for p in garbage_patterns if p in text)
garbage_ratio = garbage_count / max(len(garbage_patterns), 1)

# Heuristic 3: Response is just the stop-word / special token output
meaningful_words = [w for w in words if len(w) > 1 and not w.startswith('<') and not w.startswith('\\\\')]
meaningful_ratio = len(meaningful_words) / max(len(words), 1)

# Heuristic 4: Exact expected output for 'Repeat exactly: HELLO123' 
contains_hello = 'HELLO123' in text

# Score
if len(text) > 100 and garbage_ratio > 0.2:
    print('GARBAGE_LATEX')
elif unique_ratio < 0.3 and len(words) >= 5:
    print('REPETITIVE')
elif not meaningful_words and words:
    print('NONSENSE')
elif contains_hello:
    print('OK')
elif len(text) > 0 and len(text) < 30 and unique_ratio > 0.5:
    print('OK')
else:
    print(f'UNCERTAIN(u={unique_ratio:.2f},g={garbage_ratio:.2f},m={meaningful_ratio:.2f})')
" 2>/dev/null > /tmp/npc_verdict_${model//\//_}.txt

    verdict=$(cat /tmp/npc_verdict_${model//\//_}.txt 2>/dev/null || echo "UNKNOWN")
    short_content=$(echo "$content" | head -c 80)

    case "$verdict" in
        OK)
            echo -e "${GREEN}OK${NC} → $short_content"
            pass "$model — coherent output"
            ;;
        GARBAGE*|REPETITIVE|NONSENSE)
            echo -e "${RED}${verdict}${NC} → $short_content"
            fail "$model — garbage output (verdict: $verdict)"
            ;;
        EMPTY)
            echo -e "${RED}EMPTY${NC}"
            fail "$model — empty response"
            ;;
        *)
            echo -e "${YELLOW}${verdict}${NC} → $short_content"
            warn "$model — uncertain coherence (verdict: $verdict)"
            ;;
    esac
    rm -f "/tmp/npc_diag_${model//\//_}.json" "/tmp/npc_verdict_${model//\//_}.txt"
done
echo ""

# ── Step 3: GGUF metadata audit ─────────────────────────────────────
echo -e "${BOLD}❸  GGUF Model File Audit${NC}"

# Get GGUF files from inside the container (if docker available)
if command -v docker &> /dev/null && docker ps --format '{{.Names}}' | grep -q localai; then
    CONTAINER=$(docker ps --format '{{.Names}}' | grep localai | head -1)
    echo -e "  Using container: ${CYAN}$CONTAINER${NC}"
    
    GGUF_FILES=$(docker exec "$CONTAINER" find /models -name "*.gguf" -type f 2>/dev/null)
    
    for gguf in $GGUF_FILES; do
        fname=$(basename "$gguf")
        fsize=$(docker exec "$CONTAINER" stat --format="%s" "$gguf" 2>/dev/null || echo "0")
        fsize_gb=$(python3 -c "print(f'{float($fsize)/1e9:.1f}')" 2>/dev/null)
        
        # Check GGUF header with increased buffer and full-string search fallback
        docker exec "$CONTAINER" python3 -c "
import struct, sys
try:
    with open('$gguf', 'rb') as f:
        h = f.read(65536)  # 64KB for metadata
    if h[:4] != b'GGUF':
        print('  NOT a GGUF file')
        sys.exit(0)
    ver = struct.unpack('<I', h[4:8])[0]
    tensors = struct.unpack('<Q', h[8:16])[0]
    
    # Simple string search for key metadata
    arch = 'unknown'
    nam = 'unknown'
    for key_bytes, val in [(b'general.architecture', None), (b'general.name', None)]:
        idx = h.find(key_bytes + b'\x08')
        if idx >= 0:
            pos = idx + len(key_bytes) + 5  # skip key + type byte
            vlen = struct.unpack('<Q', h[pos:pos+8])[0]
            val = h[pos+8:pos+8+vlen].decode('utf-8', errors='replace')
            if key_bytes == b'general.architecture': arch = val
            elif key_bytes == b'general.name': nam = val
    
    # Detect quantization from filename or file_type metadata
    fname = '$fname'
    quant = 'unknown'
    if 'q4_k_m' in fname.lower(): quant = 'Q4_K_M'
    elif 'q8_0' in fname.lower(): quant = 'Q8_0'
    elif 'q4_0' in fname.lower(): quant = 'Q4_0'
    elif 'f16' in fname.lower(): quant = 'F16'
    
    print(f'  arch={arch} name={nam} tensors={tensors} gguvf={ver} quant={quant}')
except Exception as e:
    print(f'  ERROR: {e}')
" 2>/dev/null > /tmp/gguf_meta_${fname}.txt

        meta=$(cat /tmp/gguf_meta_${fname}.txt 2>/dev/null || echo "PARSE_ERROR")
        size_display="${fsize_gb}G"
        
        echo -e "  • ${CYAN}$fname${NC} (${size_display})"
        echo -e "    ${meta}"
        rm -f "/tmp/gguf_meta_${fname}.txt"
    done
else
    warn "Docker not available — skipping GGUF file inspection"
fi
echo ""

# ── Step 4: Scene configuration check ──────────────────────────────
echo -e "${BOLD}❹  Unity Scene Configuration${NC}"
if [ -f "$PROJECT_ROOT/$UNITY_SCENE" ]; then
    # Check which model is configured in the scene (Unity YAML format)
    SCENE_MODEL=$(grep -oP 'remoteModel:\s*\K\S+' "$PROJECT_ROOT/$UNITY_SCENE" 2>/dev/null | tr -d '{}' || echo "NOT_FOUND")
    SCENE_HOST=$(grep -oP 'remoteHost:\s*\K\S+' "$PROJECT_ROOT/$UNITY_SCENE" 2>/dev/null | tr -d '{}' || echo "NOT_FOUND")
    SCENE_PORT=$(grep -oP 'remotePort:\s*\K\S+' "$PROJECT_ROOT/$UNITY_SCENE" 2>/dev/null | tr -d '{}' || echo "NOT_FOUND")
    
    echo -e "  Scene: ${CYAN}$UNITY_SCENE${NC}"
    echo -e "  Configured model: ${BOLD}$SCENE_MODEL${NC}"
    echo -e "  Remote endpoint: ${BOLD}$SCENE_HOST:$SCENE_PORT${NC}"
    
    # Cross-reference with inference test results
    if [ "$SCENE_MODEL" != "NOT_FOUND" ]; then
        # Check if this model was tested
        if echo "$MODELS" | grep -q "$SCENE_MODEL"; then
            echo -e "  ${GREEN}✓${NC} Model '$SCENE_MODEL' is registered in LocalAI"
        else
            fail "Model '$SCENE_MODEL' in scene config NOT found in LocalAI models"
        fi
    fi
else
    warn "Scene file not found at $UNITY_SCENE"
fi
echo ""

# ── Summary ─────────────────────────────────────────────────────────
summary() {
    TOTAL=$((PASS + FAIL + WARN))
    echo -e "${BOLD}${CYAN}══════════════════════════════════════════════════════${NC}"
    echo -e "  ${BOLD}RESULTS:${NC}  ${GREEN}$PASS passed${NC}  ${RED}$FAIL failed${NC}  ${YELLOW}$WARN warnings${NC}  |  $TOTAL total"
    echo -e "${BOLD}${CYAN}══════════════════════════════════════════════════════${NC}"
    echo ""

    if [ $FAIL -gt 0 ] || [ $WARN -gt 0 ]; then
        echo -e "${YELLOW}Diagnostic Legend:${NC}"
        echo -e "  • A model that fails the inference test produces GARBAGE output."
        echo -e "  • This is MOST LIKELY a fine-tuning pipeline issue (Unsloth save_pretrained_gguf)."
        echo -e "  • Switch NPCDialogueManager.remoteModel to a STOCK model (e.g. llama-3.1-8b-q4-k-m)"
        echo -e "    for immediate recovery."
        echo ""
        echo -e "  ${BOLD}See the full root-cause analysis below for fix guidance.${NC}"
    fi
}

summary
