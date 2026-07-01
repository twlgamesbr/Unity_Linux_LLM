# System Architecture Overview

**Table of Contents**
- [High-Level System Context](#high-level-system-context)
- [Layer Architecture](#layer-architecture)
- [Component Interactions](#component-interactions)
- [Data Flow Patterns](#data-flow-patterns)
- [Integration Points](#integration-points)

## High-Level System Context

```mermaid
graph TB
    Player["👤 Player/Client"]
    Game["🎮 Unity Game Engine<br/>NPC Dialogue System"]
    LocalAI["🧠 LocalAI<br/>Model Management"]
    Models["📦 GGUF Models<br/>/mnt/data/models/localai/"]
    Qdrant["🔍 Qdrant Vector DB<br/>Semantic Search"]
    Cognee["🧠 Cognee<br/>Knowledge Graph"]
    GladeKit["🔧 GladeKit MCP<br/>Editor Tools"]
    
    Player <-->|WebSocket| Game
    Game <-->|HTTP REST| LocalAI
    LocalAI <-->|Load| Models
    Game <-->|HTTP gRPC| Qdrant
    Game <-->|HTTP| Cognee
    GladeKit <-->|MCP| Game
    
    style Game fill:#4da6ff
    style Player fill:#99ff99
    style LocalAI fill:#ff9999
    style Models fill:#ffcc99
    style Qdrant fill:#cc99ff
    style Cognee fill:#ffb3cc
    style GladeKit fill:#b3d9ff
```

**Key Integration Points:**
- **LocalAI**: Hosts GGUF models; NPC requests inference via HTTP
- **Qdrant**: Vector database for semantic memory retrieval
- **Cognee**: Optional long-term memory system (experimental)
- **GladeKit**: Editor-time inspection and automation
- **Player**: Communicates via WebSocket or direct C# calls

## Layer Architecture

```mermaid
graph LR
    UI["🖼️ UI Layer<br/>NPCDialogueUIController<br/>NotebookUIController"]
    
    Dialog["💬 Dialogue Layer<br/>NPCDialogueManager<br/>NPCDialogueActionPlanner"]
    
    Profile["👤 Profile Layer<br/>NPCProfile<br/>NPCProfilePromptComposer"]
    
    Memory["💾 Memory Layer<br/>QdrantRAGService<br/>NPCHistoryStore<br/>NPCEvidenceState"]
    
    Network["🌐 Network Layer<br/>NPCNetworkBootstrap<br/>NPCTransportConfig"]
    
    LLMInt["🔌 LLM Integration Layer<br/>LLMUnity Bridge<br/>LocalAI HTTP Client<br/>RemoteLLMClient"]
    
    Infrastructure["⚙️ Infrastructure<br/>Logging: NPCFlowLogger<br/>Serialization: JSON<br/>Async/Await Runtime"]
    
    UI --> Dialog
    Dialog --> Profile
    Dialog --> Memory
    Dialog --> Network
    Dialog --> LLMInt
    LLMInt --> Infrastructure
    Memory --> Infrastructure
    Network --> Infrastructure
    
    style UI fill:#b3d9ff
    style Dialog fill:#4da6ff
    style Profile fill:#99ccff
    style Memory fill:#99ff99
    style Network fill:#ffcc99
    style LLMInt fill:#ff9999
    style Infrastructure fill:#e6b3ff
```

**Layer Descriptions:**

| Layer | Responsibility | Key Classes |
|-------|-----------------|------------|
| **UI** | User interaction, response display | `NPCDialogueUIController`, `NotebookUIController` |
| **Dialogue** | Orchestration, response generation, dialogue flow | `NPCDialogueManager`, `NPCDialogueActionPlanner` |
| **Profile** | NPC identity, persona, knowledge base | `NPCProfile`, `NPCProfilePromptComposer` |
| **Memory** | Context retrieval, history tracking, evidence | `QdrantRAGService`, `NPCHistoryStore`, `NPCEvidenceState` |
| **Network** | Transport, WebSocket, multiplayer | `NPCNetworkBootstrap`, `NPCTransportConfig` |
| **LLM Integration** | Model execution, API bridges | LLMUnity, LocalAI HTTP client |
| **Infrastructure** | Logging, serialization, async utilities | `NPCFlowLogger`, JSON |

## Component Interactions

### Primary Dialogue Flow Architecture

```mermaid
graph TD
    Input["Player Input"]
    
    Manager["NPCDialogueManager<br/>(Orchestrator)"]
    
    Profile["NPCProfile<br/>(Identity & Knowledge)"]
    
    Composer["NPCProfilePromptComposer<br/>(Prompt Construction)"]
    
    Memory["QdrantRAGService<br/>(Context Retrieval)"]
    
    History["NPCHistoryStore<br/>(Dialogue History)"]
    
    LLM["LLM Provider<br/>(Inference)"]
    
    Planner["NPCDialogueActionPlanner<br/>(Validation & Actions)"]
    
    Logger["NPCFlowLogger<br/>(Structured Logging)"]
    
    Output["Response to Player"]
    
    Input --> Manager
    Manager --> Profile
    Manager --> Memory
    Manager --> History
    Profile --> Composer
    Memory --> Composer
    History --> Composer
    Composer --> LLM
    LLM --> Planner
    Planner --> Output
    
    Manager -.-> Logger
    Profile -.-> Logger
    Memory -.-> Logger
    Planner -.-> Logger
    
    style Manager fill:#4da6ff
    style Profile fill:#99ccff
    style Composer fill:#99ff99
    style Memory fill:#99ccff
    style History fill:#99ccff
    style LLM fill:#ff9999
    style Planner fill:#ffcc99
    style Logger fill:#e6b3ff
    style Input fill:#99ff99
    style Output fill:#99ff99
```

### Key Component Classes

**NPCDialogueManager**
```csharp
public class NPCDialogueManager : MonoBehaviour
{
    // Configuration
    public LLM llm;                           // Local or remote LLM
    public LLMAgent llmAgent;                 // Alternative agent-based LLM
    public QdrantRAGService qdrantRag;        // Vector search
    public NPCProfile[] profiles;             // Available NPCs
    public bool useRemoteServer;              // Remote vs local inference
    
    // Core Methods
    public Task InitializeAsync();            // Initialize all subsystems
    public Task<string> GetResponseAsync(     // Get dialogue response
        string userInput, NPCProfile profile);
    
    // Events
    public UnityEvent<string> onResponseStart;
    public UnityEvent<string> onResponseUpdated;
    public UnityEvent<string, string> onResponseComplete;
    public UnityEvent<string> onError;
}
```

**NPCProfile**
```csharp
public class NPCProfile : ScriptableObject
{
    public string slug;                       // Unique identifier
    public string displayName;                // Human-readable name
    public string systemPrompt;               // Persona instruction
    public TextAsset[] knowledgeDocuments;    // RAG knowledge base
    public bool enableMemoryEncoding;         // Record interaction
    public string[] actionTypes;              // Possible NPC actions
}
```

**QdrantRAGService**
```csharp
public class QdrantRAGService : MonoBehaviour
{
    public string host = "localhost";
    public int port = 6333;
    
    public Task InitializeAsync();
    public Task<List<string>> RetrieveAsync(
        string query, int topK = 5);
    public Task IndexDocumentsAsync(
        string collectionName, 
        List<string> documents);
}
```

## Data Flow Patterns

### Pattern 1: Complete Dialogue Exchange

```mermaid
sequenceDiagram
    participant Player as 👤 Player
    participant UI as 🖼️ UI Controller
    participant Mgr as 💬 Dialogue Manager
    participant RAG as 🔍 RAG Service
    participant LLM as 🧠 LLM Provider
    participant Planner as 📋 Action Planner
    participant Log as 📊 Logger

    Player->>UI: Send message
    UI->>Mgr: ProcessInput(message)
    
    Mgr->>RAG: RetrieveContext(query)
    RAG-->>Mgr: Relevant documents
    
    Mgr->>Mgr: Compose prompt
    Mgr->>LLM: Generate response
    
    LLM-->>Mgr: Response text (streaming)
    Mgr->>Planner: Validate response
    Planner-->>Mgr: Action list
    
    Mgr->>Log: LogDialogueEvent()
    
    Mgr-->>UI: Response complete
    UI->>Player: Display response
```

### Pattern 2: Network Multiplayer Dialogue

```mermaid
graph TB
    subgraph Clients["🖥️ Client Instances"]
        C1["Client 1<br/>Player Input"]
        C2["Client 2<br/>Spectator"]
    end
    
    subgraph Server["🖥️ Dedicated Server"]
        Bootstrap["NPCNetworkBootstrap<br/>Server Host"]
        Manager["NPCDialogueManager<br/>Centralized"]
        LLMServer["LLM Provider<br/>Shared"]
    end
    
    subgraph External["☁️ External Services"]
        LocalAI["LocalAI<br/>GGUF Models"]
        Qdrant["Qdrant<br/>Vector DB"]
    end
    
    C1 <-->|WebSocket| Bootstrap
    C2 <-->|WebSocket| Bootstrap
    Bootstrap --> Manager
    Manager --> LLMServer
    Manager --> Qdrant
    LLMServer --> LocalAI
    
    style C1 fill:#99ff99
    style C2 fill:#b3ff99
    style Bootstrap fill:#4da6ff
    style Manager fill:#4da6ff
    style LLMServer fill:#ff9999
    style LocalAI fill:#ff9999
    style Qdrant fill:#cc99ff
```

### Pattern 3: RAG Context Retrieval

```mermaid
graph LR
    Query["📝 Dialogue Query<br/>User message"]
    
    Embed["🔌 Embed Query<br/>MiniLM Model<br/>Or Remote"]
    
    Vector["⬜ Query Vector<br/>384 dimensions"]
    
    Search["🔍 Vector Search<br/>Qdrant cosine similarity"]
    
    Results["📚 Retrieved Documents<br/>Top 5 results"]
    
    Inject["💉 Inject Context<br/>Into LLM prompt"]
    
    Query --> Embed
    Embed --> Vector
    Vector --> Search
    Search --> Results
    Results --> Inject
    
    style Query fill:#99ff99
    style Embed fill:#ffcc99
    style Vector fill:#ffb3b3
    style Search fill:#cc99ff
    style Results fill:#99ccff
    style Inject fill:#99ff99
```

## Integration Points

### Integration with LLMUnity

```mermaid
graph LR
    Manager["NPCDialogueManager"]
    
    LLMUnity["LLMUnity Runtime<br/>Model Loading"]
    
    Local["Local GGUF<br/>Inference"]
    
    Remote["Remote HTTP<br/>Inference"]
    
    Config["LLM Configuration<br/>Model path, quantization"]
    
    Manager -->|References| LLMUnity
    LLMUnity -->|Execute| Local
    LLMUnity -->|Execute| Remote
    Config --> LLMUnity
    
    style Manager fill:#4da6ff
    style LLMUnity fill:#ff9999
    style Local fill:#ffcc99
    style Remote fill:#ffcc99
    style Config fill:#e6b3ff
```

### Integration with Qdrant

```mermaid
graph LR
    Service["QdrantRAGService<br/>Dialogue System"]
    
    Client["Qdrant Python Client<br/>Vector API"]
    
    Qdrant["Qdrant Server<br/>:6333"]
    
    Collections["Collections<br/>dialogue-memory<br/>npc-knowledge"]
    
    Service -->|gRPC| Client
    Client -->|HTTP| Qdrant
    Qdrant -->|Manage| Collections
    
    style Service fill:#4da6ff
    style Client fill:#99ccff
    style Qdrant fill:#cc99ff
    style Collections fill:#e6ccff
```

### Integration with LocalAI

```mermaid
graph LR
    Manager["NPCDialogueManager"]
    
    HTTP["HTTP REST API<br/>http://localhost:8080"]
    
    LocalAI["LocalAI Service<br/>Model Manager"]
    
    Models["GGUF Models<br/>/mnt/data/models/localai/"]
    
    Manager -->|POST /chat/completions| HTTP
    HTTP --> LocalAI
    LocalAI -->|Load| Models
    
    style Manager fill:#4da6ff
    style HTTP fill:#99ff99
    style LocalAI fill:#ff9999
    style Models fill:#ffcc99
```

## Configuration & Settings

### NPCTransportConfig Structure

```csharp
[Serializable]
public struct NPCTransportConfig
{
    public string connectAddress;              // "127.0.0.1"
    public string listenAddress;               // "0.0.0.0"
    public ushort port;                        // 7777
    public bool useWebSockets;                 // true
    public string webSocketPath;               // "/npc-dialogue"
    public NPCNetworkAutoStartMode autoStartMode; // Manual|Client|Host|Server
}
```

### NPCDialogueManager Configuration

Key settings in the Inspector:

| Setting | Type | Purpose |
|---------|------|---------|
| `useRemoteServer` | bool | Use remote LLM server vs local |
| `remoteHost` | string | LocalAI server address |
| `remotePort` | int | LocalAI server port |
| `remoteModel` | string | Model identifier in LocalAI |
| `useQdrantRag` | bool | Enable vector search |
| `useCogneeMemory` | bool | Enable knowledge graph memory |
| `enableRAG` | bool | Enable all RAG features |
| `maxHistoryPerNPC` | int | Dialogue history limit |
| `persistHistory` | bool | Save history to disk |

---

**Next**: [Understand Individual Systems](../3_Core_Systems/README.md)
