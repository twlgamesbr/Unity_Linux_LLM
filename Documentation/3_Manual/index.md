# NPCSystem Manual

> **Build a WebGL Multiplayer Unity Game with an AI-Powered NPC Dialogue System**
>
> From zero to a shipping scene: assembly definitions, namespaces, script execution order,
> service architecture, WebGL networking, LLM backends, vector databases, auth persistence,
> and production monitoring — all in one project.
>
> **Audience:** Unity developers who want to build a WebGL multiplayer game with NPC dialogue,
> but need the architectural foundation that stops it from crashing in the browser.
>
> **NPC Senior Dev:** Throughout this manual, a sarcastic, funny Senior Developer NPC
> (who also happens to be the NPC *inside* the game) will guide you with real talk,
> hard-won lessons, and the occasional bad joke.

---

## Quick Start

Want to jump straight in? Here's the minimum path:

```
1. Open Assets/Scenes/NPCDialoguePrototype1.unity
2. Enter Play Mode
3. Type a message in the input field
4. Watch the NPC respond with codebase-aware answers
```

> **🧑‍💻 Dev NPC:** "Wait — you didn't read the manual? Fine. But when your WebGL build
> crashes with an out-of-memory error in IL2CPP, don't come crying to me. Start with
> Chapter 1. I'll wait."

---

## Table of Contents

### Part I: Foundations — Why Your WebGL Game Crashes Without This

| # | Chapter | What You'll Learn |
|---|---------|-------------------|
| 01 | [Assembly Definitions & Why They Matter](Part1_Foundations/01_AssemblyDefinitions.md) | How `.asmdef` files isolate code, why they're mandatory for WebGL, and how they prevent memory bloat |
| 02 | [Namespaces — Your Code's Address Book](Part1_Foundations/02_Namespaces.md) | Why namespaces matter for IL2CPP tree-shaking, the 16-namespace map, and migration from flat structure |
| 03 | [Script Execution Order — The Hidden Time Bomb](Part1_Foundations/03_ScriptExecutionOrder.md) | How `[DefaultExecutionOrder]` prevents null refs, the 4-phase init pipeline, and the service self-init pattern |

**Key insight from this part:** One flat namespace + no asmdefs = IL2CPP compiles everything
into your WebGL build. Domain-separated namespaces + asmdefs = the linker strips unused
assemblies, saving megabytes that keep your game under the browser's 2GB WASM heap limit.

### Part II: Designing the NPCSystem Universe

| # | Chapter | What You'll Learn |
|---|---------|-------------------|
| 04 | [Service Architecture — The Component Discovery Pattern](Part2_Architecture/04_ServiceArchitecture.md) | Parent-child hierarchy, `GetComponentInChildren`, async initialization, and the complete service map |
| 05 | [The NPCProfile — One NPC to Rule Them All](Part2_Architecture/05_NPCProfile.md) | ScriptableObject profiles, template variables, inventory items, and creating the Developer NPC |
| 06 | [The Dialogue Pipeline — From Player Input to Game State Change](Part2_Architecture/06_DialoguePipeline.md) | Full flow: input → LLM → RAG → parse actions → state changes + Supabase persistence |

### Part III: WebGL Multiplayer Integration

| # | Chapter | What You'll Learn |
|---|---------|-------------------|
| 07 | [WebGL Multiplayer Networking](Part3_Networking/07_WebGLNetworking.md) | Server-authoritative model, WebSocket transport, CLI args, and why WebGL can't use raw sockets |
| 08 | [The Network Bridge Pattern](Part3_Networking/08_NetworkBridge.md) | NPCDialogueNetworkBridge as client↔server diplomat, RPC routing, partial classes |

### Part IV: Backend Services

| # | Chapter | What You'll Learn |
|---|---------|-------------------|
| 09 | [LocalAI — Your NPC Brain](Part4_Backend/09_LocalAI.md) | LLM inference server setup, model management, chat/embeddings APIs, port configuration |
| 10 | [Qdrant — Codebase Memory](Part4_Backend/10_Qdrant.md) | Vector database for RAG, codebase-embedder pipeline, collection schema |
| 11 | [Supabase — Auth & Persistence](Part4_Backend/11_Supabase.md) | GoTrue auth flow, PostgREST dialogue history, schema cache issues |
| 12 | [Datadog — Watching Everything](Part4_Backend/12_Datadog.md) | Metrics, traces, WebGL-safe emission, dashboard JSON, telemetry pipeline |

### Part V: Scene Wiring & Build

| # | Chapter | What You'll Learn |
|---|---------|-------------------|
| 13 | [The Scene Blueprint](Part5_SceneAndBuild/13_SceneBlueprint.md) | Complete GameObject hierarchy, component wiring, service discovery, scene setup from scratch |
| 14 | [WebGL Build Checklist](Part5_SceneAndBuild/14_WebGLBuild.md) | 12-item pre-deploy checklist, IL2CPP settings, Addressables, Docker deployment |

---

## Architecture Diagrams

| # | Diagram | View |
|---|---------|------|
| D1 | [Full System Architecture](diagrams/01_arch_overview.html) | Unity → LocalAI → Qdrant → Supabase → Datadog layer diagram |
| D2 | [Asmdef Dependency Graph](diagrams/02_asmdef_graph.html) | NPCSystem.Runtime + NPCSystem.Monitoring isolation |
| D3 | [Execution Order Timeline](diagrams/03_execution_order.excalidraw.json) | Phase 0→1→2→3 init sequence with annotations |
| D4 | [Dialogue Pipeline Flow](diagrams/04_dialogue_pipeline.excalidraw.json) | Player→UI→Manager→LLM→RAG→Response→Action→State |
| D5 | [Scene Hierarchy Tree](diagrams/05_scene_hierarchy.html) | Complete NPCDialoguePrototype1.unity GameObject tree |
| D6 | [Network Flow](diagrams/06_network_flow.html) | Client↔Server↔LLM bridge with RPC routing |

> **To open Excalidraw diagrams (D3, D4):** Drag the `.excalidraw.json` file onto
> [excalidraw.com](https://excalidraw.com). No account needed.
>
> **To open SVG diagrams (D1, D2, D5, D6):** Open the `.html` file in any browser.
> Works offline, no dependencies.

---

## The Big Picture

```
┌─────────────────────────────────────────────────────────────────┐
│                    Unity WebGL Game (Browser)                    │
│  ┌──────────┐  ┌──────────────┐  ┌───────────────────────────┐  │
│  │  Canvas   │  │ NPCDialogue  │  │     NPCNetworkSystem      │  │
│  │ UICtrl    │←→│ System       │←→│     (NetworkBootstrap)    │  │
│  │ (input)   │  │ Core/Backend │  │                           │  │
│  │ (display) │  │ Services/Net │  │  Port 11474 (WS/WSS)      │  │
│  └──────────┘  └──────┬───────┘  └───────────┬───────────────┘  │
└───────────────────────┼──────────────────────┼──────────────────┘
                        │ RPC                  │ Transport
┌───────────────────────┼──────────────────────┼──────────────────┐
│              NPCDialogueNetworkBridge         │                   │
│              (Server-side authority)         │                   │
├───────────────────────┴──────────────────────┴──────────────────┤
│                                                                  │
│     ┌──────────┐    ┌──────────┐    ┌──────────┐                 │
│     │ LocalAI  │    │  Qdrant  │    │ Supabase │                 │
│     │ :8080    │    │ :6333    │    │ :8091-92 │                 │
│     │ LLM+Emb  │    │ VectorDB │    │ Auth+DB  │                 │
│     └──────────┘    └──────────┘    └──────────┘                 │
│                                                                  │
│     ┌──────────────────────────────────────────┐                 │
│     │        Datadog Agent (:8125/:8126)        │                 │
│     │   Metrics • Traces • Dashboard            │                 │
│     └──────────────────────────────────────────┘                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## Cross-Reference Map

| Concept | Primary Chapter | Also See |
|---------|----------------|----------|
| Assembly Definitions | 01 | 02 (namespace isolation), 14 (WebGL build) |
| Namespaces | 02 | 01 (asmdef boundaries), 04 (service discovery) |
| Script Execution Order | 03 | 13 (scene wiring) |
| Service Architecture | 04 | 03 (execution order), 13 (scene hierarchy) |
| NPCProfile | 05 | 06 (dialogue pipeline) |
| Dialogue Pipeline | 06 | 04 (service chain), 09 (LocalAI), 10 (Qdrant) |
| WebGL Networking | 07 | 08 (bridge pattern), 14 (build checklist) |
| Network Bridge | 08 | 07 (transport config) |
| LocalAI | 09 | 10 (embeddings), 11 (comparison) |
| Qdrant | 10 | 06 (RAG pipeline) |
| Supabase | 11 | 06 (history persistence) |
| Datadog | 12 | 13 (scene init logger phase) |
| Scene Blueprint | 13 | 03 (execution order), 04 (services) |
| Build Checklist | 14 | 01 (asmdef → IL2CPP), 07 (WebGL transport) |

---

## Project File Structure

```
Assets/
├── Scenes/
│   └── NPCDialoguePrototype1.unity        ← Active scene
├── Scripts/
│   ├── Runtime/                            ← NPCSystem.Runtime asmdef
│   │   ├── Auth/                           ← NPCSystem.Auth
│   │   ├── Character/Animation/            ← NPCSystem.Character.Animation
│   │   ├── Character/NPC/                  ← NPCSystem.Character.NPC
│   │   ├── Character/Player/               ← NPCSystem.Character.Player
│   │   ├── Dialogue/Core/                  ← NPCSystem.Dialogue.Core
│   │   ├── Dialogue/Persistence/           ← NPCSystem.Dialogue.Persistence
│   │   ├── Dialogue/RAG/                   ← NPCSystem.Dialogue.RAG
│   │   ├── Dialogue/Session/               ← NPCSystem.Dialogue.Session
│   │   ├── Dialogue/UI/                    ← NPCSystem.Dialogue.UI
│   │   ├── Initialization/                 ← NPCSystem.Initialization
│   │   ├── Items/                          ← NPCSystem.Items
│   │   ├── LocalAI/                        ← NPCSystem.LocalAI
│   │   ├── Monitoring/Core/                ← NPCSystem.Monitoring asmdef
│   │   ├── Monitoring/Datadog/             ← NPCSystem.Monitoring.Datadog
│   │   ├── Network/Bridges/                ← NPCSystem.Network.Bridges
│   │   └── Network/Core/                   ← NPCSystem.Network.Core
│   └── Editor/                             ← Editor scripts
└── ... (materials, prefabs, etc.)
```

---

## Reading Paths

**For WebGL beginners (start here):**
1 → 2 → 7 → 14 → 3 → 4 → 13 → 5 → 6 → 9 → 10 → 11 → 12 → 8

**For backend specialists:**
9 → 10 → 11 → 12 → 6 → 8 → 7 → 1 → 2 → 3 → 4 → 5 → 13 → 14

**For existing Unity devs who just want it working:**
1 → 2 → 3 → 4 → 5 → 6 → 13 → 7 → 8 → 14

---

> "This project is a base prototype to every game project we are going to develop
> in the future. We're creating a manual that developers who do not have coding
> expertise can set up an environment to test modern databases, vector stores,
> and DevOps systems."
>
> — Project vision statement

---

*Generated from the NPCSystem codebase — Unity_Linux_LLM*
*Last updated: July 2026*
