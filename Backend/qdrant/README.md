# Qdrant Vector Database

Single-node Qdrant instance for the NPC system's vector search needs.

## Usage

```bash
# Start Qdrant
docker compose up -d

# Check health
curl http://localhost:6333/healthz

# List collections
curl http://localhost:6333/collections

# Stop
docker compose down
```

## Collections

| Collection | Purpose | Managed By |
|-----------|---------|------------|
| `npc_knowledge` | NPC dialogue knowledge embeddings | `QdrantRAGService` (Unity runtime) |
| `unity_linux_llm_codebase_v2` | Codebase embeddings for LLM context | `CodebaseEmbedder` (Python tool) |

## Storage

Persistent data lives at `/mnt/data/Projects_SSD/qdrant_storage/` (bind-mounted).

## Monitoring

Datadog collects Qdrant metrics via OpenMetrics from `localhost:6333/metrics`
— configured in `Backend/datadog-host/conf.d/qdrant.d/conf.yaml`.
