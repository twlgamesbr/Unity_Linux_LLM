#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 1 ]]; then
  echo "usage: $0 <collection-name> [additional codebase-embedder index args...]" >&2
  exit 2
fi

collection="$1"
shift || true

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../.." && pwd)"
embedder_dir="$repo_root/Tools/CodebaseEmbedder"

export UV_CACHE_DIR="${UV_CACHE_DIR:-/tmp/uv-cache}"
export UV_TOOL_DIR="${UV_TOOL_DIR:-/tmp/uv-tools}"

cd "$embedder_dir"

echo "== status =="
uv run codebase-embedder status --root ../.. --collection "$collection"

echo
echo "== local audit =="
uv run codebase-embedder audit --root ../.. --scenario localai-llmunity --local --collection "$collection" || true

echo
echo "== live index =="
uv run codebase-embedder index --root ../.. --collection "$collection" "$@"

echo
echo "== smoke queries =="
prompts=(
  "list namespaces used by NPCSystem"
  "which asmdefs own npc dialogue runtime"
  "where is LocalAI chat transport implemented"
  "what classes handle qdrant rag"
)

for prompt in "${prompts[@]}"; do
  echo
  echo "-- $prompt"
  uv run codebase-embedder query --root ../.. --collection "$collection" "$prompt" || true
done
