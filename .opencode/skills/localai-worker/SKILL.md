---
name: localai-worker
description: Use when delegating subtasks to LocalAI or Modal models for token economy. Covers 3-tier model routing, subagent invocation, and persistent memory maintenance. Activates for any task mentioning code scanning, code analysis, bulk file processing, fine-tuning, or Modal deployment.
---

# LocalAI Worker — 3-Tier Delegation Skill

## Memory system

Agent memory lives in `.opencode/memory/`. Load on session start, save on session end.

| File | Purpose |
|---|---|
| `identity.json` | Who you are, what you can do |
| `long-term.json` | Persistent facts about project & user |
| `sessions/YYYY-MM-DD--slug.json` | Per-session summary |
| `index.json` | Embedding index for semantic search |

### On every session start
```bash
cat .opencode/memory/identity.json
cat .opencode/memory/long-term.json
```

### On every session end
Append important facts to `long-term.json` and update `index.json` with a new embedding.

### Semantic memory search
```bash
EMBED=$(bash .opencode/scripts/localai.sh embed "your query here")
# Then cosine-similarity against index.json entries
```

## 3-Tier Model Routing

| Tier | Models | Cost | For |
|---|---|---|---|
| **1** | `qwen2.5-1.5b-instruct-q4-k-m`, `llama-3.2-3b-instruct:q8_0` | ~free (local GPU) | Bulk code scan, class ID, file summaries, simple Q&A |
| **2** | `llama-3.1-8b-q4-k-m`, `gemma-4-e2b-it` | ~free (local GPU) | Refactoring, generation, medium reasoning |
| **3** | `modal-vllm-qwen` (Modal H100), `qwen2.5:32b` (Modal A100) | Cloud GPU $ | Complex analysis, architecture, large-scale refactoring |

**Rule:** Always use the cheapest tier that can do the job. Escalate only when the task requires it.

## Subagent delegation

Use the `task` tool with these agents:

| Agent | Model | Permission | When |
|---|---|---|---|
| `code-scanner` | `localai/qwen2.5-1.5b-instruct-q4-k-m` | read-only | Bulk code analysis, class scanning |
| `architect` | `localai/llama-3.1-8b-q4-k-m` | read + write | Complex design, refactoring, architecture |
| `trainer` | `localai/llama-3.1-8b-q4-k-m` | read + write + bash | Fine-tuning orchestration |

The subagents know to call `bash .opencode/scripts/localai.sh` directly for their work.

## Calling LocalAI directly (from this agent)

```bash
# Chat completion
bash .opencode/scripts/localai.sh chat llama-3.2-3b-instruct:q8_0 "Your prompt here"

# Embedding
bash .opencode/scripts/localai.sh embed "Text to embed"

# List models
bash .opencode/scripts/localai.sh models
```

## Modal deployment

When deploying or improving Modal infrastructure:

1. Examples live at `/mnt/data/Projects_SSD/modal-examples/`
2. The currently deployed Modal vLLM: `modal-qwen-vllm` (Qwen3-8B on H100)
3. The LocalAI gateway proxies Modal at `modal-vllm-qwen` model name
4. Global opencode config has `modal-vllm` and `modal-ollama` providers configured at `~/.config/opencode/opencode.jsonc`

Useful Modal commands:
```bash
cd /mnt/data/Projects_SSD/modal-examples
modal deploy 06_gpu_and_ml/llm-serving/modal_qwen_vllm.py   # Deploy vLLM
modal deploy 06_gpu_and_ml/llm-serving/vllm_low_latency.py   # Low-latency vLLM
modal run 06_gpu_and_ml/llm-serving/vllm_inference.py        # Test inference
modal run --detach 06_gpu_and_ml/llm-serving/vllm_throughput.py  # Batch job
modal deploy 06_gpu_and_ml/llm-serving/ollama_llama.py       # Ollama on A100
```

## Fine-tuning

When running training jobs, use the `trainer` subagent. It knows how to construct dataset pairs, run LoRA fine-tuning scripts, and manage training runs through LocalAI's fine-tuning API or Modal batch jobs.

## Infrastructure — Docker containers

Live containers (check with `docker ps`):

| Container | Port | Access |
|---|---|---|
| `pc-resource-localai` | 8080 | `http://localhost:8080/v1` |
| `qdrant-codebase` | 6333 | `http://localhost:6333` |
| `unity-linux-llm-auth-db` | 5433 (ext) / 5432 (int) | `postgresql://postgres:postgres@localhost:5433/unity_linux_llm_auth` |
| `npc-dedicated-server` | 11474 (host) | `localhost:11474` |

The NPC dedicated server uses `network_mode: host`, so it can reach all other services on localhost directly. Qdrant is on the bridge network.

All compose files are tracked in `long-term.json`. Snapshot current state with:

```bash
bash .opencode/scripts/infra-snapshot.sh
```

## Project-specific models (via LocalAI gateway)

The LocalAI at `localhost:8080` proxies these models:
- `modal-vllm-qwen` → Modal vLLM H100 (Qwen3-8B)
- All local gguf models are served directly
