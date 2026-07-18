# NPC LLM Dialogue Automation - Professional Developer Documentation

**Version**: 1.0 (Baseline Analysis)  
**Last Updated**: 2026-07-01  
**Target Audience**: New and existing developers integrating with this system

## Quick Start for New Developers

This documentation provides comprehensive technical guidance for understanding and extending the NPC LLM Dialogue Automation system. Start here based on your role:

- **Architecture Overview**: [System Architecture](2_Architecture/README.md) - Start here for system-level design
- **Backend Services Topology**: [Backend Services Topology](2_Architecture/Backend_Services_Topology.md) - Full local Docker/systemd infrastructure graph (WebGL client, dedicated server, LocalAI, Qdrant, Supabase, Cognee, Datadog), live endpoints, config file map, and known infra issues
- **Core Systems**: [Understanding the Core Systems](3_Core_Systems/README.md) - Deep-dives into each subsystem
- **Integration**: [Integration Guides](4_Integration_Guides/README.md) - Working with external systems (LLMUnity, LocalAI, Qdrant, Cognee)
- **Developer Guide**: [Development Guidelines](5_Developer_Guide/README.md) - Extending and modifying the system
- **Diagrams**: [Architecture Diagrams](Diagrams/README.md) - Visual system architecture and data flows

## What This Project Does

The **NPC LLM Dialogue Automation** system is a hybrid architecture that enables realistic, context-aware dialogue with non-player characters (NPCs) in Unity. It combines:

- **Local LLM Inference** via LLMUnity (GGUF-based models from LocalAI)
- **Retrieval-Augmented Generation (RAG)** for contextual memory (Qdrant-backed vector search)
- **Networking Infrastructure** for multiplayer dialogue scenarios
- **Profiling System** for distinct NPC personalities and knowledge bases
- **Structured Logging** for dialogue flow analysis and debugging
- **Long-term Memory** via optional Cognee integration

## Project Structure at a Glance

```
Assets/
├── Scripts/
│   ├── Runtime/
│   │   ├── Initialization/          # Deterministic scene initialization order
│   │   ├── Networking/              # Transport, WebSocket configuration, network bootstrap
│   │   ├── NPCDialogue/             # Core dialogue manager, RAG services, action planning
│   │   │   └── Logging/             # Structured dialogue flow logging
│   │   ├── Samples/RAG/              # Optional grounded-Qdrant sample controllers
│   │   └── NPCSystem.Runtime.asmdef # Assembly definition
│   ├── Editor/
│   │   ├── NPC factory tools
│   │   └── NPCSystem.Editor.asmdef
│   └── Tests/
│       └── NPCSystem.Tests.asmdef
├── LLMUnity/                        # In-game LLM runtime library
├── Scenes/
│   └── NPCDialoguePrototype1.unity   # Main prototype scene
├── Prefabs/
│   └── NPC templates and UI
├── Resources/
│   ├── NPC profiles and knowledge bases
│   └── RAG embeddings
└── StreamingAssets/
    └── Model configuration
```

## Technology Stack

| Component | Technology | Purpose |
|-----------|-----------|---------|
| **Runtime Environment** | Unity 6000.5.1f1 | Game engine |
| **LLM Inference** | LLMUnity + LocalAI GGUF | In-game model execution |
| **Vector Search** | Qdrant (optional) + U-Search | Semantic search for RAG |
| **Memory Backend** | Cognee (optional) | Long-term memory and knowledge graphs |
| **Networking** | Unity Netcode + WebSockets | Multiplayer dialogue |
| **Serialization** | Newtonsoft JSON | Configuration and data persistence |
| **C# Version** | .NET 6 | Language runtime |

## Key Concepts

### NPCProfile
Defines an individual NPC's identity, personality, and knowledge base. Each profile can have:
- Unique persona instructions for the LLM
- Knowledge documents for RAG retrieval
- Personal dialogue history
- Custom embeddings

### Dialogue Flow
1. Player input → NPC receives message
2. System retrieves contextual memory (RAG)
3. Prompt constructed with profile + context
4. LLM inference (local or remote)
5. Response validation and action planning
6. Result returned to player
7. History stored for future context

### RAG (Retrieval-Augmented Generation)
Provides semantic memory to NPCs by:
- Indexing knowledge documents with embeddings (MiniLM or remote)
- Retrieving relevant context for each dialogue
- Injecting context into LLM prompt
- Enabling grounded, factual responses

### Networking
Supports both:
- **Local**: Single-player dialogue with local inference
- **Multiplayer**: Multiple clients with centralized LLM server

## Core Systems Overview

| System | File(s) | Responsibility |
|--------|---------|-----------------|
| **Scene Initialization** | `NPCSceneInitializationController.cs` | Deterministic scene startup, validation, and optional network start |
| **Dialogue Manager** | `NPCDialogue/NPCDialogueManager.cs` | Main orchestrator, dialogue lifecycle, response generation |
| **Networking** | `NPCTransportConfig.cs`, `NPCNetworkBootstrap.cs` | WebSocket transport, connection management, multiplayer sync |
| **Profile System** | `NPCProfile.cs`, `NPCProfilePromptComposer.cs` | NPC identity, persona, knowledge base |
| **RAG Service** | `QdrantRAGService.cs`, `NPCRAGImporter.cs` | Vector search, context retrieval |
| **Action Planner** | `NPCDialogueActionPlanner.cs` | Response validation, action planning |
| **History Store** | `NPCHistoryStore.cs` | Dialogue history persistence |
| **Logging** | `NPCFlowLogger.cs` | Structured dialogue flow logging |
| **Evidence State** | `NPCEvidenceState.cs` | Game state tracking for context injection |

## Assembly Organization

The project uses three main assemblies:

### NPCSystem.Runtime (Auto-referenced)
Main runtime assembly for dialogue functionality.
**Dependencies**: LLMUnity, GladeKit, Unity.Netcode, U-Search, JSON

### NPCSystem.Editor (Manual reference)
Editor-only tools for NPC setup and debugging.
**Dependencies**: NPCSystem.Runtime, GladeKit.Bridge, LLMUnity

### NPCSystem.Tests (Test assembly)
Unit and integration tests for core systems.
**Dependencies**: NPCSystem.Runtime, Unity.Netcode, JSON

## Getting Started: First 5 Minutes

1. **Understand the Scene**: Open `NPCDialoguePrototype1.unity` in Unity Editor
2. **Review Profile Data**: Look at `Assets/Resources/NPC/` for NPC profiles
3. **Inspect RAG Data**: Check `Assets/Resources/RAG/` for knowledge bases
4. **Check Configuration**: Review `NPCDialogueManager` component settings
5. **Run Prototype**: Press Play in the Editor to see dialogue in action

## Documentation Map

```
Documentation/
├── 1_Overview/
│   ├── ProjectScope.md              # Problem statement and capabilities
│   ├── TechStack.md                 # Technology choices and rationale
│   └── CurrentState.md              # Known limitations and future direction
├── 2_Architecture/
│   ├── README.md                    # System architecture overview
│   ├── SystemContext.md             # External integrations
│   ├── LayerArchitecture.md         # Internal layer design
│   ├── DataModels.md                # Core data structures
│   └── NamespaceStructure.md        # Code organization
├── 3_Core_Systems/
│   ├── README.md                    # Systems index
│   ├── DialogueSystem.md            # Deep-dive into dialogue lifecycle
│   ├── NetworkingSystem.md          # Transport and multiplayer
│   ├── RAGSystem.md                 # Vector search and context retrieval
│   ├── ProfileSystem.md             # NPC identity and personalization
│   ├── LoggingSystem.md             # Structured event logging
│   └── MemorySystem.md              # History and evidence tracking
├── 4_Integration_Guides/
│   ├── README.md                    # Integration overview
│   ├── LLMUnityIntegration.md       # LLMUnity model loading
│   ├── LocalAIIntegration.md        # Remote LLM endpoints
│   ├── QdrantIntegration.md         # Vector search setup
│   ├── CogneeIntegration.md         # Long-term memory system
│   └── NetworkingIntegration.md     # Multiplayer configuration
├── 5_Developer_Guide/
│   ├── README.md                    # Development guidelines
│   ├── CodingStandards.md           # C# conventions
│   ├── AddingNPCProfiles.md         # Creating new NPCs
│   ├── ExtendingDialogue.md         # Custom dialogue logic
│   ├── DebuggingGuide.md            # Debugging tools and techniques
│   └── KnownIssues.md               # Documented problems and workarounds
└── Diagrams/
    ├── Architecture/                # System-level diagrams
    ├── DataFlow/                    # Process flow diagrams
    └── Components/                  # Internal component diagrams
```

## Key Workflow Example: Understanding a Dialogue Exchange

Here's a simplified view of what happens when a player talks to an NPC:

```
Player Input
    ↓
[Networking] Receive message from client/player
    ↓
[Dialogue Manager] Switch to NPC profile context
    ↓
[RAG Service] Query vector store for relevant knowledge
    ↓
[Profile Composer] Build prompt: persona + context + input
    ↓
[LLM Provider] Execute inference (local or remote)
    ↓
[Action Planner] Validate response, plan NPC actions
    ↓
[Logging] Record dialogue event
    ↓
[History Store] Persist for future context
    ↓
Response → Player
```

## Common Development Workflows

### Workflow 1: Adding a New NPC Profile
1. Create profile `.scriptable` in `Assets/Resources/NPC/`
2. Configure persona and knowledge base
3. Add to `NPCDialogueManager.profiles` array
4. Test dialogue in prototype scene

See: [Adding NPC Profiles](5_Developer_Guide/AddingNPCProfiles.md)

### Workflow 2: Extending Dialogue Logic
1. Inherit from `NPCDialogueActionPlanner` or modify evaluation logic
2. Override response validation methods
3. Register custom action handlers
4. Test with structured logging

See: [Extending Dialogue](5_Developer_Guide/ExtendingDialogue.md)

### Workflow 3: Debugging Connection Issues
1. Enable `NPCFlowLogger` for detailed flow traces
2. Check `NPCTransportConfig` connectivity
3. Verify LocalAI endpoint is running
4. Use GladeKit MCP for runtime inspection

See: [Debugging Guide](5_Developer_Guide/DebuggingGuide.md)

## Important Notes for New Developers

- **Namespace Discipline**: Always use `NPCSystem` namespace for runtime code. Respect assembly boundaries.
- **Initialization Order**: `NPCDialogueManager.InitializeAsync()` must complete before dialogue operations
- **Thread Safety**: RAG queries and LLM calls are async—use `async/await` pattern
- **Resource Loading**: NPC profiles load from `Assets/Resources/NPC/`—follow naming conventions
- **Logging**: Use `NPCFlowLogger` for all diagnostic output, not `Debug.Log`
- **Known Issues**: See [Known Issues](5_Developer_Guide/KnownIssues.md) before reporting bugs

## Getting Help

- **System Architecture Questions**: See [2_Architecture/README.md](2_Architecture/README.md)
- **How to Add/Modify X**: Check [5_Developer_Guide/README.md](5_Developer_Guide/README.md)
- **Integration Issues**: See [4_Integration_Guides/README.md](4_Integration_Guides/README.md)
- **Understanding a System**: Look in [3_Core_Systems/](3_Core_Systems/)
- **Visual Overview**: Check [Diagrams/](Diagrams/README.md)

---

**Next**: Start with [System Architecture Overview](2_Architecture/README.md)
