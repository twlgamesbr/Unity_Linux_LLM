# Chapter 07: WebGL Multiplayer Networking

> **Duration:** 25-minute read
> **Audience:** Intermediate Unity developers experienced with Netcode for GameObjects (NGO)
> **Prerequisites:** Chapters 01–03 (Assembly Definitions, Namespaces, Script Execution Order), Chapter 04 (Service Architecture)

---

> **🧑‍💻 Dev NPC:**
> *"WebGL networking is like playing catch in a phone booth. You've got the same ball, the same gloves — but the booth is made of JavaScript, the windows don't open, and somebody walled off the raw socket closet. You can still throw the ball, but only through a specific straw called WebSocket, and everybody on the other end better be expecting it."*

---

## 1. WebGL Networking is Different

If you've built multiplayer Unity games for standalone platforms (Windows, Mac, Linux, consoles), you're used to raw socket access. The `System.Net.Sockets` namespace, UDP, TCP — it's all there. The operating system gives your process a port and lets you send bytes.

**WebGL doesn't run on your operating system.** It runs in a browser sandbox — a carefully restricted environment where JavaScript is the only language, and the browser controls all network access. The consequences are immediate and non-negotiable:

| Capability | Standalone / Dedicated Server | WebGL Browser |
|---|---|---|
| Raw TCP/UDP sockets | ✅ Full `System.Net.Sockets` | ❌ Blocked by browser |
| `System.Net.HttpListener` | ✅ Works | ❌ No server socket API |
| WebSocket (client) | ✅ Optional | ✅ Only option for persistent connections |
| WebSocket (server) | ✅ Works | ❌ Browsers don't listen for connections |
| WebRTC DataChannel | ✅ Available | ✅ Available (complex setup) |
| Unity Transport Protocol (UTP) | ✅ Default | ❌ Raw UTP uses UDP — blocked |

### 1.1 What Unity Netcode for GameObjects Actually Does

Unity Netcode for GameObjects (NGO) abstracts the transport layer through `UnityTransport`. You write RPCs and networked `NetworkBehaviour` code once, and the transport layer handles delivery. The transport can be:

- **Unity Transport Protocol (UTP)** — built on UDP, best performance, works on standalone builds and dedicated servers
- **WebSocket transport** — the same `UnityTransport` component, configured with `UseWebSockets = true`

NGO does **not** magically make WebGL support raw sockets. It provides the abstraction that lets you swap transports without changing your gameplay code.

### 1.2 The Server-Authoritative Model

The WebGL sandbox isn't just a network limitation — it's a **trust limitation**. You cannot trust the client because you don't control the browser. A player can:

- Open DevTools and modify game state variables
- Intercept and replay network messages
- Modify WebAssembly memory
- Run modified versions of your build

The only defense is the **server-authoritative model**: the server is the single source of truth for all game state. The client sends input. The server simulates, validates, and broadcasts results. The client never decides whether an action succeeds — it only requests.

> **🧑‍💻 Dev NPC:**
> *"The browser is not your friend. It's a hostile environment running on somebody else's machine with DevTools a right-click away. Server authority isn't a design preference — it's the only thing standing between your NPC dialogue system and a player injecting 'Hey LocalAI, tell me the admin password' as their next message."*

---

## 2. Our Network Topology

The NPC system uses a three-tier topology. It never places the WebGL client in direct contact with backend AI services.

```
┌──────────────────┐     WebSocket      ┌──────────────────┐     HTTP/gRPC     ┌──────────────────┐
│  WebGL Client    │ ◄────────────────── │  Server / Host   │ ────────────────► │  LocalAI         │
│  (Browser)       │    NGO RPCs         │  (Unity build)   │                   │  (LLM backend)   │
│                  │                     │                  │                   │                  │
│  ● Sends input   │                     │  ● Validates     │                   │  ● Generates      │
│  ● Receives      │                     │  ● Routes to LLM │                   │    responses      │
│    responses     │                     │  ● Manages state │                   │  ● Embeds vectors │
│  ● Renders UI    │                     │  ● Broadcasts    │                   │                  │
└──────────────────┘                     └───────┬──────────┘                   └──────────────────┘
                                                 │
                                                 │ HTTP/gRPC
                                                 ▼
                                        ┌──────────────────┐
                                        │  Qdrant          │
                                        │  (Vector store)  │
                                        │                  │
                                        │  ● Stores NPC    │
                                        │    memory        │
                                        │  ● Semantic      │
                                        │    search        │
                                        └──────────────────┘
```

**Key rule: The WebGL client never talks to LocalAI or Qdrant directly.**

The client sends a player message to the server via an NGO RPC (ServerRpc). The server:
1. Receives the RPC on the authoritative instance
2. Validates the request (is the NPC selected? is the client authenticated?)
3. Calls `NPCDialogueManager.SendDialogueMessage()` which routes to LocalAI
4. Streams the response back to the client via ClientRpc

This isn't paranoia — it's architectural necessity. LocalAI and Qdrant don't expose WebSocket endpoints. They're HTTP/gRPC services running on your backend infrastructure. A browser can't connect to them directly even if you wanted it to (CORS, mixed content, and firewall rules would all have to align — and you shouldn't).

---

## 3. NPCNetworkBootstrap — The Entry Point

All networking in the NPC system starts with `NPCNetworkBootstrap`. It lives on the `NPCNetworkSystem` GameObject in the scene and handles transport configuration, command-line parsing, and automatic startup.

### 3.1 Lifecycle

```
Awake()
├── ApplyCommandLineOverrides()   ← parse CLI args from command line
├── ApplyRuntimeSettings()        ← force run-in-background for servers
├── ResolveReferences()           ← find NetworkManager, UnityTransport
├── RegisterRuntimeCallbacks()    ← hook OnServerStarted, OnClientConnected, OnClientDisconnected
└── ApplyTransportConfiguration() ← configure UnityTransport with settings

Start()
├── Detect -npc-server in batch mode → set AutoStartMode = Server
└── Auto-start in configured mode if applicable
```

### 3.2 Awake() — ConfigureTransport()

```csharp
void Awake()
{
    if (TransportConfig.Port == 0)
    {
        TransportConfig = NPCTransportConfig.CreateDefault();
    }

    ApplyCommandLineOverrides();
    ApplyRuntimeSettings();
    ResolveReferences();
    RegisterRuntimeCallbacks();

    if (ConfigureOnAwake)
    {
        ApplyTransportConfiguration();
    }
}
```

The default `NPCTransportConfig` is created by `NPCTransportConfig.CreateDefault()`:

```csharp
public static NPCTransportConfig CreateDefault()
{
    return new NPCTransportConfig
    {
        ConnectAddress = "127.0.0.1",
        ListenAddress = "0.0.0.0",
        Port = 11474,
        UseWebSockets = false,
        WebSocketPath = "/npc-dialogue",
        AutoStartMode = NPCNetworkAutoStartMode.Manual,
    };
}
```

### 3.3 Start() — Server Mode Detection

In `Start()`, `NPCNetworkBootstrap` checks whether the process was launched with `-npc-server`. If so, and the application is in batch mode (no window, headless), it automatically starts in `Server` mode:

```csharp
void Start()
{
    if (Application.isBatchMode && HasCommandLineArg("-npc-server"))
    {
        TransportConfig.AutoStartMode = NPCNetworkAutoStartMode.Server;
    }

    if (
        (
            AutoStartInPlayMode
            || (
                Application.isBatchMode
                && TransportConfig.AutoStartMode != NPCNetworkAutoStartMode.Manual
            )
        ) && Application.isPlaying
    )
    {
        StartConfiguredMode();
    }
}
```

### 3.4 WebSocket Forcing for WebGL Builds

Inside `ApplyTransportConfiguration()` (in the `NPCNetworkBootstrap.TransportConfig.cs` partial), a preprocessor directive forces WebSocket mode when running in a WebGL build:

```csharp
#if UNITY_WEBGL && !UNITY_EDITOR
    TransportConfig.UseWebSockets = true;
#endif
```

This means:
- **In the Unity Editor** — you use the default UTP transport (UDP), even if you're testing a WebGL build target
- **In an actual WebGL build** — WebSocket mode is automatically forced, no manual flag needed
- **On a dedicated server** — the flag is set per your CLI args or config; WebSockets are optional

> **🧑‍💻 Dev NPC:**
> *"The `#if UNITY_WEBGL` check is your safety net. You don't want to remember to flip a checkbox every time you do a WebGL build — because you will forget, the build will fail to connect, and you'll spend an hour debugging before you notice the websocket toggle is still off. Let the preprocessor be your memory."*

---

## 4. CLI Arguments Reference

`NPCNetworkBootstrap.ApplyCommandLineOverrides()` parses the following arguments from the command line. Use these when launching a dedicated server or a test client from the terminal.

| Argument | Type | Description | Example |
|---|---|---|---|
| `-npc-server` | Flag | Start as a dedicated server (no local player, headless). Sets `AutoStartMode = Server`. | `./NPCServer.x86_64 -npc-server` |
| `-npc-host` | Flag | Start as a host (server + local player). Sets `AutoStartMode = Host`. | `./NPCServer.x86_64 -npc-host` |
| `-npc-client` | Flag | Start as a client connecting to a remote server. Sets `AutoStartMode = Client`. | `./NPCServer.x86_64 -npc-client` |
| `-npc-websockets` | Flag | Force WebSocket transport regardless of platform. Sets `UseWebSockets = true`. | `./NPCServer.x86_64 -npc-server -npc-websockets` |
| `-port` | `ushort` | Override the transport port (default: `11474`). Consumes the next argument as the value. | `./NPCServer.x86_64 -npc-server -port 11475` |
| `-address` | string | Override the connect address (IP or hostname). Consumes the next argument as the value. | `./NPCServer.x86_64 -npc-client -address 192.168.1.100` |

### 4.1 Parsing Behavior

- Arguments are **case-insensitive** (the parser lowercases them before comparison)
- Only parsed when `Application.isBatchMode` is true (standalone server) **or** when running in the Editor (for local multi-instance testing)
- `-port` and `-address` each consume the next argument as their value (`args[i + 1]`)
- Unknown arguments are silently ignored (passing `--help` won't print anything — NGO arguments like `-logfile`, `-screen-fullscreen` still work)

### 4.2 Typical Launch Commands

**Dedicated server (headless, WebSocket):**
```bash
./NPCServer.x86_64 -npc-server -npc-websockets -port 11474
```

**Headless client (for automated testing):**
```bash
./NPCServer.x86_64 -npc-client -address 10.0.0.50 -port 11474
```

**Local multi-instance testing in Editor:**
The `NPCPlayModeInstanceResolver` assigns unique client bind ports automatically based on player index when `AutoAssignClientBindPort` is enabled.

---

## 5. WebSocket Transport Configuration

### 5.1 Why WebGL Needs WebSockets

The browser's networking stack is simple: HTTP requests and WebSocket connections. There is no UDP. There is no raw TCP socket API. Unity Transport Protocol (UTP) uses UDP for its reliable/unreliable channel system, which means it **cannot work in WebGL** without a translation layer.

Unity Transport's WebSocket mode wraps NGO's reliable messaging inside WebSocket frames. The NGO RPC system works the same way — your `[Rpc(SendTo.Server)]` and `[Rpc(SendTo.Clients)]` attributes don't change. Only the underlying Protocol changes.

### 5.2 ApplyTransportConfiguration()

`ApplyTransportConfiguration()` (defined in `NPCNetworkBootstrap.TransportConfig.cs`) is the single method that writes transport settings to `UnityTransport`. It handles:

```csharp
public void ApplyTransportConfiguration()
{
    ResolveReferences();
    // ... null checks ...

    TransportConfig.NormalizeInPlace();

#if UNITY_WEBGL && !UNITY_EDITOR
    TransportConfig.UseWebSockets = true;
#endif

    if (!TransportConfig.TryValidate(out string errorMessage))
        return;

    UnityTransport.UseWebSockets = TransportConfig.UseWebSockets;

    UnityTransport.ConnectionAddressData connectionData = UnityTransport.ConnectionData;
    connectionData.Address = TransportConfig.ConnectAddress;
    connectionData.Port = TransportConfig.Port;
    connectionData.ServerListenAddress = TransportConfig.ListenAddress;
    connectionData.WebSocketPath = TransportConfig.WebSocketPath;
    connectionData.ClientBindPort = ResolveClientBindPort();
    UnityTransport.ConnectionData = connectionData;
}
```

### 5.3 WebSocket Path

The `WebSocketPath` property (default: `/npc-dialogue`) sets the URL path the WebSocket handshake uses. This matters if you're running behind a reverse proxy or load balancer that routes WebSocket connections by path. The default value is fine for direct connections.

### 5.4 When the Server Uses WebSockets

A dedicated server can accept WebSocket connections from WebGL clients while still using HTTP internally to reach LocalAI. The server's `UnityTransport` is configured with `UseWebSockets = true`, so it speaks WebSocket to the browser. But its HTTP calls to LocalAI (`NPCDialogueManager` → `NPCLocalAIConfig.LocalAIDirectPort :8080`) are standard HTTP — those aren't affected by the transport setting.

---

## 6. Server-Authoritative Rule

This is the most important networking rule in the NPC system:

> **All LLM calls happen on the server. The client only sends player input and receives responses.**

### 6.1 The ShouldUseNetworkBridge() Pattern

The `NPCDialogueNetworkBridge` class implements this pattern in every public method. Before routing a request, it checks whether it should use the network bridge (client side) or call the dialogue manager directly (server side):

```csharp
public void SubmitPlayerMessage(string playerMessage)
{
    ResolveReferences();

    var request = new NPCDialogueRequestMessage
    {
        requestId = Guid.NewGuid().ToString("N"),
        npcSlug = /* ... */,
        playerMessage = playerMessage,
    };
    request.SanitizeInPlace();

    // ── Server-side path: call manager directly ──
    if (
        !Application.isPlaying
        || NetworkManager == null
        || !NetworkManager.IsListening
        || IsServer          // ← Server calls manager directly
    )
    {
        _dialogueManager?.SendDialogueMessage(request.playerMessage);
        return;
    }

    // ── Client-side path: send ServerRpc ──
    SubmitDialogueServerRpc(request);
}
```

The guard condition `IsServer` is the key. On the server:
- The method calls `_dialogueManager?.SendDialogueMessage()` directly — no network round-trip
- The dialogue manager routes the message to LocalAI over HTTP
- The response is sent back to all clients via `ClientRpc`

On the client:
- The method calls `SubmitDialogueServerRpc()` — a `[Rpc(SendTo.Server)]`
- The server receives the RPC, validates it, and processes it
- Responses come back via `ClientRpc` methods like `ReceiveResponseStartClientRpc`, `ReceiveResponseCompleteClientRpc`

### 6.2 IsNetworkReady

The `NPCDialogueNetworkBridge` exposes `IsNetworkReady` for consumers that need to check whether the network bridge is functional:

```csharp
public bool IsNetworkReady =>
    Application.isPlaying
    && NetworkManager != null
    && NetworkManager.IsListening
    && IsSpawned
    && (IsClient || IsServer);
```

Use this in UI controllers and initialization code to decide whether to route through the bridge or fall back to direct manager calls (e.g., in local testing or single-player mode).

### 6.3 What the Server Controls

The server has exclusive authority over:

| Concern | Server | Client |
|---|---|---|
| LLM dialogue generation | ✅ Calls LocalAI via HTTP | ❌ Never sends HTTP to LocalAI |
| NPC state changes | ✅ Validates and applies | ❌ Sends requests only |
| Item trading | ✅ `ItemTradeService` processes trades | ❌ Receives results only |
| Animation state | ✅ Drives `Animator.Set*` calls | ❌ No Animator.Set calls |
| Session management | ✅ Seeds and tracks sessions | ❌ Sends selection only |
| Queue management | ✅ Orders pending requests | ❌ Cannot bypass queue |

### 6.4 Why Not Let the Client Call LocalAI?

Three reasons, from pragmatic to critical:

1. **Latency masking** — The server can queue, batch, and prioritize requests. If two clients send messages simultaneously, the server orders them fairly. Direct client-to-LLM calls would create a race condition with no arbiter.

2. **API key exposure** — If the WebGL client calls LocalAI directly, your API endpoint and any keys are visible in the browser's DevTools network tab. On a server, they stay on the server.

3. **Bandwidth control** — LLM responses can be large (hundreds of tokens, several KB). A server can compress, cache, or throttle responses. A browser just downloads whatever comes back.

---

## 7. Network Object Registration

Unity Netcode for GameObjects requires that any GameObject with `NetworkBehaviour` components be registered as a network prefab and have a `NetworkObject` component. Two objects in the NPC system carry this requirement:

### 7.1 NPCDialogueNetworkBridge (Dialogue Flow)

```csharp
[DefaultExecutionOrder(-900)]
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public partial class NPCDialogueNetworkBridge : NetworkBehaviour
```

This is the primary network bridge for all dialogue-related communication. It:
- Lives on the `NPCDialogueSystem` GameObject in the scene hierarchy
- Requires `NetworkObject` for NGO spawning and RPC routing
- Implements the server-authoritative pattern described in §6
- Manages the queuing system for concurrent dialogue requests
- Handles NPC selection, message submission, cancellation, and streaming responses

### 7.2 ItemTradeService (Item Trading)

```csharp
[RequireComponent(typeof(NetworkObject))]
public class ItemTradeService : NetworkBehaviour
```

Handles server-authoritative item trading triggered by `[give_item]` and `[trade_item]` tags in LLM responses. It:
- Processes trade actions only on the server
- Validates inventory state before applying changes
- Broadcasts trade results to all clients via ClientRpc

### 7.3 Registration in NPCNetworkBootstrap

Both objects are registered with the network manager through `NPCNetworkBootstrap.RegisterNetworkPrefabs()`:

```csharp
public void RegisterNetworkPrefabs()
{
    if (NetworkManager == null)
        return;

    TryRegisterNetworkPrefab(PlayerPrefab, "player");
    TryRegisterNetworkPrefab(ServerNpcPrefab, "serverNpc");
}
```

Prefabs are loaded from `Resources/` using the paths `Networking/NPCPlayerAvatar` and `Networking/NPCServerCharacter`. Each prefab must have a `NetworkObject` component — the registration code checks and logs an error if it's missing:

```csharp
if (!prefab.TryGetComponent<NetworkObject>(out _))
{
    // Error: prefab has no NetworkObject, skipping registration
    return;
}
```

---

## 8. Port Configuration

### 8.1 Default Port: 11474

The NPC system uses port **11474** for Unity Transport communication. This port was chosen because it's non-standard and avoids conflicts with:

| Service | Port | Notes |
|---|---|---|
| LocalAI | `:8080` | Dialogue LLM, embeddings |
| LocalAI Proxy | `:8090` | Observability (not in gameplay path) |
| Supabase Gotrue | `:8091` | Auth |
| Supabase PostgREST | `:8092` | REST API |
| Qdrant | `:6333` | Vector store |
| Cognee | `:8000` | Memory service (disabled in active scene) |

### 8.2 WebSocket Fallback

When WebSocket mode is enabled, the same port `11474` is used. Unity Transport's WebSocket implementation listens on `ws://<listenAddress>:11474/<webSocketPath>` (default path: `/npc-dialogue`).

No separate port is needed for WebSocket vs. UTP — the transport handles the protocol upgrade internally.

### 8.3 NPCNetworkUtils.IsLocalHost()

For local development, the `NPCNetworkUtils` class provides a simple check:

```csharp
public static class NPCNetworkUtils
{
    public static bool IsLocalHost(string host)
    {
        return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
               || string.Equals(host, "127.0.0.1", StringComparison.Ordinal);
    }
}
```

This is used by the WebGL URL-resolution logic in auth and dialogue components. When the server address is `localhost` or `127.0.0.1`, the system knows it's running a local development build and can adjust URL construction accordingly (e.g., rewriting WebSocket URLs for the browser's loopback limitations).

> **⚠️ Note:** Do not reintroduce raw `"localhost"` string comparisons. Use `NPCNetworkUtils.IsLocalHost()` instead — it centralizes the check and handles edge cases (case sensitivity, IP vs. hostname).

### 8.4 Multi-Instance Development

When running multiple instances locally (e.g., one server + two clients in the Editor), `NPCPlayModeInstanceResolver` assigns unique client bind ports to avoid port contention:

```csharp
ushort ResolveClientBindPort()
{
    // 1. Check command-line override
    // 2. Check AutoAssignClientBindPort setting
    // 3. Auto-assign based on player index
    return NPCPlayModeInstanceResolver.ResolveClientBindPortForPlayerIndex(
        playerIndex,
        TransportConfig.Port,
        ClientBindPortOverride
    );
}
```

This lets you run multiple Editor instances without manually configuring `-port` for each.

---

## 9. Common WebGL Networking Pitfalls

### 9.1 CORS Issues

When the WebGL client tries to connect, the browser first sends a WebSocket handshake (an HTTP upgrade request). If the server doesn't respond with the right CORS headers, the connection is rejected before it starts.

**Symptoms:**
- WebSocket connection fails with `(anonymous)` and no clear error in Unity
- Browser console shows `WebSocket connection to 'ws://...' failed:`
- The `NetworkManager` never reports `OnClientConnected`

**Solutions:**
- Unity Transport handles WebSocket CORS automatically when `UseWebSockets = true` — it sends the correct `Access-Control-Allow-Origin` headers
- If behind a reverse proxy (nginx, Caddy, Cloudflare), configure it to pass WebSocket upgrade headers through
- Test with `curl -H "Upgrade: websocket" -H "Connection: Upgrade" http://localhost:11474/npc-dialogue` to verify the handshake

### 9.2 WebSocket Connection Drops

WebSocket connections are more fragile than TCP. A brief network interruption that TCP would survive can kill a WebSocket.

**Common causes:**
- Browser tab goes to background → browser throttles WebSocket traffic
- Mobile device switches from Wi-Fi to cellular
- Load balancer or proxy has a short idle timeout
- Server process restarts (e.g., during hot reload)

**Symptoms:**
- `OnClientDisconnectCallback` fires with empty `DisconnectReason`
- Server sees the client as disconnected but the client's NGONetworkManager state is stale
- Dialogue requests hang (the queued request never gets processed)

**Solutions:**
- Set `ForceRunInBackground = true` on the bootstrap (default) to keep network updates running when the window is not focused
- Implement reconnection logic (see §9.3)
- Configure reverse proxy idle timeout to at least 120 seconds

### 9.3 Reconnection Logic

The NPC system does not include automatic reconnection in the current version (because Unity Netcode for GameObjects does not natively support it). However, the architecture makes it feasible:

1. **Detect disconnection** — The `OnClientDisconnected` callback fires on the server. The server should mark the client's session as idle but retain it for a timeout period.

2. **Client-side retry** — A reconnection manager (future work) would:
   - Detect `OnClientDisconnectCallback` on the client
   - Wait 1 second, then attempt `NetworkManager.StartClient()` again
   - Back off exponentially (1s, 2s, 4s, 8s, max 30s)
   - Surface a "Reconnecting..." UI state

3. **Session resumption** — The `NPCNetworkSessionManager` tracks per-client state by `clientId`. After reconnection, the server can restore the client's session (selected NPC, dialogue history context).

> **🧑‍💻 Dev NPC:**
> *"Reconnection in WebGL is like trying to resume a phone call after going through a tunnel. The connection drops, the other person keeps talking, and when you come back you have to ask 'wait, what did I miss?' Automatic reconnection is the polite way of asking — without making the player sit through an awkward silence."*

### 9.4 Port Conflicts with Other Services

Port `11474` is chosen to avoid conflicts, but conflicts can still happen if:
- Another process binds to `11474` before your server starts
- You run multiple server instances on the same machine without different `-port` values
- A firewall or security group blocks the port (common in cloud deployments)

**Diagnosis:**
```bash
# Check if port 11474 is already in use
sudo netstat -tlnp | grep 11474

# Or with ss (modern Linux)
ss -tlnp | grep 11474
```

**Prevention:**
- Use the scripted launch commands in your Docker Compose or deployment scripts to specify unique ports
- Firewall rules should open port `11474` (or whatever `-port` value you choose) for both TCP and WebSocket traffic
- LocalAI on `:8080` and Qdrant on `:6333` will not conflict — they use different ports

### 9.5 Build Configuration

WebGL builds need specific Player Settings for networking to work:

| Setting | Required Value |
|---|---|
| **Player Settings → Publishing Settings → Enable Exceptions** | Explicitly disable (or use full without stack trace for debugging) |
| **Player Settings → Resolution → Run In Background** | Checked (forced by `NPCNetworkBootstrap.ForceRunInBackground`) |
| **Player Settings → WebGL → WebGL Memory Size** | 256 MB minimum (512 MB recommended for LLM-heavy scenes) |
| **Player Settings → WebGL → WebSocket Support** | Checked (included by default in Unity 2022+) |

### 9.6 No Animator.Set Calls from Client

Networked animation state is driven exclusively by the server. The client never calls `Animator.SetBool`, `Animator.SetTrigger`, `Animator.SetFloat`, or any other `Animator.Set*` method.

**Why:**
- Animation state is game state. If the client could set animation parameters, it could display false animations (e.g., playing a "victory" animation when no victory occurred)
- Animation parameters desynchronize when clients join mid-game or reconnect
- The server is the single source of truth for character state

**How it works:**
- The server updates animation parameters based on validated game state
- NPC character animation is networked via `NPCOwnerNetworkTransform` for position/rotation
- Server-driven RPCs can trigger animation changes on clients
- Client-side animation prediction is intentionally avoided — it adds complexity and the NPC dialogue system doesn't need frame-perfect animation sync

---

> **🧑‍💻 Dev NPC:**
> *"WebGL networking is like playing catch in a phone booth. You can still throw the ball — but only through a straw, you can't throw it if the booth moves, and if you drop it you have to ask the other person to throw it back because you can't bend over. The constraints aren't arbitrary — they're the price of running in a sandbox that 3 billion people trust with their bank accounts."*
>
> *"Treat those constraints as design requirements, not bugs. They'll keep you honest about what belongs on the server, what belongs on the client, and what the LLM should never, ever be asked by a browser you don't control."*

---

### Chapter Summary

| Concept | Key Takeaway |
|---|---|
| WebGL transport | Only WebSockets work — UTP/UDP is blocked by browsers |
| Topology | Client ↔ Server ↔ LLM backends — no direct browser-to-LocalAI |
| Bootstrap | `NPCNetworkBootstrap` handles CLI args, transport config, auto-start |
| Default port | `11474` — non-standard to avoid conflicts with backend services |
| Server authority | All LLM calls on server — client only sends input, receives responses |
| Network objects | `NPCDialogueNetworkBridge` + `ItemTradeService` need `NetworkObject` |
| WebSocket forcing | `#if UNITY_WEBGL && !UNITY_EDITOR` auto-enables WebSockets in builds |
| IsLocalHost | Use `NPCNetworkUtils.IsLocalHost()` — no raw string comparisons |
| Animation | No `Animator.Set*` from client — server drives all networked animation |

---

**Next up:** [Chapter 08 — NetworkBridge](08_NetworkBridge.md) — deep dive into the RPC system, message models, and queuing logic.
