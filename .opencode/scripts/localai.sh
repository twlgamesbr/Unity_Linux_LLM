#!/usr/bin/env bash
# localai.sh — helper for LocalAI & Modal API calls
# Usage:
#   localai.sh chat <model> <prompt> [temperature]
#   localai.chat stream <model> <prompt>
#   localai.sh embed <text>
#   localai.sh models
#   localai.sh modal <model> <prompt>   # route through Modal proxy

set -euo pipefail

BASE="${LOCALAI_BASE:-http://localhost:8080/v1}"

cmd="${1:-help}"

case "$cmd" in
  chat)
    model="$2"
    prompt="$3"
    temp="${4:-0.7}"
    payload=$(jq -n --arg m "$model" --arg p "$prompt" --argjson t "$temp" \
      '{model: $m, messages: [{role: "user", content: $p}], temperature: $t, stream: false}')
    curl -s "$BASE/chat/completions" \
      -H 'Content-Type: application/json' \
      -d "$payload" | jq -r '.choices[0].message.content // .error.message // .'
    ;;

  stream)
    model="$2"
    prompt="$3"
    payload=$(jq -n --arg m "$model" --arg p "$prompt" \
      '{model: $m, messages: [{role: "user", content: $p}], stream: true}')
    curl -s --no-buffer "$BASE/chat/completions" \
      -H 'Content-Type: application/json' \
      -d "$payload" | while IFS= read -r line; do
        [[ "$line" =~ data:\ (.*) ]] || continue
        chunk="${BASH_REMATCH[1]}"
        [ "$chunk" = "[DONE]" ] && break
        echo "$chunk" | jq -r '.choices[0].delta.content // empty' 2>/dev/null
      done
    ;;

  embed)
    text="$2"
    model="${3:-nomic-embed-text-v1.5}"
    payload=$(jq -n --arg m "$model" --arg t "$text" \
      '{model: $m, input: $t}')
    curl -s "$BASE/embeddings" \
      -H 'Content-Type: application/json' \
      -d "$payload" | jq -c '.data[0].embedding'
    ;;

  models)
    curl -s "$BASE/models" | jq -r '.data[].id'
    ;;

  modal)
    model="$2"
    prompt="$3"
    payload=$(jq -n --arg m "$model" --arg p "$prompt" \
      '{model: $m, messages: [{role: "user", content: $p}], stream: false}')
    curl -s "$BASE/chat/completions" \
      -H 'Content-Type: application/json' \
      -d "$payload" | jq -r '.choices[0].message.content // .error.message // .'
    ;;

  *)
    echo "Usage:"
    echo "  localai.sh chat <model> <prompt> [temp]"
    echo "  localai.sh stream <model> <prompt>"
    echo "  localai.sh embed <text> [model]"
    echo "  localai.sh models"
    echo "  localai.sh modal <model> <prompt>"
    echo ""
    echo "Example models:"
    echo "  Tier 1: qwen2.5-1.5b-instruct-q4-k-m, llama-3.2-3b-instruct:q4_k_m"
    echo "  Tier 2: llama-3.1-8b-q4-k-m, gemma-4-e2b-it"
    echo "  Tier 3 (Modal): modal-vllm-qwen"
    ;;
esac
