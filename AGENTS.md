# Unity_Linux_LLM Agent Reference

This file is the project-wide source of truth for agents. Keep it short, stable, and operational. Deep details live in the linked docs.

## Quick links

| Topic | Read first |
|---|---|
| Architecture / live services | `Documentation/2_Architecture/Backend_Services_Topology.md` |
| Code rules | `#1-code-conventions` |
| Codebase scanning/rules | `#5-codebase-embedder` |
| Scene wiring | `#3-scene-wiring-current` |
| Tests | `#7-testing` |
| Networking / auth | `#8-networking--auth` |
| Editor tooling | `#9-editor-tooling` |
| Safety gates | `#10-safety-gates` |
| Addressables / build level | `#13-addressables--compatibility-level` |

---

## 1. Code conventions

Dual source of truth (do not duplicate rule text here):

| Concern | Owner |
|---|---|
| Formatting + naming (IDE) | `.editorconfig` |
| Unity-specific + architectural rules (CI) | `.codebaserules.yaml` |

- Namespaces for project code: domain-specific (`NPCSystem.Auth`, `NPCSystem.Dialogue.Core`, etc.) — see §2 for full map.
- All domain namespaces are under `NPCSystem.*` root. Files in `Monitoring/Core/` use `NPCSystem.Monitoring` (separate asmdef).
- Editor code: `NPCSystem.Editor`. Tests: `NPCSystem.Tests`.
- If conventions change: update `.editorconfig` first, then `.codebaserules.yaml` if needed.
- After touching rules: `uv run --directory Tools/CodebaseEmbedder codebase-embedder check --root ..`.
- Agents: retrieve policy via `codebase-embedder query` (`project_rule` records) or read the two files above.

## 2. Project map

- Active scene: `Assets/Scenes/NPCDialoguePrototype1.unity`.
- **Domain structure** (all under `Assets/Scripts/Runtime/`):

| Domain | Path | Namespace |
|---|---|---|
| Auth | `Auth/` | `NPCSystem.Auth` |
| Character (Animation) | `Character/Animation/` | `NPCSystem.Character.Animation` |
| Character (NPC) | `Character/NPC/` | `NPCSystem.Character.NPC` |
| Character (Player) | `Character/Player/` | `NPCSystem.Character.Player` |
| Dialogue (Core) | `Dialogue/Core/` | `NPCSystem.Dialogue.Core` |
| Dialogue (Persistence) | `Dialogue/Persistence/` | `NPCSystem.Dialogue.Persistence` |
| Dialogue (RAG) | `Dialogue/RAG/` | `NPCSystem.Dialogue.RAG` |
| Dialogue (Session) | `Dialogue/Session/` | `NPCSystem.Dialogue.Session` |
| Dialogue (UI) | `Dialogue/UI/` | `NPCSystem.Dialogue.UI` |
| Initialization | `Initialization/` | `NPCSystem.Initialization` |
| Items | `Items/` | `NPCSystem.Items` |
| LocalAI | `LocalAI/` | `NPCSystem.LocalAI` |
| Monitoring (Core) | `Monitoring/Core/` | `NPCSystem.Monitoring` |
| Monitoring (Datadog) | `Monitoring/Datadog/` | `NPCSystem.Monitoring.Datadog` |
| Network (Bridges) | `Network/Bridges/` | `NPCSystem.Network.Bridges` |
| Network (Core) | `Network/Core/` | `NPCSystem.Network.Core` |

- Key asmdefs: `NPCSystem.Runtime` (root), `NPCSystem.Monitoring` (Monitoring/Core/).
- Dedicated server output: `Builds/Server/`; Docker lives in `docker/`.

## 3. Scene wiring (current)

- `NPCDialogueSystem` (root, 10 missing scripts — old refs, harmless):
  - `Core` — `NPCDialogueManager`, `NPCDialogueSmokeValidator`
  - `Backend` — `NPCBackendReadinessService`
  - `Services` — `NPCDialogueHistoryService`, `NPCDialogueRetrievalService`, `NPCDialogueSessionService`, `PlayerDialogueContextService`
  - `Network` — `NetworkObject`, `NPCDialogueNetworkBridge`, `ItemTradeService`
- `NPCNetworkSystem` — `NPCNetworkBootstrap`
- `Canvas` — `NPCDialogueUIController` (references Core→DialogueManager, Network→NetworkBridge, PlayerInput, AiText, StopButton)
- `NPCSceneInitialization` — `NPCSceneInitializationController` (references FlowLogger, NetworkBootstrap, DialogueManager, BackendReadiness, NetworkBridge, SmokeValidator)
- `LLM` and `LLMAgent` exist but are disabled; `NPCDialogueManager` currently uses HTTP to LocalAI **via port 8080** (direct, no proxy — see `NPCLocalAIConfig.LocalAIDirectPort`).
- `LLMRAG` is the active local embedding model; `CogneeMemoryService` exists but is disabled in the scene.
- `Assets/Scenes/NPCDialoguePrototype1.unity` is the only authoritative scene file. Do not edit `.unity` files by hand.

## 4. Backend services

- **OpenAPI / "Super Swagger" Specification**: Located locally at `Backend/openapi.yaml`. Defines and documents all endpoints, request payloads, response structures, query parameters, schemas, and metrics for **Supabase Auth (`8091`)**, **Supabase REST (`8092`)**, **Qdrant Vector DB (`6333`)**, and **LocalAI / Proxy (`8080/8090`)**. Agents can read this file directly to understand integration schemas.
- LocalAI (upstream): **port 8080** — all gameplay dialogue, embeddings, and health checks go directly to this port. Static config in `NPCLocalAIConfig.LocalAIDirectPort`.
- localai-proxy (observability): **port 8090** — provides token/latency tracking for CLI tools, codebase-watchdog, and ad-hoc debugging. NOT in the gameplay path. Static config in `NPCLocalAIConfig.LocalAIProxyPort`.
- Supabase: Gotrue `:8091`, PostgREST `:8092`.
- Qdrant: client `/mnt/data/Projects_SSD/qdrant-client/`, storage `/mnt/data/Projects_SSD/qdrant_storage/`, port `6333`.
- Cognee: `http://localhost:8000/api/v1`, Postgres `127.0.0.1:5432`, disabled in the active scene.
- WebGL host rewriting uses `NPCNetworkUtils.IsLocalHost()`; do not reintroduce raw `"localhost"` checks.

## 5. Codebase embedder (unified toolchain)

- Tool root: `Tools/CodebaseEmbedder/`.
- **Roslyn parser** (`Tools/CodebaseEmbedder/roslyn_parser/`): C# .NET 10 console app using Microsoft.CodeAnalysis.CSharp 4.9.2.
  Extracts **10 symbol types** (file_overview, namespace, type, member, field, serialized_field, constructor, property, event, using_directive) and **5 relation kinds** (inherits, implements, calls, namespace-contains-type, type-contains-member).
  Includes cross-file symbol resolution: `calls` relation targets are linked to their declaration stable keys.

### Commands

| Command | What it does |
|---|---|
| `scan` | Discover files, run Roslyn parser, write `.codebase-index/` artifacts |
| `index` | Embed artifacts via LocalAI and upsert to Qdrant |
| `query` | Semantic or lexical search against Qdrant or local artifacts |
| `check` | Config-driven rule checking against `.codebaserules.yaml` |
| `audit` | Scene-aware retrieval audit |
| `status` | File counts + Qdrant health |

### Common usage

```
uv run codebase-embedder status --root ../..
uv run codebase-embedder scan --root ../..
uv run codebase-embedder index --root ../..   # embeds + upserts to Qdrant
uv run codebase-embedder index --root ../.. --clear   # delete all points first, then re-index
uv run codebase-embedder query --root ../.. --local "<concept>"
uv run codebase-embedder check --root ../..   # runs all 21 rules from .codebaserules.yaml
uv run codebase-embedder check --root ../.. --target Assets/Scripts/Runtime --output check-report.md
uv run codebase-embedder audit --root ../.. --scene Assets/Scenes/NPCDialoguePrototype1.unity --scenario localai-llmunity --local
uv run --extra test pytest -q
```

- Use `UV_CACHE_DIR=/tmp/uv-cache` and `UV_TOOL_DIR=/tmp/uv-tools` if the default uv cache is read-only.
- Current defaults: profile `runtime`, collection `unity_linux_llm_codebase_v2`. Live collections: `npc_knowledge` and `unity_linux_llm_codebase_v2`.

### Rule checking (.codebaserules.yaml)

- Rules defined in `.codebaserules.yaml` at project root — **10 rules** covering Unity-specific serialization (SER01-SER02), anti-patterns (BPR01, TODO01, CMT01, NET01, AWS01), and special cases (NAM07, VAR01, SND01, DOC01). Formatting and naming conventions are enforced by `.editorconfig` (not duplicated here).
- Three check types: `text` (raw substring), `regex` (per-line match), `roslyn` (structural — requires full `scan`).
- Rule reference: `AGENTS.md §1.1` (naming via `.editorconfig`), `§1.4` (formatting via `.editorconfig`), `§1.6` (anti-patterns via `.codebaserules.yaml`).

### Artifact structure (.codebase-index/)

| File | Contents |
|---|---|
| `manifest.json` | Collection metadata, counts, timestamp |
| `symbols.jsonl` | All extracted symbol records (types, members, fields, etc.) |
| `relations.jsonl` | Full relation graph (calls, inherits, implements, containment) |
| `chunks.jsonl` | Embedding-ready text chunks for Qdrant |
| `asmdefs.json` | Assembly definition metadata |

## 6. Datadog monitoring

- Single host-level agent: `Backend/datadog-host/` (`dd-agent`), no sidecars.
- Ports: DogStatsD `8125/udp`, APM `8126/tcp`.
- Unity emits custom metrics from `Assets/Scripts/Runtime/Monitoring/DatadogMetricsService.cs` and spans from `DatadogTraceService.cs`.
- Dashboard JSON: `Backend/datadog-host/dashboard.json`.
- Key metric families: `llm.request.*`, `dialogue.*`, `qdrant.search.*`, `auth.login.*`, `network.*`.
- SAST: `code-security.datadog.yaml` uses rulesets `csharp-best-practices`, `csharp-code-style`, `csharp-security`, `csharp-inclusive`.
- SAST operates on a different layer from `.editorconfig` (formatting) and `.codebaserules.yaml` (arch rules). Naming rules in `csharp-code-style` use MS conventions (`s_` for static fields) — our project uses `_camelCase` uniformly. If SAST reports false positives on naming, tune via `.datadog/static-analysis.yml` or update `code-security.datadog.yaml` per-rule overrides.

## 7. Testing

- All tests live under `Assets/Scripts/Tests/Editor/` and use `[NUnit.Framework]`.
- Standard pattern: instantiate with `new GameObject() + AddComponent<T>()`, then `Object.DestroyImmediate()` in `finally`.
- Keep test names explicit: `Subject_Scenario_ExpectedBehavior()`.
- No magic strings unless documented.
- After touching NPCDialogue scripts or rule docs, run `uv run --directory Tools/CodebaseEmbedder codebase-embedder check --root ..`.

## 8. Networking & auth

- `NPCNetworkBootstrap`: `Awake()` applies transport config; `Start()` auto-starts server mode when `-npc-server` is present.
- CLI args: `-npc-server`, `-npc-websockets`, `-port`, `-address`, `-npc-client`, `-npc-host`.
- `NPCTransportConfig` defaults: connect `127.0.0.1`, listen `0.0.0.0`, port `11474`, WebSockets off unless WebGL.
- WebGL forces WebSockets in `ApplyTransportConfiguration()`.
- Auth flow: `PlayerAuthService.InitializeAsync()` → `AuthUIController` → `AuthNetworkBridge.HandleAuthSuccess()` → host/client selection.
- WebGL URL rewrite: if the runtime host is local, replace `RemoteHost` with `Application.absoluteURL` host via `NPCNetworkUtils.IsLocalHost()`.

## 9. Editor tooling

- Use GladeKit MCP for scene hierarchy, component inspection, Play Mode state, object creation, and script creation/editing.
- `compile_scripts` must be idle with `hasErrors=false` before `add_component`.
- Use `create_primitive` for visible geometry; `create_game_object` creates empty objects.
- Never edit `.unity` scene files directly.
- Editor scripts live in `Assets/Scripts/Editor/` and `Assets/Scripts/Editor/Tools/`.

---

## 10. Safety gates

- Explicit approval required for: mutating Unity scene files, changing runtime architecture, deleting/moving/archiving files, adding or removing Ollama, running LoRA training, committing, or pushing.
- Docs-only changes are always allowed.

## 11. Scene audit checklist (mandatory before declaring "done")

Before any scene-related task is marked complete, ALL of these must pass. Do NOT skip a step because the task seems small.

### Step 1 — Full Hierarchy Scan
Get the complete scene hierarchy with `get_scene_hierarchy(includeInactive=true)`. Note every root GameObject. Do not assume you know what's in the scene.

### Step 2 — Component Audit on Every Root GO
For each root GO, call `get_gameobject_components()`. Check:
- `missingCount` — if > 0, those must be cleaned
- Every component name looks correct for that object
- No unexpected duplicates (e.g., two `NPCFlowLogger` GOs)

### Step 3 — Read Source Scripts for Every Component
For every MonoBehaviour on the GO, read the source script and identify ALL `[SerializeField]` fields + their types. This is the only way to know what fields *should* exist. Pay attention to:
- `ObjectReference` fields that need other GOs/components
- Arrays/lists (`_profiles`, `_definitions`, catalogs) that need items
- Config fields (host, port, model, timeout) that need correct defaults

### Step 4 — Trace Every Missing Script to Root Cause
When `missingCount > 0`:
1. Read the scene `.unity` file around the GameObject's YAML block
2. Extract the stale GUID from each `MissingScript` component
3. Search `.cs.meta` files for that GUID
4. If found → it's a legitimate component, identify it
5. If NOT found → determine why (deleted? renamed? moved? new assembly?)
6. Clean with `GameObjectUtility.RemoveMonoBehavioursWithMissingScript`
7. **CRITICAL: After cleaning, re-add the correct component types. Unity does NOT restore serialized fields — every field will be empty/default.** You must manually re-wire all references.

### Step 5 — Verify Every Serialized Field (after re-add)
For every custom MonoBehaviour, call `get_component_inspector_properties(onlyTopLevel=false)` — **do NOT use `onlyReferences=true` alone**, that misses empty arrays, config values, and non-reference fields. Check:
- Every ObjectReference is assigned and points to the correct target
- Every array/list has the expected number of items (profiles, catalogs, items)
- Every config value matches the expected default (host, port, model, timeout)
- No field that should be assigned is left at `null` or `[]`

### Step 6 — Verify Compilation
Call `compile_scripts()`. Wait until `isCompiling=false` and `hasErrors=false`.

### Step 7 — Categorical Wiring Validation
Check every category below. For each MonoBehaviour, verify ALL serialized fields are assigned and point to the correct object/asset. Read the scene YAML if `get_component_inspector_properties` shows unexpected nulls.

**Category A: Network Infrastructure**
| GameObject | Components | Verify |
|---|---|---|
| `Network_Manager` | NetworkManager, UnityTransport | TransportConfig port=11474, address config |
| `NPCNetworkSystem` | NPCNetworkBootstrap | Refs: NetworkManager, UnityTransport, PlayerPrefab, ServerNpcPrefab; TransportConfig: connectAddress, port, useWebSockets |

**Category B: Auth System (AuthUI)**
| Component | Refs to Verify |
|---|---|
| `AuthUIController` | authPanel, authTitle, usernameInput, passwordInput, confirmPasswordGroup, submitButton, switchModeButton, errorText, authService (→PlayerAuthService on same GO) |
| `PlayerAuthService` | supabaseUrl (localhost:8091), restApiUrl (localhost:8092), timeout, stored session config |
| `AuthNetworkBridge` | _authController (→AuthUIController), _networkBootstrap (→NPCNetworkSystem), _gameplayLoadController (→WebGLGameplayLoadController) |

**Category C: Dialogue System (NPCDialogueSystem)**
| GO | Components | Serialized Refs |
|---|---|---|
| `Core` | NPCDialogueManager, NPCDialogueSmokeValidator, NPCLocalAIClient | Manager: _chatClient, _qdrantRag, _supabaseRepo, _profiles (≥1 NPCProfile asset). LocalAIClient: _host=127.0.0.1, _port=8080, _model |
| `Backend` | NPCBackendReadinessService | Singleton, no duplicates |
| `Services` | NPCDialogueHistoryService, NPCDialogueRetrievalService, NPCDialogueSessionService, PlayerDialogueContextService, QdrantRAGService, SupabaseDialogueRepository, NPCLocalAIEmbedder | All 7 present and unique |
| `Network` | NetworkObject, NPCDialogueNetworkBridge, ItemTradeService, NPCNetworkSessionManager | NetworkBridge: _dialogueManager→Core, _sessionManager→Network (NPCNetworkSessionManager). ItemTradeService: _catalog→ItemCatalog asset |

**Category D: Initialization**
| GO | Components | Refs |
|---|---|---|
| `NPCSceneInitialization` | NPCSceneInitializationController | **6 refs**: _flowLogger, _networkBootstrap, _dialogueManager, _backendReadiness, _networkBridge, _smokeValidator — ALL must be non-null |
| `WebGLGameplayLoadController` | WebGLGameplayLoadController | Self-contained |

**Category E: UI System (Canvas)**
| GO | Components | Refs |
|---|---|---|
| `Canvas` | Canvas, CanvasScaler, GraphicRaycaster, NPCDialogueUIController | UIController: DialogueManager, NetworkBridge, PlayerInput, AiText, StopButton. Unassigned refs (e.g. LegacyKnowledgeBaseController) are OK if unused. |
| `StopButton` | Image, Button | Button wired to UIController.StopPressed? |
| `PlayerInput` | TMP_InputField | Input field wired |
| `AIImage` | Image | Visual container |
| `AIImage/AIText` | TextMeshProUGUI | AI response text |

**Category F: Engine/Environment**
| GO | Components | Notes |
|---|---|---|
| `Main Camera` | Camera, AudioListener, URP data | Default config OK |
| `Directional Light` | Light, URP data | Default config OK |
| `EventSystem` | EventSystem, InputSystemUIInputModule | Required for UI input |
| `Ground` | MeshFilter, MeshRenderer, MeshCollider, NavMeshSurface | Mesh assigned? Material assigned? |

**Category G: Flow/Logging**
| GO | Components | Notes |
|---|---|---|
| `NPCFlowLogger` | NPCFlowLogger | Singleton — exactly 1 instance |

**Category H: Duplicate Prevention**
Check every name at each hierarchy level. `get_scene_hierarchy` list must have unique entries for each root GO and each child under a parent. Duplicate names mean stale/multiple copies.

### Step 7 — Save Scene
`save_scene()` — always.

### Root cause of "clean but not ready"
The Phase 7 domain restructure regenerated `.meta` file GUIDs. The scene held stale GUIDs for:
- 10 components on NPCDialogueSystem (old manager, services, repos)
- 1 component on NPCSceneInitialization (old NPCNetworkBootstrap)
- 3 components on AuthUI (old AuthUIController, PlayerAuthService, AuthNetworkBridge)

After cleaning MissingScripts, **re-add any valid components** that were lost and re-wire their references. Also check `_profiles` arrays on managers (ScriptableObject asset refs don't auto-restore). Major missed items during first "clean" pass:
- NPCSceneInitializationController._backendReadiness was null (fixed)
- NPCDialogueManager._qdrantRag and _supabaseRepo were null (fixed)
- NPCDialogueManager._profiles was empty (fixed)
- QdrantRAGService, SupabaseDialogueRepository, NPCLocalAIEmbedder missing from Services (fixed)
- ItemTradeService._catalog (ItemCatalog asset) was null (fixed — created empty ItemCatalog.asset)
- NPCDialogueNetworkBridge._sessionManager was null (fixed — added NPCNetworkSessionManager to Network)
- NPCDialogueSmokeValidator._logger was null (fixed — wired to NPCFlowLogger)

## 12. Known compile warnings

- `CS0618`: `FindFirstObjectByType` / `FindObjectOfType` usage in several scripts.
- `CS0108`: `NPCDialogueManager.SendMessage(string)` hides `Component.SendMessage(string)`; avoid the inherited `Component.SendMessage()`.

## 13. Dedicated server

- Build output: `Builds/Server/`.
- Runtime flags: `-batchmode -npc-server -port 11474 -address 0.0.0.0 -npc-websockets`.
- Docker: `docker/Dockerfile`, `docker-compose.yml`, `docker/entrypoint.sh`.
- `NPCNetworkBootstrap` starts in `Start()` so `NetworkManager.Awake()` runs first.

## 14. Addressables / compatibility

- All build profiles must use `apiCompatibilityLevel: 2` (.NET Standard 2.1). Level 6 breaks Addressables, Unity Transport, Serialization, and RP Core packages.
- After changing `apiCompatibilityLevel`, delete `Library/` and rebuild.
- `m_BuildAddressablesWithPlayerBuild` should be `0` for iteration builds. Rebuild Addressables manually when needed.
- If SBP errors appear, clear `Library/com.unity.addressables/`, `Temp/com.unity.addressables/`, and stale `addressables_content_state.bin` files, then rebuild.

## 15. Holistic Unity Session Workflow (MANDATORY)

Before ANY Unity analysis or editing session, follow this complete pipeline. Every step must be ticked before declaring work complete or "ready."

### Phase A — Source-of-Truth Ingestion (start every session)
1. **Load latest Project Auditor report** — read `Project_Auditor/*.projectauditor` (latest dated file). Extract ALL issues with severity Error / Critical / Major. These are the ground truth for the current state of the project.
2. **Check last build logs** — read `Logs/Editor.log` tail (grep for "Build completed", "error CS", "Failed"). Identify any build failures and their root causes.
3. **Check apiCompatibilityLevel** — confirm `ProjectSettings/ProjectSettings.asset` has `apiCompatibilityLevel: 2` (.NET Standard 2.1). Level 6 (.NET 8) WILL break Unity Transport, Serialization, and RP Core packages, causing build failures. If wrong, fix before any other work.

### Phase B — Automated Settings Verification (SettingsGuard)
4. **Run SettingsGuard** — in Unity Editor: `Tools > SettingsGuard > Verify`. Or from CLI:
   ```
   Unity -batchmode -quit -projectPath . -logFile Logs/settings-guard.log \
       -executeMethod NPCSystem.Editor.Tools.SettingsGuard.Verify
   ```
   SettingsGuard checks: apiCompatibilityLevel (default + per-platform), editor assemblies level, scripting defines, build profile scenes, and more. Reports errors and warnings with exit code 0/1.
5. **Fix and re-verify** if errors found — `Tools > SettingsGuard > Fix and Verify` or:
   ```
   Unity -batchmode -quit -projectPath . -logFile Logs/settings-guard.log \
       -executeMethod NPCSystem.Editor.Tools.SettingsGuard.FixAndVerify
   ```
6. **Create default config** if first run — `Tools > SettingsGuard > Create Default Config`. Creates `Assets/Settings/SettingsGuardConfig.asset` with expected values for .NET Standard 2.1 across all platforms.

### Phase C — Pre-Audit (before touching anything)
7. **Load current AGENTS.md scene audit checklist** (§11) and categorical wiring table.
8. **Remind** of known failure patterns: after cleaning MissingScripts, EVERY serialized field resets to default. Always re-verify.

### Phase D — Scene Audit Execution (use GladeKit MCP)
Run AGENTS.md §11 Steps 1-8 (full hierarchy → component audit → read scripts → trace missing → verify ALL fields → compile → categorical wiring → save).

### Phase E — Post-Audit Verification
9. **Compile** — 0 errors mandatory.
10. **Run codebase-embedder check** — `uv run codebase-embedder check --root ../..`
11. **Re-index if artifacts changed** — `uv run codebase-embedder index --root ../.. --clear`
12. **Save everything** — scene, scripts, prefabs.

### Known failure patterns (from Project Auditor + past mistakes)

| # | Pattern | How to catch | Severity |
|---|---|---|---|
| 1 | `apiCompatibilityLevel` set to 6 instead of 2 | Run `SettingsGuard.Verify` or check `ProjectSettings.asset` lines 867, 950 | **Build failure** |
| 2 | MissingScript on re-added component hides null serialized fields | Use `get_component_inspector_properties(onlyTopLevel=false)` on EVERY re-added component | **Runtime silent failure** |
| 3 | `_profiles` arrays empty on managers | Read source script to know what arrays exist, then verify contents not just existence | **NPC won't talk** |
| 4 | ScriptableObject assets (`ItemCatalog`, `NPCProfile`) missing | `check_asset_exists()` then `set_script_component_property()` | **NullRef at runtime** |
| 5 | Service components missing from child GOs | Read source script of parent Manager to see what it expects via GetComponentInChildren | **Discoverability broken** |

## 16. Editor Console Pro Integration

Console Pro is installed at `Assets/ConsolePro/` with version-specific DLLs for Unity 6000.5+.

### Features integrated into the NPC telemetry pipeline

| Feature | File | What it does |
|---|---|---|
| `ConsoleProTelemetrySink` | `Monitoring/Core/ConsoleProTelemetrySink.cs` | Routes ALL telemetry events through Console Pro filters by category (dialog, rag, auth, network, items). Uses `LogToFilter()` and `LogAsType()` for structured, filterable logs. |
| `ConsoleProWatcher` | `Monitoring/Core/ConsoleProWatcher.cs` | Real-time watch panel tracking LLM duration, Qdrant latency, FPS, memory, active sessions, messages sent. |
| `ConsoleProBehaviour` | `Monitoring/Core/ConsoleProBehaviour.cs` | MonoBehaviour that drives the Watcher each frame. Attach to a persistent GO (e.g. NPCFlowLogger). |
| `ConsoleProDebug.Watch` | Real-time Watch panel counters | `FPS`, `Memory`, `LLM Duration` |

### Features (all active)

| Layer | Mechanism | Effect |
|---|---|---|
| **Temp Filters** | `#NPC# #category#` tags in every log | Console Pro auto-creates colored filter buttons for `dialog`, `llm`, `rag`, `auth`, `network`, `items`, `system` — zero setup required |
| **Log Interceptor** | `Application.logMessageReceivedThreaded` hook | Catches ALL stray `Debug.Log` from `[NPC...]`, `[Supabase...]`, `[Qdrant...]` prefixed calls and routes through Console Pro API |
| **Watch Panel** | `ConsoleProDebug.Watch()` push every frame | Live counters: FPS, frame time, memory, GC alloc, LLM duration/tokens, RAG latency, active sessions, auth logins, network ping, items traded |
| **Severity Mapping** | `LogAsType()` | Errors → red highlight, Warnings → yellow, Info → filtered by category |
| **Shared Settings** | `.cep` file via Preferences > Shared Settings | Commit custom filters with colors/icons to version control for team consistency |
| **Remote Server** | `ConsoleProRemoteServer` component | Receives logs from WebGL/mobile builds over LAN during development |

### Setup

1. Open Console Pro: **Window > Console Pro 3** (or _Cmd+\\_)
2. Run **Tools > Console Pro > Apply Project Setup** — applies define, verifies DLLs
3. Attach `ConsoleProBehaviour` to NPCFlowLogger for live Watch panel
4. **[Optional]** Right-click Console Pro toolbar > **Preferences > Custom Filters** — add permanent filters matching the `#category#` tags
5. **[Optional]** Preferences > **Shared Settings** — save to `Assets/settings.cep` for team sharing
6. **[WebGL]** **Tools > Console Pro > Add Remote Server to Scene** — build with Development Build to receive remote logs

### How it works

- `TelemetryRouter.Point("req-1", "QdrantRAGService", "rag", "success", "Query OK")` → Console Pro shows it under filter `#NPC# #rag#`
- `NPCFlowLogger.Log(...)` → `WriteUnityConsole()` uses Console Pro API with stage-derived category
- `ConsoleProBehaviour` on NPCFlowLogger pushes live counters to Watch panel every 3 frames
- `ConsoleProLogInterceptor` catches every un-routed `Debug.Log` from NPC system scripts

### Structured logging conventions

All telemetry should use the TelemetryRouter API, NOT raw `Debug.Log`:
- ✅ `TelemetryRouter.Point(id, source, "dialogue", "success", message)`
- ❌ `Debug.Log("Dialogue completed successfully")`

Exception: one-shot initialization messages (Bootstrapper, startup config) and log-system failure fallbacks may still use `Debug.Log` — the interceptor catches them.

### Quick reference (tags available as filters)

| Tag | Category | Color (auto) |
|---|---|---|
| `#dialog#` | Dialogue system | Green |
| `#llm#` | LocalAI LLM requests | Blue |
| `#rag#` | Qdrant vector search | Purple |
| `#auth#` | Supabase auth | Orange |
| `#network#` | Netcode/transport | Cyan |
| `#items#` | Trade/inventory | Yellow |
| `#system#` | Bootstrap/setup | Gray |

### Console Pro docs
`Assets/ConsolePro/Editor Console Pro Documentation.pdf`

## 17. Build & Deploy checklist (before any build attempt)

1. **Run SettingsGuard** — `Tools > SettingsGuard > Fix and Verify` (or CLI equivalent)
2. **Verify `apiCompatibilityLevel: 2`** in `ProjectSettings.asset` — both the default AND the per-platform override
3. **Scene audit** (§11) — 0 missing scripts, 0 unassigned critical refs, 0 compile errors
4. **If build fails with package-level CS errors** (CS0246, CS0103 on packages) — **always apiCompatibilityLevel first**, not package reinstall
5. **Clear stale artifacts** if changing compatibility level: delete `Library/`, `Temp/com.unity.addressables/`, `Library/com.unity.addressables/`, `addressables_content_state.bin`

## 18. Recurring code quality workflow

Repeatable audit → fix → verify cycle using existing tools:

1. **Scan** — `uv run codebase-embedder scan --root ../..` (refreshes `.codebase-index/` artifacts)
2. **Check** — `uv run codebase-embedder check --root ../.. --target <path> --output report.md` (runs `.codebaserules.yaml` rules)
3. **Fix** — address violations in reported files (SER01 → private fields, NET01 → remove hard-coded localhost, BPR01 → split bool params, CMT01 → delete commented code)
4. **Re-check** — run `check` again, confirm 0 violations
5. **Scene audit** (when Unity Editor is open) — run the full §15 holistic workflow
6. **Re-index** — `uv run codebase-embedder index --root ../.. --clear` for a clean re-index when artifacts change significantly

Key flags:
- `--target <path>` — scope `check` to a subdirectory
- `--output <file>` — write report to file
- `--clear` — delete all Qdrant points before re-indexing
- `--no-qdrant` — skip Qdrant upsert (local artifacts only)

---

*Generated from completed code-quality-improvement phases 1-9 (2026-07-09) + Project Auditor integration (2026-07-19) + SettingsGuard + ConsolePro integration (2026-07-19).*
