# Cognee Architecture for Unity NPC LLM

## Overview

Cognee serves as the **memory and retrieval layer** for both the Hermes agent and the Unity NPC dialogue system. It is a separate service (port 8000) with an all-Postgres backend (relational + pgvector + graph), running on the same machine as LocalAI (port 8080).

## Relationship to Qdrant

| Layer | Qdrant (6333) | Cognee (8000) |
|-------|---------------|---------------|
| Content | Codebase index (enriched chunks) | Agent memories + NPC dialogue history |
| Schema | Versioned collections with structured payloads | Datasets with pgvector + optional graph |
| Source | CodebaseEmbedder pipeline | Runtime text (agent tools, NPC dialogue) |
| Update cadence | Manual re-index | Continuous (agent writes + NPC conversation) |
| Query pattern | C#/Python SDK for codebase RAG | HTTP API + Hermes tools for memory recall |

**Cognee has no access to Qdrant.** They are completely independent systems sharing nothing but the same VM.

## Pipeline Flow

```
                        ┌──────────────────────────┐
                        │    CodebaseEmbedder       │
                        │  chunks.jsonl (2,815 rec) │
                        └──────────┬───────────────┘
                                   │
                                   │ cognee_bridge.py
                                   │ (91 files → 329 sub-docs)
                                   ▼
┌──────────────────────────────────────────────────────────┐
│              Cognee API (localhost:8000)                   │
│                                                           │
│  /api/v1/remember  ──►  classify_documents                │
│                           │                               │
│                           ▼                               │
│                       chunking (1200 tokens, 200 overlap) │
│                           │                               │
│                           ▼                               │
│  embed with nomic-embed-text-v1.5 (768d, 2048 ctx)       │
│  via LocalAI (localhost:8080)                             │
│                           │                               │
│                           ▼                               │
│  COGNEE_SKIP_SUMMARIZATION=true ──► NO GRAPH              │
│  (skips entity/graph extraction entirely)                 │
│                           │                               │
│                           ▼                               │
│                    store in pgvector                      │
└──────────────────────────────────────────────────────────┘
```

## Current State (as of 2026-07-01)

### Running
- Cognee API v1.2.2-local on port 8000 (gunicorn + uvicorn)
- All-Postgres backend (relational DB, pgvector, graph DB)
- COGNEE_SKIP_SUMMARIZATION=true (no entity/graph extraction)
- nomic-embed-text-v1.5 via LocalAI for embeddings
- Hermes plugin at ~/.hermes/cognee.json connects to :8000

### Datasets
- `unity_npc_llm_codebase` — codebase content (being populated now)
- Various `hermes_project_*` datasets — agent conversation memories

### NOT running
- No graph/KG extraction (intentionally disabled)
- No summarization (intentionally disabled)
- CogneeMemoryService in Unity scene is inactive
- Cognee MCP server is not configured

## KG / Graph Decision

**Recommendation: skip the graph entirely.** Here's why:

1. The LLM-based entity extraction (Qwen2.5-1.5B) hallucinates badly on code content — we saw "albert einstein", "acme", and "acted_in" edges from real code documents.

2. The graph adds latency and complexity without meaningful retrieval value — the enriched runtime_summary records already capture structural relationships in explicit text (e.g., "Assembly NPCSystem.Runtime — OWNS this runtime file", "Declared namespace: NPCSystem — declares types: NPCDialogueManager").

3. With COGNEE_SKIP_SUMMARIZATION=true, the pipeline is much faster and only stores vector embeddings, which is all you need for semantic search.

4. If you need structured relationships in the future, the CodebaseEmbedder Qdrant collections already have them (relations.jsonl, 7,730 relations). Don't re-derive from Cognee's LLM extraction.

## Hermes Agent Integration

### Current State
Hermes uses Cognee via two paths:
1. **Cognee tools** (`cognee_search`, `cognee_conclude`, `cognee_profile`) — direct HTTP calls to :8000
2. **Cognee plugin** (`~/.hermes/cognee.json`) — configures the plugin that drives these tools

### Configuration
```json
{
  "dataset_prefix": "hermes_memory",
  "project_datasets": {
    "unity_npc_llm": "hermes_project_unity_npc_llm",
    ...
  },
  "project_ontologies": {
    "unity_npc_llm": ["unity_npc_ontology"]
  },
  "skip_summarization": true,
  "top_k": 5
}
```

### Ontology Files
- `/mnt/data/Projects_SSD/cognee/ontologies/hermes_brain.ttl` — Hermes agent memory ontology (Agent, Task, Memory, Project classes)
- `/mnt/data/Projects_SSD/cognee/ontologies/unity_npc.ttl` — Unity NPC ontology (NPC, Dialogue, Quest, Scene classes)
- `/mnt/data/Projects_SSD/cognee/ontologies/localai.ttl` — LocalAI/model ontology
- `/mnt/data/Projects_SSD/cognee/ontologies/cognee.ttl` — Cognee system ontology

These ontologies are loaded via `ONTOLOGY_FILE_PATH` env var but are only used by the graph pipeline, which is disabled. They're currently decoration.

### MCP vs Direct Endpoints
**Recommendation: stick with direct endpoints.** The Cognee MCP server adds another service to manage and the Hermes Cognee tools already wrap the API cleanly. The `cognee_search`/`cognee_conclude`/`cognee_profile` tools are well-integrated with the agent loop. Only add MCP if agents outside the Hermes ecosystem need Cognee access.

## Bridge Script

Location: `Tools/CodebaseEmbedder/scripts/cognee_bridge.py`

Usage:
```bash
cd Tools/CodebaseEmbedder
uv run python3 scripts/cognee_bridge.py \
  --root ../.. \
  --dataset unity_npc_llm_codebase \
  --batch-size 5 \
  --max-chars 2500
```

The bridge reads `.codebase-index/chunks.jsonl` (output of `codebase-embedder index`), groups chunks by source file, splits into sub-documents that fit within the embedding model's context window, and sends them to Cognee via `/api/v1/remember`.

## Future Work

### Short-term
- [ ] Activate CogneeMemoryService in the Unity scene and set useCogneeMemory=true in NPCDialogueManager
- [ ] Route NPC dialogue history to Cognee for persistent long-term memory
- [ ] Remove the hardcoded 2-second timeout in CogneeMemoryService.cs
- [ ] Make dataset name configurable per NPC profile

### Medium-term
- [ ] Decide if ontology files should be evolved for non-graph use (e.g., metadata tagging)
- [ ] Evaluate if the graph pipeline with a better LLM (e.g., llama-3.1-8b) would be useful for NPC dialogue memory
- [ ] Consider adding a `cognee_memories` collection name convention that mirrors the Qdrant `_codebase_v1` naming

### Not recommended
- [ ] Using the graph/KG pipeline for code content (hallucination risk, no benefit over existing text + Qdrant)
- [ ] Running multiple concurrent pipeline runs on the same dataset (Cognee has pipeline-run-ID collision bugs)
