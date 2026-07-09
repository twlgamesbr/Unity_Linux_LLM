# Unity_Linux_LLM — Agent Guidelines

This repository is a Unity 6 NPC dialogue prototype that uses direct LocalAI HTTP calls as its primary dialogue backend, with local `.rag` retrieval via LLMUnity, optional Qdrant/Cognee integrations, and a small set of editor automation tools. These notes describe the project as it is currently wired in the repo and scene.

---

## 1. Current project state

- Unity version: `6000.5.1f1`
- Main scene: `Assets/Scenes/NPCDialoguePrototype1.unity`
- Secondary scene present: `Assets/Scenes/SampleScene.unity`
- Root runtime assembly: `Assets/Scripts/Runtime/NPCSystem.Runtime.asmdef`
- Editor assembly: `Assets/Scripts/Editor/NPCSystem.Editor.asmdef`
- Test assembly: `Assets/Scripts/Tests/Editor/NPCSystem.Tests.asmdef`
- Third-party/runtime dependencies used directly by runtime code include:
  - `undream.llmunity.Runtime`
  - `GladeAgenticAI.Core`
  - `Unity.InputSystem`
  - `Unity.Nuget.Newtonsoft-Json`
  - `Cloud.Unum.USearch`
- GladeKit bridge is referenced only from the Editor assembly.

---

## 2. Actual scene wiring

The authoritative scene is `Assets/Scenes/NPCDialoguePrototype1.unity`.

### `NPCDialogueSystem` GameObject

Attached components in the current scene:

- `NPCDialogueManager`
- `NPCDialogueBootstrapper`
- `NPCDialogueSmokeValidator`
- `QdrantRAGService`
- `NPCDialogueActionPlanner`

Current `NPCDialogueManager` scene values:

- `llm` -> shared `LLM`
- `llmAgent` -> shared `LLMAgent`
- `rag` -> `RAG`
- `ragEmbeddingPath` -> `RAG/NPCDialogues-minilm-chunked.rag`
- `qdrantRag` -> attached `QdrantRAGService`
- `useQdrantRag` -> `false`
- `actionPlanner` -> attached `NPCDialogueActionPlanner`
- `useRemoteServer` -> `true`
- `remoteHost` -> `localhost`
- `remotePort` -> `8080`
- `remoteModel` -> `llama-3.1-8b-q4-k-m`
- `useCogneeMemory` -> `false`
- `forceRemoteEmbedder` -> `false`
- `profiles` -> Butler, Maid, Chef

Important implication:

- The codebase supports Qdrant and Cognee, but the shipped scene currently has both optional memory layers disabled in `NPCDialogueManager`.

### `LLM` GameObject

Current scene state:

- Component: `LLMUnity.LLM`
- `enabled = false`
- `_remote = 0`
- `_port = 13333`
- `_model = llama-3.1-8b-q4_k_m.gguf`
- `_embeddingsOnly = 0`

This is the shared chat-model reference used by `NPCDialogueManager` and the shared `LLMAgent`, but the manager currently talks to LocalAI directly through HTTP when `useRemoteServer` is enabled.

### `LLMAgent` GameObject

Current scene state:

- Component: `LLMUnity.LLMAgent`
- `enabled = false`
- `_remote = 0`
- `_llm` -> shared `LLM`
- `_host = localhost`
- `_port = 13333`

### `LLMRAG` GameObject

Current scene state:

- Component: `LLMUnity.LLM`
- `_remote = 0`
- `_model = all-MiniLM-L12-v2.Q4_K_M.gguf`
- `_embeddingsOnly = 1`
- `_embeddingLength = 384`

This is the local embedding model used for `.rag` generation/search.

### `RAG` GameObject

- Present and assigned to `NPCDialogueManager.rag`
- Uses the embedding-side LLM stack for local retrieval

### `CogneeMemoryService` GameObject

Current scene state:

- Separate GameObject, not attached to `NPCDialogueSystem`
- Endpoint: `http://localhost:8000/api/v1`
- Available for integration, but not currently enabled by `NPCDialogueManager`

### `FunctionCalling` surfaces

Current scene state:

- `FunctionCalling` GameObject is inactive
- `FunctionCalling.llmAgent` points to `FunctionCallingAgent`
- `FunctionCallingAgent` has `_remote = 1`, `_host = localhost`, `_port = 8080`
- `FunctionCallingAgent._llm` currently points to the shared `LLM`
- `FunctionCallingLLM` exists separately with `_remote = 1` and LocalAI model path configured

Important implication:

- The scene does contain a dedicated function-calling agent, but it is not fully isolated from the shared `LLM` the way older docs implied.
- Do not document `FunctionCallingLLM` as the active `FunctionCallingAgent.llm` unless you first confirm and change the scene wiring.

---

## 3. Runtime architecture that matters

### `NPCDialogueManager`

Source: `Assets/Scripts/Runtime/NPCDialogue/NPCDialogueManager.cs`

- Initializes references, profiles, history, and local `.rag` state
- Persists per-NPC conversation history via `NPCHistoryStore`
- Builds prompts from `NPCProfile` data
- Uses `NPCDialogueActionPlanner` to attach gameplay-oriented guidance
- Can query:
  - local `.rag`
  - optional Qdrant
  - optional Cognee
- Sends direct HTTP requests to LocalAI when `useRemoteServer = true`

The active scene currently uses the direct LocalAI HTTP path.

### `QdrantRAGService`

Source: `Assets/Scripts/Runtime/NPCDialogue/QdrantRAGService.cs`

- Uses `rag.search.llmEmbedder` to create embeddings
- POSTs to Qdrant `/collections/<collection>/points/search`
- Current scene collection name is `unity_linux_llm_codebase_v1`

Important implication:

- The Qdrant service exists, but the current scene collection name is a codebase-index collection name, not an NPC-specific collection name.
- Treat that as current state, not as validated production intent.

### `NPCDialogueActionPlanner`

Source: `Assets/Scripts/Runtime/NPCDialogue/NPCDialogueActionPlanner.cs`

- Keyword-routes player input into light action intents such as:
  - `PuzzleHint`
  - `ShowNotes`
  - `ShowMap`
  - `ShowSolve`
  - `ShowHelp`
  - `PressSuspect`
  - `RecallEvidence`

This planner is part of the active scene and should be included in any architecture summary.

### `NPCHistoryStore`

Source: `Assets/Scripts/Runtime/NPCDialogue/NPCHistoryStore.cs`

- Stores JSON history under `Application.persistentDataPath`
- Normalizes saved history into strict alternating `user` / `assistant` turns
- Repairs malformed saved histories by dropping invalid entries

### `NPCFlowLogger`

Source: `Assets/Scripts/Runtime/NPCDialogue/Logging/NPCFlowLogger.cs`

- Scene logger is present in `NPCDialoguePrototype`
- Writes JSONL logs by default
- Default log directory is `Application.persistentDataPath/NPCDialogue/Logs`
- Subscribes to Cognee diagnostics when available

This logging layer is a real runtime subsystem, not just debug noise. Do not omit it from current-state docs.

---

## 4. Data and content layout

### NPC profiles

Current assets:

- `Assets/Data/NPCProfiles/Butler.asset`
- `Assets/Data/NPCProfiles/Maid.asset`
- `Assets/Data/NPCProfiles/Chef.asset`

Each profile currently carries:

- identity and portrait
- system prompt and behavior tuning
- action permissions
- sampling settings
- knowledge source path
- LoRA adapter path
- history save path

### StreamingAssets knowledge

Current knowledge files:

- `Assets/StreamingAssets/NPCs/butler/knowledge.md`
- `Assets/StreamingAssets/NPCs/maid/knowledge.md`
- `Assets/StreamingAssets/NPCs/chef/knowledge.md`

Current folder also contains `profile.json` files per NPC.

### Local RAG assets

- Active scene path: `Assets/StreamingAssets/RAG/NPCDialogues-minilm-chunked.rag`
- Folder documentation exists at `Assets/StreamingAssets/RAG/README.md`

---

## 5. Editor tooling that actually exists

Editor scripts under `Assets/Scripts/Editor/` are not hypothetical. Current tooling includes:

- `NPCFactoryWindow.cs`
- `MysterySceneTemplateGenerator.cs`
- `HeadlessPlayMode.cs`
- `TestQdrantRAG.cs`
- `Tools/CreateNPCTool.cs`

Important current behavior:

- `NPCFactoryWindow` uses an in-editor `LLM`/`LLMAgent` workflow to generate prompts and knowledge, test chat, and optionally ingest to Cognee.
- `CreateNPCTool` auto-registers with Glade Agentic AI tooling and creates profile assets plus StreamingAssets knowledge files.

Do not describe this repo as runtime-only. It has a meaningful editor-automation layer.

---

## 6. CodebaseEmbedder workflow

The repo includes `Tools/CodebaseEmbedder/`.

Current retrieval state:

- Stable live collection: `unity_linux_llm_codebase_v1`
- Experimental structural collection: `unity_linux_llm_codebase_structural_v1`
- Experimental hierarchy collection: `unity_linux_llm_codebase_hierarchy_v1`
- Current smoke-query result: the structural experiment ties the stable collection on the default matrix, and the hierarchy experiment improves some namespace-summary prompts but still loses overall on the default matrix
- Query ranking now boosts structural records like `namespace`, `file_overview`, `assembly`, `relation`, and `using_directive`
- The hierarchy profile adds `namespace_summary` records for namespace-level aggregation
- A dedicated optimizer skill exists at `.agents/skills/unity-codebase-rag-optimizer/`
- Comparison reports are written under `.codebase-index/collection-comparison-*.md`
- Indexed with named vectors (`dense` 768d Cosine + `code_keywords` sparse) for hybrid search
- Sparse vectors use deterministic SHA-256 token hashing into 2M-dim space with TF normalization
- Qdrant collection: HNSW (ef_construct=200), scalar int8 quantization, indexing_threshold=10000
- Watchdog service available as `docker_codebase_watchdog/` for persistent file monitoring

Useful commands:

```bash
cd Tools/CodebaseEmbedder
env UV_CACHE_DIR=/tmp/uv-cache UV_TOOL_DIR=/tmp/uv-tools uv run codebase-embedder status --root ../..
env UV_CACHE_DIR=/tmp/uv-cache UV_TOOL_DIR=/tmp/uv-tools uv run codebase-embedder query --root ../.. --local "<concept>"
env UV_CACHE_DIR=/tmp/uv-cache UV_TOOL_DIR=/tmp/uv-tools uv run codebase-embedder audit --root ../.. --scene Assets/Scenes/NPCDialoguePrototype1.unity --scenario localai-llmunity --local
# Persistent watchdog (Docker):
docker compose -f docker_codebase_watchdog/docker-compose.yml up -d
```

Use `/tmp` cache/tool dirs if the default `uv` cache location is read-only in the current session.

---

## 7. Testing surfaces

### Runtime/editor tests present

- `Assets/Scripts/Tests/Editor/NPCFlowLoggerTests.cs`

Current coverage in repo is centered on:

- flow-event serialization
- text sanitization
- JSONL logging
- scope/request-id behavior

### CodebaseEmbedder tests

```bash
cd Tools/CodebaseEmbedder
env UV_CACHE_DIR=/tmp/uv-cache UV_TOOL_DIR=/tmp/uv-tools uv run --extra test pytest -q
```

---

## 8. Documentation rules for future edits

- Prefer documenting what is actually wired in `Assets/Scenes/NPCDialoguePrototype1.unity`, not the intended architecture from older notes.
- Distinguish clearly between:
  - present in code
  - present in scene
  - enabled in current runtime config
- Do not claim Cognee or Qdrant are active unless `NPCDialogueManager` has them enabled in scene or code has been changed accordingly.
- Do not claim `FunctionCallingAgent` is isolated onto `FunctionCallingLLM` without checking the actual scene reference.
- Keep Runtime -> Editor assembly boundaries clean.
- When creating new runtime code, prefer extending `Assets/Scripts/Runtime/` over other locations.
