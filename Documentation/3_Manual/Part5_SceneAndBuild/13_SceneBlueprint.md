# Part V — Scene Wiring & Build

# Chapter 13: The Scene Blueprint

> **Duration:** 30-minute read
> **Audience:** Intermediate Unity developers who have read Part I–IV
> **Prerequisites:** Chapters 01–12 (Assembly Definitions, Namespaces, Execution Order, Service Architecture, Networking, Backend Services)
> **Reference Scene:** `Assets/Scenes/NPCDialoguePrototype1.unity`

---

> **🧑‍💻 Dev NPC:** "So you've read twelve chapters of theory. Now comes the part where we actually wire it up. Think of this chapter as the IKEA instructions for your NPC scene — except every Allen wrench is a `GetComponentInChildren` call, and there's always one extra screw labeled 'missing script' that you're absolutely supposed to ignore."

---

## 1. What You're Building

The scene `NPCDialoguePrototype1.unity` is the **single authoritative scene** for the entire NPC system. It's not a demo scene, it's not a test scene — it's *the* scene. Everything you've read about in Chapters 1–12 converges here.

At a high level, the scene does the following:

1. **Bootstraps** all systems in a strict phase order (logging → references → network → dialogue → backends → bridge → validation → spawning)
2. **Discovers** services through parent-child hierarchy — no `FindObjectOfType` spaghetti at runtime
3. **Renders** a dialogue UI that connects player input to the LLM pipeline
4. **Connects** to LocalAI (port 8080), Qdrant (port 6333), and Supabase (ports 8091–8092)
5. **Monitors** everything via Datadog metrics and structured logging

By the end of this chapter, you'll understand why each GameObject exists, how its components are wired, and how to rebuild the scene from scratch if it ever breaks.

---

## 2. The 14 Root Objects — What They Are and Why They Exist

Open `NPCDialoguePrototype1.unity` and look at the Hierarchy. You'll see exactly **14 root objects**. Here's every one of them:

| # | GameObject | Purpose | Key Component(s) |
|---|------------|---------|------------------|
| 1 | **Network_Manager** | Unity Netcode's singleton manager. Handles session connection, spawn management, and transport configuration. | `NetworkManager` |
| 2 | **NPCSceneInitialization** | The bootloader. Runs phase 0 of the execution pipeline (`[DefaultExecutionOrder(-2000)]`). Wired to every major system. | `NPCSceneInitializationController` |
| 3 | **Main Camera** | Standard Unity camera for the 3D viewport. Renders the scene. | `Camera`, `AudioListener` |
| 4 | **Directional Light** | Basic scene lighting. | `Light` |
| 5 | **EventSystem** | Required for Unity UI input processing (mouse clicks, keyboard, gamepad). | `EventSystem`, `StandaloneInputModule` |
| 6 | **AuthPanel** | Login/registration UI panel. Contains input fields for username, password, confirmation, remember-me toggle, error text, submit button, and mode switch. | `AuthUIContent` (custom), child `TMP_InputField`s, `Button`s, `Toggle` |
| 7 | **AuthUI** | Auth controller that manages authentication state. Bridges to Supabase GoAuth. | `AuthUIController` |
| 8 | **Canvas** | The main gameplay UI: player input field, AI response text, stop button. Bridges to dialogue manager or network bridge. | `NPCDialogueUIController`, `Canvas`, `CanvasScaler` |
| 9 | **NPCDialogueSystem** | The full dialogue subsystem. Contains Core, Backend, Services, and Network children. This is the brain. | `NPCDialogueManager`, child service components |
| 10 | **NPCFlowLogger** | Structured logging service. Writes flow events (phases, errors, warnings) to file and Datadog sinks. | `NPCFlowLogger` |
| 11 | **Ground** | A simple plane so the 3D viewport has visual context. Optional. | `MeshRenderer`, `MeshFilter` (Plane), `MeshCollider` |
| 12 | **WebGLGameplayLoadController** | Handles WebGL-specific loading state: shows a loading screen while WASM initializes, then transitions to gameplay. | `WebGLGameplayLoadController` |
| 13 | **NPCNetworkSystem** | Network transport and bootstrap. Configures WebSocket transport, registers networked prefabs, starts the server/client. | `NPCNetworkBootstrap` |

> **🧑‍💻 Dev NPC:** "Fourteen root objects. Count them. I dare you. Every time someone adds a fifteenth root object without asking, a kitten loses its `NetworkManager`. Keep it tidy."

---

### 2.1 Why Not Fewer Objects?

You might think: "Can't I just merge `NPCSceneInitialization` and `NPCNetworkSystem` into one object?"

**No.** Here's why:

- **Initialization and networking have different lifecycles.** The initialization controller boots and then is *done* — it doesn't need to exist for the rest of the session. The network bootstrap stays alive to manage connections and reconnections.
- **The `NetworkManager` must be a root object.** Unity Netcode requires `NetworkManager` to be a root GameObject. Don't move it.
- **Separation of concerns.** The dialogue system, auth, and UI each own their own lifecycle. Merging them creates a god object that's impossible to test.

---

## 3. The NPCDialogueSystem Hierarchy — A Deep Dive

The `NPCDialogueSystem` GameObject is the most complex object in the scene. It has **four child objects**, each containing specific services:

```
NPCDialogueSystem (10 missing scripts — harmless, see §5)
├── Core
│   ├── NPCDialogueManager         [NPCDialogueManager]
│   └── NPCDialogueSmokeValidator  [NPCDialogueSmokeValidator]
├── Backend
│   └── NPCBackendReadinessService [NPCBackendReadinessService]
├── Services
│   ├── NPCDialogueHistoryService     [NPCDialogueHistoryService]
│   ├── NPCDialogueRetrievalService   [NPCDialogueRetrievalService]
│   ├── NPCDialogueSessionService     [NPCDialogueSessionService]
│   └── PlayerDialogueContextService  [PlayerDialogueContextService]
└── Network
    ├── NetworkObject               [Unity.Netcode.NetworkObject]
    ├── NPCDialogueNetworkBridge    [NPCDialogueNetworkBridge]
    └── ItemTradeService            [ItemTradeService]
```

### 3.1 Core — The Central Nervous System

**`NPCDialogueManager`** (`[DefaultExecutionOrder(-1500)]`):
- Orchestrates the entire dialogue pipeline
- Resolves its child services via `GetComponentInChildren` (see §6)
- Initializes the chat client, RAG service, and session service
- Acts as the bridge between UI input and LLM output
- Exposes UnityEvents: `OnNpcChanged`, `OnResponseStart`, `OnResponseComplete`, `OnError`

**`NPCDialogueSmokeValidator`**:
- Post-initialization validation: checks that all required services are wired, endpoints respond, and the scene is configured correctly

### 3.2 Backend — Readiness Checks

**`NPCBackendReadinessService`**:
- Probes LocalAI readiness (health endpoint, model loaded)
- Probes Qdrant readiness
- Probes Supabase readiness
- Returns a readiness summary that the initialization controller uses to decide whether to proceed

### 3.3 Services — The Workhorses

| Service | Responsibility |
|---------|---------------|
| `NPCDialogueHistoryService` | Loads, caches, and persists dialogue history via Supabase `PostgREST`. Handles per-NPC history with configurable max entry count. |
| `NPCDialogueRetrievalService` | RAG engine: builds and queries the Qdrant vector index for NPC knowledge. Uses LocalAI embeddings (`minilm`). |
| `NPCDialogueSessionService` | Manages the active dialogue session: sends messages to LocalAI, streams responses, cancels in-flight requests. |
| `PlayerDialogueContextService` | Tracks per-player context (player name, client ID) for contextual dialogue. |

### 3.4 Network — Multiplayer Connectivity

**`NetworkObject`**: Required by Netcode for any GameObject that needs to exist on both client and server. Enables the bridge to send RPCs.

**`NPCDialogueNetworkBridge`**:
- The client↔server diplomat (see Chapter 8)
- Routes dialogue requests from the client to the server, which forwards them to LocalAI
- Broadcasts responses back via ClientRpc

**`ItemTradeService`**:
- Handles NPC↔player item trades (inventory items defined in NPCProfile)

---

## 4. How References Are Wired

The scene uses **three different reference resolution strategies**. Understanding which is used where will save you hours of debugging.

### 4.1 NPCSceneInitializationController — Inspector-Wired References

The `NPCSceneInitializationController` has **6 serialized reference fields** that appear in the Inspector:

| Serialized Field | Type | Found Via |
|-----------------|------|-----------|
| `_flowLogger` | `NPCFlowLogger` | `NPCFlowLogger.FindOrCreate()` (auto-initializes) |
| `_networkBootstrap` | `NPCNetworkBootstrap` | `FindAnyObjectByType<NPCNetworkBootstrap>(IncludeInactive)` |
| `_dialogueManager` | `NPCDialogueManager` | `FindAnyObjectByType<NPCDialogueManager>(IncludeInactive)` |
| `_backendReadiness` | `NPCBackendReadinessService` | `FindAnyObjectByType<NPCBackendReadinessService>(IncludeInactive)` |
| `_networkBridge` | `NPCDialogueNetworkBridge` | `FindAnyObjectByType<NPCDialogueNetworkBridge>(IncludeInactive)` |
| `_smokeValidator` | `NPCDialogueSmokeValidator` | `FindAnyObjectByType<NPCDialogueSmokeValidator>(IncludeInactive)` |

> **🧑‍💻 Dev NPC:** "Yes, that's `FindAnyObjectByType` — the successor to the deprecated `FindObjectOfType`. You'll see CS0618 warnings about it. Ignore them. The new API takes an `FindObjectsInactive` enum instead of a boolean. Progress!"

The controller's `ResolveReferences()` method (called in `Awake()`, `OnValidate()`, and `Reset()`) auto-discovers any unassigned references at edit time. This means you **don't have to drag references manually** — just having the objects in the scene is enough. But if you *do* assign them in the Inspector, those assignments take priority.

**Important:** The controller does NOT attempt to wire itself during `ResolveReferences()` if the references are already assigned. This allows you to override auto-discovery with specific selections.

### 4.2 NPCDialogueUIController — Hybrid Wiring

The `NPCDialogueUIController` on the `Canvas` object has **5 wired references**:

| Serialized Field | Type | Resolved Via |
|-----------------|------|-------------|
| `DialogueManager` | `NPCDialogueManager` | `GetComponent<>` on self, then `FindAnyObjectByType` |
| `NetworkBridge` | `NPCDialogueNetworkBridge` | `GetComponent<>` on self, then `FindAnyObjectByType` |
| `LegacyKnowledgeBaseController` | `Behaviour` | `FindObjectsByType<Behaviour>` with type name match |
| `PlayerInput` | `TMP_InputField` | `GetComponentInChildren<TMP_InputField>` matching `"Canvas/PlayerInput"` |
| `AiText` | `TMP_Text` | `GetComponentInChildren<TMP_Text>` matching `"Canvas/AIImage/AIText"` |
| `StopButton` | `Button` | `GetComponentInChildren<Button>` matching `"Canvas/StopButton"` |

The `LegacyKnowledgeBaseController` reference is notable — it finds any `Behaviour` whose type name matches `LLMUnitySamples.KnowledgeBaseGame`. If found, it auto-disables it (see `DisableLegacyController()`). This is how the scene gracefully handles leftover references from earlier phases without removing them.

### 4.3 NPCDialogueManager — Service Discovery Pattern

See §6 below — this is the key architectural pattern.

---

## 5. Missing Scripts — What They Are, Why They're Harmless

If you select the `NPCDialogueSystem` GameObject in the Hierarchy, the Inspector shows **10 missing scripts** at the bottom. This alarms new developers. Here's why it's fine:

```
NPCDialogueSystem (10 Missing Scripts)
```

These are **leftover component references from earlier development phases** — components that were attached to this object during prototyping but have since been refactored into child objects (see §3). The Unity serialization system keeps the `MonoBehaviour` stub references because it doesn't know the script was intentionally removed.

### Why they don't cause problems:

1. **They're not referenced by code.** No `GetComponent<>`, `FindObjectOfType`, or serialized field points to them. They're orphaned serialization artifacts.
2. **They don't throw errors.** Unity silently skips missing scripts during `Awake()`/`Start()` — it just can't resolve the type.
3. **They don't affect WebGL builds.** IL2CPP generates code only for actually-accessible types. Missing scripts are serialization-only artifacts that don't survive the build.
4. **Removing them is optional.** You *can* remove them via the Inspector's "Remove All Missing Scripts" context menu, but it's purely cosmetic.

> **🧑‍💻 Dev NPC:** "I once spent three hours tracking down a bug only to find it was caused by *removing* a missing script that some other system's reflection was depending on. I'm not saying that's your situation. I'm saying 'missing scripts' and 'harmless' are best friends. Don't break them up."

### What were they?

Based on the project history, the missing scripts were components from earlier iterations:
- A direct LLM RAG controller (moved to `Services/NPCDialogueRetrievalService`)
- An older version of the network bridge (moved to `Network/NPCDialogueNetworkBridge`)
- A knowledge base controller (moved and renamed)
- Various prototype-only MonoBehaviours that were consolidated

---

## 6. The Service Discovery Pattern — Children Find Siblings via GetComponentInChildren

This is the most important architectural pattern in the project. Let's look at `NPCDialogueManager.ResolveServices()`:

```csharp
void ResolveServices()
{
    _historyService ??= GetComponentInChildren<NPCDialogueHistoryService>(true)
        ?? GetComponent<NPCDialogueHistoryService>();
    _retrievalService ??= GetComponentInChildren<NPCDialogueRetrievalService>(true)
        ?? GetComponent<NPCDialogueRetrievalService>();
    _sessionService ??= GetComponentInChildren<NPCDialogueSessionService>(true)
        ?? GetComponent<NPCDialogueSessionService>();
    _contextService ??= GetComponentInChildren<PlayerDialogueContextService>(true)
        ?? GetComponent<PlayerDialogueContextService>();
}
```

### What's happening here?

1. **`GetComponentInChildren<T>(true)`** — searches this GameObject and ALL children (including inactive ones) for a component of type `T`.
2. **`?? GetComponent<T>()`** — fallback: if the child search found nothing, try on this exact GameObject.

The pattern relies on the scene hierarchy:

```
NPCDialogueSystem                  ← GetComponentInChildren searches from here
├── Core/                          ← NPCDialogueManager is here (self)
├── Services/
│   ├── NPCDialogueHistoryService  ← Found as child
│   ├── NPCDialogueRetrievalService
│   ├── NPCDialogueSessionService
│   └── PlayerDialogueContextService
```

The `NPCDialogueManager` sits on `NPCDialogueSystem/Core`, and its sibling services live in `NPCDialogueSystem/Services/`. Because `GetComponentInChildren` searches the entire subtree starting from `NPCDialogueSystem`, it finds all of them.

### Why this pattern instead of FindObjectOfType?

| Approach | Pro | Con |
|----------|-----|-----|
| `FindObjectOfType` / `FindAnyObjectByType` | Works regardless of hierarchy | Slow, scans entire scene, order-dependent, fragile |
| `GetComponentInChildren` | Blazing fast (limited to subtree), hierarchy-aware, predictable | Services MUST be children of the same parent |
| Inspector drag-and-drop | Explicit, no search needed | Manual, breaks on prefab instantiation, easy to forget |

The `GetComponentInChildren` pattern is the **sweet spot**: it's fast, implicit, and enforces a clean hierarchy structure. If you accidentally move a service out of `NPCDialogueSystem`, the discovery fails — which is a *good* thing, because it surfaces a hierarchy bug immediately.

### The sibling-discovery contract:

```
Every service MUST be a child (or deeper descendant) of the parent that owns it.
Services MUST NOT rely on other services' Start() having run.
Services MUST self-initialize (see InitializeAsync in each service).
```

---

## 7. Component Inspector Properties for Key Objects

### NPCSceneInitializationController (GameObject: NPCSceneInitialization)

```
[DefaultExecutionOrder(-2000)]
[DisallowMultipleComponent]

Inspector Fields:
  ── References ──
  _flowLogger:         NPCFlowLogger          (auto: NPCFlowLogger.FindOrCreate())
  _networkBootstrap:   NPCNetworkBootstrap    (auto: FindAnyObjectOfType)
  _dialogueManager:    NPCDialogueManager     (auto: FindAnyObjectOfType)
  _backendReadiness:   NPCBackendReadinessService (auto: FindAnyObjectOfType)
  _networkBridge:      NPCDialogueNetworkBridge   (auto: FindAnyObjectOfType)
  _smokeValidator:     NPCDialogueSmokeValidator  (auto: FindAnyObjectOfType)

  ── Startup ──
  _initializeOnStart:               true   ← Bootstrap on scene load
  _configureNetworkTransport:       false  ← NetworkManager handles its own
  _initializeDialogueManager:       false  ← Deferred until post-login (WebGL)
  _verifyBackendsDuringInit:        false  ← Probe LocalAI/Qdrant on boot?
  _initializeNetworkBridge:         true   ← Bridge starts on init
  _validateAfterInitialization:     true   ← Run SmokeValidator after init
  _startNetworkingAfterInit:        false  ← NetworkManager starts manually
```

### NPCDialogueUIController (GameObject: Canvas)

```
[DefaultExecutionOrder(-400)]

Inspector Fields:
  ── References ──
  DialogueManager:       NPCDialogueManager          (auto: Get/Find)
  NetworkBridge:         NPCDialogueNetworkBridge    (auto: Get/Find)
  LegacyKnowledgeBase:   Behaviour                   (auto: Find)

  ── Dialogue UI ──
  PlayerInput:           TMP_InputField              (auto: GetComponentInChildren)
  AiText:                TMP_Text                    (auto: GetComponentInChildren)
  StopButton:            Button                      (auto: GetComponentInChildren)

  ── Initialize ──
  InitializeOnStart:     false  ← Deferred init for WebGL

  ── Runtime Status (read-only) ──
  ActiveProfile:         <current NPC display name>
  ActiveSlug:            <current NPC slug>
  HasDialogueManager:    true/false
  IsInitialized:         true/false
```

### NPCDialogueManager (GameObject: NPCDialogueSystem/Core)

```
[DefaultExecutionOrder(-1500)]

Inspector Fields (grouped by FoldoutGroup):
  ── Chat Client ──
  _chatClient:          NPCLocalAIClient  [Required]

  ── RAG Services ──
  _useQdrantRag:        true
  _qdrantRag:           QdrantRAGService

  ── Persistence ──
  _supabaseRepo:        SupabaseDialogueRepository

  ── LLM Configuration ──
  _remoteHost:          "localhost"
  _remotePort:          8080                       (NPCLocalAIConfig.LocalAIDirectPort)
  _remoteModel:         "llama-3.2-3b-instruct:q8_0"
  _remoteEmbeddingHost: "localhost"
  _remoteEmbeddingPort: 8080                       (same as direct port)
  _ragEmbeddingPath:    "RAG/NPCDialogues-minilm-chunked.rag"

  ── Profiles ──
  _profiles:            NPCProfile[]               (array of ScriptableObjects)
  _maxHistoryPerNPC:    20
  _initializeOnStart:   false

  ── Events (UnityEvent) ──
  OnNpcChanged:         UnityEvent<string>
  OnResponseStart:      UnityEvent<string>
  OnResponseComplete:   UnityEvent<string, string>
  OnError:              UnityEvent<string>

  ── Runtime Status (read-only) ──
  DirectLocalAiEndpoint:  http://localhost:8080/v1/chat/completions
  ActiveProfile:          <current NPC display name>
```

---

## 8. Step-by-Step Scene Setup (Starting from Scratch)

Imagine you just created a new Unity scene. Here's how to build `NPCDialoguePrototype1.unity` from nothing.

### Step 1: Create the Root Structure

| Order | Action | Result |
|-------|--------|--------|
| 1 | Create empty GameObject named `Network_Manager` | Root manager |
| 2 | Add `NetworkManager` component | Netcode singleton |
| 3 | Create empty `NPCSceneInitialization` | Root initializer |
| 4 | Create empty `Main Camera` | Scene camera |
| 5 | Create empty `Directional Light` | Scene light |
| 6 | Create empty `EventSystem` | UI input handling |
| 7 | Create empty `Ground` (use a Plane primitive) | Visual reference |
| 8 | Create empty `NPCFlowLogger` | Structured logging |
| 9 | Create empty `WebGLGameplayLoadController` | WebGL loading screen |

### Step 2: Create the NPC Dialogue System

1. Create empty `NPCDialogueSystem` root
2. Create child `Core` → add `NPCDialogueManager` + `NPCDialogueSmokeValidator`
3. Create child `Backend` → add `NPCBackendReadinessService`
4. Create child `Services` → add:
   - `NPCDialogueHistoryService`
   - `NPCDialogueRetrievalService`
   - `NPCDialogueSessionService`
   - `PlayerDialogueContextService`
5. Create child `Network` → add:
   - `NetworkObject`
   - `NPCDialogueNetworkBridge`
   - `ItemTradeService`

### Step 3: Create the Network System

1. Create empty `NPCNetworkSystem` root
2. Add `NPCNetworkBootstrap` component

### Step 4: Create the Auth UI

1. Create empty `AuthUI` root → add `AuthUIController`
2. Create child `AuthPanel` → add `AuthUIContent`
3. Under `AuthPanel`, create the UI elements:
   - `Username` (TMP_InputField)
   - `Password` (TMP_InputField)
   - `ConfirmPassword` (TMP_InputField)
   - `RememberToggle` (Toggle)
   - `ErrorText` (TMP_Text)
   - `SubmitButton` (Button)
   - `SwitchModeButton` (Button)

### Step 5: Create the Canvas (Dialogue UI)

1. Create `Canvas` (via right-click → UI → Canvas) → add `NPCDialogueUIController`
2. Under Canvas, create:
   - `StopButton` (Button)
   - `PlayerInput` (TMP_InputField)
   - Empty `AIImage` → child `AIText` (TMP_Text)

### Step 6: Wire the References

- **NPCSceneInitializationController**: The `ResolveReferences()` method auto-discovers everything. Open the Inspector to verify all 6 references are populated. If any are missing, click "Auto-Assign References" or manually drag them in.
- **NPCDialogueUIController**: Click `Auto-Assign References` button in the Inspector. Verify `PlayerInput`, `AiText`, and `StopButton` are populated.
- **NPCDialogueManager**: Drag the NPCProfile ScriptableObject asset(s) into the `_profiles` array. Set the LLM host/port. Verify `_chatClient` and `_qdrantRag` are assigned.

### Step 7: Configure NetworkManager

1. Set `NetworkConfig` → `Transport` to the WebSocket transport (UNet or Unity Transport)
2. Add all networked prefabs to the `NetworkPrefabsList` (NetworkObject-prefixed objects)
3. Set `Connection Approval` to false (or wire an approval callback)

### Step 8: Configure NPCNetworkBootstrap

1. Set the transport configuration (WebSocket for WebGL, Unity Transport for standalone)
2. Configure auto-start mode (Manual for development, AutoHost for testing)
3. Verify port is 11474

### Step 9: Run the Smoke Test

1. Save the scene
2. Enter Play Mode
3. Check the Console: you should see flow log entries from `NPCSceneInitializationController` logging each phase
4. If all phases complete, the dialogue input field should be enabled
5. Type a message and verify the NPC responds

### Step 10: Verification Checklist

- [ ] Console shows no `NullReferenceException` during initialization
- [ ] All 6 references on `NPCSceneInitialization` are assigned
- [ ] `NPCDialogueManager.ResolveServices()` finds all 4 child services
- [ ] Canvas UI input field is interactable after init
- [ ] Typing a message sends a request to LocalAI (visible in LocalAI logs)
- [ ] `NPCFlowLogger` writes structured flow entries (check `NPCFlowLog/` directory)

> **🧑‍💻 Dev NPC:** "If you followed all ten steps and it doesn't work, check three things in order: (1) Did you assign the NPCProfile? (2) Is LocalAI running on port 8080? (3) Did you actually save the scene before pressing Play? I can't count how many times #3 was the answer."

---

## Cross-Reference Table

| Concept | See Also |
|---------|----------|
| Script Execution Order (why -2000?) | [Chapter 03](Part1_Foundations/03_ScriptExecutionOrder.md) |
| Service Architecture (discovery pattern) | [Chapter 04](Part2_Architecture/04_ServiceArchitecture.md) |
| Network Bridge (NPCDialogueNetworkBridge) | [Chapter 08](Part3_Networking/08_NetworkBridge.md) |
| NPCProfile (ScriptableObject setup) | [Chapter 05](Part2_Architecture/05_NPCProfile.md) |
| LocalAI port configuration | [Chapter 09](Part4_Backend/09_LocalAI.md) |
| Qdrant RAG service | [Chapter 10](Part4_Backend/10_Qdrant.md) |
| WebGL build checklist | [Chapter 14](Part5_SceneAndBuild/14_WebGLBuild.md) |

---

*Next: [Chapter 14 — WebGL Build Checklist](Part5_SceneAndBuild/14_WebGLBuild.md)*
