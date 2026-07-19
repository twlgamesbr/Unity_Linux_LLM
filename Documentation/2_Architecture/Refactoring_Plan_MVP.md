# NPC Dialogue System — Refactoring MVP Plan

## Goal

Strip the mystery-solving narrative, reduce to **one developer NPC**, make it work perfectly on **WebGL multiplayer**, add a **professional monitoring/telemetry pipeline**, and restructure into **domain-separated namespaces and folders**.

---

## Current Problems

| Problem | Detail |
|---|---|
| **Flat namespace** | Everything in `NPCSystem.*` — 35+ scripts, no domain separation |
| **Mystery narrative** | Butler/Maid/Chef profiles, "evidence-ledger" items, secret knowledge, accusations |
| **3 NPCs** | `NPCServerCharacterSpawner` spawns 3, `NPCDialogueUIController` has a dropdown to pick |
| **Client-owned animation** | `NPCCharacterAnimatorBridge` reads local motor in `Update()` — WebGL can't guarantee sync; server should own animation state |
| **No unified telemetry** | `NPCFlowLogger` + separate `DatadogMetricsService` + separate `SessionAnalyticsService` — no correlated cross-flow traces |
| **Bloated orchestrator** | `NPCDialogueManager` does startup, service wiring, bootstrapper flags, event forwarding |
| **Auth → spawn pipeline brittle** | `AuthNetworkBridge` (699 lines), `PlayerAuthService` (623 lines), `AuthUIController` (677 lines) — tangled concerns |
| **Item system wired for mystery** | `NPCTransferableItem` default ID is `"evidence-ledger"`, NPC spells like `"butler"` |
| **Partial class sprawl** | `NPCDialogueNetworkBridge` (712 lines) + `.ProfileProvider.cs` partial |

---

## New Architecture

### Namespace Layout

| Namespace | Responsibility |
|---|---|
| `NPCSystem.Dialogue.Core` | Manager, profile, prompt composer, dialogue entry models |
| `NPCSystem.Dialogue.Session` | Session service, history, retrieval, context |
| `NPCSystem.Dialogue.RAG` | Qdrant hybrid search, codebase search, embeddings |
| `NPCSystem.Dialogue.UI` | UI controller, state machine |
| `NPCSystem.Dialogue.Persistence` | Supabase CRUD, session store |
| `NPCSystem.Auth` | Supabase auth service, auth UI, auth→network bridge |
| `NPCSystem.Character.Player` | Player controller, motor, input |
| `NPCSystem.Character.NPC` | Server NPC identity, spawner, nameplate |
| `NPCSystem.Character.Animation` | Animator bridge, server-owned animator, camera |
| `NPCSystem.Network.Core` | Bootstrap, transport config, session manager, utils |
| `NPCSystem.Network.Bridges` | Dialogue network bridge, message models |
| `NPCSystem.Items` | Inventory, transferable items, trading, item definitions |
| `NPCSystem.Monitoring` | Flow logger, Datadog, telemetry pipeline |
| `NPCSystem.LocalAI` | LLM client, embedder, config |
| `NPCSystem.Initialization` | Boot pipeline, readiness, scene init |

### Folder Layout
```
Assets/Scripts/Runtime/
├── Dialogue/Core/
├── Dialogue/Session/
├── Dialogue/RAG/
├── Dialogue/UI/
├── Dialogue/Persistence/
├── Auth/
├── Character/Player/
├── Character/NPC/
├── Character/Animation/
├── Network/Core/
├── Network/Bridges/
├── Items/
├── Monitoring/Core/
├── Monitoring/Datadog/
├── Monitoring/Telemetry/
├── LocalAI/
├── Initialization/
└── NPCSystem.Runtime.asmdef
```

---

## Phase Plan

### Phase 0 — Tooling & Foundation (create structure, split asmdefs)

**Goal**: Build the folder scaffold, split the monolithic asmdef into domain assemblies, create the telemetry router.

**Changes**:

1. **Create new folder tree** under `Assets/Scripts/Runtime/` matching the namespace layout above.
2. **Split `NPCSystem.Runtime.asmdef`** into domain-specific asmdefs:
   - `NPCSystem.Dialogue.asmdef` → references `NPCSystem.LocalAI`, `NPCSystem.Monitoring`
   - `NPCSystem.Auth.asmdef` → references `NPCSystem.Network` (for bridge), Supabase SDKs
   - `NPCSystem.Character.asmdef` → references `NPCSystem.Network`, `NPCSystem.Monitoring`
   - `NPCSystem.Network.asmdef` → references Unity.Netcode, no project deps
   - `NPCSystem.Items.asmdef` → references `NPCSystem.Network`, `NPCSystem.Character`
   - `NPCSystem.Monitoring.asmdef` → minimal, standalone
   - `NPCSystem.LocalAI.asmdef` → standalone
   - `NPCSystem.Initialization.asmdef` → references everything (composition root)
3. **Create `TelemetryRouter.cs`** — singleton that routes structured events to pluggable sinks:
   ```csharp
   // NPCSystem.Monitoring.Telemetry
   public interface ITelemetrySink
   {
       void Emit(TelemetryEvent evt);
   }
   public sealed class TelemetryRouter
   {
       public static TelemetryRouter Instance { get; } = new();
       readonly List<ITelemetrySink> _sinks = new();
       public void Register(ITelemetrySink sink) => _sinks.Add(sink);
       public void Emit(TelemetryEvent evt) { foreach (var s in _sinks) s.Emit(evt); }
   }
   ```
4. **Create `DatadogTelemetrySink.cs`** — adapts existing `DatadogMetricsService` + `DatadogTraceService` as sinks.
5. **Create `FileTelemetrySink.cs`** — structured JSONL output with per-request correlation IDs.
6. **Create `TelemetryEvent.cs`** — unified event envelope with `requestId`, `source`, `durationMs`, `category`, `tags`.

**Files moved**: None yet — Phase 0 creates folders and new infrastructure. All existing files stay in place until Phases 1–7 move them.

---

### Phase 1 — Auth → Spawn Pipeline Cleanup

**Goal**: Clean, reliable Supabase login → network spawn sequence. WebGL-safe.

**Changes**:

1. **Extract `AuthService.cs`** from `PlayerAuthService` (623 lines → split into `AuthService` + `PlayerAuthService` wrapper). Pure API logic, no MonoBehaviour.
2. **Extract `AuthUIBinding.cs`** from `AuthUIController` (677 lines). UI event wiring only.
3. **Simplify `AuthNetworkBridge`** — remove test accessors, remove legacy mode flags. Single path: `AuthSuccess → StartNgo(AuthNetworkBridge.ResolvedNetworkStartupMode) → RegisterPlayerName`.
4. **Add WebGL auth flow** — ensure `UnitySessionStore` works in WebGL (IndexedDB via PlayerPrefs fallback), or skip session persistence for WebGL (re-login each session).

**Verification**: One WebGL build test: user sees login UI → enters credentials → auth success → client connects → avatar spawned.

---

### Phase 2 — Character Controller & Server-Owned Animation

**Goal**: Smooth player control on WebGL with server-authoritative animation. Resolve the "animator must be server owned" concern.

**Current state**: Two player controllers exist:
- `NPCNetworkPlayerController` (465 lines, monolithic, reads InputSystem directly)
- `NPCPlayerCharacterController` (315 lines, modular — uses `NPCMultiplayerInputActions` + `NPCCharacterMotor` + `NPCCharacterAnimatorBridge`)

**Changes**:

1. **Deprecate `NPCNetworkPlayerController`** — remove from scene, set `NPCPlayerCharacterController` as the single player prefab controller.
2. **Make animation server-authoritative**:
   - `NPCCharacterAnimatorBridge` becomes a **client-side reader only** — reads from `NetworkVariable<AnimatorSnapshot>` instead of local motor state.
   - Create `NPCNetworkAnimatorState.cs` — `NetworkBehaviour` with `NetworkVariable<FixedString128Bytes>` containing serialized animator parameters (MoveX, MoveY, Speed, Grounded, Sprinting, Jump).
   - Server updates animator state in `FixedUpdate()` after motor simulation.
   - Client applies via `OnValueChanged` → `animator.SetFloat(...)`.
   - **Why**: WebGL client doesn't run physics simulation — server must push animation state. The client's `NPCCharacterAnimatorBridge` reads the network variable and applies to local animator.
3. **Ensure `NPCThirdPersonCameraController` works in WebGL** — camera stays client-side (local transform, no network).

**Network architecture for animation**:
```
Server FixedUpdate:
  NPCCharacterMotor.Simulate() → move character
  NPCNetworkAnimatorState.SnapshotState(motor) → update NetworkVariable

Client OnValueChanged:
  NPCCharacterAnimatorBridge.Update() → apply animator.SetFloat/SetBool/SetTrigger
```

---

### Phase 3 — Single Developer NPC

**Goal**: Replace 3 NPCs (Butler/Maid/Chef) with 1 "Game Developer" NPC. Remove all mystery narrative.

**Changes**:

1. **Create single `DeveloperNPC` profile** (`Developers/Resources/NPCProfiles/DevNPC.asset`):
   - Slug: `"game-developer"`
   - Display Name: `"Dev"` or `"Code Sage"`
   - System Prompt: *"You are a senior game developer trapped inside the game's codebase. You help the player understand the game's architecture, fix bugs, optimize systems, and suggest features. You have access to the entire Qdrant-indexed codebase and can search for relevant code snippets. You can trade code items, documentation, and tools with the player."*
   - No personality brief, no speaking style about mystery/hints/accusations
   - `UseQdrantRag = true`, `ragResults = 5`, collection = `unity_linux_llm_codebase_v2`
   - Action functions: `give_item`, `explain_code`, `suggest_fix`
2. **Delete old profiles** — Butler, Maid, Chef `.asset` files (after confirming no scene references).
3. **Update `NPCServerCharacterSpawner`** — spawn exactly 1 NPC instead of grid layout from `_profiles.Length`. Remove `_spawnSpacing`, `_maxColumns`, `_spawnOrigin` grid logic. Simple single-spawn.
4. **Update `NPCDialogueUIController`** — remove `CharacterSelect` dropdown (no longer needed). Remove Butler/Maid/Chef portrait references (`ButlerImage`, `MaidImage`, `ChefImage`). Single NPC = always active.
5. **Remove `NPCDialogueSmokeValidator`** — integrate its validation into `GameInitializationPipeline` (Phase 7) instead of a separate Start() validator.

**Codebase search flow** (hybrid Qdrant search):
```
Player: "How does the character controller work?"
  → NPCDialogueSessionService.SendDialogueMessage()
    → NPCDialogueRetrievalService.SearchAsync(profile, message)
      → QdrantRAGService.SearchMemoryAsync(query, topK, reqId, npcSlug)
        → NPCLocalAIEmbedder.EncodeAsync(query)  // dense vector
        → HTTP POST to Qdrant /collections/unity_linux_llm_codebase_v2/points/search
          (hybrid: dense + sparse)
        → return top-K chunks
    → NPCProfilePromptComposer injects chunks into system prompt
  → NPCLocalAIClient.ChatAsync(messages) → LocalAI
  → Response displayed in UI
```

---

### Phase 4 — Dialogue System Cleanup & Single-NPC Flow

**Goal**: Remove mystery narrative, bootstrapper flags, and dead code. Streamline to 1-NPC flow.

**Changes**:

1. **Clean `NPCDialogueManager`**:
   - Remove `_autoSelectDefaultNPC`, `_defaultNpcSlug` (always auto-select the single developer NPC)
   - Remove `RemoteEmbeddingHost`/`RemoteEmbeddingPort` (use `NPCLocalAIConfig` exclusively)
   - Remove `_cachedModelNames` dropdown
   - Remove `OnNpcChanged` event (single NPC, never changes)
   - Remove `LegacyKnowledgeBaseController` from UI controller references
   - Remove `persistHistory` toggle (always persist to Supabase)
   - Remove `initializeOnStart` toggle (always init on start)
   - Collapse folded groups for simplicity
2. **Clean `NPCProfile`**:
   - Remove `secret_knowledge` export field
   - Remove `can_give_puzzle_hints`, `can_accuse_suspects`, `can_reveal_secrets` (mystery-only)
   - Remove `rag_category` (single collection)
   - Remove `preferredActionFunctions`/`forbiddenActionFunctions` (too granular; replace with simple action allowlist)
   - Add `InventoryItems` list for items the NPC can trade
3. **Clean `NPCProfilePromptComposer`**:
   - Remove `BuildActionPolicyText()` (mystery-specific)
   - Remove `BuildKnowledgeRouteText()` (simplified)
   - Add code-search context injection section
4. **Remove `NPCProfileDatasetExporter`** editor tool or gut it for new schema.
5. **Remove `NPCDialogueSmokeValidator.cs`** — its validation moves to `GameInitializationPipeline`.

---

### Phase 5 — Item Trading System

**Goal**: NPC can give/trade code-related items to the player. Replace "evidence-ledger" with game-dev items.

**Changes**:

1. **Create `ItemDefinition` ScriptableObject**:
   ```csharp
   // NPCSystem.Items
   [CreateAssetMenu]
   public class ItemDefinition : ScriptableObject
   {
       public string itemId;        // "code-snippet-optimization"
       public string displayName;   // "Optimization Pattern: Object Pooling"
       public string description;   // "A code snippet showing how to implement object pooling..."
       public ItemRarity rarity;
       public Sprite icon;
       public string[] tags;        // "pattern", "performance", "csharp"
   }
   ```
2. **Create `ItemCatalog`** — singleton Registry (ScriptableObject or static) mapping `itemId → ItemDefinition`.
3. **Create `ItemTradeService`** — network behaviour:
   - `ServerTryGiveItem(ulong playerClientId, string itemId)` — adds to `NPCPlayerInventory`
   - `ServerTryRemoveItem(ulong playerClientId, string itemId)` — removes from inventory
   - Triggered by NPC dialogue action parsing (NPC says "I'll give you the optimization pattern" → `ItemTradeService.GiveItem`)
4. **Rename `NPCTransferableItem._itemId`** default from `"evidence-ledger"` to `""` (empty = no default).
5. **Wire dialogue action → item trade** — in `NPCDialogueSessionService`, after LLM response, parse for `[give_item:id=code-snippet-optimization]` pattern → call `ItemTradeService.GiveItem` via server RPC.

---

### Phase 6 — Advanced Monitoring & Telemetry Pipeline

**Goal**: Full dataflow tracing with correlation IDs. Every operation timed and tagged.

**Changes**:

1. **Unify all logging under `TelemetryRouter`**:
   - `NPCFlowLogger.Log()` → internally calls `TelemetryRouter.Instance.Emit()`
   - All log calls keep same API but now route through telemetry
   - `NPCFlowScope.Success/Error/Fallback` → emit scope completion as telemetry event with duration
2. **Add correlation ID flow**:
   - Each player dialogue request gets a `requestId` (GUID)
   - `requestId` propagates through: UI → SessionService → RetrievalService → Qdrant → LocalAI → Response
   - Telemetry events carry `requestId` for stitching
3. **Create `PerformanceTracker.cs`** — lightweight `using` scope that emits start/end events:
   ```csharp
   using var perf = PerformanceTracker.Start("dialogue.full_turn", requestId);
   // ... all sub-operations ...
   perf.Tag("rag_ms", ragStopwatch.ElapsedMilliseconds);
   perf.Tag("llm_ms", llmStopwatch.ElapsedMilliseconds);
   // On dispose: emits full TelemetryEvent with all tags
   ```
4. **Add Datadog APM traces** for key flows:
   - `auth.login` — time from form submit to avatar spawn
   - `dialogue.full_turn` — player message → NPC response rendered
   - `rag.search` — Qdrant search time
   - `llm.inference` — LocalAI chat completion time
   - `item.trade` — item transfer time
   - `network.spawn` — player/NPC spawn time
5. **Add WebGL-specific metrics**:
   - WebSocket connection time
   - Input latency (client → server → client round-trip for animation state)
   - Dialogue response render time (waiting for LLM through WebSocket)
6. **Create structured log file per session** with schema version — `NPCDialogue/Logs/{sessionId}_flow.jsonl`.

---

### Phase 7 — Move Files to New Structure

**Goal**: Physically move all scripts to new folder hierarchy. Update namespaces.

This is **last** so nothing breaks mid-phase. All previous phases work on the flat structure, then Phase 7 atomically moves everything with a cleanup pass.

**Move map** (not exhaustive — ~35 files):

| Current Path | New Path | New Namespace |
|---|---|---|
| `Runtime/NPCDialogue/NPCDialogueManager.cs` | `Runtime/Dialogue/Core/` | `NPCSystem.Dialogue.Core` |
| `Runtime/NPCDialogue/NPCProfile.cs` | `Runtime/Dialogue/Core/` | `NPCSystem.Dialogue.Core` |
| `Runtime/NPCDialogue/NPCProfilePromptComposer.cs` | `Runtime/Dialogue/Core/` | `NPCSystem.Dialogue.Core` |
| `Runtime/NPCDialogue/DialogueEntry.cs` | `Runtime/Dialogue/Core/` | `NPCSystem.Dialogue.Core` |
| `Runtime/NPCDialogue/NPCDialogueSessionService.cs` | `Runtime/Dialogue/Session/` | `NPCSystem.Dialogue.Session` |
| `Runtime/NPCDialogue/NPCDialogueHistoryService.cs` | `Runtime/Dialogue/Session/` | `NPCSystem.Dialogue.Session` |
| `Runtime/NPCDialogue/NPCDialogueRetrievalService.cs` | `Runtime/Dialogue/Session/` | `NPCSystem.Dialogue.Session` |
| `Runtime/NPCDialogue/PlayerDialogueContextService.cs` | `Runtime/Dialogue/Session/` | `NPCSystem.Dialogue.Session` |
| `Runtime/NPCDialogue/PlayerDialogueContext.cs` | `Runtime/Dialogue/Session/` | `NPCSystem.Dialogue.Session` |
| `Runtime/NPCDialogue/QdrantRAGService.cs` | `Runtime/Dialogue/RAG/` | `NPCSystem.Dialogue.RAG` |
| `Runtime/LocalAI/NPCLocalAIEmbedder.cs` | `Runtime/Dialogue/RAG/` | `NPCSystem.Dialogue.RAG` |
| `Runtime/NPCDialogue/NPCDialogueUIController.cs` | `Runtime/Dialogue/UI/` | `NPCSystem.Dialogue.UI` |
| `Runtime/NPCDialogue/SupabaseDialogueRepository.cs` | `Runtime/Dialogue/Persistence/` | `NPCSystem.Dialogue.Persistence` |
| `Runtime/NPCDialogue/SupabaseDialogueModels.cs` | `Runtime/Dialogue/Persistence/` | `NPCSystem.Dialogue.Persistence` |
| `Runtime/NPCDialogue/UnitySessionStore.cs` | `Runtime/Auth/` | `NPCSystem.Auth` |
| `Runtime/NPCDialogue/PlayerAuthService.cs` → split | `Runtime/Auth/` | `NPCSystem.Auth` |
| `Runtime/NPCDialogue/SupabaseAuthClient.cs` | `Runtime/Auth/` | `NPCSystem.Auth` |
| `Runtime/NPCDialogue/AuthUIController.cs` → split | `Runtime/Auth/` | `NPCSystem.Auth` |
| `Runtime/Networking/AuthNetworkBridge.cs` | `Runtime/Auth/` | `NPCSystem.Auth` |
| `Runtime/Networking/NPCPlayerCharacterController.cs` | `Runtime/Character/Player/` | `NPCSystem.Character.Player` |
| `Runtime/Networking/NPCCharacterMotor.cs` | `Runtime/Character/Player/` | `NPCSystem.Character.Player` |
| `Runtime/Networking/NPCMultiplayerInputActions.cs` | `Runtime/Character/Player/` | `NPCSystem.Character.Player` |
| `Runtime/Networking/NPCCharacterAnimatorBridge.cs` | `Runtime/Character/Animation/` | `NPCSystem.Character.Animation` |
| `Runtime/Networking/NPCThirdPersonCameraController.cs` | `Runtime/Character/Animation/` | `NPCSystem.Character.Animation` |
| `Runtime/Networking/NPCServerCharacter.cs` | `Runtime/Character/NPC/` | `NPCSystem.Character.NPC` |
| `Runtime/Networking/NPCServerCharacterSpawner.cs` | `Runtime/Character/NPC/` | `NPCSystem.Character.NPC` |
| `Runtime/Networking/NPCNameplate.cs` | `Runtime/Character/NPC/` | `NPCSystem.Character.NPC` |
| `Runtime/Networking/NPCPlayerNetworkAvatar.cs` | `Runtime/Character/Player/` | `NPCSystem.Character.Player` |
| `Runtime/Networking/NPCNetworkBootstrap.cs` + partials | `Runtime/Network/Core/` | `NPCSystem.Network.Core` |
| `Runtime/Networking/NPCTransportConfig.cs` | `Runtime/Network/Core/` | `NPCSystem.Network.Core` |
| `Runtime/Networking/NPCNetworkSessionManager.cs` | `Runtime/Network/Core/` | `NPCSystem.Network.Core` |
| `Runtime/Utilities/NPCNetworkUtils.cs` | `Runtime/Network/Core/` | `NPCSystem.Network.Core` |
| `Runtime/Networking/NPCDialogueNetworkBridge.cs` + partial | `Runtime/Network/Bridges/` | `NPCSystem.Network.Bridges` |
| `Runtime/Networking/NPCDialogueMessageModels.cs` | `Runtime/Network/Bridges/` | `NPCSystem.Network.Bridges` |
| `Runtime/Networking/NPCPlayerInventory.cs` | `Runtime/Items/` | `NPCSystem.Items` |
| `Runtime/Networking/NPCTransferableItem.cs` | `Runtime/Items/` | `NPCSystem.Items` |
| `Runtime/Networking/NPCNetworkItemInteractor.cs` | `Runtime/Items/` | `NPCSystem.Items` |
| `Runtime/NPCDialogue/Logging/` (5 files) | `Runtime/Monitoring/Core/` | `NPCSystem.Monitoring` |
| `Runtime/Monitoring/DatadogMetricsService.cs` | `Runtime/Monitoring/Datadog/` | `NPCSystem.Monitoring.Datadog` |
| `Runtime/Monitoring/DatadogTraceService.cs` | `Runtime/Monitoring/Datadog/` | `NPCSystem.Monitoring.Datadog` |
| `Runtime/LocalAI/NPCLocalAIClient.cs` | `Runtime/LocalAI/` | `NPCSystem.LocalAI` |
| `Runtime/LocalAI/NPCLocalAIConfig.cs` | `Runtime/LocalAI/` | `NPCSystem.LocalAI` |
| `Runtime/Initialization/NPCBackendReadinessService.cs` | `Runtime/Initialization/` | `NPCSystem.Initialization` |
| `Runtime/Initialization/NPCSceneInitializationController.cs` | `Runtime/Initialization/` | `NPCSystem.Initialization` |

**After move**: Update all `using NPCSystem;` to domain-specific namespaces. Update asmdef references. Fix scene component references (Unity will show missing scripts — re-assign from new paths).

---

### Phase 8 — Scene & Prefab Rebuild

**Goal**: Update the active scene (`NPCDialoguePrototype1.unity`) to reflect new architecture.

**Changes** (via GladeKit, never by hand):

1. Strip old NPC profile assets from scene references
2. Assign single Developer NPC profile
3. Remove character select dropdown from UI
4. Remove Butler/Maid/Chef portrait RawImages
5. Re-wire `NPCPlayerCharacterController` as player prefab (not `NPCNetworkPlayerController`)
6. Add `NPCNetworkAnimatorState` to player prefab
7. Add `ItemTradeService` to NPC system GameObject
8. Add `ItemCatalog` reference to trade service
9. Wire `TelemetryRouter` initialization in boot sequence
10. Save scene → verify no missing script references

---

## Key Design Decisions

| Decision | Rationale |
|---|---|
| **Single NPC** | User explicitly said "player must start a dialogue with just one NPC, not 3" |
| **Server-owned animator** | WebGL clients don't run physics; server must own movement + animation state and replicate via NetworkVariable |
| **Qdrant codebase collection** | Already exists (`unity_linux_llm_codebase_v2`), NPC "as a developer of the game" searches it |
| **Hybrid Qdrant search** | Dense (semantic) + sparse (keyword) = best results for code retrieval |
| **Domain asmdefs** | Enforces compile-time dependency boundaries, parallel compilation, cleaner API surfaces |
| **TelemetryRouter with sinks** | Pluggable: Datadog in production, file JSONL for local debugging, future: Grafana/OTel |
| **ItemDefinition ScriptableObject** | Data-driven item catalog, can be extended without code changes |
| **Phase 7 (move) is last** | All logic changes happen in-place first, then files move atomically — avoids broken references mid-project |

---

## Risk Areas

| Risk | Mitigation |
|---|---|
| **WebGL + Netcode doesn't support `NetworkTransform` client-authoritative** | Already using `NPCOwnerNetworkTransform` — verify it works with WebSockets transport. Server-authoritative animation bypasses this entirely. |
| **Supabase CORS blocks WebGL** | Confirm `supabaseStackRest` has CORS headers for the WebGL origin. May need nginx proxy. |
| **Qdrant HTTP from WebGL** | WebGL can't do raw TCP. Qdrant REST API on `:6333` must be accessible from browser. May need nginx proxy to forward `/qdrant/*` → `localhost:6333`. |
| **LocalAI HTTP from WebGL** | Same issue — WebGL can't reach `localhost:8080`. The `ResolveWebGlHost()` logic in `NPCDialogueManager` rewrites host to `Application.absoluteURL` host, but the backend must serve on that origin. Requires reverse proxy (nginx) mapping `/localai/*` → `localhost:8080`. |

---

## Success Criteria

1. [ ] WebGL build loads and shows login UI
2. [ ] Supabase login succeeds (or register)
3. [ ] Client connects to host via WebSockets
4. [ ] Player avatar spawns with server-authoritative animation
5. [ ] Single Developer NPC visible in scene
6. [ ] Player can walk up to NPC and press interact
7. [ ] Dialogue shows in UI, sent to LocalAI via HTTP proxy
8. [ ] NPC performs hybrid Qdrant search on codebase collection
9. [ ] NPC returns code-relevant response (not mystery narrative)
10. [ ] NPC can trade items based on dialogue content
11. [ ] Telemetry events flow with correlation IDs, visible in JSONL file
12. [ ] All files moved to domain folders with correct namespaces
13. [ ] Codebase embedder `check` passes with 0 violations
