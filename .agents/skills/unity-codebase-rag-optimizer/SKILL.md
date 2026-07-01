---
name: unity-codebase-rag-optimizer
description: Use when improving Unity codebase retrieval quality, Qdrant collection design, CodebaseEmbedder parsing, structural code queries, namespace/reference answers, symbol hierarchy, or collection experiments for this project.
---

# Unity Codebase RAG Optimizer

Use this skill when the task is about improving how the Unity project is parsed, indexed, ranked, queried, or consumed through Qdrant-backed code retrieval.

## Goals

- Make answers useful for real Unity coding decisions, not generic semantic summaries.
- Prefer structural truth for questions about namespaces, references, asmdefs, symbols, scene wiring, and runtime ownership.
- Treat collection design as an experiment surface: compare, promote, and keep a stable default.

## Default Workflow

1. Classify the query failure before editing anything.
   Semantic failure:
   The right file/class exists in the collection but ranking is weak.
   Structural failure:
   The question requires namespaces, using directives, symbol ownership, asmdef edges, or cross-file relations.
   Consumer failure:
   The Unity-side retrieval prompt/filtering is too generic or too narrow.
   Index failure:
   The collection is stale, duplicated, or missing the needed payload fields.

2. Check current state first.
   Run:
   ```bash
   cd Tools/CodebaseEmbedder
   env UV_CACHE_DIR=/tmp/uv-cache UV_TOOL_DIR=/tmp/uv-tools uv run codebase-embedder status --root ../..
   env UV_CACHE_DIR=/tmp/uv-cache UV_TOOL_DIR=/tmp/uv-tools uv run codebase-embedder audit --root ../.. --scene Assets/Scenes/NPCDialoguePrototype.unity --scenario localai-llmunity --local
   ```
   If the active scene is different, point `--scene` at the real scene.

   For scene questions, prefer GladeKit MCP first:
   - `get_scene_hierarchy`
   - `find_game_objects`
   - `get_gameobject_components`
   - `get_component_inspector_properties`
   - `find_component_usages`
   Use scene YAML or audit artifacts only if the MCP tools are unavailable.

3. Improve the parser before over-tuning prompts.
   Prioritize these records and fields:
   - `assembly`: asmdef name, root namespace, references
   - `namespace`: declared types, path, asmdef, region
   - `using_directive`: imported namespace, declared namespaces, path
   - `file_overview`: namespaces, types, members, region, asmdef
   - `type`: namespace, type name, base types, interfaces, purpose
   - `member`: method/function signature, owning type, path, lines
   - `relation`: `namespace-contains-type`, `namespace-uses-namespace`, `inherits`, `implements`, selected high-signal call/reference edges

4. Encode hierarchy importance in ranking.
   For structural questions, prefer:
   - `namespace`
   - `file_overview`
   - `assembly`
   - `relation`
   - `type`
   - `member`
   - `using_directive`
   Runtime records should outrank Editor records unless the question explicitly targets editor tooling.

5. Separate collections by experiment strategy when needed.
   Do not overwrite the stable collection until the experiment wins.
   Suggested names:
   - `unity_linux_llm_codebase_v1`
   - `unity_linux_llm_codebase_structural_v1`
   - `unity_linux_llm_codebase_hierarchy_v1`
- `unity_linux_llm_codebase_runtime_v1`
  - `unity_linux_llm_codebase_npcdialogue_v1`
  - `unity_linux_llm_codebase_llmunity_v1`
   Read `references/collection-strategy.md` when choosing one.

6. Validate with smoke prompts, not intuition.
   Use prompts that mirror real developer asks:
   - `list namespaces used by NPCSystem`
   - `which asmdefs own npc dialogue runtime`
   - `where is LocalAI chat transport implemented`
   - `what classes handle qdrant rag`
   - `which scripts reference LLMAgent`
   Read `references/smoke-prompts.md` for the full set and scoring guidance.

7. Update the Unity-side consumer only after the collection is good enough.
   For structure-heavy questions, the Unity consumer should:
   - expand retrieval breadth
   - filter to structural record types
   - build a structural answer prompt
   - show source paths and relation context

## Long-Run Work Split

If subagents or parallel local workers are available, split long jobs by responsibility:

- `parser-worker`
  Focus on C# analyzer changes, new record types, line ownership, and relation extraction.
- `retrieval-worker`
  Focus on ranking, query classification, structural boosts, diversification, and smoke-query evaluation.
- `collection-worker`
  Focus on alternate collection names, indexing runs, payload schema validation, namespace summary records, runtime summary records, scene YAML inclusion, and promotion criteria.
- `unity-consumer-worker`
  Focus on the Unity-side retrieval filter/prompt path and scene-facing behavior.

When scene-facing behavior matters, have the worker read the live scene through GladeKit MCP before it reads repo text artifacts.

Do not let the worker that changes ranking also judge success alone. Keep evaluation at least partially independent.

## Experiment Discipline

- Keep one stable collection for normal use.
- Run risky schema/ranking changes in a separate collection first.
- Rebuild from scratch if point ID semantics changed.
- Prefer stable IDs based on `stable_key`, not content hash.
- If LocalAI is flaky, validate local artifacts first with `--local`, then run live Qdrant indexing.

## Tools

- For collection experiments, use:
  [scripts/run-collection-experiment.sh](scripts/run-collection-experiment.sh)
- For multi-collection comparison and promotion reports, use:
  [scripts/compare_collections.py](scripts/compare_collections.py)
- For collection naming and promotion rules, read:
  [references/collection-strategy.md](references/collection-strategy.md)
- For query classes and what record types they need, read:
  [references/query-taxonomy.md](references/query-taxonomy.md)
- For smoke prompts and acceptance checks, read:
  [references/smoke-prompts.md](references/smoke-prompts.md)
  The default automation matrix lives in:
  [references/experiment-matrix.json](references/experiment-matrix.json)

## Promotion Rule

Promote an experiment collection only if:

- structural questions return structural records near the top
- runtime/backend questions still hit the right runtime owners
- Unity compile/runtime behavior is unchanged unless intentionally modified
- the collection rebuild is clean and non-duplicating
