# Unity Codebase Embedder

Offline-first developer tooling for indexing this Unity project into Qdrant.

## Commands

```bash
cd Tools/CodebaseEmbedder
uv run --extra test pytest -q
uv run codebase-embedder status --root ../..
uv run codebase-embedder scan --root ../..
uv run codebase-embedder index --root ../.. --no-qdrant
uv run codebase-embedder query --root ../.. --local "where is qdrant rag search implemented"
uv run codebase-embedder audit --root ../.. --script Assets/Scripts/Runtime/NPCDialogue/QdrantRAGService.cs --scenario localai-llmunity
uv run codebase-embedder audit --root ../.. --scene Assets/Scenes/NPCDialoguePrototype.unity --scenario localai-llmunity --local
uv run codebase-embedder unity-validate --root ../..
```

The scanner writes artifacts under `.codebase-index/` at the Unity project root:

- `manifest.json`
- `asmdefs.json`
- `symbols.jsonl`
- `relations.jsonl`
- `chunks.jsonl`
- `index-report.md`

## Design

The v1 indexer scans filesystem assets, asmdef files, package metadata, selected docs, and C# syntax. It does not require Unity to be open. GladeKit MCP is optional for validation/enrichment.

The `audit` command is the best way to evaluate retrieval quality for holistic Unity decisions. It combines:

- script-level structural audit (namespaces, asmdef ownership, symbol counts, relation counts)
- smoke-query retrieval checks
- scenario presets such as `localai-llmunity` to inspect how well runtime/backend scripts outrank editor/docs for LocalAI + LLMUnity tasks
- optional scene-aware overlay via `--scene Assets/...unity` that parses the Unity scene YAML and reports LocalAI transport wiring, dedicated-agent splits, and Qdrant/Cognee/FunctionCalling hotspots in one report

Qdrant collection: `unity_linux_llm_codebase_v1`.
