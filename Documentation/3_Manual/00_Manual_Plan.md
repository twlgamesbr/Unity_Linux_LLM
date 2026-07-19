# NPCSystem Manual — Plan & Architecture

> **Audience:** Developers with basic Unity knowledge but no prior NPC system,
> multiplayer networking, or backend integration experience.
>
> **Tone:** Tutorial-style, clear step-by-step instructions, with a running
> "NPC Senior Dev" character (sarcastic, funny, Unity project specialist)
> commenting in callout blocks to guide the reader.

---

## 1. What the Manual Covers

A WebGL multiplayer Unity game where a single Developer NPC answers questions
about the project's own codebase. The NPC uses LocalAI (LLM) + Qdrant (vector
DB) to retrieve relevant code context before answering. The scene loads in the
browser without memory crashes.

| Layer | Technology | Purpose |
|-------|-----------|---------|
| Game Engine | Unity 6 + Netcode for GameObjects | Scene, UI, multiplayer, WebGL build |
| LLM Backend | LocalAI (llama.cpp) | NPC dialogue generation |
| Vector DB | Qdrant | Codebase RAG retrieval |
| Auth | Supabase (GoTrue) | Player authentication |
| Telemetry | Datadog | Performance monitoring (FPS, memory, LLM latency) |
| Persistence | Supabase (PostgREST) | Dialogue history, NPC state |
| Container Runtime | Docker | LocalAI, Supabase, Qdrant |

---

## 2. Manual Structure (12 Chapters)

### Part I: Foundations — Why Your WebGL Game Crashes Without This

| # | Chapter | Key Concepts | Diagram |
|---|---------|-------------|---------|
| 1 | **Assembly Definitions & Why They Matter** | .asmdef isolation, domain reload, compile time, WebGL memory | Asmdef dependency graph |
| 2 | **Namespaces — Your Code's Address Book** | Namespace ≠ folder, why one namespace overloads the IL2CPP AOT compiler | Namespace scope map |
| 3 | **Script Execution Order — The Hidden Time Bomb** | Awake→Start→Update order, DefaultExecutionOrder, service initialization ordering | Execution order timeline |

### Part II: Designing the NPCSystem Universe

| # | Chapter | Key Concepts | Diagram |
|---|---------|-------------|---------|
| 4 | **Service Architecture** | Singleton vs DI, GetComponentInChildren pattern, service self-init | Component service diagram |
| 5 | **The NPCProfile — One NPC to Rule Them All** | ScriptableObject profiles, template variables, inventory items | Profile data flow |
| 6 | **Dialogue Pipeline** | User input → LLM → RAG → response → action tags → game state | Dialogue sequence diagram |

### Part III: WebGL Multiplayer Integration

| # | Chapter | Key Concepts | Diagram |
|---|---------|-------------|---------|
| 7 | **Networking for WebGL** | NetworkBehaviour, NetworkObject, server-authoritative state | Network topology |
| 8 | **The Network Bridge Pattern** | Client→Server→LLM→Client flow, RPCs, ownership | Bridge flow diagram |

### Part IV: Backend Services

| # | Chapter | Key Concepts | Diagram |
|---|---------|-------------|---------|
| 9 | **LocalAI — Your NPC Brain** | LLM serving, embeddings, health checks, port configuration | LocalAI topology |
| 10 | **Qdrant — Codebase Memory** | Collection schema, embedding model, search/filter, upsert | Qdrant collection map |
| 11 | **Supabase — Auth & Persistence** | GoTrue auth flow, PostgREST dialogue history | Auth/db flow |
| 12 | **Datadog — Watching Everything** | Metrics, traces, WebGL-safe emission, dashboard | Datadog pipeline |

### Part V: Scene Wiring & Build

| # | Chapter | Key Concepts | Diagram |
|---|---------|-------------|---------|
| 13 | **The Scene Blueprint** | GameObject hierarchy, component references, missing scripts | Scene hierarchy tree |
| 14 | **WebGL Build Checklist** | Addressables, IL2CPP stripping, memory budget, build settings | Build config table |

---

## 3. Key Concepts Deep-Dive

### 3.1 Why Namespace Isolation Prevents WebGL Crashes

Unity's IL2CPP AOT compiler generates native code for every type it can reach.
When all scripts share one namespace (or no namespace), the compiler cannot
tree-shake unused code. The result: a larger binary, more memory at runtime,
and increased risk of out-of-memory crashes in the browser's 2GB WASM limit.

**Correct approach:** Domain-separated namespaces (NPCSystem.Dialogue.Core,
NPCSystem.Network.Core, etc.) matched 1:1 with asmdefs. Each assembly compiles
independently. IL2CPP can strip entire assemblies that aren't referenced,
saving megabytes.

### 3.2 Script Execution Order — The NPC Senior Dev Rule

```
Phase 0: NPCSceneInitializationController (-2000)
  ↓ Logger → Bootstrap telemetry first
  ↓ Network transport config
  ↓ Dialogue services
  ↓ Backend readiness check (async, fails fast)
  ↓ Network bridge init
  ↓ Validation + spawning

Phase 1: NPCDialogueUIController (-400)
  ↓ Resolve references, bind UI, auto-select first profile

Phase 2: NPCDialogueManager (default 0)
  ↓ Resolve child services, start initialization
```

**Rule:** Initialization controllers run first (negative execution order).
Services resolve their own dependencies. No service should depend on another
service's `Start()` having already run.

### 3.3 Asmdef Rules

1. **One asmdef per major domain** — `NPCSystem.Runtime` (root), `NPCSystem.Monitoring`
2. **Never create circular references** — if A depends on B and B depends on A,
   move the shared types to a third asmdef or restructure the dependency
3. **Add explicit `EditorAttributes` reference** where EditorAttributes are used
   in runtime code (FoldoutGroup, Button, etc.)
4. **Monitoring asmdef must NOT reference Runtime** — telemetry emits, it
   doesn't import domain types

---

## 4. Diagrams to Generate

| Diagram | Type | File | Content |
|---------|------|------|---------|
| D1 | SVG Architecture | `Documentation/3_Manual/diagrams/01_arch_overview.html` | Full-system layer diagram (Unity → LocalAI → Qdrant → Supabase → Datadog) |
| D2 | SVG Asmdef | `Documentation/3_Manual/diagrams/02_asmdef_graph.html` | Assembly dependency graph showing NPCSystem.Runtime + NPCSystem.Monitoring |
| D3 | Excalidraw | `Documentation/3_Manual/diagrams/03_execution_order.excalidraw.json` | Script execution order timeline with phase annotations |
| D4 | Excalidraw | `Documentation/3_Manual/diagrams/04_dialogue_pipeline.excalidraw.json` | User → UI → Manager → LLM → RAG → Response → Action flow |
| D5 | SVG | `Documentation/3_Manual/diagrams/05_scene_hierarchy.html` | Scene GameObject tree with component labels |
| D6 | SVG | `Documentation/3_Manual/diagrams/06_network_flow.html` | Client ↔ Server ↔ LLM bridge flow |

---

## 5. Writing Style & Conventions

### NPC Senior Dev Callouts

> **🧑‍💻 Dev NPC:** "Oh, you put everything in one namespace? Bold move.
> Let me grab some popcorn while IL2CPP generates 40MB of dead code for your
> WebGL build. Seriously — namespaces are free. Use them."

### Code Blocks

```csharp
// Every code block has comments explaining WHY, not just WHAT
[DefaultExecutionOrder(-2000)] // Run before ANYTHING else
public sealed class NPCSceneInitializationController : MonoBehaviour
{
    // ...
}
```

### Step Numbers

```
Step 1: Create the asmdef
Step 2: Set the namespace
Step 3: Add references
Step 4: Compile
```

---

## 6. Execution Plan

| Step | What | Output | Est. Time |
|------|------|--------|-----------|
| 1 | **Approve plan** | This document reviewed → go/no-go | — |
| 2 | Generate 6 diagrams | `diagrams/` folder with SVG HTML + Excalidraw JSON | 1.5h |
| 3 | Write Ch 1-3: Foundations | `01_asmdefs.md`, `02_namespaces.md`, `03_execution_order.md` | 2h |
| 4 | Write Ch 4-6: Architecture | `04_services.md`, `05_profiles.md`, `06_dialogue.md` | 2h |
| 5 | Write Ch 7-8: Networking | `07_networking.md`, `08_bridge.md` | 1.5h |
| 6 | Write Ch 9-12: Backend | `09_localai.md` through `12_datadog.md` | 2h |
| 7 | Write Ch 13-14: Scene + Build | `13_scene.md`, `14_webgl_build.md` | 1h |
| 8 | Write `index.md` table of contents | Root manual navigation | 0.5h |
| 9 | Review + verify | Read through, fix gaps | 1h |
| | **Total** | | **~11.5h** |

---

## 7. File Tree (Output)

```
Documentation/3_Manual/
├── index.md                          # Table of contents + quick-start
├── 00_Manual_Plan.md                 # This plan
├── Part1_Foundations/
│   ├── 01_AssemblyDefinitions.md
│   ├── 02_Namespaces.md
│   └── 03_ScriptExecutionOrder.md
├── Part2_Architecture/
│   ├── 04_ServiceArchitecture.md
│   ├── 05_NPCProfile.md
│   └── 06_DialoguePipeline.md
├── Part3_Networking/
│   ├── 07_WebGLNetworking.md
│   └── 08_NetworkBridge.md
├── Part4_Backend/
│   ├── 09_LocalAI.md
│   ├── 10_Qdrant.md
│   ├── 11_Supabase.md
│   └── 12_Datadog.md
├── Part5_SceneAndBuild/
│   ├── 13_SceneBlueprint.md
│   └── 14_WebGLBuild.md
└── diagrams/
    ├── 01_arch_overview.html          # SVG architecture
    ├── 02_asmdef_graph.html           # SVG asmdef
    ├── 03_execution_order.excalidraw.json
    ├── 04_dialogue_pipeline.excalidraw.json
    ├── 05_scene_hierarchy.html        # SVG scene tree
    └── 06_network_flow.html           # SVG network flow
```

---

**Ready for your review.** If the structure, depth, and topics look right,
say "proceed" and I'll start with the diagrams and Part 1.
