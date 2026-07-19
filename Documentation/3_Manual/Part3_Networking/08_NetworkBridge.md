# Chapter 08: The Network Bridge Pattern

> **Duration:** 20-minute read
> **Audience:** Intermediate Unity developers familiar with Netcode for GameObjects (NGO) RPCs
> **Prerequisites:** Chapter 07 (WebGL Networking), familiarity with `NetworkBehaviour` and `NetworkObject`

---

> **🧑‍💻 Dev NPC:**
> *"A network bridge is the diplomatic translator of your game. The client speaks 'I clicked submit', the server speaks 'I need to validate this and call the LLM', and the LLM speaks HTTP — none of them understand each other. The bridge sits in the middle, translates every message into the right protocol, and makes sure nobody accidentally insults the server's session state. It's not glamorous work, but without it your client would be shouting JSON at a function that expects RPCs and wondering why nobody answers."*

---

## 1. What's a Network Bridge?

A **network bridge** in the NPC system is a `NetworkBehaviour` that sits between the client and server, routing dialogue messages through RPCs. It provides a single, unified API that the UI controller calls — regardless of whether the game is running in single-player (no network), as a dedicated server, or as a WebGL client connected to a remote host.

Without a bridge, every UI interaction would need a conditional check:

```csharp
// Without a bridge — scattered conditionals everywhere
void OnInputFieldSubmit(string text)
{
    if (NetworkManager.Singleton.IsClient && NetworkManager.Singleton.IsConnected)
        SubmitDialogueServerRpc(text);  // RPC path
    else
        dialogueManager.SendDialogueMessage(text);  // Direct path
}
```

With the bridge, the UI controller doesn't know (or care) about the transport layer. It calls one method:

```csharp
// With the bridge — the UI just talks to one object
void OnInputFieldSubmit(string text)
{
    NetworkBridge.SubmitPlayerMessage(text);
}
```

The bridge decides whether to fire a `ServerRpc`, call the local manager directly, or queue the request. The UI stays dumb. That's by design.

### 1.1 Where It Lives

The bridge lives in the `NPCSystem.Network.Bridges` namespace as a partial class split across two files:

| File | Path | Purpose |
|------|------|---------|
| Main class | `Assets/Scripts/Runtime/Network/Bridges/NPCDialogueNetworkBridge.cs` | RPC surface, lifecycle, request handling, queuing |
| Profile provider | `Assets/Scripts/Runtime/Network/Bridges/NPCDialogueNetworkBridge.ProfileProvider.cs` | NPC profile lookup, display name resolution |

It's attached to the `NPCDialogueSystem` GameObject in the active scene and requires a `NetworkObject` component for NGO spawning and RPC routing.

### 1.2 The Core Premise

```
┌─────────────┐     .SubmitPlayerMessage()     ┌──────────────────┐
│  UI Client  │ ──────────────────────────────► │ Network Bridge   │
│  (WebGL)    │                                 │ (NetworkBehaviour)│
│             │◄─────────────────────────────── │                  │
└─────────────┘     OnResponseStart/Complete     │  ┌────────────┐  │
                                                │  │ Dialogue   │  │
                                                │  │ Manager    │  │
                                                │  │ (Server)   │  │
                                                │  └─────┬──────┘  │
                                                │        │         │
                                                │        ▼         │
                                                │  ┌────────────┐  │
                                                │  │  LocalAI   │  │
                                                │  │  (LLM)     │  │
                                                │  └────────────┘  │
                                                └──────────────────┘
```

The bridge owns three responsibilities:
1. **Protocol translation** — Converts method calls into RPCs and vice versa
2. **Authority routing** — On the server, calls the dialogue manager directly; on the client, sends RPCs to the server
3. **State management** — Tracks active requests, pending queues, NPC selection, and client sessions

---

## 2. Client-Side Role

On the client (typically a WebGL build running in the browser), the bridge acts as the **outbound proxy** for all dialogue interactions. It receives calls from the UI controller and converts them into `ServerRpc` calls addressed to the server.

### 2.1 What the Client Can Do

| Method | RPC Target | Purpose |
|--------|-----------|---------|
| `SubmitPlayerMessage(string)` | `SubmitDialogueServerRpc` | Send player input to the LLM via the server |
| `RequestNpcSelectionAsync(string)` | `RequestNpcSelectionServerRpc` | Switch to a different NPC |
| `CancelActiveRequest()` | `CancelActiveRequestServerRpc` | Cancel the current in-progress request |

### 2.2 What the Client Cannot Do

The client never:
- Calls `NPCDialogueManager` methods directly (those run on the server)
- Sends HTTP requests to LocalAI or Qdrant
- Modifies session state
- Processes item trades
- Drives animation state

### 2.3 Client Event Subscriptions

The client listens for response events broadcast from the server. These are `UnityEvent` fields on the bridge that the `NPCDialogueUIController` binds to:

```csharp
// In NPCDialogueUIController.cs
void BindNetworkBridgeEvents()
{
    NetworkBridge.OnResponseStart.AddListener(HandleResponseStart);
    NetworkBridge.OnResponseUpdated.AddListener(HandleResponseUpdated);
    NetworkBridge.OnResponseComplete.AddListener(HandleResponseComplete);
    NetworkBridge.OnError.AddListener(HandleError);
    NetworkBridge.OnNpcChanged.AddListener(HandleNpcChanged);
}
```

---

> **🧑‍💻 Dev NPC:**
> *"Think of the client as a library patron. They can request a book (SubmitPlayerMessage), switch to a different section (RequestNpcSelection), or declare they're leaving (CancelActiveRequest). What they can't do is walk behind the counter, check the card catalog themselves, and decide which books go on which shelf. That's the librarian's job — and the librarian runs on the server."*

---

## 3. Server-Side Role

On the server (the authoritative Unity instance), the bridge receives incoming `ServerRpc` calls, validates them, forwards work to the `NPCDialogueManager`, and sends results back via `ClientRpc`.

### 3.1 Request Flow (Server Side)

```
Client → SubmitDialogueServerRpc(request)
  │
  ▼
HandleSubmitDialogueServerAsync(senderClientId, request)
  │
  ├─ Validate: session seeded? NPC selected? manager available?
  │   └─ On failure → SendErrorToClient()
  │
  ├─ Is another request active?
  │   └─ Yes → EnqueueDialogueRequest() → wait
  │
  └─ No → BeginDialogueRequestAsync(senderClientId, request)
        │
        ├─ SetActiveClient() → cache persistent RPC target
        ├─ SendResponseStartToClient() → "Thinking..."
        ├─ dialogueManager.SendDialogueMessage()
        ├─ Await completion
        ├─ SendResponseCompleteToClient() → full response
        └─ ClearActiveClient() → process next queued request
```

### 3.2 Server Authority Boundaries

| Concern | Server Decides |
|---------|---------------|
| **NPC selection** | Validates slug exists in profiles, seeds session, fires `SwitchToNPCAsync` |
| **Dialogue requests** | Validates NPC selected, queues if busy, routes to LLM |
| **Cancellation** | Only cancels if the sender matches the active client |
| **Session cleanup** | On `OnClientDisconnected`, clears queued work and session state |
| **Request ordering** | FIFO queue per client, one active request at a time |

### 3.3 Queue Management

The server manages a `Queue<PendingDialogueRequest>` to handle concurrent clients fairly. When a request arrives while another is in progress, it's queued and processed when the active request completes:

```csharp
readonly Queue<PendingDialogueRequest> _pendingRequests =
    new Queue<PendingDialogueRequest>();

// Inside HandleSubmitDialogueServerAsync:
if (_activeClientId.HasValue || _dialogueManager.IsResponding)
{
    EnqueueDialogueRequest(senderClientId, request);
    return;
}

await BeginDialogueRequestAsync(senderClientId, request);

// After completion:
ClearActiveClient();
TryProcessNextQueuedRequest();  // Dequeue and start the next one
```

This prevents a fast-typing client from starving others and ensures every player's message gets processed in order.

---

## 4. The Bridge API

Below is the complete public API surface of `NPCDialogueNetworkBridge`. This is everything the UI controller, initialization code, and tests interact with.

### 4.1 Public Properties

| Property | Type | Description |
|----------|------|-------------|
| `IsNetworkReady` | `bool` | Returns `true` when the bridge is fully spawned and connected. See §7. |
| `DialogueManager` | `NPCDialogueManager` | Public accessor (used by tests and scene initialization). |
| `Profiles` | `NPCProfile[]` | All available NPC profiles, proxied from the dialogue manager. |
| `CurrentProfile` | `NPCProfile` | The currently selected NPC profile (client-aware — resolves locally or from session state). |

### 4.2 Public Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `SubmitPlayerMessage(string)` | `void` | Send player text to the LLM. Routes to `ServerRpc` on client, direct manager call on server. |
| `SendDialogueMessage(string)` | `void` | Alias for `SubmitPlayerMessage`. Exists for API symmetry with `NPCDialogueManager`. |
| `RequestNpcSelectionAsync(string)` | `Task` | Request an NPC switch by slug. Server validates, seeds session, calls `SwitchToNPCAsync`. |
| `CancelActiveRequest()` | `void` | Cancel the current request. Only honored if the caller is the active client on the server. |
| `InitializeAsync()` | `Task` | Server-side initialization. Resolves references and initializes the dialogue manager if needed. |
| `FindProfileBySlug(string)` | `NPCProfile` | Look up an NPC profile by its slug from the manager's profile list. |

### 4.3 UnityEvents

These are the events the UI controller subscribes to. Each is invoked on the client when the corresponding `ClientRpc` is received.

| Event | Signature | When It Fires |
|-------|-----------|---------------|
| `OnNpcChanged` | `UnityEvent<string>` | NPC selection completed. Parameter: display name. |
| `OnResponseStart` | `UnityEvent<string>` | Server acknowledged the request and is processing. Parameter: request ID or "...". |
| `OnResponseUpdated` | `UnityEvent<string>` | Partial response content (for streaming). Parameter: content chunk. |
| `OnResponseComplete` | `UnityEvent<string, string>` | Full response received. Parameters: display name, content. |
| `OnError` | `UnityEvent<string>` | An error occurred. Parameter: error message. |

### 4.4 Internal Helpers (Public for Partial Access)

| Helper | Purpose |
|--------|---------|
| `CloneHistorySnapshot(Dictionary)` | Static utility for deep-cloning dialogue history between session and manager. |
| `LooksLikeFallbackPlayerName(string)` | Static check for auto-generated "Player 12345" names. |

---

> **🧑‍💻 Dev NPC:**
> *"That API table is your contract with the rest of the codebase. Every public method is a promise — call this, and the right thing happens regardless of whether we're on a server, a client, or the editor. UnityEvents are your notification channel. Methods are your commands. Keep the surface tight. A bridge with 47 public methods isn't a bridge — it's a spaghetti junction."*

---

## 5. How UI Talks to the Bridge

The bridge is designed to be a **drop-in replacement** for direct manager calls. The `NPCDialogueUIController` checks whether a network bridge is available and network-ready, then routes accordingly:

```csharp
// NPCDialogueUIController decides which path to use
bool ShouldUseNetworkBridge() => NetworkBridge != null && NetworkBridge.IsNetworkReady;

void OnInputFieldSubmit(string text)
{
    if (ShouldUseNetworkBridge())
        NetworkBridge.SubmitPlayerMessage(text);     // ➜ ServerRpc path
    else
        DialogueManager.SendDialogueMessage(text);    // ➜ Direct path (single-player / editor)
}
```

This conditional at the entry point is the **only** place where the UI controller distinguishes between networked and local modes. Every other interaction flows through the same abstraction:

```csharp
void OnNpcDropdownSelected(int index)
{
    string slug = npcDropdown.options[index].text;
    if (ShouldUseNetworkBridge())
        _ = NetworkBridge.RequestNpcSelectionAsync(slug);
    else
        _ = DialogueManager.SwitchToNPCAsync(slug);
}

void OnCancelButtonPressed()
{
    if (ShouldUseNetworkBridge())
        NetworkBridge.CancelActiveRequest();
    else
        DialogueManager.CancelRequests();
}
```

### 5.1 Why Not Abstract Further?

You could wrap both paths behind a single interface (`IDialogueService`), but the NPC system intentionally doesn't. The bridge is a `NetworkBehaviour` with a lifecycle tied to NGO spawning. An interface would hide the network readiness check and make it harder to debug which path is actually being used. The explicit `ShouldUseNetworkBridge()` check is visible, testable, and easy to trace in logs.

---

## 6. Partial Class Architecture

The bridge uses C# `partial` classes to separate concerns without introducing inheritance or composition wrappers.

### 6.1 The Two Files

```
Assets/Scripts/Runtime/Network/Bridges/
├── NPCDialogueNetworkBridge.cs          # ~845 lines — core flow
└── NPCDialogueNetworkBridge.ProfileProvider.cs  # ~126 lines — profile access
```

### 6.2 NPCDialogueNetworkBridge.cs (Main)

This file contains the core bridge functionality:

| Section | Lines | Purpose |
|---------|-------|---------|
| Fields & properties | 37–85 | Serialized fields, UnityEvents, internal state |
| Lifecycle | 88–124 | `Awake`, `OnNetworkSpawn`, `OnNetworkDespawn`, `OnDestroy`, `InitializeAsync` |
| Network readiness | 128–139 | `IsNetworkReady` property |
| Public API | 142–237 | `SubmitPlayerMessage`, `RequestNpcSelectionAsync`, `CancelActiveRequest` |
| Server RPCs | 320–463 | `RequestNpcSelectionServerRpc`, `SubmitDialogueServerRpc`, `CancelActiveRequestServerRpc` |
| Client RPC senders | 467–519 | `SendNpcChangedToClient`, `SendResponseStartToClient`, `SendErrorToClient` |
| Client RPC receivers | 522–565 | `ReceiveNpcChangedClientRpc`, `ReceiveResponseStartClientRpc`, etc. |
| Queue & session | 672–826 | `EnqueueDialogueRequest`, `BeginDialogueRequestAsync`, `ClearActiveClient` |

### 6.3 NPCDialogueNetworkBridge.ProfileProvider.cs (Profile Access)

This partial file handles all NPC profile-related operations:

- `Profiles` property — returns all profiles from the dialogue manager
- `CurrentProfile` property — client-aware profile resolution (checks local state vs. server state)
- `FindProfileBySlug(string)` — linear search through manager's profile list
- `CloneHistorySnapshot(Dictionary)` — deep clone for session/manager handoff
- `ResolvePlayerDisplayName(ulong)` — extracts display name from player's `NetworkObject`
- `LooksLikeFallbackPlayerName(string)` — checks if a name is an auto-generated fallback

### 6.4 Why Use Partial Classes?

Three concrete reasons:

1. **Separation of concerns without multiple inheritance** — Unity uses single-inheritance `MonoBehaviour`. You can't split logic across two base classes. Partial files let you group related methods by topic while keeping them in the same compiled class.

2. **File size management** — An 845-line file is manageable but dense. Moving profile logic (~126 lines) to a separate file makes the main flow easier to navigate. The header comment in each partial tells you what's inside.

3. **Merge-friendly diffs** — When multiple developers work on the same class, edits in different partial files don't conflict. One person can refactor the RPC flow while another adds profile features without touching the same file.

```csharp
// NPCDialogueNetworkBridge.cs — header annotation
/// <summary>
/// Network bridge for NPC dialogue over NGO (Netcode for GameObjects).
/// Owns the RPC surface for dialogue requests, responses,
/// and NPC profile management across the multiplayer session.
/// Implemented as partial classes: RequestHandling, ResponseHandling,
/// ProfileProvider, ItemTransfer.
/// </summary>
public partial class NPCDialogueNetworkBridge : NetworkBehaviour
```

---

> **🧑‍💻 Dev NPC:**
> *"Partial classes are Unity's way of saying 'I know you want multiple inheritance, but you can't have it, so here's a compromise.' Use them to group concerns, not to hide 3000 lines of spaghetti. If your partial files start having partial files, you've missed the point. One logical concern per partial file. Your future self — and whoever has to debug a merge conflict at 3 AM — will thank you."*

---

## 7. Network Readiness Check

The `IsNetworkReady` property is the gatekeeper for all network operations. It prevents the bridge from sending RPCs before the `NetworkObject` is fully spawned — a common source of silent failures in NGO.

### 7.1 The Property

```csharp
public bool IsNetworkReady =>
    Application.isPlaying
    && NetworkManager != null
    && NetworkManager.IsListening
    && IsSpawned
    && (IsClient || IsServer);
```

### 7.2 What Each Check Prevents

| Check | Prevents |
|-------|----------|
| `Application.isPlaying` | Editor edit-mode calls (scene view, OnValidate, etc.) |
| `NetworkManager != null` | `NullReferenceException` on `NetworkManager.Singleton` |
| `NetworkManager.IsListening` | Sending RPCs when the transport isn't bound (port conflict, not started) |
| `IsSpawned` | **Critical** — RPCs sent before `OnNetworkSpawn` are silently dropped by NGO |
| `IsClient \|\| IsServer` | Calls in disconnected states (shutdown, not yet connected) |

### 7.3 Why IsSpawned Matters Most

Unity Netcode for GameObjects does **not** queue RPCs sent before the `NetworkObject` is spawned. If you call a `ServerRpc` on line 10 of `Start()` but the object hasn't finished spawning yet, the RPC disappears into the void — no warning, no error, no callback.

This is especially dangerous in initialization code:

```csharp
// WRONG — RPC may be silently dropped
void Start()
{
    SubmitPlayerMessage("hello");  // IsSpawned is false → RPC lost
}

// RIGHT — check readiness
void Start()
{
    if (IsNetworkReady)
        SubmitPlayerMessage("hello");  // Safe
    else
        dialogueManager.SendDialogueMessage("hello");  // Fallback to direct
}
```

### 7.4 When to Check

- **UI controllers** (before calling any bridge method) — use `ShouldUseNetworkBridge()`
- **Scene initialization** (before calling `InitializeAsync()`) — check to decide whether to initialize the server path or skip network setup
- **Tests** — mock `IsNetworkReady` to test both paths

---

## 8. Code Example: Full RPC Flow with Annotations

Here's the complete flow of a player message, annotated from client input to server response.

```csharp
// ─────────────────────────────────────────────────────────────────
// STEP 1: Client calls public API  (NPCDialogueUIController)
// ─────────────────────────────────────────────────────────────────
void OnInputFieldSubmit(string text)
{
    if (ShouldUseNetworkBridge())
        NetworkBridge.SubmitPlayerMessage(text);
    //               ▲
    //               │ enters the bridge
}

// ─────────────────────────────────────────────────────────────────
// STEP 2: Bridge builds request and routes to ServerRpc
// ─────────────────────────────────────────────────────────────────
// NPCDialogueNetworkBridge.cs
public void SubmitPlayerMessage(string playerMessage)
{
    ResolveReferences();

    // Build the message struct
    var request = new NPCDialogueRequestMessage
    {
        requestId = Guid.NewGuid().ToString("N"),  // Unique per request
        npcSlug = CurrentProfile?.GetNpcSlug() ?? _localSelectedNpcSlug,
        playerMessage = playerMessage,
    };
    request.SanitizeInPlace();  // Trim whitespace, strip bad chars

    // Validate — don't send empty messages across the network
    if (string.IsNullOrWhiteSpace(request.playerMessage))
    {
        RaiseErrorLocal("Player message is required.");
        return;
    }

    // Non-network fallback (editor, server-only mode)
    if (!Application.isPlaying || NetworkManager == null
        || !NetworkManager.IsListening || IsServer)
    {
        _dialogueManager?.SendDialogueMessage(request.playerMessage);
        return;  // ← direct path, no RPC needed
    }

    // Network path: fire the ServerRpc
    SubmitDialogueServerRpc(request);  // ──► STEP 3
}

// ─────────────────────────────────────────────────────────────────
// STEP 3: ServerRpc arrives on the server instance
// ─────────────────────────────────────────────────────────────────
[Rpc(SendTo.Server)]
void SubmitDialogueServerRpc(
    NPCDialogueRequestMessage request,
    RpcParams rpcParams = default
)
{
    if (!IsServer) return;  // Safety check — NGO double-checks but be explicit

    // Fire-and-forget to avoid blocking the RPC transport thread
    FireAndForget(
        () => HandleSubmitDialogueServerAsync(
            request, rpcParams.Receive.SenderClientId),
        nameof(SubmitDialogueServerRpc)
    );
}

// ─────────────────────────────────────────────────────────────────
// STEP 4: Server validates, queues if busy, processes
// ─────────────────────────────────────────────────────────────────
async Task HandleSubmitDialogueServerAsync(
    NPCDialogueRequestMessage request, ulong senderClientId)
{
    ResolveReferences();
    request.SanitizeInPlace();

    // Guard: manager or session missing
    if (_sessionManager == null || _dialogueManager == null)
    {
        SendErrorToClient(senderClientId,
            "Dialogue network bridge is not configured.");
        return;
    }

    // Guard: must have selected an NPC first
    EnsureClientSessionSeeded(senderClientId);
    if (!_sessionManager.TryGetSelectedNpcSlug(
            senderClientId, out string selectedNpcSlug)
        || string.IsNullOrWhiteSpace(selectedNpcSlug))
    {
        SendErrorToClient(senderClientId, "No NPC selected.");
        return;
    }

    request.npcSlug = selectedNpcSlug;

    // Queue if another request is in progress
    if (_activeClientId.HasValue || _dialogueManager.IsResponding)
    {
        EnqueueDialogueRequest(senderClientId, request);
        return;  // ← queued, will be processed later
    }

    // All clear — begin processing
    await BeginDialogueRequestAsync(senderClientId, request);
    //               ▲
    //               │ enters STEP 5
}

// ─────────────────────────────────────────────────────────────────
// STEP 5: Server calls LLM, streams response back via ClientRpc
// ─────────────────────────────────────────────────────────────────
async Task BeginDialogueRequestAsync(
    ulong senderClientId, NPCDialogueRequestMessage request)
{
    // Mark this client as active (blocks other clients from cutting in)
    SetActiveClient(senderClientId, request.requestId);

    // Tell the client we're thinking
    SendResponseStartToClient(senderClientId, new NPCDialogueResponseMessage
    {
        requestId = request.requestId,
        npcSlug = request.npcSlug,
        content = "...",           // "typing..." indicator
    });
    //          │
    //          ▼  (ClientRpc fires, client's OnResponseStart fires)

    var tcs = new TaskCompletionSource<bool>();
    string responseContent = string.Empty;
    string errorContent = string.Empty;

    // Subscribe to dialogue manager events to await completion
    UnityAction<string> onStart = _ => { };
    UnityAction<string, string> onComplete = (reqId, response) =>
    {
        if (reqId != request.requestId) return;
        responseContent = response;
        // Unsubscribe and signal completion
        _dialogueManager.OnResponseComplete.RemoveListener(onComplete);
        _dialogueManager.OnError.RemoveListener(onError);
        tcs.TrySetResult(true);
    };
    UnityAction<string> onError = (error) =>
    {
        errorContent = error;
        _dialogueManager.OnResponseComplete.RemoveListener(onComplete);
        _dialogueManager.OnError.RemoveListener(onError);
        tcs.TrySetResult(false);
    };

    _dialogueManager.OnResponseComplete.AddListener(onComplete);
    _dialogueManager.OnError.AddListener(onError);

    // Submit to dialogue manager → LocalAI → LLM response
    _dialogueManager.SendDialogueMessage(request.playerMessage);

    // Wait for LLM to finish
    await tcs.Task;
    //           │
    //           ▼  LLM returned (or errored)

    // Send result back to client
    if (!string.IsNullOrEmpty(errorContent))
        SendErrorToClient(senderClientId, errorContent);
    else
        SendResponseCompleteToClient(senderClientId,
            new NPCDialogueResponseMessage
            {
                requestId = request.requestId,
                npcSlug = request.npcSlug,
                content = responseContent,
            });
    //          │
    //          ▼  (ClientRpc fires, client's OnResponseComplete fires)

    // Clean up and process next in queue
    ClearActiveClient();
    TryProcessNextQueuedRequest();
}

// ─────────────────────────────────────────────────────────────────
// STEP 6: ClientRpc arrives on the client, invokes UnityEvent
// ─────────────────────────────────────────────────────────────────
// NPCDialogueNetworkBridge.cs (Client RPC receivers)
[Rpc(SendTo.SpecifiedInParams)]
void ReceiveResponseStartClientRpc(
    NPCDialogueResponseMessage payload, RpcParams rpcParams = default)
{
    OnResponseStart?.Invoke(payload.content);  // UI shows "Thinking..."
}

[Rpc(SendTo.SpecifiedInParams)]
void ReceiveResponseCompleteClientRpc(
    NPCDialogueResponseMessage payload, RpcParams rpcParams = default)
{
    _localSelectedNpcSlug = payload.npcSlug;
    OnResponseComplete?.Invoke(payload.displayName, payload.content);
    //              │
    //              ▼  UI renders the LLM response in the dialogue panel
}
```

### 8.1 Flow Summary

```
Client (WebGL)              Server (Host/Dedicated)
─────────────────          ─────────────────────────
OnInputFieldSubmit()
  │
  └► SubmitPlayerMessage()
       │
       └► SubmitDialogueServerRpc() ──► HandleSubmitDialogueServerAsync()
                                              │
                                              ├─ Validate
                                              ├─ Queue if busy
                                              └─ BeginDialogueRequestAsync()
                                                   │
                                                   ├─ SetActiveClient()
                                                   ├─ SendResponseStartToClient()
      ◄── OnResponseStart ───── ReceiveResponseStartClientRpc() ◄──┘
                                                   │
                                                   ├─ dialogueManager.SendDialogueMessage()
                                                   │     └► LocalAI → LLM response
                                                   │
      ◄── OnResponseComplete ── ReceiveResponseCompleteClientRpc() ◄──┘
                                                   │
                                                   ├─ ClearActiveClient()
                                                   └─ TryProcessNextQueuedRequest()
```

---

## Chapter Summary

| Concept | Key Takeaway |
|---------|-------------|
| **Bridge purpose** | A `NetworkBehaviour` that translates method calls ↔ RPCs, hiding transport complexity from the UI |
| **Client role** | SubmitPlayerMessage, RequestNpcSelectionAsync, CancelActiveRequest — all via ServerRpc |
| **Server role** | Validate, queue, forward to LLM, stream results back via ClientRpc |
| **Partial classes** | Two files (`Main` + `ProfileProvider`) separate concerns without multiple inheritance |
| **IsNetworkReady** | Five-part predicate preventing RPCs before spawning — `IsSpawned` is the most critical |
| **Queue** | FIFO `PendingDialogueRequest` queue prevents concurrent request conflicts |
| **UI integration** | Single `ShouldUseNetworkBridge()` check at the entry point; everything else is abstracted |
| **UnityEvents** | `OnResponseStart`, `OnResponseUpdated`, `OnResponseComplete`, `OnError`, `OnNpcChanged` |

---

> **🧑‍💻 Dev NPC:**
> *"Bridges are the diplomatic translators of your game. The client speaks clicks, the server speaks authority, and the LLM speaks HTTP. Without a bridge, you're asking the UI to negotiate a peace treaty between three protocols that have never met each other. With a bridge, everyone just talks to the translator and pretends the others don't exist. That's not laziness — that's clean architecture. The best bridges are the ones nobody notices."*

---

**Next up:** [Chapter 09 — LocalAI: Your NPC Brain](../Part4_Backend/09_LocalAI.md) — configuring, running, and connecting the LLM backend.
