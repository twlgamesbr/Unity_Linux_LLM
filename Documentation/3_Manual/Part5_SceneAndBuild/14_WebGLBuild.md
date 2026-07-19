# Part V — Scene Wiring & Build

# Chapter 14: WebGL Build Checklist

> **Duration:** 25-minute read
> **Audience:** Developers preparing to ship a WebGL build
> **Prerequisites:** Chapters 01 (Assembly Definitions), 07 (WebGL Networking), 09–12 (Backend Services)
> **Build Profile:** `Assets/Settings/Build Profiles/WebGL - Desktop - Development.asset`

---

> **🧑‍💻 Dev NPC:** "So you think you're ready to build for WebGL. Let me guess — you pressed the Build button, it compiled, you uploaded the files to your server, and... it crashed the browser tab. Welcome to WebGL. The browser is a harsh mistress. This chapter is your pre-flight checklist. Do not skip steps. Do not pass Go. Do not collect 200 MB of WASM."

---

## 1. The Challenge — WebGL Is Not Standalone

Building for WebGL is fundamentally different from building for Windows, Mac, or Linux:

| Concern | Standalone | WebGL |
|---------|-----------|-------|
| Runtime | Native .NET IL (JIT or AOT) | WASM (AOT only via IL2CPP) |
| Memory | System RAM (often 16 GB+) | Browser heap (hard cap ~2 GB) |
| Networking | TCP/UDP sockets | WebSocket / WebRTC only |
| File I/O | Full filesystem | Virtual filesystem (IndexedDB) |
| Threading | Full multi-threading | Single-threaded (no `Thread` class) |
| Assembly loading | Runtime reflection | Everything linked at build time |
| Debugging | IDE debugger | Browser DevTools + source maps |

Every item in this chapter exists because one of these differences bit us during development.

---

## 2. Build Settings Profile

The project uses **Unity 6 Build Profiles** — `.asset` files that store per-target settings. The WebGL profile is:

**Profile:** `Assets/Settings/Build Profiles/WebGL - Desktop - Development.asset`

| Setting | Value | Why |
|---------|-------|-----|
| **Scripting Backend** | IL2CPP | Required for WebGL — browsers don't run managed .NET |
| **Api Compatibility Level** | .NET Standard 2.1 | Balance between API surface and AOT compatibility |
| **Managed Stripping** | High | Removes unused managed code before IL2CPP conversion |
| **IL2CPP Code Generation** | Optimize Size | Favors binary size over raw throughput |
| **WASM Code Optimization** | DiskSizeLTO | Link-time optimization for smallest WASM binary |
| **Exception Support** | None | No C++ exception tables — safe for release builds |
| **Brotli Compression** | On | Compresses `.wasm`, `.data`, `.framework.js` (~12 MB total) |
| **Data Caching** | On | IndexedDB caching for fast reload |
| **Debug Symbols** | Off | Strips DWARF info from WASM |
| **Maximum Memory Size** | 4096 MB | Allows heap growth to 4 GB (browser max) |
| **WebGL 2.0** | On | Required for modern rendering |

### 2.1 Why .NET Standard 2.1 and Not 4.x?

The `apiCompatibilityLevel` is set to **6** (`.NET Standard 2.1`) in `ProjectSettings.asset`:

```yaml
apiCompatibilityLevelPerPlatform:
  WebGL: 6
apiCompatibilityLevel: 6
```

`.NET Standard 2.1` gives you `Span<T>`, `ValueTask`, `async IAsyncEnumerable<T>`, and `System.Text.Json` — all of which the dialogue system uses. `.NET 4.x` includes APIs that IL2CPP can't tree-shake well on WebGL, producing a larger binary.

### 2.2 Why IL2CPP?

WebGL **requires** IL2CPP — there is no Mono runtime for WebAssembly. IL2CPP:

1. Converts .NET IL to C++
2. Compiles C++ to WASM via Emscripten
3. Links everything into a single `.wasm` binary

The downside: IL2CPP is an AOT compiler. It can't load types at runtime (`Assembly.Load`, reflection emit). Everything must be known at build time. This is why assembly definitions (Chapter 1) are critical — they give IL2CPP enough information to strip unused code.

> **🧑‍💻 Dev NPC:** "IL2CPP is like building a ship in a bottle. You have to plan ahead because you can't add parts once the bottle is sealed. But the result is a ship that floats in a browser — which is honestly kind of amazing when you think about it."

---

## 3. Addressables — Build Addressables = 0

The project uses Unity Addressables for dynamic asset loading. **Critical setting:**

```yaml
m_BuildAddressablesWithPlayerBuild = 0
```

This flag in the Addressables settings controls whether Addressables content is rebuilt with every player build. For **iteration builds** (development), set it to `0`:

- **0**: Don't rebuild Addressables on player build. Faster iteration because you build content once and rebuild only scripts/scenes.
- **1**: Rebuild everything every time. Only use for production releases.

### Workflow for iteration:

```
1. Build Addressables manually:  Window → Asset Management → Addressables → Build
2. Build Player:                 File → Build Settings → Build (skips Addressables rebuild)
3. Change an asset?              Rebuild Addressables (step 1), skip player rebuild
4. Change a script?              Rebuild player (step 2), skip Addressables rebuild
```

For production, set it to `1` once and let it build everything together.

---

## 4. Server Build — Builds/Server/

The project uses a **Unity Dedicated Server** for multiplayer. The server is a separate build:

### Build Steps

1. **Open Build Settings** → `File → Build Settings`
2. **Select Server subtarget** (StandaloneLinux64 → Server)
3. **Set output** to `Builds/Server/` (project root)
4. **Build**

The server binary is an IL2CPP Linux executable that runs in batch mode with CLI arguments:

```bash
./LinuxDedicatedServer.x86_64 \
  -batchmode \
  -nographics \
  -npc-server \
  -port 11474 \
  -address 0.0.0.0 \
  -npc-websockets
```

| Flag | Purpose |
|------|---------|
| `-batchmode` | Run without a window (headless) |
| `-nographics` | Disable GPU initialization (not needed for a headless server) |
| `-npc-server` | Custom flag: tells the game code to start in server mode |
| `-port 11474` | UDP/TCP port for Unity Transport |
| `-address 0.0.0.0` | Listen on all interfaces |
| `-npc-websockets` | Enable WebSocket transport (required for WebGL clients) |

### Server Build Output Structure

```
Builds/Server/
├── LinuxDedicatedServer.x86_64       ← Main executable
├── UnityPlayer.so                     ← Unity engine
├── GameAssembly.so                    ← IL2CPP-compiled game code
├── LinuxDedicatedServer_Data/        ← Data directory
│   ├── Resources/
│   ├── StreamingAssets/
│   ├── level0 / level1 / ...
│   └── ...
```

### Docker Deployment

The server containerizes using a two-stage Docker build:

```bash
# 1. Build the server in Unity → Builds/Server/

# 2. Build the Docker image
docker build -t npc-dedicated-server \
  -f Backend/unity-dedicated-server/Dockerfile .
```

**Dockerfile** (`Backend/unity-dedicated-server/Dockerfile`):

```dockerfile
FROM ubuntu:22.04 AS base
RUN apt-get update && apt-get install -y --no-install-recommends \
    libc6 libstdc++6 libgcc-s1 libgtk-3-0 ca-certificates
RUN groupadd -r npc && useradd -r -g npc -d /server -s /sbin/nologin npc

FROM base AS with-build
COPY Builds/Server/ /server/

FROM base AS runtime
COPY --chmod=755 Backend/unity-dedicated-server/entrypoint.sh /entrypoint.sh
COPY --from=with-build --chown=npc:npc /server /server
WORKDIR /server
EXPOSE 11474/udp
EXPOSE 11474/tcp
ENTRYPOINT ["/entrypoint.sh"]
```

**Development workflow** (bind-mount for fast iteration):

```yaml
# Backend/unity-dedicated-server/docker-compose.yml
services:
  npc-server:
    build:
      context: ../..
      dockerfile: Backend/unity-dedicated-server/Dockerfile
    container_name: npc-dedicated-server
    network_mode: host
    environment:
      - SERVER_PORT=11474
      - SERVER_ADDRESS=0.0.0.0
      - USE_WEBSOCKETS=true
    volumes:
      - ../../Builds/Server1:/server:rw
    restart: unless-stopped
```

> **🧑‍💻 Dev NPC:** "The bind-mount trick is a lifesaver. You rebuild the server in Unity (takes ~30 seconds), then just `docker compose restart npc-server`. No `docker build` needed. I do this about forty times per development session."

---

## 5. Memory Management — Asmdefs for Tree-Shaking

The single biggest factor in WebGL memory is **code stripping**. IL2CPP can only strip code it can *prove* is unused. With a monolithic `Assembly-CSharp`, IL2CPP conservatively keeps everything.

### The Asmdef Strategy

The project uses **3 assembly definitions**:

```
Assets/Scripts/Runtime/NPCSystem.Runtime.asmdef      ← Main runtime
Assets/Scripts/Runtime/Monitoring/Core/NPCSystem.Monitoring.asmdef  ← Monitoring
Assets/Scripts/Editor/NPCSystem.Editor.asmdef         ← Editor-only
```

**`NPCSystem.Runtime.asmdef`** references:

```json
{
    "references": [
        "Unity.InputSystem",
        "Unity.Collections",
        "Unity.Netcode.Runtime",
        "Unity.DedicatedServer.MultiplayerRoles",
        "Unity.TextMeshPro",
        "EditorAttributes",
        "NPCSystem.Monitoring"
    ],
    "precompiledReferences": [
        "Newtonsoft.Json.dll",
        "Supabase.dll",
        "Supabase.Core.dll",
        "Supabase.Gotrue.dll",
        "Supabase.Postgrest.dll",
        ...
    ],
    "autoReferenced": true
}
```

**`NPCSystem.Monitoring.asmdef`** is intentionally *not* referenced by the runtime asmdef — it's an independent assembly that telemetry emits from without creating a dependency cycle.

### What IL2CPP Tree-Shakes

| Scenario | WASM Size | Browser Memory | Notes |
|----------|-----------|---------------|-------|
| Monolithic Assembly-CSharp | ~68 MB | ~1.8 GB | Everything compiled, nothing stripped |
| Asmdef-isolated (this project) | ~34 MB | ~850 MB | Monitoring code stripped from runtime |
| Asmdef + High Stripping | ~28 MB | ~780 MB | Unused methods removed, but careful with reflection |

The Supabase DLLs are the biggest contributors to the remaining size — each DLL pulls in JSON serialization, HTTP clients, and reactive streams infrastructure.

### Avoid Large Assemblies

Do NOT add these to your WebGL build unless absolutely necessary:

- Full `System.dll` (use `System.Net.Http` subset)
- `Microsoft.CodeAnalysis` (Editor only — use assembly definitions to exclude)
- `Mono.Cecil` (Editor only)
- Large third-party SDKs (check if they support IL2CPP stripping)

---

## 6. The 12-Item Pre-Deploy Checklist

This is the **production pre-deploy checklist**. Run these steps in order **every time** before building for WebGL deployment.

| # | Check | Why | How to Verify |
|---|-------|-----|---------------|
| 1 | **Delete `Library/` after changing `apiCompatibilityLevel`** | Unity caches compiled assemblies in `Library/`. Changing compatibility level without a clean Library will produce mismatched IL code that crashes at runtime. | Delete `Library/` via file manager, let Unity regenerate on next open. |
| 2 | **Clear `Temp/com.unity.addressables/` on SBP errors** | The Scriptable Build Pipeline (SBP) caches build artifacts. Stale cache produces cryptic "Build Failed" errors that aren't real script errors. | Delete the folder, rebuild Addressables. |
| 3 | **Verify all `NetworkObject` components have unique `NetworkPrefab` list** | Duplicate or missing prefabs in the `NetworkPrefabsList` cause silent spawn failures. Each prefab with a `NetworkObject` must appear exactly once. | Open `NetworkManager` → `NetworkPrefabs` → check for duplicates. |
| 4 | **Set `m_BuildAddressablesWithPlayerBuild = 0`** | Prevents Addressables from being rebuilt every player build during iteration. | Check `AddressableAssetSettings` → `Build Addressables on Player Build` = `False`. |
| 5 | **Check console for known warnings (CS0618, CS0108)** | These warnings are *expected* (see §7) but should not be new or unexpected. New warnings indicate a regression. | Open Console → filter by "Warning" → verify only known warnings. |
| 6 | **Run codebase-embedder check** | Validates codebase rules (`.codebaserules.yaml`) including serialization, anti-patterns, and naming conventions. | `uv run codebase-embedder check --root ../..` |
| 7 | **Verify port config in `NPCLocalAIConfig`** | Gameplay dialogue goes to port 8080 (direct, no proxy). Port 8090 is for the observability proxy (CLI tools only). | Check `NPCLocalAIConfig.cs`: `LocalAIDirectPort = 8080`, `LocalAIProxyPort = 8090`. |
| 8 | **Confirm WebSocket transport for WebGL** | WebGL cannot use raw TCP/UDP sockets. Unity Transport must be configured with the WebSocket transport. | Check `NPCNetworkBootstrap` → `TransportConfig` → WebSocket selected. |
| 9 | **Check Datadog agent is running** | Telemetry emissions fail silently if the agent is down, but you won't see metrics in the dashboard. | `systemctl status datadog-agent` or Docker ps for `dd-agent` container. |
| 10 | **Verify Supabase services are up** | Auth (GoTrue :8091) and PostgREST (:8092) must be running for login and history persistence. | Check Docker: `docker ps \| grep supabase` or hit the health endpoints. |
| 11 | **Run smoke test: enter Play Mode, type a message** | Full integration test: initialization, service discovery, LocalAI connectivity, dialogue flow. | Enter Play Mode → wait for init → type "hello" → verify NPC responds. |
| 12 | **Build and test in browser DevTools** | Final validation: build, host on your server, open in a browser, check Network tab for requests, Console for errors. | Build → serve → open DevTools (F12) → Network tab → Console tab → Memory tab. |

> **🧑‍💻 Dev NPC:** "Step 12 is the one that always humbles me. Everything works in the Editor. Everything works in Play Mode. Then you build for WebGL and the browser says 'Out of memory: wasm memory' and you realize you forgot step 1. Every. Single. Time."

---

## 7. Compilation Warning Reference Table

The following warnings are **known and expected**. They are not bugs — they're artifacts of Unity API evolution and our deliberate architectural choices.

| Warning ID | Message | Cause | Why It's Safe |
|------------|---------|-------|---------------|
| **CS0618** | `'FindObjectOfType' is obsolete: 'FindObjectOfType has been deprecated. Use FindAnyObjectByType instead.'` | Our code uses `FindAnyObjectByType` (the new API), but some Unity packages or third-party assets still use the old API. | The old API is deprecated but still functional in Unity 6. Will be removed in Unity 7+ — track as tech debt. |
| **CS0108** | `'NPCDialogueManager.SendMessage' hides inherited member 'Component.SendMessage'. Use the new keyword if hiding was intended.` | `NPCDialogueManager` has a public `SendDialogueMessage()` method (note: `Dialogue` in the name). This is **not** hiding — the warning is a false positive from a previous iteration where a method was named `SendMessage`. | The method is actually `SendDialogueMessage`, not `SendMessage`. The warning may appear if an older serialized reference still points to a `SendMessage` method signature. Verify by checking the actual method name in `NPCDialogueManager.cs`. |
| **CS0649** | `Field '...' is never assigned to, and will always have its default value null` | Serialized private fields with `[SerializeField]` but no public assignment. | These fields ARE assigned — via the Unity Inspector serialization system. The compiler doesn't know about Unity serialization. Ignore. |
| **CS0067** | `The event '...' is never used` | UnityEvents that are declared but only used through the Inspector event bindings. | The events are wired in the Inspector (UI event callbacks). The compiler sees no direct C# subscribers. Ignore. |

### How to Suppress (Not Recommended)

```csharp
#pragma warning disable CS0618 // We use the old API for compatibility
// ... code ...
#pragma warning restore CS0618
```

We choose **not** to suppress these warnings because they serve as useful markers for tech debt. When Unity 7 drops, the `FindObjectOfType` warnings will become errors — having them visible reminds us to update.

---

## 8. Docker Commands for Server Deployment

### Development Quick Start

```bash
# Build the dedicated server in Unity (File → Build Settings → Server → Build)
# Then run with bind-mount (no Docker rebuild needed):
docker compose -f Backend/unity-dedicated-server/docker-compose.yml up --build

# Restart after a rebuild (fastest iteration):
docker compose -f Backend/unity-dedicated-server/docker-compose.yml restart npc-server
```

### Production Deployment

```bash
# 1. Build the server (output to Builds/Server/)
# 2. Build the immutable Docker image:
docker build -t npc-dedicated-server:latest \
  -f Backend/unity-dedicated-server/Dockerfile .

# 3. Tag and push to registry:
docker tag npc-dedicated-server:latest \
  registry.example.com/npc-dedicated-server:$(git rev-parse --short HEAD)
docker push registry.example.com/npc-dedicated-server:$(git rev-parse --short HEAD)

# 4. Run on production host:
docker run -d --rm \
  --name npc-server \
  --network host \
  -e SERVER_PORT=11474 \
  -e SERVER_ADDRESS=0.0.0.0 \
  -e USE_WEBSOCKETS=true \
  -e DD_SERVICE=npc-server \
  -e DD_ENV=production \
  -e DD_AGENT_HOST=dd-agent \
  npc-dedicated-server:latest
```

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `SERVER_PORT` | `11474` | UDP/TCP port for Unity Transport |
| `SERVER_ADDRESS` | `0.0.0.0` | Listen address |
| `USE_WEBSOCKETS` | `false` | Enable WebSocket transport for WebGL clients |
| `DD_SERVICE` | `npc-server` | Datadog service name |
| `DD_ENV` | `production` | Datadog environment tag |
| `DD_SITE` | `us5.datadoghq.com` | Datadog site endpoint |
| `DD_AGENT_HOST` | `dd-agent` | Datadog agent hostname |
| `DD_TRACE_AGENT_PORT` | `8126` | Datadog trace agent port |

### Container Architecture

```
┌─────────────────────────────────────────────────┐
│ Host Machine                                     │
│  ┌──────────────┐  ┌──────────────────────────┐ │
│  │  LocalAI      │  │  Docker Container        │ │
│  │  :8080        │  │  npc-dedicated-server    │ │
│  │  (LLM + Emb)  │──│  port 11474 (UDP/TCP)   │ │
│  └──────────────┘  │  host networking          │ │
│  ┌──────────────┐  │  localhost works for all  │ │
│  │  Qdrant       │  │  backend services        │ │
│  │  :6333        │  └──────────────────────────┘ │
│  └──────────────┘                                │
│  ┌──────────────┐                                │
│  │  Supabase     │                                │
│  │  :8091-8092   │                                │
│  └──────────────┘                                │
│  ┌──────────────┐                                │
│  │  dd-agent     │                                │
│  │  :8125/8126   │                                │
│  └──────────────┘                                │
└─────────────────────────────────────────────────┘
```

> **🧑‍💻 Dev NPC:** "If you're running this in production, make sure LocalAI has loaded the model *before* the Unity server starts. The readiness check in `NPCBackendReadinessService` will fail gracefully and keep retrying, but nobody likes seeing 'Backend not ready' in their logs on day one."

---

## 9. WebGL-Specific Runtime Considerations

### 9.1 Deferred Initialization

The scene uses **deferred initialization** for WebGL:

```csharp
bool ShouldDeferInitializationForWebGL()
{
    return Application.platform == RuntimePlatform.WebGLPlayer;
}
```

When true, `NPCSceneInitializationController` skips automatic initialization and logs:

```
Deferred automatic scene initialization for WebGL startup to avoid browser
bootstrap instability. Call InitializeSceneAsync after the page finishes
loading and the player is ready.
```

The `NPCDialogueManager` also defers:

```csharp
#if UNITY_WEBGL && !UNITY_EDITOR
    ResolveWebGlHost();
#endif
```

`ResolveWebGlHost()` reads the page URL via `Application.absoluteURL` and rewrites `RemoteHost` from `"localhost"` to the actual server host — because in the browser, `localhost` points to the user's machine, not the server.

### 9.2 WebSocket Transport

WebGL builds use **WebSocket transport** exclusively. Configure this in `NPCNetworkBootstrap.TransportConfig`:

| Setting | WebGL Value | Standalone Value |
|---------|-------------|------------------|
| Transport Type | WebSocket | Unity Transport (UDP) |
| Port | 11474 (TCP/WS) | 11474 (UDP) |
| Secure | WSS if HTTPS, WS if HTTP | N/A |

The entrypoint script handles this:

```bash
if [ "$USE_WEBSOCKETS" = "true" ]; then
    EXTRA_ARGS="-npc-websockets"
fi
```

### 9.3 Datadog Metrics in WebGL

Monitoring in WebGL is limited:
- `DatadogMetricsService` and `DatadogTracer` initialize only in Editor or non-WebGL builds
- WebGL uses the HTTP-based Datadog intake instead of agent-based DogStatsD UDP
- Structured flow logging still works (writes to the virtual filesystem)

```csharp
#if !UNITY_WEBGL || UNITY_EDITOR
    DatadogMetricsService.Initialize();
    DatadogTracer.Initialize();
#endif
```

---

## 10. Build Profile Comparison

| Build Profile | Platform | Subtarget | WASM Size | Use Case |
|--------------|----------|-----------|-----------|----------|
| `WebGL - Desktop - Development` | WebGL | Player | ~12 MB | Development iteration |
| `Linux` | StandaloneLinux64 | Player | N/A | Local testing with full debug |
| `Linux Server` | StandaloneLinux64 | Server | N/A | Dedicated server deployment |

CLI build command:

```bash
/path/to/Unity -quit -batchmode -nographics \
  -projectPath /path/to/project \
  -activeBuildProfile "Assets/Settings/Build Profiles/WebGL - Desktop - Development.asset" \
  -executeMethod NPCDialogueBuild.PerformWebGLBuild
```

---

## Cross-Reference Table

| Topic | See Also |
|-------|----------|
| Assembly Definitions (why asmdefs matter) | [Chapter 01](Part1_Foundations/01_AssemblyDefinitions.md) |
| WebGL Networking (transport details) | [Chapter 07](Part3_Networking/07_WebGLNetworking.md) |
| Network Bridge Pattern | [Chapter 08](Part3_Networking/08_NetworkBridge.md) |
| LocalAI Port Configuration | [Chapter 09](Part4_Backend/09_LocalAI.md) |
| Supabase Service Health | [Chapter 11](Part4_Backend/11_Supabase.md) |
| Datadog Monitoring | [Chapter 12](Part4_Backend/12_Datadog.md) |
| Scene Blueprint (scene hierarchy) | [Chapter 13](Part5_SceneAndBuild/13_SceneBlueprint.md) |
| Codebase Embedder (rule checking) | `AGENTS.md §5` or `Tools/CodebaseEmbedder/README.md` |

---

*Previous: [Chapter 13 — The Scene Blueprint](Part5_SceneAndBuild/13_SceneBlueprint.md)*  
*Manual Index: [Table of Contents](index.md)*
