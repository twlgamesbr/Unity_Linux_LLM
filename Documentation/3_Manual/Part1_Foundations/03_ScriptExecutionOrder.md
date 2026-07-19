# Chapter 03: Script Execution Order — The Hidden Time Bomb

> **Duration:** 20-minute read
> **Audience:** Intermediate Unity developers
> **Prerequisites:** Basic MonoBehaviour lifecycle knowledge; Chapters 01–02 (Assembly Definitions, Namespaces)

---

> **NPC Senior Dev says:**
> *"Script execution order is the #1 thing nobody teaches juniors but everybody expects them to know. It's the 'we'll figure it out at runtime' of architecture — and then runtime figures it out by throwing `NullReferenceException` in your face at 2 AM before a deadline."*

---

## 1. The Unity Execution Order Trap

Every Unity developer learns the MonoBehaviour lifecycle early on:

| Method | When It Runs |
|---|---|
| `Awake()` | On object instantiation, before any other code |
| `OnEnable()` | When the component is enabled (can run multiple times) |
| `Start()` | First frame the component is active, after Awake |
| `Update()` | Every frame |
| `LateUpdate()` | Every frame, after all Update calls |
| `FixedUpdate()` | Fixed timestep (physics) |

That list looks straightforward. Here's the lie: **Unity guarantees the order of these methods within a single script, but NOT across different scripts.**

```csharp
// Script A
public class ServiceA : MonoBehaviour
{
    private ServiceB _serviceB;

    void Awake()
    {
        _serviceB = FindObjectOfType<ServiceB>();
    }

    void Start()
    {
        _serviceB.Initialize(); // BOOM — ServiceB.Start might not have run yet!
    }
}
```

This compiles fine. It might work fine in the Editor. But add one more script to the scene that also has an `Awake()`, and suddenly `_serviceB` is `null` — or worse, it's not null but its internal state hasn't been set up yet because `ServiceB.Start()` hasn't fired.

### The Classic Bug

```
NullReferenceException: Object reference not set to an instance of an object
  at ServiceA.Start () (at Assets/Scripts/ServiceA.cs:25)
```

You've seen this. I've seen this. Every Unity developer who has shipped a product has seen this. The root cause is almost never "the reference is missing" — it's **timing**. ServiceA.Start ran before ServiceB.Start, and ServiceB wasn't ready.

> **NPC Senior Dev says:**
> *"I debugged a 'random' NullReferenceException for three days once. Turned out the order of scripts in the Inspector — which I had rearranged for 'organization' — changed which Awake ran first. Three. Days. Over drag-and-drop."*

---

## 2. DefaultExecutionOrder — Your Escape Hatch

Unity gives us one official mechanism to control execution order between different scripts: the `[DefaultExecutionOrder]` attribute.

```csharp
using UnityEngine;

[DefaultExecutionOrder(-2000)]
public class NPCSceneInitializationController : MonoBehaviour
{
    // This runs before any script at default (0) or positive order
}
```

### The Rules

| Rule | Detail |
|---|---|
| **Lower numbers run first** | `-2000` runs before `-400`, which runs before `0`, which runs before `100` |
| **Negative is allowed — and encouraged** | Your initialization code should run *before* everything else |
| **Range** | `-32768` to `32767` (signed 16-bit integer) |
| **Default** | Scripts without the attribute are treated as `0` |

### Our Project's Execution Order Values

| Value | Script | Purpose |
|---|---|---|
| `-2000` | `NPCSceneInitializationController` | Bootstrap everything |
| `-400` | `NPCDialogueUIController` | UI setup (early but after init) |
| `0` | `NPCDialogueManager`, all Services | Default — rely on self-init pattern |

> **NPC Senior Dev says:**
> *"You know what's worse than forgetting `[DefaultExecutionOrder]`? Arguing with your teammate about whether -100 or -50 is 'more correct.' Use dramatic gaps — -2000, not -3. You're not bidding on eBay."*

---

## 3. Our Execution Order Architecture

Here's the full execution pipeline our project uses. Every phase has a distinct responsibility and a specific order value so that by the time anything at default (`0`) runs, the scene is fully initialized.

```
Phase 0: NPCSceneInitializationController (-2000)
  │  Logger phase (telemetry bootstrap)
  │  Scene references
  │  Network transport config
  │  Dialogue services init
  │  Backend readiness check
  │  Network bridge init
  │  Validation + Spawning
  ▼
Phase 1: NPCDialogueUIController (-400)
  │  Resolve references
  │  Bind UI listeners
  │  Auto-select first NPC profile
  │  Enable input
  ▼
Phase 2: NPCDialogueManager (0)
  │  Resolve child services (GetComponentInChildren)
  │  Initialize async pipeline
  ▼
Phase 3: Services (0)
     NPCDialogueHistoryService
     NPCDialogueSessionService
     NPCDialogueRetrievalService
     PlayerDialogueContextService
```

### What Actually Happens Frame-by-Frame

```
Frame 0:
  ┌─────────────────────────────────────────────────┐
  │ Awake() calls for ALL active scripts,           │
  │ ordered by DefaultExecutionOrder:               │
  │                                                 │
  │   1. NPCSceneInitializationController (-2000)   │
  │   2. NPCDialogueUIController (-400)             │
  │   3. NPCDialogueManager (0)                     │
  │   4. NPCDialogueHistoryService (0)              │
  │   5. NPCDialogueSessionService (0)              │
  │   6. NPCDialogueRetrievalService (0)            │
  │   7. PlayerDialogueContextService (0)            │
  │                                                 │
  │ IMPORTANT: All Awakes run before any Start()!   │
  └─────────────────────────────────────────────────┘

Frame 1 (first active frame):
  ┌─────────────────────────────────────────────────┐
  │ Start() calls for ALL active scripts,           │
  │ in the SAME execution order:                    │
  │                                                 │
  │   1. NPCSceneInitializationController (-2000)   │
  │   2. NPCDialogueUIController (-400)             │
  │   3. NPCDialogueManager (0)                     │
  │   4. All Services (0)                           │
  └─────────────────────────────────────────────────┘
```

This means `NPCSceneInitializationController.Awake()` and `Start()` both run completely before `NPCDialogueManager.Awake()` and `Start()` — and crucially, before any service's `Start()`.

---

## 4. Why Negative Execution Order?

Why `-2000` instead of just `-1` or `-10`? Two reasons:

### 1. Most Unity Code Runs at Default (0)

When you install an asset store package, import a plugin, or write a quick MonoBehaviour, it almost always runs at `0`. By setting your initialization controller to `-2000`, you create a **comfortable buffer** between your startup code and everything else.

### 2. Room for Engine Internals

Unity's own internal systems can use very low execution orders. For example, Unity's `Update` methods for physics, audio, and rendering all hook in at or below `-30000`. Your `-2000` slot is aggressive but respectful — you're early without fighting the engine.

| Execution Order Bucket | Typical Users |
|---|---|
| `-32768` to `-30000` | Unity engine internals |
| `-2000` | Our initialization controller |
| `-1000` to `-100` | Third-party init systems, plugin bootstrap |
| `-400` | Our UI controller |
| `0` | Default — most scripts |
| `100` to `32767` | Late-running scripts, emergency overrides |

### The Safety Margin

Think of it this way: when `NPCSceneInitializationController` finishes its `Start()`, every script at `0` or higher hasn't even *started* yet. The scene is fully bootstrapped and ready. Any script at `0` that checks `FindObjectOfType<NPCSceneInitializationController>()` during its own `Awake()` will find a fully initialized object.

> **NPC Senior Dev says:**
> *"I once worked with a guy who set his execution order to -1 because 'it's just barely above default.' Then a new version of a plugin started using -5 and everything broke. That's why we use -2000. It's not a bidding war — it's a safety margin."*

---

## 5. The Service Self-Init Pattern

We just described that `NPCDialogueManager` and all four services run at default (`0`). Doesn't that mean they're racing each other? **Yes, if they depended on each other's Start().**

That's why we use the **Service Self-Init Pattern**:

### The Rules

1. **Services find each other via `GetComponentInChildren`**, which works in `Awake()` because it resolves the hierarchy immediately — it doesn't depend on other scripts' lifecycle methods.

2. **No service calls another service's methods in its own `Awake()` or `Start()`.** You can cache a reference, but don't invoke anything.

3. **Lazy initialization:** Services initialize their internal state on first public use, not in the constructor or `Awake()`.

```csharp
public class NPCDialogueManager : MonoBehaviour
{
    private NPCDialogueHistoryService _historyService;

    void Awake()
    {
        // ✓ SAFE: GetComponentInChildren works immediately
        // It resolves the component reference from the hierarchy
        _historyService = GetComponentInChildren<NPCDialogueHistoryService>();
    }

    void Start()
    {
        // ✓ SAFE: We don't call _historyService.DoSomething() here
        // We just set up our own async pipeline
        InitializeAsyncPipeline();
    }

    public async Task<DialogueResponse> ProcessDialogue(string input)
    {
        // ✓ SAFE: First use triggers lazy init if needed
        // Both this service and _historyService are guaranteed to have
        // their Awake/Start completed by the time user input arrives
        return await _historyService.RecordAndRespond(input);
    }
}
```

### Why This Removes Order Dependency Entirely

Because `GetComponentInChildren` works in `Awake()` — it queries the Unity scene graph, not a managed service registry — the order of `Awake()` between `NPCDialogueManager` and `NPCDialogueHistoryService` doesn't matter. Both will have valid references to each other after their `Awake()` methods complete.

And because neither calls the other's methods until user input triggers action (which happens many frames later, long after all `Start()` methods have completed), there's zero race condition.

---

## 6. What Goes Wrong (Real Examples)

These are bugs we've actually encountered in this project. Each one traces back to execution order assumptions.

### 6.1 Null References in History Service

```
NullReferenceException: Object reference not set to an instance of an object
  at NPCDialogueManager.Start () (at .../NPCDialogueManager.cs:42)
```

**Root cause:** `NPCDialogueManager` tried to call `_historyService.Initialize()` in `Start()`, but `NPCDialogueHistoryService` — at the same execution order — hadn't had its own `Start()` called yet. The reference wasn't null; the service's internal dictionaries were.

**Fix:** Remove the `Initialize()` call from `Start()`. Let the service lazy-init on first use.

### 6.2 Network Transport Not Configured

```
NullReferenceException: NetworkTransport
  at NPCNetworkBootstrap.Awake () (at .../NPCNetworkBootstrap.cs:22)
```

**Root cause:** `NPCNetworkBootstrap` ran before `NPCSceneInitializationController` because the bootstrap didn't have a `[DefaultExecutionOrder]` attribute. The transport configuration wasn't applied yet.

**Fix:** `NPCSceneInitializationController` now runs at `-2000` and configures the transport during its Phase 0. `NPCNetworkBootstrap.Awake()` applies transport config implicitly through the initialization controller's setup.

### 6.3 UI Tried to Show Dialogue Before Profiles Loaded

```
Trying to display dialogue for NPC 'GuildMaster' but profile list is empty.
```

**Root cause:** `NPCDialogueUIController.Start()` tried to auto-select the first NPC profile, but `NPCDialogueManager` hadn't loaded profiles yet because both run at default `0` and the UI ran first.

**Fix:** `NPCDialogueUIController` now runs at `-400` — before the DialogueManager — but instead of calling `DialogueManager.GetProfiles()` in `Start()`, it only binds listeners there. The auto-select happens when `OnEnable()` fires after profiles are loaded, or when the user triggers it.

> **NPC Senior Dev says:**
> *"Every one of these bugs follows the same pattern: 'But it worked in the Editor!' Yeah, because the Editor's domain reload happened to order your Start() calls the way you expected. That's not a guarantee — that's a coincidence wearing a trench coat."*

---

## 7. Best Practices Checklist

| # | Practice | Why |
|---|---|---|
| 1 | Use `[DefaultExecutionOrder(-2000)]` for initialization controllers | Ensures bootstrap runs before everything else |
| 2 | Use `GetComponentInChildren` for service discovery (not `FindObjectOfType`) | Works in `Awake()`, doesn't need other scripts to have started |
| 3 | Never call another service's methods in `Awake()` | The target service may not be initialized yet |
| 4 | Initialize async in `Start()` if you need to wait for services | `Start()` runs after all `Awake()` calls, so references exist |
| 5 | Check for null before accessing cross-service references | Defensive coding catches ordering mistakes early |
| 6 | Keep dramatic gaps between execution order values | `-2000` and `-400` leave room for third-party plugins |
| 7 | Document execution order in your project's onboarding docs | Because six months from now, *you* will be the onboarding |

```csharp
// ✅ RIGHT — defensive, self-initing
void Awake()
{
    _serviceB = GetComponentInChildren<ServiceB>();
}

void Start()
{
    // Don't call ServiceB methods here — just set up self
    _myOwnStuff = new List<string>();
}

public void DoWork()
{
    // First actual use — service is guaranteed ready by now
    if (_serviceB != null)
        _serviceB.SomeMethod();
}
```

```csharp
// ❌ WRONG — assumes ordering
void Awake()
{
    _serviceB = FindObjectOfType<ServiceB>();
    _serviceB.Initialize(); // BAD! ServiceB.Awake may not have run yet
}
```

---

## 8. Code Example: NPCSceneInitializationController

Here's the full `NPCSceneInitializationController` showing the execution order attribute and phase enum:

```csharp
using UnityEngine;
using NPCSystem.Dialogue.Core;
using NPCSystem.Dialogue.Session;
using NPCSystem.Dialogue.Persistence;
using NPCSystem.Dialogue.RAG;
using NPCSystem.Initialization;
using NPCSystem.Network.Core;
using NPCSystem.Network.Bridges;

namespace NPCSystem.Initialization
{
    /// <summary>
    /// Phase 0 initialization controller.
    /// Runs at -2000 to ensure the entire scene is bootstrapped
    /// before any default-order script executes.
    /// </summary>
    [DefaultExecutionOrder(-2000)]
    public class NPCSceneInitializationController : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private NPCNetworkBootstrap _networkBootstrap;
        [SerializeField] private NPCDialogueManager _dialogueManager;
        [SerializeField] private NPCBackendReadinessService _backendReadiness;
        [SerializeField] private NPCDialogueNetworkBridge _networkBridge;
        [SerializeField] private NPCDialogueSmokeValidator _smokeValidator;

        /// <summary>
        /// Ordered initialization phases.
        /// Each phase depends on the previous having completed.
        /// </summary>
        private enum InitPhase
        {
            NotStarted,
            LoggerBootstrap,        // Telemetry/flow logger setup
            SceneReferences,        // Resolve [SerializeField] targets
            NetworkTransportConfig, // Apply transport configuration
            DialogueServicesInit,   // Prime dialogue service pipelines
            BackendReadiness,       // Check LocalAI / backend health
            NetworkBridgeInit,      // Configure network bridge
            ValidationAndSpawning,  // Smoke test + object validation
            Complete                // All done
        }

        private InitPhase _currentPhase = InitPhase.NotStarted;

        private void Awake()
        {
            ExecutePhase(InitPhase.LoggerBootstrap);
        }

        private void Start()
        {
            // Run all remaining phases sequentially
            ExecutePhase(InitPhase.SceneReferences);
            ExecutePhase(InitPhase.NetworkTransportConfig);
            ExecutePhase(InitPhase.DialogueServicesInit);
            ExecutePhase(InitPhase.BackendReadiness);
            ExecutePhase(InitPhase.NetworkBridgeInit);
            ExecutePhase(InitPhase.ValidationAndSpawning);

            _currentPhase = InitPhase.Complete;

            Debug.Log(
                $"[{nameof(NPCSceneInitializationController)}] " +
                $"Scene initialization complete. " +
                $"Everything else will see a ready state."
            );
        }

        private void ExecutePhase(InitPhase phase)
        {
            _currentPhase = phase;
            Debug.Log(
                $"[{nameof(NPCSceneInitializationController)}] " +
                $"Executing phase: {phase}"
            );

            switch (phase)
            {
                case InitPhase.LoggerBootstrap:
                    // FlowLogger setup — must be first so all other
                    // phases can log through it
                    InitializeLogger();
                    break;

                case InitPhase.SceneReferences:
                    // [SerializeField] targets are resolved during
                    // Unity serialization, but we validate them here
                    ValidateSceneReferences();
                    break;

                case InitPhase.NetworkTransportConfig:
                    _networkBootstrap?.Awake();
                    break;

                case InitPhase.DialogueServicesInit:
                    _dialogueManager?.InitializeServices();
                    break;

                case InitPhase.BackendReadiness:
                    _backendReadiness?.CheckBackendHealth();
                    break;

                case InitPhase.NetworkBridgeInit:
                    _networkBridge?.Initialize();
                    break;

                case InitPhase.ValidationAndSpawning:
                    _smokeValidator?.Validate();
                    SpawnInitialNPCs();
                    break;
            }
        }

        private void InitializeLogger()
        {
            // Bootstrap telemetry — no dependencies
            Debug.Log("[NPCSceneInitializationController] Logger initialized");
        }

        private void ValidateSceneReferences()
        {
            // Crash fast if references are missing
            Debug.Assert(_networkBootstrap != null,
                "NPCNetworkBootstrap reference is missing!");
            Debug.Assert(_dialogueManager != null,
                "NPCDialogueManager reference is missing!");
        }

        private void SpawnInitialNPCs()
        {
            // Safe to call here — all services are primed
            // but none of their Start() has been invoked yet
            Debug.Log("[NPCSceneInitializationController] Spawning initial NPCs...");
        }

        /// <summary>
        /// Public query for other scripts to check init status.
        /// Safe to call from anywhere — pure read-only state check.
        /// </summary>
        public bool IsInitializationComplete =>
            _currentPhase == InitPhase.Complete;
    }
}
```

### Key Design Points

- **`[DefaultExecutionOrder(-2000)]`** — Ensures this is the first script to run `Awake()` and `Start()`.
- **Phase enum** — Makes initialization ordering explicit and debuggable. You can see exactly which phase failed in a log.
- **Debug.Assert** — Fails fast with a clear message if scene references are missing, rather than letting a cryptic null reference surface later.
- **`IsInitializationComplete`** — Other scripts can query this to verify the scene is ready, rather than assuming based on timing.

---

## 9. Common Pitfalls

### 9.1 `[DefaultExecutionOrder]` Only Works on MonoBehaviour Scripts

This attribute only affects Unity's internal script ordering. It does **not** work on plain C# classes, structs, static utilities, or `ScriptableObject`. If your initialization logic lives in a non-MonoBehaviour class, you need a MonoBehaviour wrapper.

```csharp
// ❌ WON'T WORK — ScriptableObject doesn't use execution order
[DefaultExecutionOrder(-2000)]
public class NPCConfig : ScriptableObject { }

// ✅ DO THIS — wrap it in a MonoBehaviour
[DefaultExecutionOrder(-2000)]
public class NPCConfigBootstrap : MonoBehaviour
{
    [SerializeField] private NPCConfig _config;
    void Awake() => _config.Initialize();
}
```

### 9.2 Execution Order Across Assembly Definitions

`[DefaultExecutionOrder]` works globally — even across `.asmdef` boundaries. A script in `NPCSystem.Runtime` with order `-2000` still runs before a script in `NPCSystem.Monitoring` with order `0`.

This is good for us (our init controller affects the whole project), but be aware that a poorly-behaved package with a very low execution order can accidentally run before your init controller. Always check package documentation for `[DefaultExecutionOrder]` values they might set.

### 9.3 Negative Numbers vs. Positive — Which Side Are You On?

A common confusion: **negative runs first.** `-100` runs before `0` runs before `100`.

If you're unsure where a script falls, add a temporary log:

```csharp
void Awake()
{
    Debug.Log($"{name} Awake at frame {Time.frameCount}");
}
```

Then look at the Console output order. That's your ground truth.

> **NPC Senior Dev says:**
> *"When in doubt, add a debug log. I don't mean 'when you're debugging.' I mean right now, in your Awake and Start methods, put a timestamp. When the night manager calls you at 3 AM asking why the NPC system exploded, you'll want to know which poor soul ran first — and that log entry will be your only witness."*

---

## 10. Summary

| Concept | Takeaway |
|---|---|
| Execution order is not guaranteed | Between different scripts, Unity can order Awake/Start any way |
| `[DefaultExecutionOrder]` | Lower numbers run first; negative is fine; range is `-32768` to `32767` |
| Our project uses | `-2000` (init), `-400` (UI), `0` (manager + services) |
| Service Self-Init Pattern | Use `GetComponentInChildren` in Awake, lazy-init on first use |
| Debug logging | The cheapest way to verify execution order at runtime |

**Bottom line:** Script execution order is the silent killer of many Unity projects. It's invisible, it's non-deterministic, and it only manifests under specific conditions that you'll never reproduce on your first try. But with explicit `[DefaultExecutionOrder]` values, a clear phase architecture, and the service self-init pattern, you can turn this time bomb into a predictable, debuggable pipeline.

Now go add `[DefaultExecutionOrder]` to your initialization controller. Your future self — debugging at 2 AM — will thank you.

---

> **NPC Senior Dev says:**
> *"You know what separates a senior Unity dev from a junior? The senior has been burned by execution order three times. The junior hasn't been burned yet. This chapter is your third burn in advance — you're welcome."*
