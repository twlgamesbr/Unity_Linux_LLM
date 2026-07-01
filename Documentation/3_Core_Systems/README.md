# Core Systems - Deep Dive Documentation

**Table of Contents**
- [Dialogue System](#dialogue-system)
- [Networking System](#networking-system)
- [RAG & Vector Search System](#rag--vector-search-system)
- [Profile & Personalization System](#profile--personalization-system)
- [Memory & History System](#memory--history-system)
- [Logging System](#logging-system)

---

## Dialogue System

The **Dialogue System** is the heart of the NPC LLM Dialogue Automation. It orchestrates the entire conversation flow: receiving player input, retrieving context, constructing prompts, calling LLM, validating responses, and executing NPC actions.

### Core Components

**NPCDialogueManager** (`Assets/Scripts/Runtime/NPCDialogue/NPCDialogueManager.cs`)
- **Purpose**: Central orchestrator for all dialogue operations
- **Lifecycle**: initialized through `NPCSceneInitializationController` / `InitializeAsync()` during scene start
- **Responsibilities**:
  - Manage NPC profiles
  - Orchestrate dialogue lifecycle
  - Handle LLM integration
  - Emit dialogue events
  - Maintain dialogue history

```csharp
public class NPCDialogueManager : MonoBehaviour
{
    // Configuration References
    public LLM llm;                              // Local LLM (LLMUnity)
    public LLMAgent llmAgent;                    // LLM Agent (LLMUnity)
    public RAG rag;                              // Local RAG (LLMUnity)
    public QdrantRAGService qdrantRag;           // Remote vector search
    public NPCDialogueActionPlanner actionPlanner;
    public NPCEvidenceState evidenceState;       // Game state context
    public GladeAgenticAI.Core.Memory.CogneeMemoryService cogneeMemory;
    
    // Remote Server Configuration
    public bool useRemoteServer = false;
    public string remoteHost = "localhost";
    public int remotePort = 11435;
    public string remoteModel = "default-llm";
    
    // RAG Configuration
    public bool useQdrantRag = false;
    public bool enableRAG = true;
    public int maxHistoryPerNPC = 20;
    public bool persistHistory = true;
    
    // Events emitted during dialogue
    public UnityEvent<string> onNPCChanged;
    public UnityEvent<string> onResponseStart;
    public UnityEvent<string> onResponseUpdated;
    public UnityEvent<string, string> onResponseComplete;
    public UnityEvent<string> onError;
    
    // Public API
    public Task InitializeAsync();
    public Task<string> GetResponseAsync(string userInput, NPCProfile npc);
    public NPCProfile currentProfile { get; }
    public bool isResponding { get; }
    public bool isInitialized { get; }
}
```

### Dialogue Lifecycle

```
1. INITIALIZATION (async)
   ├─ Wait for LLMUnity models to load
   ├─ Initialize RAG (local and/or Qdrant)
   ├─ Load NPC profiles from Resources
   ├─ Restore dialogue history (if persistent)
   └─ Ready for dialogue
   
2. RESPONSE GENERATION (async)
   ├─ Player input received
   ├─ Switch to NPC profile context
   ├─ [RAG] Query for relevant knowledge
   ├─ Retrieve dialogue history for context
   ├─ [Prompt Composition] Build LLM prompt
   │  └─ Persona + Evidence + History + Context + Input
   ├─ [LLM Inference] Call LLM (local or remote)
   │  └─ Stream response chunks (onResponseUpdated events)
   ├─ [Validation] Validate response quality
   ├─ [Action Planning] Plan NPC actions
   ├─ [Logging] Record dialogue event
   ├─ Persist to history
   └─ Return response to player
   
3. STATE UPDATES
   ├─ Current NPC profile
   ├─ Dialogue history (per NPC)
   ├─ Evidence/game state
   └─ Response generation state
```

### Async Initialization Pattern

```csharp
// Initialization is thread-safe and idempotent
public Task InitializeAsync()
{
    lock (_initializationLock)
    {
        _initializationTask ??= InitializeInternalAsync();
        return _initializationTask;
    }
}

// Call before any dialogue operations
// Safe to call multiple times—only initializes once
await npCDialogueManager.InitializeAsync();

// Then use:
var response = await npCDialogueManager.GetResponseAsync(
    userInput: "Hello, what do you know about X?",
    npc: myProfile
);
```

### LLM Provider Options

**Option 1: Local LLMUnity (Default)**
```csharp
// Use local GGUF models through LLMUnity
npCDialogueManager.useRemoteServer = false;
npCDialogueManager.llm.SetModel(modelPath);
// Inference runs locally in Unity
```

**Option 2: Remote HTTP (LocalAI)**
```csharp
// Use remote LLM via LocalAI
npCDialogueManager.useRemoteServer = true;
npCDialogueManager.remoteHost = "localhost";
npCDialogueManager.remotePort = 11435;
npCDialogueManager.remoteModel = "neural-chat";
// Requests sent to LocalAI HTTP API
```

### Key Internal Methods

| Method | Purpose | Async |
|--------|---------|-------|
| `InitializeAsync()` | Initialize subsystems | Yes |
| `GetResponseAsync(input, npc)` | Generate dialogue response | Yes |
| `SwitchNPCAsync(npcSlug)` | Change active NPC | Yes |
| `SetCustomInstructionsAsync(instructions)` | Override system prompt | Yes |
| `ClearHistoryAsync(npcSlug)` | Reset dialogue history | Yes |
| `SaveHistoryAsync()` | Persist dialogue history | Yes |
| `LoadHistoryAsync()` | Restore dialogue history | Yes |

---

## Networking System

The **Networking System** enables multiplayer dialogue scenarios where multiple clients connect to a centralized server for shared NPC interactions.

### Core Components

**NPCTransportConfig** (`NPCTransportConfig.cs`)
```csharp
[Serializable]
public struct NPCTransportConfig
{
    public string connectAddress;              // Address to connect to
    public string listenAddress;               // Address to listen on
    public ushort port;                        // WebSocket port
    public bool useWebSockets;                 // True = WebSocket, False = Raw TCP
    public string webSocketPath;               // WebSocket endpoint path
    public NPCNetworkAutoStartMode autoStartMode; // When to start networking
    
    // Factory methods
    public static NPCTransportConfig CreateDefault();
    public void NormalizeInPlace();            // Normalize addresses/paths
    public bool TryValidate(out string errorMessage); // Validate settings
}
```

**NPCNetworkBootstrap** (`NPCNetworkBootstrap.cs`)
```csharp
public class NPCNetworkBootstrap : MonoBehaviour
{
    public NPCTransportConfig transportConfig;
    public string gameMode = "host";           // host, server, client
    
    // Initialize network from config
    public Task InitializeAsync();
    
    // Network events
    public event Action<NetworkState> OnNetworkStateChanged;
    public event Action<string> OnConnectionError;
}
```

**NPCPlayerNetworkAvatar** (`NPCPlayerNetworkAvatar.cs`)
```csharp
public class NPCPlayerNetworkAvatar : NetworkBehaviour
{
    // Synchronized across network
    [Networked] public string playerName { get; set; }
    [Networked] public string currentNPCSlug { get; set; }
    
    // RPC methods for dialogue
    [Rpc] public void SendDialogueMessage(string message);
    [Rpc] public void OnResponseReceived(string response);
}
```

### Configuration

**Default Configuration**
```csharp
NPCTransportConfig config = NPCTransportConfig.CreateDefault();
// connectAddress: "127.0.0.1"
// listenAddress: "0.0.0.0"
// port: 7777
// useWebSockets: true
// webSocketPath: "/npc-dialogue"
// autoStartMode: Manual
```

**Validation Example**
```csharp
config.NormalizeInPlace();  // Cleans up addresses/paths

if (!config.TryValidate(out string error))
{
    Debug.LogError($"Invalid config: {error}");
    // Handle configuration error
}
```

### Network Topologies

**Single-Player (Local Only)**
```
┌──────────────┐
│ Unity Client │
├──────────────┤
│ Dialogue Mgr │  (no networking)
│ Local LLM    │
│ Local RAG    │
└──────────────┘
```

**Client-Server (Shared LLM)**
```
┌──────────────┐           ┌──────────────────┐
│ Client 1     │           │ Server/Host      │
├──────────────┤           ├──────────────────┤
│ Input Only   │           │ Dialogue Mgr     │
└──────────────┘           │ LLM/RAG Shared   │
      ↓                    │ All Profiles     │
      └────────WebSocket───→└──────────────────┘
                            ↑
┌──────────────┐           │
│ Client 2     │           │
├──────────────┤           │
│ Input Only   │           │
└──────────────┘           │
      ↓                    │
      └────────WebSocket───→
```

---

## RAG & Vector Search System

The **RAG (Retrieval-Augmented Generation) System** allows NPCs to answer questions grounded in knowledge documents by retrieving semantically relevant context before LLM inference.

### Core Components

**QdrantRAGService** (`QdrantRAGService.cs`)
```csharp
public class QdrantRAGService : MonoBehaviour
{
    public string host = "localhost";
    public int port = 6333;
    public string collectionName = "npc-knowledge";
    
    // Initialization
    public Task InitializeAsync();
    public Task<bool> IsReadyAsync();
    
    // Retrieval
    public Task<List<string>> RetrieveAsync(
        string query,
        int topK = 5,
        float similarityThreshold = 0.5f
    );
    
    // Indexing
    public Task IndexDocumentsAsync(
        string collectionName,
        List<string> documents,
        List<string> metadataIds = null
    );
    
    // Embedding
    private Task<float[]> EmbedQueryAsync(string query);
    private Task<List<float[]>> EmbedDocumentsAsync(List<string> docs);
}
```

**NPCRAGImporter** (`NPCRAGImporter.cs`)
```csharp
public class NPCRAGImporter : MonoBehaviour
{
    // Import knowledge documents for an NPC
    public Task ImportNPCKnowledgeAsync(
        NPCProfile profile,
        QdrantRAGService ragService
    );
    
    // Parse and chunk documents
    private List<string> ChunkDocument(string text, int chunkSize = 500);
    
    // Generate embeddings
    private Task<List<float[]>> GenerateEmbeddingsAsync(List<string> chunks);
}
```

**NPCRAGMetadata** (`NPCRAGMetadata.cs`)
```csharp
[Serializable]
public class NPCRAGMetadata
{
    public string npcSlug;                    // Which NPC owns this knowledge
    public string documentName;               // Source document
    public int chunkIndex;                    // Position in document
    public List<string> keywords;             // Search keywords
    public DateTime importedAt;               // When indexed
}
```

### Knowledge Indexing Workflow

```
1. SELECT KNOWLEDGE FILES
   └─ From NPCProfile.knowledgeDocuments (TextAsset array)

2. CHUNK DOCUMENTS
   ├─ Split into 500-token chunks
   ├─ Create metadata for each chunk
   └─ Preserve document boundaries

3. EMBED CHUNKS
   ├─ Use MiniLM (local) or remote embedder
   ├─ Generate 384-dim vectors per chunk
   └─ Store vectors with metadata

4. INDEX IN QDRANT
   ├─ Create/update collection
   ├─ Store vectors with metadata
   ├─ Set similarity metric (cosine)
   └─ Build HNSW index

5. MARK READY FOR RETRIEVAL
   └─ Enable RAG queries for this NPC
```

### Retrieval Query Flow

```csharp
// 1. Input query
string query = "What do you know about the ancient temple?";

// 2. Embed query (same model as documents)
var queryVector = await embedder.EmbedAsync(query); // 384 dims

// 3. Search Qdrant for similar vectors
var results = await qdrant.SearchAsync(
    collectionName: "npc-knowledge",
    vector: queryVector,
    topK: 5,
    threshold: 0.5
);

// 4. Extract context chunks
var context = results
    .Select(hit => hit.Payload["text"])
    .ToList();

// 5. Inject into prompt
string prompt = $"""
    Knowledge base context:
    {string.Join("\n---\n", context)}
    
    User question: {query}
""";

// 6. LLM generates grounded response
var response = await llm.GenerateAsync(prompt);
```

### Embedding Models

**Local (MiniLM)**
- Model: `all-MiniLM-L12-v2.Q4_K_M.gguf`
- Dimensions: 384
- Speed: Fast (local)
- Accuracy: High
- **Configuration**: `Assets/Resources/LLMRAG/` in LLMUnity

**Remote (E5)**
- Endpoint: `http://localhost:8080/embeddings`
- Dimensions: 768 or 1024 (configurable)
- Speed: Depends on server
- Accuracy: Very high
- **Configuration**: Set `forceRemoteEmbedder: true` in DialogueManager

### RAG Configuration

```csharp
public class NPCDialogueManager : MonoBehaviour
{
    // RAG Settings
    public bool enableRAG = true;
    public RAG rag;                           // Local LLMUnity RAG
    public string ragEmbeddingPath = "RAG/NPCDialogues.rag";
    
    public QdrantRAGService qdrantRag;
    public bool useQdrantRag = false;
    
    public bool forceRemoteEmbedder = false;
    public string remoteEmbeddingHost = "localhost";
    public int remoteEmbeddingPort = 8080;
    
    public bool rebuildRagFromKnowledgeIfMissing = true;
}
```

---

## Profile & Personalization System

The **Profile System** defines individual NPC identities, personalities, and knowledge bases.

### Core Components

**NPCProfile** (ScriptableObject)
```csharp
public class NPCProfile : ScriptableObject
{
    // Identity
    public string slug;                       // Unique identifier (e.g., "elder-sage")
    public string displayName;                // Human-readable name (e.g., "Elder Sage")
    public Texture2D portraitImage;           // Character portrait
    
    // Knowledge & Personalization
    public string systemPrompt;               // Persona instructions for LLM
    public TextAsset[] knowledgeDocuments;    // Documents for RAG indexing
    public bool enableMemoryEncoding;         // Record interactions for memory
    
    // NPC Behaviors
    public string[] actionTypes;              // Possible NPC actions (e.g., "speak", "emote")
    public float responseTimeout = 30f;       // Max time for response generation
    
    // Dialogue Style
    public bool useMarkdown = true;           // Support markdown formatting
    public int maxResponseLength = 1000;      // Response truncation limit
}
```

**NPCProfilePromptComposer** (`NPCProfilePromptComposer.cs`)
```csharp
public class NPCProfilePromptComposer
{
    // Assemble final LLM prompt from components
    public static string ComposePrompt(
        NPCProfile profile,
        List<string> ragContext,
        List<DialogueEntry> dialogueHistory,
        NPCEvidenceState gameState,
        string userInput
    );
    
    // Individual builders
    private static string BuildPersonaSection(NPCProfile profile);
    private static string BuildContextSection(List<string> ragContext);
    private static string BuildHistorySection(List<DialogueEntry> history);
    private static string BuildStateSection(NPCEvidenceState state);
    private static string BuildInputSection(string input);
}
```

### Example Profile

```csharp
// File: Assets/Resources/NPC/WiseMage.asset
{
    "slug": "wise-mage",
    "displayName": "Wise Mage",
    "systemPrompt": """
        You are the Wise Mage, an ancient scholar with vast knowledge of magic.
        - Speak with philosophical wisdom
        - Reference mystical concepts
        - Always explain technical magic in simple terms
        - Be cryptic but ultimately helpful
    """,
    "knowledgeDocuments": [
        "Magic Systems.txt",
        "Spell Grimoire.txt",
        "Historical Records.txt"
    ],
    "actionTypes": ["speak", "cast_spell", "gesture"],
    "responseTimeout": 30.0,
    "maxResponseLength": 2000
}
```

### Prompt Composition Template

```
═══════════════════════════════════════════════════════════
PERSONA INSTRUCTIONS
═══════════════════════════════════════════════════════════
{NPC system prompt}

═══════════════════════════════════════════════════════════
CONTEXTUAL KNOWLEDGE
═══════════════════════════════════════════════════════════
{RAG-retrieved documents}

═══════════════════════════════════════════════════════════
DIALOGUE HISTORY
═══════════════════════════════════════════════════════════
Previous exchanges with this NPC:
{Last 5-10 dialogue entries}

═══════════════════════════════════════════════════════════
GAME STATE (EVIDENCE)
═══════════════════════════════════════════════════════════
Current quest status: {state}
Known information: {evidence}
NPC current location: {location}

═══════════════════════════════════════════════════════════
USER INPUT
═══════════════════════════════════════════════════════════
Player says: "{user message}"

Generate a response as the Wise Mage. Keep it concise and in character.
```

---

## Memory & History System

The **Memory System** tracks dialogue history and game evidence for context injection.

### Core Components

**NPCHistoryStore** (`NPCHistoryStore.cs`)
```csharp
public class NPCHistoryStore
{
    // Dialogue entry
    [Serializable]
    public struct DialogueEntry
    {
        public string npcSlug;
        public string playerMessage;
        public string npcResponse;
        public DateTime timestamp;
        public List<string> retrievedContext;  // RAG results used
        public float confidence;               // Response confidence score
    }
    
    // Storage API
    public void RecordExchange(DialogueEntry entry);
    public List<DialogueEntry> GetHistoryFor(string npcSlug, int maxEntries = 20);
    public void ClearHistory(string npcSlug);
    public Task SaveAsync(string savePath);
    public Task LoadAsync(string savePath);
    
    // Query
    public List<DialogueEntry> GetRecentFor(string npcSlug, int count);
    public List<DialogueEntry> GetSince(string npcSlug, DateTime sinceTime);
}
```

**NPCEvidenceState** (`NPCEvidenceState.cs`)
```csharp
public class NPCEvidenceState : MonoBehaviour
{
    // Track game state for NPC context
    public Dictionary<string, object> evidenceData;
    
    // Game state queries
    public bool HasKnowledge(string key);
    public object GetEvidence(string key);
    public void UpdateEvidence(string key, object value);
    
    // Inject into prompts
    public string SerializeForPrompt();
}
```

### History Persistence

```
History Storage Format (JSON):
{
  "npc_slug": "wise-mage",
  "entries": [
    {
      "timestamp": "2026-07-01T10:30:00Z",
      "player_message": "What is magic?",
      "npc_response": "Magic is...",
      "retrieved_context": ["Magic Systems.txt chunk 3", ...],
      "confidence": 0.92
    },
    ...
  ]
}

File Location:
- Default: {project_root}/Saves/Dialogue/
- Per-NPC: dialogue_history_{npc_slug}.json
```

---

## Logging System

The **Logging System** provides structured event logging for dialogue analysis and debugging.

### Core Components

**NPCFlowLogger** (`NPCFlowLogger.cs`)
```csharp
public class NPCFlowLogger : MonoBehaviour
{
    // Structured event logging
    public void LogEvent(NPCFlowEvent ev);
    
    // Event builders
    public NPCFlowEvent CreateDialogueEvent(
        string npcSlug,
        string playerInput,
        string npcResponse,
        float latencyMs
    );
    
    public NPCFlowEvent CreateRAGEvent(
        string query,
        List<string> results,
        float similarityScore
    );
    
    // Output
    public void FlushToFile(string path);           // JSONL format
    public IEnumerable<NPCFlowEvent> GetEvents();
}
```

**NPCFlowEvent** (Data structure)
```csharp
[Serializable]
public class NPCFlowEvent
{
    public string eventId;                    // Unique ID
    public NPCFlowStage stage;                // Initialization, Input, RAG, LLM, etc.
    public NPCFlowStatus status;              // Started, Completed, Error
    public NPCFlowLogLevel level;             // Info, Warning, Error, Debug
    public string message;                    // Human-readable description
    public float elapsedMs;                   // Duration if applicable
    public DateTime timestamp;
    public Dictionary<string, object> metadata; // Context data
}
```

### Event Stages

```csharp
public enum NPCFlowStage
{
    Initialization,        // System startup
    DialogueInput,         // Receiving player message
    RAGRetrieval,         // Vector search
    PromptComposition,    // Building LLM prompt
    LLMInference,         // Calling LLM
    ResponseValidation,   // Action planning
    HistoryPersistence,   // Saving dialogue
    NetworkTransport,     // Network communication
    ErrorHandling         // Error recovery
}
```

### Logging Examples

```csharp
var logger = NPCFlowLogger.FindOrCreate();

// Log dialogue input
logger.LogEvent(new NPCFlowEvent
{
    stage = NPCFlowStage.DialogueInput,
    status = NPCFlowStatus.Completed,
    level = NPCFlowLogLevel.Info,
    message = "Received input from player",
    metadata = new()
    {
        { "npc_slug", "wise-mage" },
        { "input", "What is magic?" },
        { "input_length", 16 }
    }
});

// Log RAG retrieval
logger.LogEvent(new NPCFlowEvent
{
    stage = NPCFlowStage.RAGRetrieval,
    status = NPCFlowStatus.Completed,
    elapsedMs = 145.5f,
    message = "Retrieved 5 context chunks",
    metadata = new()
    {
        { "query", "What is magic?" },
        { "results_count", 5 },
        { "avg_similarity", 0.87 }
    }
});

// Log LLM inference
logger.LogEvent(new NPCFlowEvent
{
    stage = NPCFlowStage.LLMInference,
    status = NPCFlowStatus.Completed,
    elapsedMs = 2340.1f,
    message = "LLM generated response",
    metadata = new()
    {
        { "model", "neural-chat" },
        { "tokens_generated", 128 },
        { "temperature", 0.7f }
    }
});
```

### Log Output Format (JSONL)

```json
{"eventId":"evt_001","stage":"DialogueInput","status":"Completed","level":"Info","timestamp":"2026-07-01T10:30:00Z","elapsedMs":0,"message":"Received input","metadata":{"npc_slug":"wise-mage","input":"What is magic?"}}
{"eventId":"evt_002","stage":"RAGRetrieval","status":"Completed","level":"Info","timestamp":"2026-07-01T10:30:00.145Z","elapsedMs":145.5,"message":"Retrieved 5 chunks","metadata":{"query":"What is magic?","results":5}}
{"eventId":"evt_003","stage":"LLMInference","status":"Completed","level":"Info","timestamp":"2026-07-01T10:30:02.485Z","elapsedMs":2340,"message":"Generated response","metadata":{"model":"neural-chat","tokens":128}}
{"eventId":"evt_004","stage":"HistoryPersistence","status":"Completed","level":"Info","timestamp":"2026-07-01T10:30:02.6Z","elapsedMs":115,"message":"Saved to history","metadata":{"npc_slug":"wise-mage"}}
```

---

**Next**: [Integration Guides](../4_Integration_Guides/README.md)
