---
name: opencode-self
description: Self-awareness skill for opencode agent. Describes all local capabilities — commands, tools, skills, agents, memory, infrastructure, and model routing. Load this when asked about your own capabilities or configuration.
---

# opencode-self — Agent Capability Map

## Where your config lives

```
.opencode/          → Project-local config (this project only)
~/.config/opencode/ → Global config (shared across all projects)
```

## Your current features

### 1. Commands (`/` in TUI)
Defined in `.opencode/commands/*.md`:

| Command | Description |
|---|---|
| `/infra` | Snapshot Docker infrastructure state |
| `/ps` | List running Docker containers |
| `/scan` | Scan codebase for classes/types (delegates to code-scanner agent) |
| `/remember` | Store a fact in long-term memory |
| `/memory` | Semantic search across long-term memory |
| `/modal` | Deploy or check Modal inference endpoints |

### 2. Custom Tools (LLM-callable)
Defined in `.opencode/tools/*.ts`:

| Tool | Description |
|---|---|
| `memory-search` | Semantic search across agent memory via LocalAI embeddings |
| `memory-store` | Store fact in long-term memory + auto-index |
| `infra-status` | Check Docker container health |
| `localai-chat` | Direct LocalAI model chat (Tier 1/2) |
| `modal-query` | Query Modal vLLM/Ollama (Tier 3) |

### 3. Skills (reusable instructions)
Defined in `.opencode/skills/*/SKILL.md`:

| Skill | Description |
|---|---|
| `localai-worker` | 3-tier model routing, subagent delegation, infrastructure |
| `opencode-self` | **This file** — your own capability map |

### 4. Agents (subagent personas)
Defined in `.opencode/agents/*.md`:

| Agent | Model | Purpose |
|---|---|---|
| `code-scanner` | LocalAI 1.5B (Tier 1) | Bulk code analysis, class scanning, read-only |
| `architect` | LocalAI 8B → Modal (Tier 2→3) | System design, architecture, refactoring plans |
| `trainer` | LocalAI 8B (Tier 2) | Fine-tuning orchestration |

### 5. Persistent Memory
Stored in `.opencode/memory/`:

| File | Purpose |
|---|---|
| `identity.json` | Your identity, capabilities, model tiers |
| `long-term.json` | Persistent facts about project, user, infrastructure |
| `sessions/` | Per-session summaries |
| `index.json` | Embedding cache for semantic search |

### 6. Infrastructure (Docker)
| Container | Port | Service |
|---|---|---|
| `pc-resource-localai` | 8080 | LocalAI (LLM + embeddings) |
| `qdrant-codebase` | 6333 | Vector database |
| `unity-linux-llm-auth-db` | 5433 | PostgreSQL (auth) |
| `npc-dedicated-server` | 11474 | Unity dedicated server |

### 7. Model Routing (3 Tiers)
| Tier | Models | When |
|---|---|---|
| 1 | `qwen2.5-1.5b`, `llama-3.2-3b` | Bulk, cheap, fast |
| 2 | `llama-3.1-8b`, `gemma-4` | Quality, balanced |
| 3 | `modal-vllm-qwen` (H100), `qwen2.5:32b` (A100) | Heavy, complex, cloud GPU |

### 8. Modal Integration
- Modal workspace: `andretwl`
- vLLM (Qwen3-8B on H100): proxied via LocalAI as `modal-vllm-qwen`
- Ollama (qwen2.5:32b on A100): direct at `andretwl--example-ollama-llama-serve.modal.run/v1`
- Examples repo: `/mnt/data/Projects_SSD/modal-examples/`
