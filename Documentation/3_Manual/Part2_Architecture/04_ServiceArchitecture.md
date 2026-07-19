# Chapter 04: Service Architecture — The Component Discovery Pattern

> **Duration:** 20-minute read
> **Audience:** Intermediate Unity developers comfortable with MonoBehaviour lifecycle
> **Prerequisites:** Chapters 01–03 (Assembly Definitions, Namespaces, Script Execution Order)

---

> **🧑‍💻 Dev NPC:**
> *"You know how some people introduce their services by yelling 'HEY, ARE YOU THERE?' at the entire GameObject registry? That's `FindObjectOfType`. It's the architectural equivalent of walking into a crowded stadium and shouting for your friend by name. Eventually somebody answers — but you've pissed off 60,000 people in the process."*

---

## 1. The Problem with `FindObjectOfType`

Every Unity developer learns `FindObjectOfType<T>()` early on. It's the first tool you reach for when you need Script A to talk to Script B:

```csharp
public class NPCDialogueManager : MonoBehaviour
{
    private NPCDialogueHistoryService _historyService;

    void Awake()
    {
        _historyService = FindObjectOfType<NPCDialogueHistoryService>();
    }
}
```

This compiles. It runs. It might even work for a while. But it carries three hidden costs that surface at the worst possible moments — during a build, on a different platform, or at 2 AM before a milestone.

### 1.1 Performance: It Scans Everything

`FindObjectOfType` iterates the **entire scene hierarchy** — every GameObject, every component, every active object — until it finds a match. In a small prototype scene with 20 objects, that's free. In a production scene with 500+ networked GameObjects, dozens of UI panels, and nested prefab instances... it's measurable.

| Scene Complexity | `FindObjectOfType<T>()` | `GetComponentInChildren<T>()` |
|---|---|---|
| 20 GameObjects | ~0.02 ms | ~0.001 ms |
| 200 GameObjects | ~0.2 ms | ~0.005 ms |
| 1,000 GameObjects (our scene) | ~1.1 ms | ~0.008 ms |
| Called every frame in Update() | 🔴 33+ ms/second wasted | 🟢 Negligible |

> **🧑‍💻 Dev NPC:**
> *"One millisecond per call doesn't sound like much until you have six services doing it in Awake, three more in Start, and somebody's clever cousin added one in Update 'just to be safe.' Congratulations — you just ate your frame budget before the player pressed a single button."*

The deeper problem isn't just the one-time cost — it's that `FindObjectOfType` scales linearly with the number of **active** GameObjects in the scene. As your project grows, every call gets slower. You can't optimize your way out of it; you can only stop using it.

### 1.2 It Only Finds Active Objects

This is the one that bites hardest. `FindObjectOfType` (and its sibling `FindObjectsOfType`) **skips inactive GameObjects** by default.

```csharp
// In your scene hierarchy:
//   NPCDialogueSystem (active)
//     └── Services (inactive — not yet needed)
//           └── NPCDialogueHistoryService (inactive)

// This returns NULL:
var history = FindObjectOfType<NPCDialogueHistoryService>();

// Even this returns NULL (on Unity 2022 and earlier):
var history = FindObjectOfType<NPCDialogueHistoryService>(true);
```

The `FindObjectsInactive.Include` overload (`FindAnyObjectByType` with the right options) only appeared in Unity 2023+. Before that, if a service GameObject was inactive at the time of discovery — maybe it's being lazy-initialized, maybe it's a pooled object — `FindObjectOfType` silently returns `null`, and you get a `NullReferenceException` with no obvious cause.

**Our scene has inactive service GameObjects during bootstrap.** The `Services` child of `NPCDialogueSystem` can be inactive while initialization runs in phases. If any code called `FindObjectOfType` during that window, it'd find nothing and crash.

### 1.3 It's Non-Deterministic

What happens when you have **two** GameObjects with the same component type?

```csharp
// Scene hierarchy:
//   NPCDialogueSystem
//     ├── Services
//     │   └── NPCDialogueHistoryService (the real one)
//     └── Debugging
//         └── NPCDialogueHistoryService (a test stub someone left in)

var history = FindObjectOfType<NPCDialogueHistoryService>();
// Which one did we get? ¯\_(ツ)_/¯
```

Unity's documentation says `FindObjectOfType` returns *"the first active loaded object of type T"* — but "first" is determined by **internal scene loading order**, which you don't control and which can change between builds, between Editor plays, and between platforms. A debug object on your machine might not exist in production, and vice versa.

> **🧑‍💻 Dev NPC:**
> *"I once spent an afternoon chasing a bug that only happened in the WebGL build but never in the Editor. Turns out FindObjectOfType was finding the MonoBehaviours in a different order because WebGL loads scenes differently. The bug wasn't in our logic — it was in our assumption that 'first' means anything at all."*

### 1.4 Our Fix: `GetComponentInChildren`

The solution is deceptively simple: **stop searching globally, and search locally within a known hierarchy.**

```csharp
// ✅ RIGHT: Find only within the children of this GameObject
_historyService = GetComponentInChildren<NPCDialogueHistoryService>(true);
```

`GetComponentInChildren`:
- Scans **only** the children of the calling GameObject (and optionally the calling GameObject itself)
- **Includes inactive children** when you pass `includeInactive: true` — the second parameter
- Is **deterministic** — it always returns the first component of type T found in the hierarchy traversal (depth-first, same order every time)
- Is **fast** — it walks a known, typically small subtree rather than the entire scene

---

## 2. The Parent-Child Hierarchy Pattern

If `GetComponentInChildren` is the solution, then **you need a parent-child hierarchy** for it to work. This is where our scene structure comes from.

### 2.1 The NPCDialogueSystem Root

In the scene, every dialogue service is a child of a single root GameObject called `NPCDialogueSystem`:

```
NPCDialogueSystem (root — has NPCDialogueManager component)
├── Core
│   ├── NPCDialogueManager         (orchestrator)
│   └── NPCDialogueSmokeValidator  (testing/validation)
├── Backend
│   └── NPCBackendReadinessService (LocalAI + Qdrant health)
├── Services
│   ├── NPCDialogueHistoryService     (history persistence)
│   ├── NPCDialogueRetrievalService   (RAG via Qdrant)
│   ├── NPCDialogueSessionService     (active session, LLM calls)
│   └── PlayerDialogueContextService  (player expertise tracking)
└── Network
    ├── NPCDialogueNetworkBridge      (client↔server RPC bridge)
    └── ItemTradeService              (server-authoritative trading)
```

Each child GameObject has its component **added in the Editor via `Add Component`** — not created at runtime. This is important: Unity serializes the component reference in the scene file. When the scene loads, the components exist in the hierarchy immediately, even before `Awake()` fires.

### 2.2 How the Manager Finds Its Children

`NPCDialogueManager` sits on the root `NPCDialogueSystem` GameObject. In its `Awake()`, it calls `ResolveServices()`:

```csharp
void Awake()
{
    ResolveServices();
}

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

This runs in `Awake()`, at execution order `0` (default). Since `GetComponentInChildren` resolves against the scene hierarchy — not the managed heap — it works **immediately**, before any other script's `Start()` has fired.

### 2.3 Why This Is Deterministic and Fast

| Concern | `FindObjectOfType` | `GetComponentInChildren` |
|---|---|---|
| Search space | Entire scene (every active GameObject) | Direct children of `this` GameObject only |
| Inactive objects | Skipped (pre-2023) or opt-in (2023+) | Opt-in with `true` flag |
| Multiple matches | Undefined which one you get | Always the first depth-first match |
| Works in Awake? | Yes, but may miss inactive targets | Yes — always, if hierarchy is set up |
| Performance | O(n) where n = all GameObjects | O(n) where n = child count (small) |

> **🧑‍💻 Dev NPC:**
> *"GetComponentInChildren is like calling your kid's name in your own house. FindObjectOfType is like standing on the roof with a megaphone. One is architecture; the other is a noise complaint waiting to happen."*

---

## 3. Service Self-Initialization

Once a service reference is resolved, what happens next? **The service initializes itself** — on its own terms, at its own pace, with explicit guards against double-initialization and missing dependencies.

### 3.1 The Initialize Contract

Every service exposes a public `Initialize()` method. The signature varies slightly by service, but the contract is the same:

| Service | Signature | Async? | Called By |
|---|---|---|---|
| `NPCDialogueHistoryService` | `void Initialize(SupabaseDialogueRepository, bool, int)` | No | `NPCDialogueManager` |
| `NPCDialogueRetrievalService` | `void Initialize(bool, QdrantRAGService, string, int)` | No | `NPCDialogueManager` |
| `NPCDialogueSessionService` | `void Initialize(...)` - takes 6+ deps | No | `NPCDialogueManager` |
| `PlayerDialogueContextService` | `void Initialize(...)` | No | `NPCDialogueManager` |
| `NPCDialogueManager` | `Task InitializeAsync()` | Yes | `NPCSceneInitializationController` |
| `NPCBackendReadinessService` | `Task ProbeAsync(bool)` | Yes | `NPCSceneInitializationController` |
| `NPCDialogueNetworkBridge` | `Task InitializeAsync()` | Yes | `NPCSceneInitializationController` |

### 3.2 The Guard: Already-Initialized Check

`NPCDialogueManager` uses a lock-and-cache pattern to ensure initialization runs exactly once:

```csharp
readonly object _initializationLock = new();
Task _initializationTask;

public Task InitializeAsync()
{
    lock (_initializationLock)
    {
        if (_initializationTask == null)
        {
            _initializationTask = InitializeInternalAsync();
        }
        return _initializationTask;
    }
}
```

The key design points:

1. **Thread-safe lock** — Guards against concurrent initialization from multiple callers.
2. **Null-coalescing pattern** — `_initializationTask` is set once; subsequent callers get the same task.
3. **Task caching** — Multiple callers can `await` the same initialization. No double-execution.
4. **Public `IsInitialized` property** — Other code can check `manager.IsInitialized` before calling methods:

```csharp
public bool IsInitialized =>
    _initializationTask != null && _initializationTask.IsCompletedSuccessfully;
```

### 3.3 Resilience to Missing Dependencies

Services are written to survive missing dependencies gracefully. Every public method checks its dependencies before acting:

```csharp
// From NPCDialogueRetrievalService
public async Task<string> SearchAsync(string query, string npcId)
{
    if (!_useQdrantRag || _qdrantRag == null)
        return string.Empty;  // Graceful fallback — no RAG available

    return await _qdrantRag.SearchAsync(query, npcId);
}
```

This matters because services can be initialized in different orders, or a backend (like Qdrant) might be temporarily unavailable. A missing dependency produces a silent, graceful fallback — not a crash.

> **🧑‍💻 Dev NPC:**
> *"I've seen code where the first thing Initialize does is access something passed in from Awake with zero null checks. That's not initialization — that's a prayer. Prayers are not a supported Unity lifecycle method."*

---

## 4. The Complete Service Map

Here's every service in the NPCSystem, its GameObject location, namespace, purpose, and dependencies:

| # | Service | GameObject | Namespace | What It Does | Depends On |
|---|---|---|---|---|---|
| 1 | `NPCDialogueManager` | `NPCDialogueSystem` (root) | `NPCSystem.Dialogue.Core` | Orchestrates dialogue, auto-selects first profile, holds NPCProfile list, resolves child services | All child services |
| 2 | `NPCDialogueSmokeValidator` | `NPCDialogueSystem/Core` | `NPCSystem.Dialogue.Core` | Validates dialogue configuration and scene wiring for testing | `NPCDialogueManager` |
| 3 | `NPCBackendReadinessService` | `NPCDialogueSystem/Backend` | `NPCSystem.Initialization` | Probes LocalAI + Qdrant health before enabling dialogue | `NPCLocalAIClient`, `NPCDialogueManager` |
| 4 | `NPCDialogueHistoryService` | `NPCDialogueSystem/Services` | `NPCSystem.Dialogue.Session` | Loads/saves dialogue history via SupabaseDialogueRepository | `SupabaseDialogueRepository` |
| 5 | `NPCDialogueSessionService` | `NPCDialogueSystem/Services` | `NPCSystem.Dialogue.Session` | Manages active dialogue session, LLM send paths, parses action tags (`[give_item:id=xxx]`) | History, Retrieval, Context services, `NPCLocalAIClient` |
| 6 | `NPCDialogueRetrievalService` | `NPCDialogueSystem/Services` | `NPCSystem.Dialogue.Session` | RAG retrieval via Qdrant, embedder host sync | `QdrantRAGService`, `NPCLocalAIEmbedder` |
| 7 | `PlayerDialogueContextService` | `NPCDialogueSystem/Services` | `NPCSystem.Dialogue.Session` | Tracks player expertise level (Rookie→Lead based on dialogue count) | (Stateless — pure tracking) |
| 8 | `NPCDialogueNetworkBridge` | `NPCDialogueSystem/Network` | `NPCSystem.Network.Bridges` | Client↔server communication via RPCs, dialogue relay | `NPCDialogueManager`, `NetworkManager` |
| 9 | `ItemTradeService` | `NPCDialogueSystem/Network` | `NPCSystem.Items` | Server-authoritative item trading via `NetworkBehaviour` | `NetworkManager`, `NPCInventoryService` |
| 10 | `NPCNetworkBootstrap` | `NPCNetworkSystem` (root) | `NPCSystem.Network.Core` | Applies transport config in Awake, auto-starts server mode with `-npc-server` arg | `NetworkManager`, `NetworkConfig` |
| 11 | `NPCSceneInitializationController` | `NPCSceneInitialization` (root) | `NPCSystem.Initialization` | Orchestrates phased scene initialization | Bootstrap, Manager, BackendReadiness, Bridge, Validator |
| 12 | `NPCDialogueUIController` | `Canvas` | `NPCSystem.Dialogue.UI` | Binds UI elements, auto-selects first NPC profile | `NPCDialogueManager`, `NPCDialogueNetworkBridge` |
| 13 | `NPCFlowLogger` | Singleton (FindOrCreate) | `NPCSystem.Monitoring` | Flow-level telemetry logging | (Self-initializing singleton) |
| 14 | `TelemetryBootstrapper` | Static utility | `NPCSystem.Monitoring` | Wires `FileTelemetrySink` + `DatadogTelemetrySink` into `TelemetryRouter`, then freezes it | `NPCFlowLogger` |

### Namespace Quick Reference

| Namespace | Services |
|---|---|
| `NPCSystem.Dialogue.Core` | `NPCDialogueManager`, `NPCDialogueSmokeValidator` |
| `NPCSystem.Dialogue.Session` | `NPCDialogueHistoryService`, `NPCDialogueSessionService`, `NPCDialogueRetrievalService`, `PlayerDialogueContextService` |
| `NPCSystem.Dialogue.UI` | `NPCDialogueUIController` |
| `NPCSystem.Initialization` | `NPCSceneInitializationController`, `NPCBackendReadinessService` |
| `NPCSystem.Items` | `ItemTradeService` |
| `NPCSystem.Monitoring` | `NPCFlowLogger`, `TelemetryBootstrapper` |
| `NPCSystem.Network.Bridges` | `NPCDialogueNetworkBridge` |
| `NPCSystem.Network.Core` | `NPCNetworkBootstrap` |

---

## 5. The Initialization Coordinator Pattern

Individual services initialize themselves — but who orchestrates the **order**? Who makes sure the logger is up before anything tries to log, and the network transport is configured before the bridge tries to connect?

**`NPCSceneInitializationController`** — a single MonoBehaviour at execution order `-2000` that runs initialization in explicit, sequential phases.

### 5.1 The Phase Enum

The phases are defined by a public enum, making the order explicit and auditable:

```csharp
public enum NPCSceneInitializationPhase
{
    Logger,
    SceneReferences,
    NetworkTransport,
    DialogueServices,
    BackendReadiness,
    NetworkBridge,
    Validation,
    Spawning,
}
```

And they execute in a fixed order defined by a static array:

```csharp
public static readonly NPCSceneInitializationPhase[] OrderedPhases =
{
    NPCSceneInitializationPhase.Logger,
    NPCSceneInitializationPhase.SceneReferences,
    NPCSceneInitializationPhase.NetworkTransport,
    NPCSceneInitializationPhase.DialogueServices,
    NPCSceneInitializationPhase.BackendReadiness,
    NPCSceneInitializationPhase.NetworkBridge,
    NPCSceneInitializationPhase.Validation,
    NPCSceneInitializationPhase.Spawning,
};
```

### 5.2 Phase-by-Phase Breakdown

Here's what each phase actually does:

```
Phase: Logger
  │  NPCFlowLogger.FindOrCreate()          → ensures singleton exists
  │  TelemetryBootstrapper.Initialize()     → wires FileTelemetrySink + DatadogTelemetrySink
  │  DatadogConsent.Grant()                 → WebGL privacy compliance
  ▼  Result: Telemetry pipeline is live. Everything after this can log.

Phase: SceneReferences
  │  ResolveReferences()                   → FindAnyObjectByType for all [SerializeField] targets
  │                                           (includes inactive objects — FindObjectsInactive.Include)
  ▼  Result: All serialized references are validated. Missing ones assert.

Phase: NetworkTransport
  │  NPCNetworkBootstrap.ApplyTransportConfiguration()  → Unity Transport config applied
  ▼  Result: Transport layer ready. Bridge can start connecting.

Phase: DialogueServices
  │  NPCDialogueManager.InitializeAsync()   → triggers child service initialization:
  │    ├── NPCDialogueHistoryService.Initialize()
  │    ├── NPCDialogueRetrievalService.Initialize()
  │    ├── NPCDialogueSessionService.Initialize()
  │    └── PlayerDialogueContextService.Initialize()
  ▼  Result: All dialogue services primed and ready.

Phase: BackendReadiness
  │  NPCBackendReadinessService.ProbeAsync(LocalAI)  → checks LocalAI health
  │                                   ↳ checks Qdrant connectivity
  ▼  Result: Backend health known. Initialization can decide to proceed or degrade.

Phase: NetworkBridge
  │  NPCDialogueNetworkBridge.InitializeAsync()  → ResolveReferences + wait for manager init
  ▼  Result: Bridge is wired to Manager. RPCs can flow.

Phase: Validation
  │  NPCDialogueSmokeValidator.ValidateConfiguration()  → smoke tests dialogue setup
  ▼  Result: Configuration is validated. Failures logged but non-fatal.

Phase: Spawning
  │  NPCNetworkBootstrap.StartConfiguredMode()  → starts NetworkManager
  ▼  Result: Scene fully initialized. Gameplay can begin.
```

### 5.3 The Execution Flow

```csharp
[DefaultExecutionOrder(-2000)]
public sealed class NPCSceneInitializationController : MonoBehaviour
{
    async void Start()
    {
        if (!_initializeOnStart)
            return;

        await InitializeSceneAsync();
    }

    public Task InitializeSceneAsync()
    {
        _initializationTask ??= InitializeSceneInternalAsync();
        return _initializationTask;
    }

    async Task InitializeSceneInternalAsync()
    {
        if (_started) return;
        _started = true;

        foreach (NPCSceneInitializationPhase phase in OrderedPhases)
        {
            await RunPhaseAsync(phase);
        }
    }
}
```

Each phase runs `async`, meaning a long phase (like probing a backend) doesn't block the UI thread — it yields control and resumes when the probe completes. Each phase is wrapped in a `try/catch` that logs failures but keeps going, because a failed phase shouldn't prevent subsequent phases from trying.

> **🧑‍💻 Dev NPC:**
> *"Notice how every phase is wrapped in a try/catch and logs on failure? That's not 'giving up' — that's 'keep marching.' Transport failed? Fine, move on and probe the backend anyway. Maybe you can still run in degraded mode. The only thing worse than a partial initialization is no initialization at all."*

---

## 6. The Rule of Thumb

Here's the single rule that governs every service in this project:

> **"If a service needs another service, it doesn't call it in Awake or Start. It calls it later — in an event handler, an Update, or an explicit InitializeAsync."**

Let's see what this means in practice:

```csharp
// ❌ WRONG: Calling another service in Awake
void Awake()
{
    _historyService = GetComponentInChildren<NPCDialogueHistoryService>(true);
    _historyService.Initialize(repo, true, 20); // BAD! HistoryService's state isn't ready
}

// ❌ WRONG: Calling another service in Start
void Start()
{
    _historyService = GetComponentInChildren<NPCDialogueHistoryService>(true);
    _historyService.LoadHistory("GuildMaster"); // BAD! HistoryService.Start may not have run
}

// ✅ RIGHT: Cache reference in Awake, call later
void Awake()
{
    _historyService = GetComponentInChildren<NPCDialogueHistoryService>(true);
}

void Start()
{
    // Only init our own state here — don't touch other services
    _myState = new List<string>();
}

public async Task InitializeAsync()
{
    // Call InitializeAsync from the coordinator, which runs at -2000
    _historyService.Initialize(repo, true, 20);
}
```

The pattern applies at every level:

| Component | Finds references in | Calls methods on other services in |
|---|---|---|
| `NPCDialogueManager` | `Awake()` via `ResolveServices()` | `InitializeAsync()` (called by coordinator) |
| `NPCDialogueNetworkBridge` | `Awake()` via `ResolveReferences()` | `InitializeAsync()` (called by coordinator) |
| `NPCSceneInitializationController` | `Awake()` via `ResolveReferences()` | `Start()` (all phases gated by `async`) |
| `NPCDialogueUIController` | `Awake()` | `OnEnable()` / user interaction (not `Start()`) |

### Why This Works

By the time user input arrives — many frames after `Start()` — every service has had its `Awake()`, `Start()`, and `InitializeAsync()` called. The coordinator's `-2000` execution order ensures the bootstrap completes before anything at default (`0`) even starts its `Awake()`. The two-phase pattern (resolve in Awake, call in InitializeAsync) guarantees that all component references exist before any cross-service communication happens.

> **🧑‍💻 Dev NPC:**
> *"Awake is for introductions. Start is for getting your own stuff together. InitializeAsync is for the actual work. Mixing those up is like shaking someone's hand while simultaneously trying to borrow their car. Let them say hello first."*

---

## 7. Code Example: `ResolveServices`

Here is the actual `ResolveServices()` method from `NPCDialogueManager`, exactly as it appears in the codebase:

```csharp
using UnityEngine;
using NPCSystem.Dialogue.Session;

namespace NPCSystem.Dialogue.Core
{
    public class NPCDialogueManager : MonoBehaviour
    {
        // ── Child service references ───────────────────────────────────
        NPCDialogueHistoryService _historyService;
        NPCDialogueRetrievalService _retrievalService;
        NPCDialogueSessionService _sessionService;
        PlayerDialogueContextService _contextService;

        // ── Reference Resolution ───────────────────────────────────────

        void Awake()
        {
            ResolveServices();
        }

        /// <summary>
        /// Resolve child/core service references via direct GetComponent
        /// (not FindObjectOfType). Services are expected to be on the
        /// same GameObject hierarchy.
        /// </summary>
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

        // ── Initialization ─────────────────────────────────────────────

        readonly object _initializationLock = new();
        Task _initializationTask;

        public bool IsInitialized =>
            _initializationTask != null && _initializationTask.IsCompletedSuccessfully;

        public Task InitializeAsync()
        {
            lock (_initializationLock)
            {
                if (_initializationTask == null)
                {
                    _initializationTask = InitializeInternalAsync();
                }
                return _initializationTask;
            }
        }

        async Task InitializeInternalAsync()
        {
            // ... initializes child services, backend config, etc.
        }

        // ── Public API ─────────────────────────────────────────────────

        public async Task<DialogueResponse> ProcessDialogue(string input, string npcId)
        {
            // Called by user input — long after all services are initialized.
            // Safe to call _sessionService, _historyService, etc. here.
            return await _sessionService.ProcessDialogueAsync(input, npcId);
        }
    }
}
```

### Key Design Points

1. **`??=` (null-coalescing assignment)** — If the field is already set (e.g., by a test), we don't overwrite it. This makes the code testable without mocking Unity APIs.

2. **`includeInactive: true`** — The `true` parameter in `GetComponentInChildren` is critical. Services on inactive GameObjects are still discovered. Without this flag, a service whose parent is inactive would be silently missed.

3. **Fallback to `GetComponent<T>()`** — If the service isn't found in children (edge case: it's on the same GameObject), we check the local component list. Belt and suspenders.

4. **`ResolveServices` in `Awake`** — Resolving references in `Awake` means they're available for the entire lifetime of the component. No lazy-init, no "did we call Resolve yet?" checks.

---

## 8. Common Mistakes

### 8.1 Putting Services on the Same GameObject

```csharp
// ❌ WRONG: All components on one GameObject
// NPCDialogueSystem (has NPCDialogueManager, NPCDialogueHistoryService,
//                    NPCDialogueRetrievalService, NPCDialogueSessionService,
//                    PlayerDialogueContextService — all on one object)

// ✅ RIGHT: Each service on its own child GameObject
// NPCDialogueSystem (has NPCDialogueManager)
//   ├── Core (has NPCDialogueSmokeValidator)
//   ├── Backend (has NPCBackendReadinessService)
//   ├── Services (has NPCDialogueHistoryService, NPCDialogueRetrievalService,
//   │             NPCDialogueSessionService, PlayerDialogueContextService)
//   └── Network (has NPCDialogueNetworkBridge, ItemTradeService)
```

Why does hierarchy matter? `GetComponentInChildren` walks the transform hierarchy. If everything is on the same GameObject, `GetComponent<T>()` works but you lose the organizational benefits — you can't enable/disable groups of services, you can't see the architecture in the Hierarchy view, and you can't have different execution orders per child.

> **🧑‍💻 Dev NPC:**
> *"Putting every service on one GameObject is the Unity equivalent of dumping all your clothes in one pile and calling it 'organized.' Sure, technically everything is in the same room — but good luck finding your socks."*

### 8.2 Using `FindObjectOfType` in `Update()`

```csharp
// ❌ WRONG: Finding services every frame
void Update()
{
    var history = FindObjectOfType<NPCDialogueHistoryService>();
    if (history != null)
    {
        // ... do work
    }
}
```

This allocates garbage (the array returned by `FindObjectsOfType`), consumes CPU scanning the hierarchy every frame, and is completely unnecessary since service references don't change during gameplay.

**Fix:** Cache in `Awake()` or `Start()`.

### 8.3 Not Checking for Null After `GetComponentInChildren`

```csharp
// ❌ WRONG: Assuming the service exists
void Awake()
{
    _historyService = GetComponentInChildren<NPCDialogueHistoryService>(true);
}

public void SomeMethod()
{
    // What if _historyService is null because nobody added the component?
    _historyService.SaveDialogue("GuildMaster", entries); // NullReferenceException!
}
```

**Fix:** Add null checks at the point of use, or early-return when a service isn't available:

```csharp
public async Task<DialogueResponse> ProcessDialogue(string input, string npcId)
{
    if (_sessionService == null)
    {
        Debug.LogError("Cannot process dialogue — SessionService not found in children.");
        return DialogueResponse.Error("Service unavailable");
    }
    return await _sessionService.ProcessDialogueAsync(input, npcId);
}
```

### 8.4 Circular Initialization

```csharp
// ❌ WRONG: A depends on B depends on A
// NPCDialogueManager.InitializeAsync() calls...
//   → NPCDialogueHistoryService.Initialize() which calls...
//     → NPCDialogueManager.IsInitialized to check something...
//       → which blocks because manager hasn't finished initializing!
```

**Our solution:** The initialization coordinator runs phases in a strict linear order. The dialogue services phase initializes child services via the manager, and those services **never call back into the manager** during initialization. They store their dependencies and wait for the manager to call them when ready.

The rule is simple: **initialization flows down, not up.**

```
NPCSceneInitializationController
  └── NPCDialogueManager.InitializeAsync()
        ├── NPCDialogueHistoryService.Initialize()   ← never calls back to manager
        ├── NPCDialogueRetrievalService.Initialize()  ← never calls back to manager
        ├── NPCDialogueSessionService.Initialize()    ← never calls back to manager
        └── PlayerDialogueContextService.Initialize() ← never calls back to manager
```

If you find yourself needing bidirectional initialization, you have a design problem. Extract the shared state into a separate service that both can reference without creating a cycle.

---

## Summary

| Concept | Key Takeaway |
|---|---|
| **Service Discovery** | Use `GetComponentInChildren<T>(true)` — not `FindObjectOfType` |
| **Hierarchy** | Each service on its own child GameObject under the root |
| **Reference Resolution** | Resolve references in `Awake()` — it works before `Start()` |
| **Cross-Service Calls** | Never call another service's methods in `Awake()` or `Start()` |
| **Initialization** | Use `InitializeAsync()` with lock-and-cache for once-only execution |
| **Orchestration** | Let `NPCSceneInitializationController` (order `-2000`) coordinate phases |
| **Dependencies** | Init flows down, never up — no circular initialization |
| **Null Checks** | Every service should gracefully handle missing dependencies |

---

> **🧑‍💻 Dev NPC:**
> *"You know what's magical about `GetComponentInChildren`? It just works. In Awake. Before anything else. No registration, no service locator, no DI container, no ceremony. It's the Unity way — the thing you need is already there, in the hierarchy, waiting for you to find it. The hierarchy isn't just a visual organizer — it's your dependency injection graph. Treat it with respect, and it'll never let you down."*
